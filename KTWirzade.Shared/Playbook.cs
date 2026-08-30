using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Policy;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using Core;
using Core.Miscellaneous;
using JetBrains.Annotations;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MessageBox = System.Windows.MessageBox;

namespace KTWirzade.Shared
{
    [Serializable]
    public class VersionNumber : IXmlSerializable
    {
        public void WriteXml(XmlWriter writer)
        {
            writer.WriteValue(this.ToString());
        }

        public void ReadXml(XmlReader reader)
        {
            var text = (string)reader.ReadElementContentAs(typeof(string), null);
            if (!String.IsNullOrEmpty(text))
            {
                var version = GetVersionNumber(text);
                this.Major = version.Major;
                this.Minor = version.Minor;
                this.Revision = version.Revision;
            }
        }

        public XmlSchema GetSchema() => null;
        
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Revision { get; set; }
        
        public static bool operator ==(VersionNumber a, VersionNumber b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            return a.IsEqual(b);
        }

        public static bool operator !=(VersionNumber a, VersionNumber b)
        {
            if (a is null && b is null) return false;
            if (a is null || b is null) return true;
            return !a.IsEqual(b);
        }
        public static bool operator >=(VersionNumber a, VersionNumber b)
        {
            if (a is null || b is null) throw new ArgumentNullException();
            return a.IsGreaterThanOrEqualTo(b);
        }
        public static bool operator >(VersionNumber a, VersionNumber b)
        {
            if (a is null || b is null) throw new ArgumentNullException();
            return a.IsGreaterThan(b);
        } 

        public static bool operator <=(VersionNumber a, VersionNumber b)
        {
            if (a is null || b is null) throw new ArgumentNullException();
            return a.IsLessThanOrEqualTo(b);
        }
        public static bool operator <(VersionNumber a, VersionNumber b)
        {
            if (a is null || b is null) throw new ArgumentNullException();
            return a.IsLessThan(b);
        }
            
        public bool IsEqual(VersionNumber other) => Major == other.Major && Minor == other.Minor && Revision == other.Revision;
        public bool IsGreaterThan(VersionNumber other) => Major > other.Major || (Major >= other.Major && Minor > other.Minor) || (Major >= other.Major && Minor >= other.Minor && Revision > other.Revision);
        public bool IsLessThan(VersionNumber other) => other.IsGreaterThan(this);
        
        public bool IsGreaterThanOrEqualTo(VersionNumber other) => IsGreaterThan(other) || IsEqual(other);
        public bool IsLessThanOrEqualTo(VersionNumber other) => IsLessThan(other) || IsEqual(other);
        
        public override bool Equals(object obj)
        {
            if (obj is VersionNumber other)
                return this == other;
            return false;
        }

        public override string ToString() => Major + "." + Minor + "." + Revision;

        public static VersionNumber GetVersionNumber(string toBeParsed)
        {
            // Examples:
            // 0.4
            // 0.4 Alpha
            // 1.0.5
            // 1.0.5 Beta
            
            VersionNumber number = new VersionNumber();
            
            // Remove characters after first space (and the space itself)
            if (toBeParsed.IndexOf(' ') >= 0)
                toBeParsed = toBeParsed.Substring(0, toBeParsed.IndexOf(' '));

            var numbers = toBeParsed.Split('.');
            if (numbers.Length <= 0)
                throw new XmlException($"Invalid version number '{toBeParsed}'");
            for (var i = 0; i < numbers.Length; i++)
            {
                if (i == 0)
                    number.Major = int.Parse(numbers[i], CultureInfo.InvariantCulture);
                if (i == 1)
                    number.Minor = int.Parse(numbers[i], CultureInfo.InvariantCulture);
                if (i == 2)
                    number.Revision = int.Parse(numbers[i], CultureInfo.InvariantCulture);

                if (i > 2)
                    throw new Exception("Version number invalid.");
            }
            return number;
        }
    }
    
    public enum ErrorLevel
    {
        Success = 0,
        Error = 1,
        FatalError = 2,
    }
    
    public class Playbook : XmlDeserializable
    {
        public double MeasureStringWidth(string candidate, Typeface typeface, double fontSize)
        {
            var formattedText = new FormattedText(
                candidate,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black,
                1);

            return formattedText.Width;
        }
        
