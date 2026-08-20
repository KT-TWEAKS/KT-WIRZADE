using Core;
using Interprocess;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using KTWirzade.GUI.Utils;
using KTWirzade.GUI.ViewModels;
using KTWirzade.Shared;

namespace KTWirzade.GUI
{
    public class PlaybookGUI : Playbook, INotifyPropertyChanged, IDragItem
    {
        public enum VerificationLevel
        {
            Verified,
            Unverified,
            Malicious,
            Unreached
        }

        public class StatusFile
        {
            internal string Hash { get; set; }

            internal VerificationLevel VerificationLevel { get; set; }

            internal string MachineGuid { get; set; }

            internal DateTime LastChecked { get; set; }

            internal string PendingUpdate { get; set; }
        }

        private string _fileNameWithoutExtension;

        private DateTime _lastChecked;

        private VerificationLevel? _verificationStatus;

        private string _displayUsername;

        private string _progressTitle;

        private string _pendingUpdate;

        private bool _updatesChecked;

        private bool _itemClickable = true;

        private Visibility _progressVisibility = Visibility.Collapsed;

        private double _progressValue;

        private ViewModelBase _currentPage;

        private static BitmapImage _defaultIcon = System.Windows.Application.Current.Dispatcher.Invoke(() => new BitmapImage(new Uri("pack://application:,,,/Icons/playbook_frame_256.png")));
       // private static BitmapImage _defaultIcon = null;

        private BitmapImage _icon = null; //change

        private BitmapImage _iconCache;

        private bool _selected;

        private double _fadeOpacity;

        private int _sidebarInitialHeight;

        public List<BitmapImage> Images = new List<BitmapImage>();

        private bool _checked;

        private Dictionary<List<string>, string> Nodes { get; set; } = new Dictionary<List<string>, string>
    {
        {
            new List<string>
            {
                "am", "at", "be", "ch", "de", "es", "fi", "gb", "gr", "is",
                "lt", "lu", "mt", "nl", "no", "pt", "ru", "se", "sk", "sp",
                "tr", "ua", "sv", "sl", "sg", "ro", "pl", "lv", "it", "hu",
                "ie", "ge", "fr", "ee", "dk", "cz", "bo", "bg", "ba", "al",
                "mk"
            },
            "wng-eu.ktwirzade.com"
        },
        {
            new List<string>
            {
                "au", "ca", "ar", "br", "cl", "cn", "co", "cr", "do", "ec",
                "gt", "hn", "hk", "hr", "id", "ni", "nz", "pe", "pa", "ph",
                "pr", "py", "sv", "tw", "th", "us", "vn"
            },
            "wng-us.ktwirzade.com"
        }
    };

        public string UsbIconUri { get; set; }

        public Task VerificationTask { get; set; } = Task.CompletedTask;

        public string FileNameWithoutExtension
        {
            get
            {
                if (UniqueId.HasValue)
                {
                    return UniqueId.ToString().ToUpper();
                }
                if (_fileNameWithoutExtension == null)
                {
                    _fileNameWithoutExtension = RemoveInvalidFilePathCharacters(Username + "-" + Name, "~");
                }
                return _fileNameWithoutExtension;
            }
        }

        public DateTime LastChecked
        {
            get
            {
                return _lastChecked;
            }
            set
            {
                _lastChecked = value;
                LastCheckedString = value.ToShortDateString();
            }
        }

        internal StatusFile StatusInfo { get; set; }

        public string Hash { get; set; }

        public VerificationLevel? VerificationStatus
        {
            get
            {
                return _verificationStatus;
            }
            set
            {
                SetProperty(ref _verificationStatus, value, "VerificationStatus");
            }
        }

        public string FilePath
        {
            get
            {
                return null;
            }
            set
            {
            }
        }

        public string DisplayUsername
        {
            get
            {
                return _displayUsername ?? Username;
            }
            set
            {
                SetProperty(ref _displayUsername, value, "DisplayUsername");
            }
        }

        public string ProgressTitle
        {
            get
            {
                return _progressTitle;
            }
            set
            {
                SetProperty(ref _progressTitle, value, "ProgressTitle");
            }
        }

        public string PendingUpdate
        {
            get
            {
                return _pendingUpdate;
            }
            set
            {
                SetProperty(ref _pendingUpdate, value, "PendingUpdate");
            }
        }

        public bool UpdatesChecked
        {
            get
            {
                return _updatesChecked;
            }
            set
            {
                SetProperty(ref _updatesChecked, value, "UpdatesChecked");
            }
        }

