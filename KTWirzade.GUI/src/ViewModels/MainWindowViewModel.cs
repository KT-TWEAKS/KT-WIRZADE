using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KTWirzade.GUI.Models;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.ViewModels
{
    internal class MainWindowViewModel : ViewModelBase
    {
        public ApplicationState state;

        private ICommand nextCommandCache;

        private ICommand cancelCommandCache;

        private ICommand previousCommandCache;

        private ICommand closeCommandCache;

        private ViewModelBase _currentViewModel;

        private bool _RemovePlaybookButtonActive = true;

        private Visibility _PlaybookColumnVisibility;

        private bool _NextButtonActive = true;

        private bool? _PreviousButtonActive;

        private bool _PulseNextButtonActive;

        private bool _CancelButtonActive = true;

        private bool _CloseButtonActive = true;

        private Visibility _NextButtonVisibility;

        private Visibility _PulseNextButtonVisibility;

        private Visibility _CancelButtonVisibility = Visibility.Hidden;

        private Visibility _PreviousButtonVisibility;

        private Visibility _CloseButtonVisibility;

        private UIElement _NextButtonContent = new TextBlock
        {
            Text = "Next"
        };

        private UIElement _CancelButtonContent = new TextBlock
        {
            Text = "Cancel"
        };

        private UIElement _PreviousButtonContent = new TextBlock
        {
            Text = "Back"
        };

        private ICommand _NextButtonCommand;

        private ICommand _PulseNextButtonCommand;

        private ICommand _CancelButtonCommand;

        private ICommand _PreviousButtonCommand;

        private ICommand _CloseButtonCommand;

        private MainNextButtonStyles _NextButtonStyle;

        private bool _UpdatesButtonActive;

        private bool _StatusButtonActive;

        private Visibility _StatusButtonVisibility;

        public ViewModelBase CurrentViewModel
        {
            get
            {
                return _currentViewModel;
            }
            set
            {
                Unsubscribe();
                SetProperty(ref _currentViewModel, value, "CurrentViewModel");
                if (!value.MainPreviousButtonActive.HasValue)
                {
                    PreviousButtonActive = value.HasPreviousPage();
                }
                ReloadViewModel(CurrentViewModel, new PropertyChangedEventArgs("CurrentViewModel"));
                Subscribe();
            }
        }

        public bool RemovePlaybookButtonActive
        {
            get
            {
                return _RemovePlaybookButtonActive;
            }
            set
            {
                SetProperty(ref _RemovePlaybookButtonActive, value, "RemovePlaybookButtonActive");
            }
        }

        public Visibility PlaybookColumnVisibility
        {
            get
            {
                return _PlaybookColumnVisibility;
            }
            set
            {
                SetProperty(ref _PlaybookColumnVisibility, value, "PlaybookColumnVisibility");
            }
        }

        public bool NextButtonActive
        {
            get
            {
                return _NextButtonActive;
            }
            set
            {
                SetProperty(ref _NextButtonActive, value, "NextButtonActive");
            }
        }

        public bool PreviousButtonActive
        {
            get
            {
                if (_PreviousButtonActive.HasValue)
                {
                    return _PreviousButtonActive.Value;
                }
                return _currentViewModel.HasPreviousPage();
            }
            set
            {
                SetProperty(ref _PreviousButtonActive, value, "PreviousButtonActive");
            }
        }

        public bool PulseNextButtonActive
        {
            get
            {
                return _PulseNextButtonActive;
            }
            set
            {
                SetProperty(ref _PulseNextButtonActive, value, "PulseNextButtonActive");
            }
        }

        public bool CancelButtonActive
        {
            get
            {
                return _CancelButtonActive;
            }
            set
            {
                SetProperty(ref _CancelButtonActive, value, "CancelButtonActive");
            }
        }

        public bool CloseButtonActive
        {
            get
            {
                return _CloseButtonActive;
            }
            set
            {
                SetProperty(ref _CloseButtonActive, value, "CloseButtonActive");
            }
        }

        public Visibility NextButtonVisibility
        {
            get
            {
                return _NextButtonVisibility;
            }
            set
            {
                SetProperty(ref _NextButtonVisibility, value, "NextButtonVisibility");
            }
        }

        public Visibility PulseNextButtonVisibility
        {
            get
            {
                return _PulseNextButtonVisibility;
            }
            set
            {
                SetProperty(ref _PulseNextButtonVisibility, value, "PulseNextButtonVisibility");
            }
        }

        public Visibility CancelButtonVisibility
        {
            get
            {
                return _CancelButtonVisibility;
            }
            set
            {
                SetProperty(ref _CancelButtonVisibility, value, "CancelButtonVisibility");
            }
        }

        public Visibility PreviousButtonVisibility
        {
            get
            {
                return _PreviousButtonVisibility;
            }
            set
            {
                SetProperty(ref _PreviousButtonVisibility, value, "PreviousButtonVisibility");
            }
        }

        public Visibility CloseButtonVisibility
        {
            get
            {
                return _CloseButtonVisibility;
            }
            set
            {
                SetProperty(ref _CloseButtonVisibility, value, "CloseButtonVisibility");
            }
        }

        public UIElement NextButtonContent
        {
            get
            {
                return _NextButtonContent;
            }
            set
            {
                SetProperty(ref _NextButtonContent, value, "NextButtonContent");
            }
        }

        public UIElement CancelButtonContent
        {
            get
            {
                return _CancelButtonContent;
            }
            set
            {
                SetProperty(ref _CancelButtonContent, value, "CancelButtonContent");
            }
        }

        public UIElement PreviousButtonContent
        {
            get
            {
                return _PreviousButtonContent;
            }
            set
            {
                SetProperty(ref _PreviousButtonContent, value, "PreviousButtonContent");
            }
        }

        public ICommand NextButtonCommand
        {
            get
            {
                return _NextButtonCommand;
            }
            set
            {
                SetProperty(ref _NextButtonCommand, value, "NextButtonCommand");
            }
        }

        public ICommand PulseNextButtonCommand
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                SetProperty(ref _PulseNextButtonCommand, value, "PulseNextButtonCommand");
            }
        }

        public ICommand CancelButtonCommand
        {
            get
            {
                return _CancelButtonCommand;
            }
            set
            {
                SetProperty(ref _CancelButtonCommand, value, "CancelButtonCommand");
            }
        }

        public ICommand PreviousButtonCommand
        {
            get
            {
                return _PreviousButtonCommand;
            }
            set
            {
                SetProperty(ref _PreviousButtonCommand, value, "PreviousButtonCommand");
            }
        }

        public ICommand CloseButtonCommand
        {
            get
            {
                return _CloseButtonCommand;
            }
            set
            {
                SetProperty(ref _CloseButtonCommand, value, "CloseButtonCommand");
            }
        }

        public MainNextButtonStyles NextButtonStyle
        {
            get
            {
                return _NextButtonStyle;
            }
            set
            {
                SetProperty(ref _NextButtonStyle, value, "NextButtonStyle");
            }
        }

        public bool UpdatesButtonActive
        {
            get
            {
                return _UpdatesButtonActive;
            }
            set
            {
                SetProperty(ref _UpdatesButtonActive, value, "UpdatesButtonActive");
            }
        }

        public bool StatusButtonActive
        {
            get
            {
                return _StatusButtonActive;
            }
            set
            {
                SetProperty(ref _StatusButtonActive, value, "StatusButtonActive");
            }
        }

        public Visibility StatusButtonVisibility
        {
            get
            {
                return _StatusButtonVisibility;
            }
            set
            {
                SetProperty(ref _StatusButtonVisibility, value, "StatusButtonVisibility");
            }
        }

        public MainWindowViewModel()
        {
            state = new ApplicationState();
            CurrentViewModel = ((GlobalsGUI.Current.Playbook != null) ? GlobalsGUI.Current.Playbook.CurrentPage : ((GlobalsGUI.Current.ISO != null) ? GlobalsGUI.Current.ISO.CurrentPage : new SelectPageViewModel()));
            GlobalsGUI.Current.PropertyChanged += Globals_OnPropertyChanged;
        }

        private void Subscribe()
        {
            if (CurrentViewModel != null)
            {
                CurrentViewModel.PropertyChanged += ReloadViewModel;
            }
        }

        private void Unsubscribe()
        {
            if (CurrentViewModel != null)
            {
                CurrentViewModel.PropertyChanged -= ReloadViewModel;
            }
        }

        private void ReloadViewModel(object sender, PropertyChangedEventArgs e)
        {
            ViewModelBase value = (ViewModelBase)sender;
            NextButtonActive = value.MainNextButtonActive;
            if (value.MainPreviousButtonActive.HasValue)
            {
                PreviousButtonActive = value.MainPreviousButtonActive.Value;
            }
            PulseNextButtonActive = value.MainPulseNextButtonActive;
            CancelButtonActive = value.MainCancelButtonActive;
            CloseButtonActive = value.MainCloseButtonActive;
            NextButtonVisibility = value.MainNextButtonVisibility;
            PulseNextButtonVisibility = value.MainPulseNextButtonVisibility;
            CancelButtonVisibility = value.MainCancelButtonVisibility;
            PreviousButtonVisibility = value.MainPreviousButtonVisibility;
            CloseButtonVisibility = value.MainCloseButtonVisibility;
            if (value.MainNextButtonContent != null)
            {
                NextButtonContent = value.MainNextButtonContent;
            }
            if (value.MainCancelButtonContent != null)
            {
                CancelButtonContent = value.MainCancelButtonContent;
            }
            if (value.MainPreviousButtonContent != null)
            {
                PreviousButtonContent = value.MainPreviousButtonContent;
            }
            if (value.MainPulseNextButtonCommand != null)
            {
                PulseNextButtonCommand = value.MainPulseNextButtonCommand;
            }
            if (value.MainNextButtonCommand != null)
            {
                if (nextCommandCache == null)
                {
                    nextCommandCache = NextButtonCommand;
                }
                NextButtonCommand = value.MainNextButtonCommand;
            }
            else if (nextCommandCache != null)
            {
                NextButtonCommand = nextCommandCache;
            }
            if (value.MainCancelButtonCommand != null)
            {
                if (cancelCommandCache == null)
                {
                    cancelCommandCache = CancelButtonCommand;
                }
                CancelButtonCommand = value.MainCancelButtonCommand;
            }
            else if (cancelCommandCache != null)
            {
                CancelButtonCommand = cancelCommandCache;
            }
            if (value.MainPreviousButtonCommand != null)
            {
                if (previousCommandCache == null)
                {
                    previousCommandCache = PreviousButtonCommand;
                }
                PreviousButtonCommand = value.MainPreviousButtonCommand;
            }
            else if (previousCommandCache != null)
            {
                PreviousButtonCommand = previousCommandCache;
            }
            if (value.MainCloseButtonCommand != null)
            {
                if (closeCommandCache == null)
                {
                    closeCommandCache = CloseButtonCommand;
                }
                CloseButtonCommand = value.MainCloseButtonCommand;
            }
            else if (closeCommandCache != null)
            {
                CloseButtonCommand = closeCommandCache;
            }
            NextButtonStyle = value.MainNextButtonStyle;
            UpdatesButtonActive = value.MainUpdatesButtonActive;
            StatusButtonActive = value.MainStatusButtonActive;
            PlaybookColumnVisibility = value.MainPlaybookColumnVisibility;
        }

        private void Globals_OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Playbook")
            {
                if (GlobalsGUI.Current.Playbook != null)
                {
                    CurrentViewModel = GlobalsGUI.Current.Playbook.CurrentPage;
                }
                else if (GlobalsGUI.Current.ISO == null)
                {
                    CurrentViewModel = new SelectPageViewModel();
                }
            }
            else if (e.PropertyName == "ISO")
            {
                if (GlobalsGUI.Current.ISO != null)
                {
                    CurrentViewModel = GlobalsGUI.Current.ISO.CurrentPage;
                }
                else if (GlobalsGUI.Current.Playbook == null)
                {
                    CurrentViewModel = new SelectPageViewModel();
                }
            }
        }

        public override ViewModelBase GetNextPage(ApplicationState state)
        {
            throw new NotImplementedException();
        }

        public override ViewModelBase GetPreviousPage(ApplicationState state)
        {
            throw new NotImplementedException();
        }

        public override bool HasPreviousPage()
        {
            throw new NotImplementedException();
        }
    }
}