        public override void Validate()
        {
            if (UniqueId != null && UniqueId == Guid.Empty)
                throw new XmlException("UniqueId must be unique, currently it is not unique. Use an online UUIDv4 generator to create a new unique ID.");
            if (Name.Any(x => System.IO.Path.GetInvalidFileNameChars().Contains(x)))
                throw new XmlException(@"Playbook Name cannot contain invalid file name characters including: \ / : * ? < > |");
            if (MeasureStringWidth(Name, new Typeface("Segoe UI"), 14.5) > 97)
                throw new XmlException("Playbook Name is too long.");
            if (MeasureStringWidth(Username, new Typeface("Segoe UI"), 13) > 100)
                throw new XmlException("Playbook Username is too long.");
            if (Wrap.ExecuteSafe(() => VersionNumber.GetVersionNumber(Version), false).Failed)
                throw new XmlException($"Improper version format '{Version}'. Version must follow one of these formats:\r\n1\r\n1.0\r\n1.0.0");

            if (SupportsISO)
            {
                if (OOBE == null || OOBE.BulletPoints == null || OOBE.BulletPoints.Count == 0)
                    throw new XmlException("OOBE BulletPoints must be specified when SupportsISO is true.");
                
                if (OOBE.BulletPoints == null || OOBE.BulletPoints.Count != 3)
                    throw new XmlException("There must be exactly three features under OOBEFeatures.");
                else if (OOBE.BulletPoints.Any(x => OOBE.BulletPoints.Any(y => y != x && y.Icon == x.Icon)))
                    throw new XmlException("OOBE feature icons must be distinct.");
                else if (OOBE.BulletPoints.Any(x => string.IsNullOrEmpty(x.Title) || string.IsNullOrWhiteSpace(x.Description)))
                    throw new XmlException("OOBE feature title and description must be non-empty.");
            }
            
            IsUpgradeApplicable("1.0.0");
        }
        
        [CanBeNull]
        public ISOSettings ISO { get; set; } = null;
        [CanBeNull]
        public OOBESettings OOBE { get; set; } = null;
        
        [XmlRequired(false)]
        public string Name { get; set; }
        [XmlRequired(false)]
        public string ShortDescription { get; set; }
        [XmlRequired(false)]
        public string Description { get; set; }
        
        [XmlRequired(false)]
        public string Title { get; set; }
        [XmlRequired(false)]
        public string Username { get; set; }
        public string Details { get; set; }
        [XmlRequired(false)]
        public string Version { get; set; }
        
        [XmlArray]
        [XmlArrayItem(Type = typeof(CheckboxPage))]
        [XmlArrayItem(Type = typeof(RadioPage))]
        [XmlArrayItem(Type = typeof(RadioImagePage))]
        public FeaturePage[] FeaturePages { get; set; }
        public Package[] Software { get; set; } = Array.Empty<Package>();

        public string ProgressText { get; set; } = "Deploying the selected Playbook configuration onto the system.";
        public int EstimatedMinutes { get; set; } = 25;
        
#nullable enable
        public string[]? SupportedBuilds { get; set; }
        public Requirements.Requirement[] Requirements { get; set; } = new Requirements.Requirement[] {};
        public string? InstallGuide { get; set; }
        public string? Git { get; set; }
        public string? DonateLink { get; set; }
        public string? Website { get; set; }
        public string? ProductCode { get; set; }
        public string? PasswordReplace { get; set; }
        public Guid? UniqueId { get; set; }
        [XmlAllowInlineArrayItem]
        public string[]? UpgradableFrom { get; set; }
        public bool? AllowUnsupportedUpgrades { get; set; } = true;
#nullable disable
        public bool Overhaul { get; set; } = false;
        public bool SupportsISO { get; set; } = false;

        [XmlIgnore]
        public string Path { get; set; }
        
        public bool? UseKernelDriver { get; set; } = null;

        [XmlIgnore]
        public List<string> Options { get; set; } = null;
        
        
        // Used for applied Playbooks
        [XmlIgnore]
        public ErrorLevel ErrorLevel = 0;
        [XmlIgnore]
        public string[] SelectedOptions = new string[] {};
        [XmlIgnore]
        public string[] AvailableOptions = new string[] {};
        [XmlIgnore]
        public DateTime AppliedTimeUTC = new DateTime();
        [XmlIgnore] [CanBeNull]
        public byte[] ImageBytes = null;

