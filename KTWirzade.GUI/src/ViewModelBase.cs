using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KTWirzade.GUI.Models;

namespace KTWirzade.GUI
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public enum MainNextButtonStyles
        {
            Normal,
            Pulse
        }

        private bool _MainNextButtonActive = true;

        private bool? _MainPreviousButtonActive;

        private bool _MainPulseNextButtonActive;

        private bool _MainCancelButtonActive = true;

        private bool _MainCloseButtonActive = true;

        private Visibility _MainUpdateNotifierVisibility = Visibility.Hidden;

        private Visibility _MainNextButtonVisibility;

        private Visibility _MainPulseNextButtonVisibility = Visibility.Hidden;

        private Visibility _MainCancelButtonVisibility;

        private Visibility _MainPreviousButtonVisibility;

        private Visibility _MainCloseButtonVisibility;

        private Visibility _MainStatusButtonVisibility;

        private UIElement _MainNextButtonContent = new TextBlock
        {
            Text = "Next"
        };

        private UIElement _MainCancelButtonContent = new TextBlock
        {
            Text = "Cancel"
        };

        private UIElement _MainPreviousButtonContent = new TextBlock
        {
            Text = "Back"
        };

        private ICommand _MainNextButtonCommand;

        private ICommand _MainPulseNextButtonCommand;

        private ICommand _MainCancelButtonCommand;

        private ICommand _MainPreviousButtonCommand;

        private ICommand _MainCloseButtonCommand;

        private MainNextButtonStyles _MainNextButtonStyle;

        private bool _MainUpdatesButtonActive = true;

        private bool _MainStatusButtonActive = true;

        private Visibility _MainPlaybookColumnVisibility;

        public bool MainNextButtonActive
        {
            get
            {
                return _MainNextButtonActive;
            }
            set
            {
                SetProperty(ref _MainNextButtonActive, value, "MainNextButtonActive");
            }
        }

        public bool? MainPreviousButtonActive
        {
            get
            {
                return _MainPreviousButtonActive;
            }
            set
            {
                SetProperty(ref _MainPreviousButtonActive, value, "MainPreviousButtonActive");
            }
        }

        public bool MainPulseNextButtonActive
        {
            get
            {
                return _MainPulseNextButtonActive;
            }
            set
            {
                SetProperty(ref _MainPulseNextButtonActive, value, "MainPulseNextButtonActive");
            }
        }

        public bool MainCancelButtonActive
        {
            get
            {
                return _MainCancelButtonActive;
            }
            set
            {
                SetProperty(ref _MainCancelButtonActive, value, "MainCancelButtonActive");
            }
        }

        public bool MainCloseButtonActive
        {
            get
            {
                return _MainCloseButtonActive;
            }
            set
            {
                SetProperty(ref _MainCloseButtonActive, value, "MainCloseButtonActive");
            }
        }

        public Visibility MainUpdateNotifierVisibility
        {
            get
            {
                return _MainUpdateNotifierVisibility;
            }
            set
            {
                SetProperty(ref _MainUpdateNotifierVisibility, value, "MainUpdateNotifierVisibility");
            }
        }

        public Visibility MainNextButtonVisibility
        {
            get
            {
                return _MainNextButtonVisibility;
            }
            set
            {
                SetProperty(ref _MainNextButtonVisibility, value, "MainNextButtonVisibility");
            }
        }

        public Visibility MainPulseNextButtonVisibility
        {
            get
            {
                return _MainPulseNextButtonVisibility;
            }
            set
            {
                SetProperty(ref _MainPulseNextButtonVisibility, value, "MainPulseNextButtonVisibility");
            }
        }

        public Visibility MainCancelButtonVisibility
        {
            get
            {
                return _MainCancelButtonVisibility;
            }
            set
            {
                SetProperty(ref _MainCancelButtonVisibility, value, "MainCancelButtonVisibility");
            }
        }

        public Visibility MainPreviousButtonVisibility
        {
            get
            {
                return _MainPreviousButtonVisibility;
            }
            set
            {
                SetProperty(ref _MainPreviousButtonVisibility, value, "MainPreviousButtonVisibility");
            }
        }

        public Visibility MainCloseButtonVisibility
        {
            get
            {
                return _MainCloseButtonVisibility;
            }
            set
            {
                SetProperty(ref _MainCloseButtonVisibility, value, "MainCloseButtonVisibility");
            }
        }

        public Visibility MainStatusButtonVisibility
        {
            get
            {
                return _MainStatusButtonVisibility;
            }
            set
            {
                SetProperty(ref _MainStatusButtonVisibility, value, "MainStatusButtonVisibility");
            }
        }

        public UIElement MainNextButtonContent
        {
            get
            {
                return _MainNextButtonContent;
            }
            set
            {
                SetProperty(ref _MainNextButtonContent, value, "MainNextButtonContent");
            }
        }

        public UIElement MainCancelButtonContent
        {
            get
            {
                return _MainCancelButtonContent;
            }
            set
            {
                SetProperty(ref _MainCancelButtonContent, value, "MainCancelButtonContent");
            }
        }

        public UIElement MainPreviousButtonContent
        {
            get
            {
                return _MainPreviousButtonContent;
            }
            set
            {
                SetProperty(ref _MainPreviousButtonContent, value, "MainPreviousButtonContent");
            }
        }

        public ICommand MainNextButtonCommand
        {
            get
            {
                return _MainNextButtonCommand;
            }
            set
            {
                SetProperty(ref _MainNextButtonCommand, value, "MainNextButtonCommand");
            }
        }

        public ICommand MainPulseNextButtonCommand
        {
            get
            {
                return _MainPulseNextButtonCommand;
            }
            set
            {
                SetProperty(ref _MainPulseNextButtonCommand, value, "MainPulseNextButtonCommand");
            }
        }

        public ICommand MainCancelButtonCommand
        {
            get
            {
                return _MainCancelButtonCommand;
            }
            set
            {
                SetProperty(ref _MainCancelButtonCommand, value, "MainCancelButtonCommand");
            }
        }

        public ICommand MainPreviousButtonCommand
        {
            get
            {
                return _MainPreviousButtonCommand;
            }
            set
            {
                SetProperty(ref _MainPreviousButtonCommand, value, "MainPreviousButtonCommand");
            }
        }

        public ICommand MainCloseButtonCommand
        {
            get
            {
                return _MainCloseButtonCommand;
            }
            set
            {
                SetProperty(ref _MainCloseButtonCommand, value, "MainCloseButtonCommand");
            }
        }

        public MainNextButtonStyles MainNextButtonStyle
        {
            get
            {
                return _MainNextButtonStyle;
            }
            set
            {
                SetProperty(ref _MainNextButtonStyle, value, "MainNextButtonStyle");
            }
        }

        public bool MainUpdatesButtonActive
        {
            get
            {
                return _MainUpdatesButtonActive;
            }
            set
            {
                SetProperty(ref _MainUpdatesButtonActive, value, "MainUpdatesButtonActive");
            }
        }

        public bool MainStatusButtonActive
        {
            get
            {
                return _MainStatusButtonActive;
            }
            set
            {
                SetProperty(ref _MainStatusButtonActive, value, "MainStatusButtonActive");
            }
        }

        public Visibility MainPlaybookColumnVisibility
        {
            get
            {
                return _MainPlaybookColumnVisibility;
            }
            set
            {
                SetProperty(ref _MainPlaybookColumnVisibility, value, "MainPlaybookColumnVisibility");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
        {
            if (!object.Equals(property, value))
            {
                property = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public abstract ViewModelBase GetNextPage(ApplicationState state);

        public abstract ViewModelBase GetPreviousPage(ApplicationState state);

        public abstract bool HasPreviousPage();
    }
}
