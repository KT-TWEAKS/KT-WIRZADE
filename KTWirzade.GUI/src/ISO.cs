using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using KTWirzade.GUI.Utils;
using KTWirzade.GUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;

namespace KTWirzade.GUI
{
    public class ISO : INotifyPropertyChanged, IDragItem
    {
        private string _name;

        private string _shortDescription;

        private string _fileNameWithoutExtension;

        private BitmapImage _icon = System.Windows.Application.Current.Dispatcher.Invoke(() => GUIUtil.GetIconResource(Environment.ExpandEnvironmentVariables("%SYSTEMROOT%\\System32\\imageres.dll"), -5205));

        private ViewModelBase _currentPage;

        private bool _selected;

        private double _fadeOpacity;

        private int _sidebarInitialHeight;

        private bool _itemClickable = true;

        private Visibility _progressVisibility = Visibility.Collapsed;

        private double _progressValue;

        private bool _checked;

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                SetProperty(ref _name, value, "Name");
            }
        }

        public Guid? UniqueId { get; set; }

        public string ShortDescription
        {
            get
            {
                return _shortDescription;
            }
            set
            {
                SetProperty(ref _shortDescription, value, "ShortDescription");
            }
        }

        public string Description { get; set; }

        public string Title { get; set; }

        public string DisplayUsername
        {
            get
            {
                return Username;
            }
            set
            {
                Username = value;
            }
        }

        public string Username { get; set; }

        public string FilePath { get; set; }

        public long? Size { get; set; }

        public string Version { get; set; }

        public ImageParsers.ImageArchitecture? Architecture { get; set; }

        public Shared.ISO Configuration { get; set; }

        public FileSystemWatcher Watcher { get; set; }

        public bool IsWindows { get; set; }

        public bool IsWindows11 { get; set; }

        public bool IsESD { get; set; }

        public string WinMajorVer { get; set; }

        public int? WinVer { get; set; }

        public int? WinUpdateVer { get; set; }

        public string UsbIconUri { get; set; } = "pack://application:,,,/Icons/usb_ame_logo.png";

        public PlaybookGUI SelectedPlaybook { get; set; }

        [XmlIgnore]
        public string FileNameWithoutExtension
        {
            get
            {
                if (_fileNameWithoutExtension == null)
                {
                    _fileNameWithoutExtension = FilePath;
                }
                return _fileNameWithoutExtension;
            }
        }

        [XmlIgnore]
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

        [XmlIgnore]
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

        [XmlIgnore]
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

        [XmlIgnore]
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

        [XmlIgnore]
        public List<string> WriteOptions { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void MergeFrom(ISO other)
        {
            if (other != null)
            {
                if (string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(other.Name))
                {
                    Name = other.Name;
                }
                if (string.IsNullOrWhiteSpace(Description) && !string.IsNullOrWhiteSpace(other.Description))
                {
                    Description = other.Description;
                }
                if (string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(other.Title))
                {
                    Title = other.Title;
                }
                if (string.IsNullOrWhiteSpace(Version) && !string.IsNullOrWhiteSpace(other.Version))
                {
                    Version = other.Version;
                }
                if (string.IsNullOrWhiteSpace(ShortDescription) && !string.IsNullOrWhiteSpace(other.ShortDescription))
                {
                    ShortDescription = other.ShortDescription;
                }
                if (string.IsNullOrWhiteSpace(DisplayUsername) && !string.IsNullOrWhiteSpace(other.DisplayUsername))
                {
                    DisplayUsername = other.DisplayUsername;
                }
                if (string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(other.Username))
                {
                    Username = other.Username;
                }
                if (!Architecture.HasValue && other.Architecture.HasValue)
                {
                    Architecture = other.Architecture;
                }
                if (Configuration == null && other.Configuration != null)
                {
                    Configuration = other.Configuration;
                }
            }
        }

        public static string RemoveInvalidFilePathCharacters(string filename, string replaceChar)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars());
            return new Regex($"[{Regex.Escape(regexSearch)}]").Replace(filename, replaceChar);
        }

        protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
        {
            if (!object.Equals(property, value))
            {
                property = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