        [CanBeNull]
        public List<WindowsUpdate> ExcludedWindowsUpdates { get; set; } = null;
        public bool ExcludeBadWindowsUpdates { get; set; } = false;

        [NotNull]
        public static Playbook[] GetAppliedPlaybooks()
        {
            var list = new List<Playbook>();
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\AME\Playbooks\Applied");
            if (key != null)
            {
                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        var guid = subKeyName.Trim(new[] { '{', '}' });
                        if (Guid.TryParse(guid, out Guid uniqueId))
                        {
                            using var subKey = key.OpenSubKey(subKeyName)!;

                            var result = new Playbook();
                            result.UniqueId = uniqueId;
                            result.Version = (string)subKey.GetValue("Version");
                            result.Name = (string)subKey.GetValue("Name");
                            result.Username = (string)subKey.GetValue("Username");
                            result.Overhaul = unchecked((int)Convert.ToUInt32(subKey.GetValue("Overhaul"))) == 1;

                            result.ErrorLevel = (ErrorLevel)unchecked((int)Convert.ToUInt32(subKey.GetValue("ErrorLevel")));
                            result.AvailableOptions = (string[])subKey.GetValue("AvailableOptions");
                            result.SelectedOptions = (string[])subKey.GetValue("SelectedOptions");
                            result.AppliedTimeUTC = DateTime.FromBinary((long)subKey.GetValue("AppliedTimeUTC"));
                            
                            result.ImageBytes = (byte[])subKey.GetValue("Image");
                            list.Add(result);
                        }
                    }
                    catch (Exception e) { }
                }
            }

