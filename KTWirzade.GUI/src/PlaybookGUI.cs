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
                if (AccentColor == null && value != null)
                {
                    AccentColor = ExtractDominantAccent(value);
                }
            }
        }

        /// <summary>
        /// Dominant vibrant color extracted from the playbook icon. Used to tint the
        /// progress bar and sidebar accent while this playbook is selected.
        /// </summary>
        public System.Windows.Media.Color? AccentColor { get; private set; }


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

        /// <summary>
        /// Samples the playbook icon and returns its most vibrant dominant color,
        /// brightness-clamped so it stays readable on dark surfaces.
        /// </summary>
        private static System.Windows.Media.Color? ExtractDominantAccent(byte[] imageBytes)
        {
            try
            {
                if (imageBytes == null || imageBytes.Length == 0)
                    return null;

                using MemoryStream stream = new MemoryStream(imageBytes);
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                return ExtractDominantAccent(bitmap);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static System.Windows.Media.Color? ExtractDominantAccent(BitmapSource source)
        {
            try
            {
                if (source == null)
                    return null;

                int width = source.PixelWidth;
                int height = source.PixelHeight;
                if (width < 4 || height < 4)
                    return null;

                FormatConvertedBitmap bitmap = new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];
                bitmap.CopyPixels(pixels, stride, 0);

                int step = Math.Max(1, Math.Min(width, height) / 24);
                Dictionary<int, int[]> buckets = new Dictionary<int, int[]>();

                for (int y = 0; y < height; y += step)
                {
                    for (int x = 0; x < width; x += step)
                    {
                        int i = y * stride + x * 4;
                        int blue = pixels[i];
                        int green = pixels[i + 1];
                        int red = pixels[i + 2];
                        int alpha = pixels[i + 3];
                        if (alpha < 120)
                            continue;

                        int max = Math.Max(red, Math.Max(green, blue));
                        int min = Math.Min(red, Math.Min(green, blue));
                        // Skip transparent, near-gray and near-black pixels.
                        if (max - min < 40 || max < 50)
                            continue;

                        int key = ((red >> 4) << 8) | ((green >> 4) << 4) | (blue >> 4);
                        if (!buckets.TryGetValue(key, out int[] bucket))
                        {
                            bucket = new int[4];
                            buckets[key] = bucket;
                        }
                        bucket[0] += red;
                        bucket[1] += green;
                        bucket[2] += blue;
                        bucket[3]++;
                    }
                }

                if (buckets.Count == 0)
                    return null;

                int[] best = buckets.Values.OrderByDescending(b => b[3]).First();
                double r = best[0] / (double)best[3];
                double g = best[1] / (double)best[3];
                double b = best[2] / (double)best[3];

                // Clamp luminance into a visible band for dark UI surfaces.
                double luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
                if (luminance > 0.001 && luminance < 0.42)
                {
                    double scale = 0.42 / luminance;
                    r = Math.Min(255, r * scale);
                    g = Math.Min(255, g * scale);
                    b = Math.Min(255, b * scale);
                }
                else if (luminance > 0.88)
                {
                    double scale = 0.88 / luminance;
                    r *= scale;
                    g *= scale;
                    b *= scale;
                }

                return System.Windows.Media.Color.FromRgb((byte)r, (byte)g, (byte)b);
            }
            catch (Exception)
            {
                return null;
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
            if (await App.AdminNodeLaunched.WaitAsync(5000))
            {
                App.AdminNodeLaunched.Release();
            }
            await InterLink.ExecuteSafeAsync((Expression<Action>)(() => WriteEncryptedStatusAdmin(encryptedString, FileNameWithoutExtension + ".status")), true, -1);
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
            if (ProductCode == null && Hash == null &&
                ((Name + " " + Username).Contains("KT WIRZADE") || (Name + Username).Contains("Ameliorated")))
            {
                // Impersonation of the KT WIRZADE/Ameliorated name without a product code
                // is always treated as malicious.
                VerificationStatus = VerificationLevel.Malicious;
                return;
            }

            if (Hash == null)
            {
                try
                {
                    await GetHash();
                }
                catch (Exception ex)
                {
                    Log.EnqueueExceptionSafe(ex, "Playbook verification skipped: no local .apbx to hash.");
                    VerificationStatus = VerificationLevel.Unverified;
                    return;
                }
            }

            // Playbooks pode não ter ProductCode (ex.: FSOS-XR10) — nesse caso
            // o registro oficial casa o .apbx pelo hash SHA-256.
            VerificationStatus = VerificationLevel.Unverified;
            switch (await IsVerified(ProductCode, Hash))
            {
                case "verified":
                    VerificationStatus = VerificationLevel.Verified;
                    break;
                case "malicious":
                    VerificationStatus = VerificationLevel.Malicious;
                    break;
                case "unverified":
                case "unknown":
                    VerificationStatus = VerificationLevel.Unverified;
                    break;
                case null:
                    VerificationStatus = VerificationLevel.Unreached;
                    break;
            }
        }

        private async Task<string> IsVerified(string productCode, string hash)
        {
            try
            {
                await System.Threading.Tasks.Task.CompletedTask;
                return null;
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Verification unavailable.", Array.Empty<(string, object)>());
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