using Microsoft.Win32;
using System.Windows;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.Utils
{
    public static class ThemeWatcher
    {
        public enum WindowsTheme
        {
            Light,
            Dark
        }

        private const string RegistryKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";

        private const string RegistryValueName = "AppsUseLightTheme";

        private static System.Threading.Timer _timer;

        private static WindowsTheme? _currentTheme;

        private static ResourceDictionary ThemeDictionary => System.Windows.Application.Current.Resources.MergedDictionaries[0];

        public static WindowsTheme CurrentTheme
        {
            get
            {
                if (!_currentTheme.HasValue)
                {
                    _currentTheme = GetWindowsTheme();
                }
                return _currentTheme.Value;
            }
            set
            {
                _currentTheme = value;
            }
        }

        public static void WatchTheme()
        {
            _timer = new System.Threading.Timer(delegate
            {
                try
                {
                    WindowsTheme windowsTheme = GetWindowsTheme();
                    if (windowsTheme != CurrentTheme)
                    {
                        CurrentTheme = windowsTheme;
                        ChangeTheme();
                    }
                }
                catch (Exception)
                {
                }
            }, null, 2000, 2000);
            CurrentTheme = GetWindowsTheme();
            ChangeTheme();
        }

        private static void ChangeTheme()
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)delegate
            {
                ThemeDictionary.MergedDictionaries.Clear();
                string text = ((GlobalsGUI.WinVer >= 22523) ? "Windows11" : "Windows10");
                ThemeDictionary.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("Themes/" + text + "/" + CurrentTheme.ToString() + "Styles.xaml", UriKind.RelativeOrAbsolute)
                });
                ThemeDictionary.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("Themes/" + text + "/" + CurrentTheme.ToString() + "Resources.xaml", UriKind.RelativeOrAbsolute)
                });
                ThemeDictionary.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("Themes/" + text + "/SharedStyles.xaml", UriKind.RelativeOrAbsolute)
                });
            });
        }

        private static WindowsTheme GetWindowsTheme()
        {
            object registryValueObject = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize")?.GetValue("AppsUseLightTheme");
            if (registryValueObject == null)
            {
                return WindowsTheme.Light;
            }
            if ((int)registryValueObject <= 0)
            {
                return WindowsTheme.Dark;
            }
            return WindowsTheme.Light;
        }
    }
}
