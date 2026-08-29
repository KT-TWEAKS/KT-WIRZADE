using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using KTWirzade.Shared;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

using KTWirzade.Shared.Customization;

namespace KTWirzade.GUI
{
    public static class GlobalsGUI
    {
        public class GUIGlobals : INotifyPropertyChanged
        {
            private PlaybookGUI _playbook;

            private ISO _ISO;

            private ObservableCollection<IDragItem> _items = new ObservableCollection<IDragItem>();

            private Playbook[] _appliedPlaybooks = (Playbook[])(object)new Playbook[0];

            private PlaybookGUI _wizardPlaybook = new PlaybookGUI(new Playbook
            {
                Name = "KT WIRZADE",
                Version = "1.0",
                Details = "KT WIRZADE v1.0 - Sistema de otimizacao e personalizacao do Windows. Modificado por kelvenapk (github.com/kelvenapk).",
                Username = "kelvenapk",
                Website = "https://github.com/kelvenapk"
            })
            {
                VerificationStatus = PlaybookGUI.VerificationLevel.Verified,
                Icon = new BitmapImage(new Uri("pack://application:,,,/Icons/wizard_icon_cropped_256.png"))
                //Icon = null
            };

            public PlaybookGUI Playbook
            {
                get
                {
                    return _playbook;
                }
                set
                {
                    _playbook = value;
                    if (value != null)
                    {
                        ISO = null;
                    }
                    ApplyPlaybookAccent(value);
                    OnPropertyChanged("Playbook");
                }
            }

            /// <summary>
            /// Tints the app accent resources (sidebar bar, progress bar) with the
            /// selected playbook's dominant color; falls back to the theme default
            /// when no playbook is selected or the playbook has no accent.
            /// </summary>
            private static void ApplyPlaybookAccent(PlaybookGUI playbook)
            {
                try
                {
                    System.Windows.Application app = System.Windows.Application.Current;
                    if (app == null)
                        return;

                    app.Dispatcher.Invoke(delegate
                    {
                        System.Windows.Media.Color? accent = playbook?.AccentColor;
                        if (accent.HasValue)
                        {
                            System.Windows.Media.SolidColorBrush brush = new System.Windows.Media.SolidColorBrush(accent.Value);
                            brush.Freeze();
                            app.Resources["PlaybookRectColor"] = brush;
                            app.Resources["ProgressBarBrush"] = brush;
                            app.Resources["ProgressBarColor"] = accent.Value;
                        }
                        else
                        {
                            // Drop the override so DynamicResource falls back to the theme dictionary.
                            app.Resources.Remove("PlaybookRectColor");
                            app.Resources.Remove("ProgressBarBrush");
                            app.Resources.Remove("ProgressBarColor");
                        }
                    });
                }
                catch (Exception)
                {
                }
            }

            public ISO ISO
            {
                get
                {
                    return _ISO;
                }
                set
                {
                    _ISO = value;
                    if (value != null)
                    {
                        Playbook = null;
                    }
                    OnPropertyChanged("ISO");
                }
            }

            public ObservableCollection<IDragItem> Items
            {
                get
                {
                    return _items;
                }
                set
                {
                    _items = value;
                    OnPropertyChanged("Items");
                }
            }

            public IEnumerable<PlaybookGUI> Playbooks => _items.OfType<PlaybookGUI>();

            public Playbook[] AppliedPlaybooks
            {
                get
                {
                    return _appliedPlaybooks;
                }
                set
                {
                    _appliedPlaybooks = value;
                    OnPropertyChanged("AppliedPlaybooks");
                }
            }

            public PlaybookGUI WizardPlaybook
            {
                get
                {
                    return _wizardPlaybook;
                }
                set
                {
                    _wizardPlaybook = value;
                    OnPropertyChanged("WizardPlaybook");
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            }

            protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
            {
                this.PropertyChanged?.Invoke(this, e);
            }
        }

        public class CommandHandler : ICommand
        {
            private Action _action;

            private Func<bool> _canExecute;

            public event EventHandler CanExecuteChanged
            {
                add
                {
                    CommandManager.RequerySuggested += value;
                }
                remove
                {
                    CommandManager.RequerySuggested -= value;
                }
            }

            public CommandHandler(Action action, Func<bool> canExecute)
            {
                _action = action;
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter)
            {
                return _canExecute();
            }

            public void Execute(object parameter)
            {
                _action();
            }
        }

        private static GUIGlobals _current = null;

        public static string UserPassword = null;

        public static string AdminPassword = null;

        public static string Username = null;

        public static bool AutoLogon = false;

        public static CustomizationProfile ActiveCustomizationProfile = null;

        public static UserCustomizationChoices ActiveCustomizationChoices = null;

        public static bool WUAStopperEngaged = false;

        public static readonly int WinVer;
        public static readonly string MachineGuid;

        static GlobalsGUI()
        {
            try
            {
                WinVer = int.Parse(Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion")?.GetValue("CurrentBuildNumber")?.ToString() ?? "0");
            }
            catch { WinVer = 0; }
            try
            {
                MachineGuid = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Cryptography")?.GetValue("MachineGuid")?.ToString() ?? string.Empty;
            }
            catch { MachineGuid = string.Empty; }
        }

        public static GUIGlobals Current
        {
            get
            {
                if (_current == null)
                {
                    _current = new GUIGlobals();
                }
                return _current;
            }
        }

        public static string CurrentVersion => KTWirzade.Shared.Globals.CurrentVersion;

        private static MainWindow _mainWindow;
        public static void SetMainWindow(MainWindow window)
        {
            _mainWindow = window;
        }

        public static void MainWindowDragBoxClick()
        {
            _mainWindow?.DragBox_OnClick(_mainWindow, null);
        }

        public static void RefreshLanguage()
        {
            _mainWindow?.UpdateLanguageDisplay();
        }

        public static string AppTitle => "KT WIRZADE";

        // false = valida build/requisitos como o AME original; o bypass so acontece
        // quando o usuario escolhe "aplicar mesmo assim" na pagina de requisitos.
        public static bool SkipBuildCheck = false;
        public static bool SkipRequirementsCheck = false;
    }
}