        public bool ItemClickable
        {
            get
            {
                return _itemClickable;
            }
            set
            {
                SetProperty(ref _itemClickable, value, "ItemClickable");
            }
        }

        public Visibility ProgressVisibility
        {
            get
            {
                return _progressVisibility;
            }
            set
            {
                SetProperty(ref _progressVisibility, value, "ProgressVisibility");
            }
        }

        public double ProgressValue
        {
            get
            {
                return _progressValue;
            }
            set
            {
                SetProperty(ref _progressValue, value, "ProgressValue");
            }
        }

        public string PendingRenamePath { get; set; }

        public string LastCheckedString
        {
            get
            {
                if (LastChecked == default(DateTime))
                {
                    return "Never";
                }
                return LastChecked.ToShortDateString();
            }
            set
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LastCheckedString"));
            }
        }

        public ViewModelBase CurrentPage
        {
            get
            {
                if (_currentPage == null)
                {
                    _currentPage = new LoadPageViewModel();
                }
                return _currentPage;
            }
            set
            {
                SetProperty(ref _currentPage, value, "CurrentPage");
            }
        }

        public BitmapImage Icon
        {
            get
            {
                return _icon;
            }
            set
            {
                SetProperty(ref _icon, value, "Icon");
            }
        }

        public BitmapImage IconCache
        {
            get
            {
                if (_iconCache != null)
                {
                    return _iconCache;
                }
                string imagePath = ((Path == null) ? null : (File.Exists(System.IO.Path.Combine(Path, "playbook.png")) ? System.IO.Path.Combine(Path, "playbook.png") : (File.Exists(System.IO.Path.Combine(Path, "Images\\playbook.png")) ? System.IO.Path.Combine(Path, "Images\\playbook.png") : null)));
                if (imagePath == null)
                {
                    return _iconCache = _defaultIcon;
                }
                BitmapImage bmi = new BitmapImage();
                bmi.BeginInit();
                bmi.CacheOption = BitmapCacheOption.OnLoad;
                bmi.UriSource = new Uri(imagePath, UriKind.Absolute);
                bmi.EndInit();
                _iconCache = bmi;
                return _iconCache;
            }
            set
            {
                SetProperty(ref _iconCache, value, "IconCache");
            }
        }

        public bool Selected
        {
            get
            {
                return _selected;
            }
            set
            {
                SetProperty(ref _selected, value, "Selected");
                FadeOpacity = (_selected ? 0.04 : 0.0);
            }
        }

        public double FadeOpacity
        {
            get
            {
                return _fadeOpacity;
            }
            private set
            {
                SetProperty(ref _fadeOpacity, value, "FadeOpacity");
            }
        }

        public int SidebarInitialHeight
        {
            get
            {
                return _sidebarInitialHeight;
            }
            set
            {
                SetProperty(ref _sidebarInitialHeight, value, "SidebarInitialHeight");
            }
        }

