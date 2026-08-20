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
                Details = "KT WIRZADE - Sistema de otimizacao e personalizacao do Windows.",
                Username = "KT Wirzade",
                Website = "https://ktwirzade.com"
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
                    OnPropertyChanged("Playbook");
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

        public static bool WUAStopperEngaged = false;

        public static readonly int WinVer = int.Parse(Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion").GetValue("CurrentBuildNumber").ToString());

        public static readonly string MachineGuid = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Cryptography").GetValue("MachineGuid").ToString();

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

        public static string AppTitle => "KT WIRZADE";
    }
}
