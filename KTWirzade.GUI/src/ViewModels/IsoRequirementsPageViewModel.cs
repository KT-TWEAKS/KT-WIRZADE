using System.Windows;
using System.Windows.Media.Imaging;
using KTWirzade.GUI.Models;
using static KTWirzade.Shared.Requirements;
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
    internal class IsoRequirementsPageViewModel : ViewModelBase
    {
        private BitmapImage _activationIcon;

        private BitmapImage _systemCheckIcon;

        private string _activationStatusText = "Detecting Activation...";

        private string _activationResultText = "";

        private string _systemCheckStatusText = "Analyzing Image...";

        private string _systemCheckResultText = "";

        private Visibility _resultTextVisiblity = Visibility.Collapsed;

        private Visibility _activationResultTextVisiblity = Visibility.Collapsed;

        private Visibility _progressBarVisibility = Visibility.Collapsed;

        public IsoRequirementsPage Model { get; private set; }

        public BitmapImage ActivationIcon
        {
            get
            {
                return _activationIcon;
            }
            set
            {
                SetProperty(ref _activationIcon, value, "ActivationIcon");
            }
        }

        public BitmapImage SystemCheckIcon
        {
            get
            {
                return _systemCheckIcon;
            }
            set
            {
                SetProperty(ref _systemCheckIcon, value, "SystemCheckIcon");
            }
        }

        public string ActivationStatusText
        {
            get
            {
                return _activationStatusText;
            }
            set
            {
                SetProperty(ref _activationStatusText, value, "ActivationStatusText");
            }
        }

        public string ActivationResultText
        {
            get
            {
                return _activationResultText;
            }
            set
            {
                SetProperty(ref _activationResultText, value, "ActivationResultText");
            }
        }

        public string SystemCheckStatusText
        {
            get
            {
                return _systemCheckStatusText;
            }
            set
            {
                SetProperty(ref _systemCheckStatusText, value, "SystemCheckStatusText");
            }
        }

        public string SystemCheckResultText
        {
            get
            {
                return _systemCheckResultText;
            }
            set
            {
                SetProperty(ref _systemCheckResultText, value, "SystemCheckResultText");
            }
        }

        public Visibility ResultTextVisibility
        {
            get
            {
                return _resultTextVisiblity;
            }
            set
            {
                SetProperty(ref _resultTextVisiblity, value, "ResultTextVisibility");
            }
        }

        public Visibility ActivationResultTextVisibility
        {
            get
            {
                return _activationResultTextVisiblity;
            }
            set
            {
                SetProperty(ref _activationResultTextVisiblity, value, "ActivationResultTextVisibility");
            }
        }

        public Visibility ProgressBarVisibility
        {
            get
            {
                return _progressBarVisibility;
            }
            set
            {
                SetProperty(ref _progressBarVisibility, value, "ProgressBarVisibility");
            }
        }

        public Requirement[] MetRequirements { get; set; }

        public bool? IsBuildSupported { get; set; }

        public IsoRequirementsPageViewModel(IsoRequirementsPage requirementsPage)
        {
            Model = requirementsPage;
            base.MainPlaybookColumnVisibility = Visibility.Visible;
        }

        public override ViewModelBase GetNextPage(ApplicationState state)
        {
            return new IsoLicensePageViewModel(state.licensePage);
        }

        public override ViewModelBase GetPreviousPage(ApplicationState state)
        {
            return new IsoPageViewModel();
        }

        public override bool HasPreviousPage()
        {
            return true;
        }
    }
}