            var appliedDir = Environment.ExpandEnvironmentVariables(@"%ProgramData%\AME\AppliedPlaybooks");
            if (Directory.Exists(appliedDir))
            {
                foreach (var appliedPB in Directory.GetDirectories(appliedDir).Reverse())
                {
                    try
                    {
                        var result = AmeliorationUtil.DeserializePlaybook(appliedPB);

                        if (File.Exists(System.IO.Path.Combine(appliedPB, "playbook.png")) && File.Exists(System.IO.Path.Combine(appliedPB, "verified.txt")))
                            result.ImageBytes = File.ReadAllBytes(System.IO.Path.Combine(appliedPB, "playbook.png"));
                        if (File.Exists(System.IO.Path.Combine(appliedPB, "errors.txt")))
                            result.ErrorLevel = ErrorLevel.Error;

                        result.Path = appliedPB;
                        list.Add(result);
                    }
                    catch { }
                }
            }
            return list.ToArray();
        }

        [CanBeNull]
        public virtual Playbook LastAppliedMatch([NotNull] IEnumerable<Playbook> appliedPlaybooks)
        {
            Playbook idMatch = null, userMatch = null;

            foreach (var item in appliedPlaybooks)
            {
                if (UniqueId == item.UniqueId)
                {
                    idMatch = item;
                    break;
                }
                else if (userMatch == null && Name == item.Name && Username == item.Username) userMatch = item;
            }

            return idMatch ?? userMatch;
        }
        
        public bool IsUpgradeApplicable(string oldVersion)
        {
            if (UpgradableFrom != null)
            {
                var oldVersionNumber = VersionNumber.GetVersionNumber(oldVersion);

                foreach (var version in UpgradableFrom)
                {
                    if (version == "any")
                        return true;
                    
                    if (version.Contains('-'))
                    {
                        var split = version.Split('-');
                        if (split.Length != 2) throw new XmlException($"Invalid version range format '{version}'");
                        var version1 = VersionNumber.GetVersionNumber(split[0]);
                        var version2 = VersionNumber.GetVersionNumber(split[1]);

                        if (oldVersionNumber.IsGreaterThanOrEqualTo(version1) && oldVersionNumber.IsLessThanOrEqualTo(version2))
                            return true;
                        
                        continue;
                    }

                    var versionNumberResult = Wrap.ExecuteSafe(() => VersionNumber.GetVersionNumber(version));
                    if (versionNumberResult.Failed)
                        throw new XmlException($"Invalid '{nameof(UpgradableFrom)}' value '{version}'. Values must follow one of the these formats:\r\n1.0.0\r\n1.0.0-2.0.0\r\nany");

                    if (versionNumberResult.Value == oldVersionNumber)
                        return true;
                }
            }
            return false;
        }


        


        public VersionNumber GetVersionNumber()
        {
            return VersionNumber.GetVersionNumber(Version);
        }

        public async Task<string> LatestPlaybookVersion()
        {

            if (!IsValidGit())
            {
                throw new ArgumentException("Link provided is not a proper Git link.");
            }

            string gitPlatform = GetPlaybookGitPlatform();

            string repo = GetRepository();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("curl/7.55.1"); //Required for GitHub

            string url = gitPlatform switch
            {
                "github.com" => $"https://api.github.com/repos/{repo}/releases",
                "gitlab.com" => $"https://gitlab.com/api/v4/projects/{Uri.EscapeDataString(repo)}/releases",
                _ => $"https://{gitPlatform}/api/v1/repos/{repo}/releases"
            };
            
            var response = await httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
                    
            var json = await response.Content.ReadAsStringAsync();
            var array = JArray.Parse(json);

            var tag = (string)array.FirstOrDefault()?["tag_name"];
            
            return tag?.TrimStart('v');
        }
        public async Task<List<string>> GetPlaybookVersions()
        {

            if (!IsValidGit())
            {
                throw new ArgumentException("Link provided is not a proper Git link.");
            }

            string gitPlatform = GetPlaybookGitPlatform();

            string repo = GetRepository();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("curl/7.55.1"); //Required for GitHub

            string url = gitPlatform switch
            {
                "github.com" => $"https://api.github.com/repos/{repo}/releases",
                "gitlab.com" => $"https://gitlab.com/api/v4/projects/{Uri.EscapeDataString(repo)}/releases",
                _ => $"https://{gitPlatform}/api/v1/repos/{repo}/releases"
            };
            
            var response = await httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();
                    
            var json = await response.Content.ReadAsStringAsync();
            var array = JArray.Parse(json);

            var result = new List<string>();
            foreach (var releaseToken in array)
                result.Add(((string)releaseToken["tag_name"])?.TrimStart('v'));

            return result;
        }
        
        public async Task DownloadLatestPlaybook(BackgroundWorker worker = null)
        {
            string repo = GetRepository();
            string gitPlatform = GetPlaybookGitPlatform();

            var httpClient = new WinUtil.HttpProgressClient();
            httpClient.Client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/7.55.1"); //Required for GitHub

            var downloadUrl = string.Empty;
            var downloadDir = System.IO.Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "AME");
            var downloadPath = System.IO.Path.Combine(downloadDir, "playbook.apbx");

            string baseUrl;
            string releasesUrl;
            string assetsKey;
            string browserDownloadUrlKey;

            switch (gitPlatform)
            {
                case "github.com":
                    baseUrl = "https://api.github.com";
                    releasesUrl = $"{baseUrl}/repos/{repo}/releases";
                    assetsKey = "assets";
                    browserDownloadUrlKey = "browser_download_url";
                    break;

                case "gitlab.com":
                    baseUrl = "https://gitlab.com/api/v4";
                    releasesUrl = $"{baseUrl}/projects/{Uri.EscapeDataString(repo)}/releases";
                    assetsKey = "assets.links";
                    browserDownloadUrlKey = "direct_asset_url";
                    break;

                default:
                    baseUrl = $"https://{gitPlatform}/api/v1";
                    releasesUrl = $"{baseUrl}/repos/{repo}/releases";
                    assetsKey = "assets";
                    browserDownloadUrlKey = "browser_download_url";
                    break;
            }

            var releasesResponse = await httpClient.GetAsync(releasesUrl);
            releasesResponse.EnsureSuccessStatusCode();

            var releasesContent = await releasesResponse.Content.ReadAsStringAsync();
            var releases = JArray.Parse(releasesContent);
            var release = releases.FirstOrDefault();

            long size = 3000000;
            
            if (release?.SelectToken(assetsKey) is JArray assets)
            {
                var asset = assets.FirstOrDefault(a => a["name"].ToString().EndsWith(".apbx"));
                if (asset != null)
                {
                    downloadUrl = asset[browserDownloadUrlKey]?.ToString();
                    
                    if (asset["size"] != null) 
                        long.TryParse(asset["size"].ToString(), out size);
                }
            }

            if (worker != null)
                worker.ReportProgress(10);
            
            // Download the release asset
            if (!string.IsNullOrEmpty(downloadUrl))
            {
                httpClient.Client.DefaultRequestHeaders.Clear();
                
                httpClient.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) => {
                    if (progressPercentage.HasValue && worker != null)
                        worker.ReportProgress((int)Math.Ceiling(10 + (progressPercentage.Value * 0.7)));
                };
                
                await httpClient.StartDownload(downloadUrl, downloadPath, size);
            }
            httpClient.Dispose();
        }

        public string GetRepository()
        {
            if (Git == null)
            {
                return null;
            }

            var urlSegments = Git.Replace("https://", "").Replace("http://", "").Split('/');
            return urlSegments[1] +"/"+ urlSegments[2];
        }

        public string GetPlaybookGitPlatform()
        {
            if (this.Git == null)
            {
                throw new NullReferenceException("No Git link available.");
            }

            return new Uri(Git).Host;
        }

        public bool IsValidGit()
        {
            if (Git == null)
            {
                throw new NullReferenceException("No Git link available.");
            }

            return Regex.IsMatch(Git, "((git|ssh|http(s)?)|(git@[\\w\\.]+))(:(//)?)([\\w\\.@\\:/\\-~]+)(/)?");;
        }
        public override string ToString()
        {
            return $"Name: {Name}\nDescription: {Description}\nUsername: {Username}\nDetails: {Details}\nRequirements: {Requirements}."; 
        }

        

        

        public class CheckboxPage : FeaturePage
        {
            public override void Validate()
            {
                if (Options.Length > 4 && TopLine != null && BottomLine != null)
                    throw new Exception(@$"CheckboxPage with a TopLine and BottomLine must not have more than 4 options.");
                if (Options.Length > 5 && (TopLine != null || BottomLine != null))
                    throw new Exception(@$"CheckboxPage with a TopLine or BottomLine must not have more than 5 options.");
                if (Options.Length > 7)
                    throw new Exception(@$"CheckboxPage must not have more than 7 options.");

                if (Options.Distinct().Count() != Options.Length)
                    throw new XmlException("Duplicate options found in CheckboxPage.");
            }
            
            public class CheckboxOption : Option
            {
                [XmlAttribute]
                public bool IsChecked { get; set; } = true;
                [XmlAttribute]
                public bool IsEnabled { get; set; } = true;
            }

            [XmlArray]
            [XmlArrayItem(ElementName = "CheckboxOption", Type = typeof(CheckboxOption))]
            public override Option[] Options { get; set; }
        }
        public class RadioPage : FeaturePage
        {
            public override void Validate()
            {
                if (Options.Length > 4 && TopLine != null && BottomLine != null)
                    throw new XmlException(@$"RadioPage with a TopLine and BottomLine must not have more than 4 options.");
                if (Options.Length > 5 && (TopLine != null || BottomLine != null))
                    throw new XmlException(@$"RadioPage with a TopLine or BottomLine must not have more than 5 options.");
                if (Options.Length > 7)
                    throw new XmlException(@$"RadioPage must not have more than 7 options.");
                    
                if (DefaultOption != null && !Options.Any(x => x.Name == DefaultOption))
                    throw new XmlException(@$"No option matching DefaultOption {DefaultOption} in Radio");
                
                if (Options.Distinct().Count() != Options.Length)
                    throw new XmlException("Duplicate options found in RadioPage.");
            }

            [XmlAttribute]
            public string DefaultOption { get; set; } = null;
            public class RadioOption : Option
            {
            }

            [XmlArray]
            [XmlArrayItem(ElementName = "RadioOption", Type = typeof(RadioOption))]
            public override Option[] Options { get; set; }
            [CanBeNull] public override string[] OptionNames() => Options?.Select(x => x.Name).ToArray();
        }
        public class RadioImagePage : FeaturePage
        {
            public override void Validate()
            {
                if (Options.Length > 8)
                    throw new XmlException(@$"RadioImagePage must not have more than 8 options.");
                
                if (DefaultOption != null && !Options.Any(x => x.Name == DefaultOption))
                    throw new XmlException(@$"No option matching DefaultOption {DefaultOption} in RadioImagePage.");

                if (Options.Distinct().Count() != Options.Length)
                    throw new XmlException("Duplicate options found in RadioImagePage.");

                if (Options.OfType<RadioImageOption>().Any(x => x.GradientTopColor == x.GradientBottomColor && !x.None))
                    throw new XmlException("RadioImageOption gradient colors must not be the same.");
                if (Options.OfType<RadioImageOption>().Any(x => !x.None && (x.GradientTopColor.Length != 7 || x.GradientBottomColor.Length != 7)))
                    throw new XmlException("RadioImageOption gradient colors must be in the format #RRGGBB.");
                if (Options.OfType<RadioImageOption>().Any(x => string.Equals(x.GradientTopColor, "#FFFFFF", StringComparison.OrdinalIgnoreCase) || string.Equals(x.GradientBottomColor, "#FFFFFF", StringComparison.OrdinalIgnoreCase) || string.Equals(x.GradientTopColor, "#000000", StringComparison.OrdinalIgnoreCase) || string.Equals(x.GradientBottomColor, "#000000", StringComparison.OrdinalIgnoreCase)))
                    throw new XmlException("RadioImageOption gradient colors must not be black or white.");
            }

            [XmlAttribute]
            public string DefaultOption { get; set; } = null;
            public class RadioImageOption : Option
            {
                public string FileName { get; set; } = null;

                public bool Fill { get; set; } = false;
                [XmlAttribute]
                public bool None { get; set; } = false;

                public string GradientTopColor { get; set; } = null;
                public string GradientBottomColor { get; set; } = null;
            }
            
            [XmlArray]
            [XmlArrayItem(Type = typeof(RadioImageOption))]
            public override Option[] Options { get; set; }
            [CanBeNull] public override string[] OptionNames() => Options?.Select(x => x.Name).ToArray();

            [XmlAttribute]
            public bool CheckDefaultBrowser { get; set; } = false;
        }
        
        public abstract class FeaturePage : XmlDeserializable
        {
            public override void Validate() => throw new Exception("FeaturePage cannot be used directly. Use CheckboxPage, RadioPage, or RadioImagePage instead.");

            [XmlAttribute]
            public string DependsOn { get; set; } = null;
            [XmlAttribute]
            public string WindowsVersion { get; set; } = null;
            [XmlAttribute]
            public bool IsRequired { get; set; } = false;
            public Line TopLine { get; set; } = null;
            public Line BottomLine { get; set; } = null;
            
            public class Option
            {
                public string Name { get; set; } = null;
                public virtual string Text { get; set; }
                
                [XmlAttribute]
                public string DependsOn { get; set; } = null;
                [XmlAttribute]
                public string WindowsVersion { get; set; } = null;
            }
            public class Line
            {
                [XmlAttribute("Text")]
                public string Text { get; set; }
                [XmlAttribute("Link")]
                public string Link { get; set; } = null;
            }
            
            [XmlArray]
            [XmlArrayItem(Type = typeof(Option))]
            public virtual Option[] Options { get; set; }
            [CanBeNull] public virtual string[] OptionNames() => Options?.Select(x => x.Name).ToArray();
            
            [XmlAttribute]
            public string Description { get; set; }
        }
    
        public class Package : XmlDeserializable
        {
            public override void Validate()
            {
                if (string.IsNullOrWhiteSpace(Name))
                    throw new XmlException("Software must have a Name.");
                if (string.IsNullOrWhiteSpace(Title))
                    throw new XmlException("Software must have a Title.");
                if (string.IsNullOrWhiteSpace(Description))
                    throw new XmlException("Software must have a Description.");
                if (string.IsNullOrWhiteSpace(Icon))
                    throw new XmlException("Software must have an Icon specified.");
            }

            [XmlAttribute]
            public string Option { get; set; } = null;
            [XmlAttribute]
            public bool Local { get; set; } = true;
            [XmlAttribute]
            public bool? DefaultWebBrowser { get; set; } = null;
            
            public string Name { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string Icon { get; set; }
        }
    }
    public enum ComponentIcon
    {
        Rocket,
        Privacy,
        Lock,
    }

    public class ISOSettings
    {
        public bool DisableBitLocker { get; set; } = false;
        public bool DisableHardwareRequirements { get; set; } = false;
    }

    public class OOBESettings
    {
        public OOBE.InternetRequirementLevel? Internet { get; set; } = null;
        public List<BulletPoint> BulletPoints { get; set; } = null;
    }
    public class BulletPoint
    {
        [XmlAttribute]
        public ComponentIcon Icon { get; set; }
        [XmlAttribute]
        public string Title { get; set; }
        [XmlAttribute]
        public string Description { get; set; }
    }
}