        public bool Checked
        {
            get
            {
                return Volatile.Read(ref _checked);
            }
            set
            {
                Volatile.Write(ref _checked, value);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public PlaybookGUI(Playbook pb)
        {
            Name = pb.Name;
            Username = pb.Username;
            ShortDescription = pb.ShortDescription;
            Description = pb.Description;
            Title = pb.Title;
            Details = pb.Details;
            Requirements = pb.Requirements;
            Version = pb.Version;
            EstimatedMinutes = pb.EstimatedMinutes;
            Git = pb.Git;
            DonateLink = pb.DonateLink;
            Website = pb.Website;
            ProductCode = pb.ProductCode;
            PasswordReplace = pb.PasswordReplace;
            SupportedBuilds = pb.SupportedBuilds;
            Path = pb.Path;
            ProgressText = pb.ProgressText;
            FeaturePages = pb.FeaturePages;
            Overhaul = pb.Overhaul;
            UseKernelDriver = pb.UseKernelDriver;
            UniqueId = pb.UniqueId;
            UpgradableFrom = pb.UpgradableFrom;
            AllowUnsupportedUpgrades = pb.AllowUnsupportedUpgrades;
            ErrorLevel = pb.ErrorLevel;
            SelectedOptions = pb.SelectedOptions;
            AvailableOptions = pb.AvailableOptions;
            AppliedTimeUTC = pb.AppliedTimeUTC;
            InstallGuide = pb.InstallGuide;
            SupportsISO = pb.SupportsISO;
            OOBE = pb.OOBE;
            ISO = pb.ISO;
            ExcludedWindowsUpdates = pb.ExcludedWindowsUpdates;
            ExcludeBadWindowsUpdates = pb.ExcludeBadWindowsUpdates;
            if (pb.ImageBytes == null)
            {
                return;
            }
            try
            {
                using MemoryStream stream = new MemoryStream(pb.ImageBytes);
                BitmapImage bmi = new BitmapImage();
                bmi.BeginInit();
                bmi.CacheOption = BitmapCacheOption.OnLoad;
                bmi.StreamSource = stream;
                bmi.EndInit();
                Icon = bmi;
                Icon.Freeze();
            }
            catch (Exception)
            {
            }
        }

        public PlaybookGUI LastAppliedMatch(IEnumerable<Playbook> appliedPlaybooks)
        {
            Playbook idMatch = null;
            Playbook userMatch = null;
            foreach (Playbook item in appliedPlaybooks ?? Array.Empty<Playbook>())
            {
                if (UniqueId.HasValue && UniqueId == item.UniqueId)
                {
                    idMatch = item;
                    break;
                }
                if (userMatch == null && Name == item.Name && Username == item.Username)
                {
                    userMatch = item;
                }
            }
            if ((idMatch ?? userMatch) == null)
            {
                return null;
            }
            return new PlaybookGUI(idMatch ?? userMatch);
        }

        private async Task GetEncryptedStatus()
        {
            string statusFile = System.IO.Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks"), FileNameWithoutExtension + ".status");
            if (!File.Exists(statusFile))
            {
                throw new FileNotFoundException("Status file was not found.");
            }
            StatusFile result = new StatusFile();
            using (StreamReader reader = new StreamReader(statusFile))
            {
                string[] split = StringCipher.Decrypt(await reader.ReadLineAsync(), "wysca").Split('|');
                result.Hash = split[0];
                result.VerificationLevel = (VerificationLevel)Enum.Parse(typeof(VerificationLevel), split[1]);
                result.MachineGuid = split[2];
                result.LastChecked = DateTime.Parse(split[3], CultureInfo.InvariantCulture);
                result.PendingUpdate = (string.IsNullOrEmpty(split[4]) ? null : split[4]);
            }
            StatusInfo = result;
        }

        public async Task WriteEncryptedStatus()
        {
            string encryptedString;
            if (VerificationStatus.HasValue)
            {
                encryptedString = StringCipher.Encrypt($"{Hash}|{VerificationStatus.Value.ToString()}|{GlobalsGUI.MachineGuid}|{LastChecked}|{PendingUpdate}", "wysca");
            }
            else
            {
                encryptedString = StringCipher.Encrypt($"hash|{VerificationLevel.Unverified.ToString()}|GUID|{LastChecked}|{PendingUpdate}", "wysca");
            }
            //if (await App.AdminNodeLaunched.WaitAsync(5000))
            //{
            //    App.AdminNodeLaunched.Release();
            //}
            //await InterLink.ExecuteSafeAsync((Expression<Action>)(() => WriteEncryptedStatusAdmin(encryptedString, FileNameWithoutExtension + ".status")), true, -1);
        }

        [InterprocessMethod(Level.Administrator)]
        public static void WriteEncryptedStatusAdmin(string encryptedString, string statusFileName)
        {
            File.WriteAllText(System.IO.Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks"), statusFileName), encryptedString);
        }

        public async Task GetHash()
        {
            string path = System.IO.Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks"), FileNameWithoutExtension + ".apbx");
            if (!File.Exists(path))
            {
                throw new Exception("GetHash was called with no apbx file present.");
            }
            SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            Hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        public async Task GetVerificationStatus()
        {
            if (ProductCode == null)
            {
                if ((Name + Username).Contains("Revision") || (Name + Username).Contains("Atlas") || (Name + Username).Contains("AME ") || (Name + Username).Contains("Ameliorated"))
                {
                    VerificationStatus = VerificationLevel.Malicious;
                }
                else
                {
                    VerificationStatus = VerificationLevel.Unverified;
                }
                return;
            }
            if (Hash == null)
            {
                await GetHash();
            }
            VerificationStatus = VerificationLevel.Unverified;
            switch (await IsVerified(ProductCode, Hash))
            {
                case "true":
                    VerificationStatus = VerificationLevel.Verified;
                    break;
                case "false":
                    VerificationStatus = VerificationLevel.Malicious;
                    break;
                case "malicious":
                    VerificationStatus = VerificationLevel.Malicious;
                    break;
                case null:
                    VerificationStatus = VerificationLevel.Unreached;
                    break;
            }
        }

        private async Task<string> IsVerified(string productCode, string hash)
        {
            _ = 1;
            try
            {
                HttpClient client = new HttpClient
                {
                    Timeout = new TimeSpan(0, 0, 0, 5)
                };
                string region = "unknown";
                try
                {
                    RegistryKey geoKey = Registry.CurrentUser.OpenSubKey("Control Panel\\International\\Geo");
                    if (geoKey != null)
                    {
                        object value = geoKey.GetValue("Name");
                        region = ((value == null) ? region : ((string)value).ToLowerInvariant());
                    }
                }
                catch
                {
                }
                string domain = null;
                foreach (List<string> nodesKey in Nodes.Keys.Where((List<string> list) => list.Contains(region)))
                {
                    Nodes.TryGetValue(nodesKey, out domain);
                }
                if (domain == null)
                {
                    domain = "wng-eu.ktwirzade.com";
                }
                string url = "http://" + domain + ":8000/isVerified?prodID=" + productCode + "&hash=" + hash;
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    dynamic data = JObject.Parse(await response.Content.ReadAsStringAsync());
                    return data["isVerified"];
                }
                Log.EnqueueSafe((LogType)1, "Unable to connect to verification server.", (SerializableTrace)null, Array.Empty<(string, object)>());
                return null;
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Unable to connect to verification server.", Array.Empty<(string, object)>());
                return null;
            }
        }

        public async Task GetStatus()
        {
            if (!File.Exists(System.IO.Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks"), FileNameWithoutExtension + ".apbx")))
            {
                return;
            }
            string statusFile = System.IO.Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks"), FileNameWithoutExtension + ".status");
            bool statusError = false;
            if (File.Exists(statusFile))
            {
                try
                {
                    await GetEncryptedStatus();
                    LastChecked = StatusInfo.LastChecked;
                    PendingUpdate = StatusInfo.PendingUpdate;
                }
                catch
                {
                    statusError = true;
                }
            }
            Task task = Task.Run(async delegate
            {
                await GetHash();
                if (File.Exists(statusFile) && !statusError)
                {
                    try
                    {
                        if (StatusInfo.MachineGuid != GlobalsGUI.MachineGuid || StatusInfo.Hash != Hash)
                        {
                            File.Delete(statusFile);
                            throw new Exception();
                        }
                        if (StatusInfo.VerificationLevel == VerificationLevel.Verified)
                        {
                            VerificationStatus = VerificationLevel.Verified;
                            return;
                        }
                        throw new Exception();
                    }
                    catch
                    {
                        await GetVerificationStatus();
                        return;
                    }
                }
                await GetVerificationStatus();
            });
            Task updTask = Task.CompletedTask;
            if (Git != null && ProductCode != null && PendingUpdate == null && (int)DateTime.Now.Subtract(LastChecked).TotalMinutes > 30)
            {
                updTask = Task.Run(async delegate
                {
                    try
                    {
                        string releaseTag = await LatestPlaybookVersion();
                        if (VersionNumber.GetVersionNumber(releaseTag) > GetVersionNumber())
                        {
                            PendingUpdate = releaseTag;
                        }
                        UpdatesChecked = true;
                        LastChecked = DateTime.Now;
                    }
                    catch (Exception)
                    {
                    }
                });
            }
            else if ((int)DateTime.Now.Subtract(LastChecked).TotalMinutes <= 30)
            {
                UpdatesChecked = true;
            }
            await task;
            await updTask;
            await WriteEncryptedStatus();
            if (VerificationStatus != VerificationLevel.Malicious)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(delegate
                {
                    Icon = IconCache;
                });
            }
            else
            {
                DisplayUsername = ((VerificationStatus == VerificationLevel.Malicious) ? "Malicious" : "Unverified");
            }
        }

        public static string RemoveInvalidFilePathCharacters(string filename, string replaceChar)
        {
            string regexSearch = new string(System.IO.Path.GetInvalidFileNameChars());
            return new Regex($"[{Regex.Escape(regexSearch)}]").Replace(filename, replaceChar);
        }

        protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
        {
            if (!Equals(property, value))
            {
                property = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        string IDragItem.Username
        {
            get => Username;
            set => Username = value;
        }

        string IDragItem.Name
        {
            get => Name;
            set => Name = value;
        }

        string IDragItem.ShortDescription
        {
            get => ShortDescription;
            set => ShortDescription = value;
        }

        Guid? IDragItem.UniqueId
        {
            get => UniqueId;
            set => UniqueId = value;
        }

        string IDragItem.Description
        {
            get => Description;
            set => Description = value;
        }

        string IDragItem.Title
        {
            get => Title;
            set => Title = value;
        }

        string IDragItem.Version
        {
            get => Version;
            set => Version = value;
        }
    }
}