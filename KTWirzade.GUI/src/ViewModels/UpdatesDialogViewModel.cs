using System.Windows;
using KTWirzade.GUI.Models;
using KTWirzade.Shared;
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
    public class UpdatesDialogViewModel : ViewModelBase
    {
        private PlaybookGUI _SelectedPlaybook;

        private PlaybookGUI _TransitionSelectedPlaybook;

        private Visibility _InstallVisibility = Visibility.Collapsed;

        private Visibility _CheckVisibility;

        private Visibility _SourceVisibility;

        private bool _SourceActive = true;

        private Thickness _SourceMargin = new Thickness(0.0, 0.0, 4.0, 0.0);

        private Visibility _DonateVisibility;

        private bool _DonateActive = true;

        private Visibility _NotVerifiedVisibility = Visibility.Collapsed;

        private Visibility _UpToDateVisibility = Visibility.Collapsed;

        private string _UpToDateText = "Playbook is up-to-date!";

        private Visibility _UpdateReadyVisibility = Visibility.Collapsed;

        private double _PublisherOpacity = 1.0;

        private Visibility _PublisherVisibility;

        private Visibility _ContentGridVisibility;

        private Visibility _NoneGridVisibility;

        private bool _CloseButtonActive = true;

        private string _SourceText = "Website";

        private string _DonateText = "Donate";

        private double _UpdateBoxOpacity = 1.0;

        private bool _UpdateButtonsActive = true;

        private Visibility _TransitionInstallVisibility = Visibility.Collapsed;

        private Visibility _TransitionCheckVisibility;

        private Visibility _TransitionSourceVisibility;

        private bool _TransitionSourceActive = true;

        private Thickness _TransitionSourceMargin = new Thickness(0.0, 0.0, 4.0, 0.0);

        private Visibility _TransitionDonateVisibility;

        private bool _TransitionDonateActive = true;

        private Visibility _TransitionNotVerifiedVisibility = Visibility.Collapsed;

        private Visibility _TransitionUpToDateVisibility = Visibility.Collapsed;

        private string _TransitionUpToDateText = "Playbook is up-to-date!";

        private Visibility _TransitionUpdateReadyVisibility = Visibility.Collapsed;

        private double _TransitionPublisherOpacity = 1.0;

        private Visibility _TransitionPublisherVisibility;

        private Visibility _TransitionContentGridVisibility;

        private Visibility _TransitionNoneGridVisibility;

        private bool _TransitionCloseButtonActive = true;

        private string _TransitionSourceText = "Website";

        private string _TransitionDonateText = "Donate";

        private double _TransitionUpdateBoxOpacity = 1.0;

        private bool _TransitionUpdateButtonsActive = true;

        public PlaybookGUI SelectedPlaybook
        {
            get
            {
                return _SelectedPlaybook;
            }
            set
            {
                if (value.VerificationStatus != PlaybookGUI.VerificationLevel.Verified || ((Playbook)value).Name != "KT WIRZADE Beta")
                {
                    SourceText = "Source Code";
                    UpToDateText = "Playbook is up-to-date!";
                    DonateText = "Donate";
                }
                if (value.PendingUpdate == null)
                {
                    InstallVisibility = Visibility.Collapsed;
                    CheckVisibility = Visibility.Visible;
                    UpdateReadyVisibility = Visibility.Collapsed;
                    if (value.UpdatesChecked)
                    {
                        UpToDateVisibility = Visibility.Visible;
                    }
                    else
                    {
                        UpToDateVisibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    CheckVisibility = Visibility.Collapsed;
                    InstallVisibility = Visibility.Visible;
                    UpToDateVisibility = Visibility.Collapsed;
                    UpdateReadyVisibility = Visibility.Visible;
                }
                if (value.VerificationStatus != PlaybookGUI.VerificationLevel.Malicious)
                {
                    SourceVisibility = Visibility.Visible;
                    DonateVisibility = Visibility.Visible;
                    if (((Playbook)value).Name == "KT WIRZADE Beta")
                    {
                        SourceText = "Website";
                        UpToDateText = "KT WIRZADE Beta is up-to-date!";
                        DonateText = "Donate";
                        DonateVisibility = Visibility.Collapsed;
                    }
                    else if (((Playbook)value).Username == "Ameliorated")
                    {
                        SourceText = "Website";
                        DonateText = "Donate";
                        DonateVisibility = Visibility.Collapsed;
                    }
                    PublisherOpacity = 1.0;
                    NotVerifiedVisibility = Visibility.Collapsed;
                    if (((Playbook)value).Git != null)
                    {
                        SourceActive = true;
                    }
                    else
                    {
                        SourceActive = false;
                    }
                    if (((Playbook)value).DonateLink != null)
                    {
                        DonateActive = true;
                    }
                    else
                    {
                        DonateActive = false;
                    }
                    if (((Playbook)value).DonateLink != null || ((Playbook)value).Git != null)
                    {
                        PublisherOpacity = 1.0;
                        if (((Playbook)value).Git != null)
                        {
                            SourceVisibility = Visibility.Visible;
                        }
                        if (((Playbook)value).DonateLink != null)
                        {
                            DonateVisibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        PublisherOpacity = 0.5;
                        DonateVisibility = Visibility.Collapsed;
                    }
                    UpdateBoxOpacity = 1.0;
                    UpdateButtonsActive = true;
                }
                else
                {
                    PublisherOpacity = 0.5;
                    SourceVisibility = Visibility.Collapsed;
                    DonateVisibility = Visibility.Collapsed;
                    NotVerifiedVisibility = Visibility.Visible;
                    UpdateBoxOpacity = 0.5;
                    UpdateButtonsActive = false;
                    UpToDateVisibility = Visibility.Collapsed;
                    UpdateReadyVisibility = Visibility.Collapsed;
                }
                if (((Playbook)value).Name == "None" && ((Playbook)value).Username == "Ameliorated" && value.VerificationStatus == PlaybookGUI.VerificationLevel.Verified)
                {
                    ContentGridVisibility = Visibility.Collapsed;
                    NoneGridVisibility = Visibility.Visible;
                }
                else
                {
                    ContentGridVisibility = Visibility.Visible;
                    NoneGridVisibility = Visibility.Collapsed;
                }
                SetProperty(ref _SelectedPlaybook, value, "SelectedPlaybook");
            }
        }

        public PlaybookGUI TransitionSelectedPlaybook
        {
            get
            {
                return _TransitionSelectedPlaybook;
            }
            set
            {
                if (value.VerificationStatus != PlaybookGUI.VerificationLevel.Verified || ((Playbook)value).Name != "KT WIRZADE Beta")
                {
                    TransitionSourceText = "Source Code";
                    TransitionUpToDateText = "Playbook is up-to-date!";
                    TransitionDonateText = "Donate";
                }
                if (value.PendingUpdate == null)
                {
                    TransitionInstallVisibility = Visibility.Collapsed;
                    TransitionCheckVisibility = Visibility.Visible;
                    TransitionUpdateReadyVisibility = Visibility.Collapsed;
                    if (value.UpdatesChecked)
                    {
                        TransitionUpToDateVisibility = Visibility.Visible;
                    }
                    else
                    {
                        TransitionUpToDateVisibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    TransitionCheckVisibility = Visibility.Collapsed;
                    TransitionInstallVisibility = Visibility.Visible;
                    TransitionUpToDateVisibility = Visibility.Collapsed;
                    TransitionUpdateReadyVisibility = Visibility.Visible;
                }
                if (value.VerificationStatus != PlaybookGUI.VerificationLevel.Malicious)
                {
                    TransitionSourceVisibility = Visibility.Visible;
                    TransitionDonateVisibility = Visibility.Visible;
                    if (((Playbook)value).Name == "KT WIRZADE Beta")
                    {
                        TransitionSourceText = "Website";
                        TransitionUpToDateText = "KT WIRZADE Beta is up-to-date!";
                        TransitionDonateText = "Donate";
                        TransitionDonateVisibility = Visibility.Collapsed;
                    }
                    else if (((Playbook)value).Username == "Ameliorated")
                    {
                        TransitionSourceText = "Website";
                        TransitionDonateText = "Donate";
                        TransitionDonateVisibility = Visibility.Collapsed;
                    }
                    TransitionPublisherOpacity = 1.0;
                    TransitionNotVerifiedVisibility = Visibility.Collapsed;
                    if (((Playbook)value).Git != null)
                    {
                        TransitionSourceActive = true;
                    }
                    else
                    {
                        TransitionSourceActive = false;
                    }
                    if (((Playbook)value).DonateLink != null)
                    {
                        TransitionDonateActive = true;
                    }
                    else
                    {
                        TransitionDonateActive = false;
                    }
                    if (((Playbook)value).DonateLink != null || ((Playbook)value).Git != null)
                    {
                        TransitionPublisherOpacity = 1.0;
                        if (((Playbook)value).Git != null)
                        {
                            TransitionSourceVisibility = Visibility.Visible;
                        }
                        if (((Playbook)value).DonateLink != null)
                        {
                            TransitionDonateVisibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        TransitionPublisherOpacity = 0.5;
                        TransitionDonateVisibility = Visibility.Collapsed;
                    }
                    TransitionUpdateBoxOpacity = 1.0;
                    TransitionUpdateButtonsActive = true;
                }
                else
                {
                    TransitionPublisherOpacity = 0.5;
                    TransitionSourceVisibility = Visibility.Collapsed;
                    TransitionDonateVisibility = Visibility.Collapsed;
                    TransitionNotVerifiedVisibility = Visibility.Visible;
                    TransitionUpdateBoxOpacity = 0.5;
                    TransitionUpdateButtonsActive = false;
                    TransitionUpToDateVisibility = Visibility.Collapsed;
                    TransitionUpdateReadyVisibility = Visibility.Collapsed;
                }
                if (((Playbook)value).Name == "None" && ((Playbook)value).Username == "Ameliorated" && value.VerificationStatus == PlaybookGUI.VerificationLevel.Verified)
                {
                    TransitionContentGridVisibility = Visibility.Collapsed;
                    TransitionNoneGridVisibility = Visibility.Visible;
                }
                else
                {
                    TransitionContentGridVisibility = Visibility.Visible;
                    TransitionNoneGridVisibility = Visibility.Collapsed;
                }
                SetProperty(ref _TransitionSelectedPlaybook, value, "TransitionSelectedPlaybook");
            }
        }

        public Visibility InstallVisibility
        {
            get
            {
                return _InstallVisibility;
            }
            set
            {
                SetProperty(ref _InstallVisibility, value, "InstallVisibility");
            }
        }

        public Visibility CheckVisibility
        {
            get
            {
                return _CheckVisibility;
            }
            set
            {
                SetProperty(ref _CheckVisibility, value, "CheckVisibility");
            }
        }

        public Visibility SourceVisibility
        {
            get
            {
                return _SourceVisibility;
            }
            set
            {
                SetProperty(ref _SourceVisibility, value, "SourceVisibility");
            }
        }

        public bool SourceActive
        {
            get
            {
                return _SourceActive;
            }
            set
            {
                SetProperty(ref _SourceActive, value, "SourceActive");
            }
        }

        public Thickness SourceMargin
        {
            get
            {
                return _SourceMargin;
            }
            set
            {
                SetProperty(ref _SourceMargin, value, "SourceMargin");
            }
        }

        public Visibility DonateVisibility
        {
            get
            {
                return _DonateVisibility;
            }
            set
            {
                SourceMargin = ((value == Visibility.Visible) ? new Thickness(0.0, 0.0, 4.0, 0.0) : new Thickness(0.0, 0.0, 0.0, 0.0));
                SetProperty(ref _DonateVisibility, value, "DonateVisibility");
            }
        }

        public bool DonateActive
        {
            get
            {
                return _DonateActive;
            }
            set
            {
                SetProperty(ref _DonateActive, value, "DonateActive");
            }
        }

        public Visibility NotVerifiedVisibility
        {
            get
            {
                return _NotVerifiedVisibility;
            }
            set
            {
                SetProperty(ref _NotVerifiedVisibility, value, "NotVerifiedVisibility");
            }
        }

        public Visibility UpToDateVisibility
        {
            get
            {
                return _UpToDateVisibility;
            }
            set
            {
                SetProperty(ref _UpToDateVisibility, value, "UpToDateVisibility");
            }
        }

        public string UpToDateText
        {
            get
            {
                return _UpToDateText;
            }
            set
            {
                SetProperty(ref _UpToDateText, value, "UpToDateText");
            }
        }

        public Visibility UpdateReadyVisibility
        {
            get
            {
                return _UpdateReadyVisibility;
            }
            set
            {
                SetProperty(ref _UpdateReadyVisibility, value, "UpdateReadyVisibility");
            }
        }

        public double PublisherOpacity
        {
            get
            {
                return _PublisherOpacity;
            }
            set
            {
                SetProperty(ref _PublisherOpacity, value, "PublisherOpacity");
            }
        }

        public Visibility PublisherVisibility
        {
            get
            {
                return _PublisherVisibility;
            }
            set
            {
                SetProperty(ref _PublisherVisibility, value, "PublisherVisibility");
            }
        }

        public Visibility ContentGridVisibility
        {
            get
            {
                return _ContentGridVisibility;
            }
            set
            {
                SetProperty(ref _ContentGridVisibility, value, "ContentGridVisibility");
            }
        }

        public Visibility NoneGridVisibility
        {
            get
            {
                return _NoneGridVisibility;
            }
            set
            {
                SetProperty(ref _NoneGridVisibility, value, "NoneGridVisibility");
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

        public string SourceText
        {
            get
            {
                return _SourceText;
            }
            set
            {
                SetProperty(ref _SourceText, value, "SourceText");
            }
        }

        public string DonateText
        {
            get
            {
                return _DonateText;
            }
            set
            {
                SetProperty(ref _DonateText, value, "DonateText");
            }
        }

        public double UpdateBoxOpacity
        {
            get
            {
                return _UpdateBoxOpacity;
            }
            set
            {
                SetProperty(ref _UpdateBoxOpacity, value, "UpdateBoxOpacity");
            }
        }

        public bool UpdateButtonsActive
        {
            get
            {
                return _UpdateButtonsActive;
            }
            set
            {
                SetProperty(ref _UpdateButtonsActive, value, "UpdateButtonsActive");
            }
        }

        public Visibility TransitionInstallVisibility
        {
            get
            {
                return _TransitionInstallVisibility;
            }
            set
            {
                SetProperty(ref _TransitionInstallVisibility, value, "TransitionInstallVisibility");
            }
        }

        public Visibility TransitionCheckVisibility
        {
            get
            {
                return _TransitionCheckVisibility;
            }
            set
            {
                SetProperty(ref _TransitionCheckVisibility, value, "TransitionCheckVisibility");
            }
        }

        public Visibility TransitionSourceVisibility
        {
            get
            {
                return _TransitionSourceVisibility;
            }
            set
            {
                SetProperty(ref _TransitionSourceVisibility, value, "TransitionSourceVisibility");
            }
        }

        public bool TransitionSourceActive
        {
            get
            {
                return _TransitionSourceActive;
            }
            set
            {
                SetProperty(ref _TransitionSourceActive, value, "TransitionSourceActive");
            }
        }

        public Thickness TransitionSourceMargin
        {
            get
            {
                return _TransitionSourceMargin;
            }
            set
            {
                SetProperty(ref _TransitionSourceMargin, value, "TransitionSourceMargin");
            }
        }

        public Visibility TransitionDonateVisibility
        {
            get
            {
                return _TransitionDonateVisibility;
            }
            set
            {
                TransitionSourceMargin = ((value == Visibility.Visible) ? new Thickness(0.0, 0.0, 4.0, 0.0) : new Thickness(0.0, 0.0, 0.0, 0.0));
                SetProperty(ref _TransitionDonateVisibility, value, "TransitionDonateVisibility");
            }
        }

        public bool TransitionDonateActive
        {
            get
            {
                return _TransitionDonateActive;
            }
            set
            {
                SetProperty(ref _TransitionDonateActive, value, "TransitionDonateActive");
            }
        }

        public Visibility TransitionNotVerifiedVisibility
        {
            get
            {
                return _TransitionNotVerifiedVisibility;
            }
            set
            {
                SetProperty(ref _TransitionNotVerifiedVisibility, value, "TransitionNotVerifiedVisibility");
            }
        }

        public Visibility TransitionUpToDateVisibility
        {
            get
            {
                return _TransitionUpToDateVisibility;
            }
            set
            {
                SetProperty(ref _TransitionUpToDateVisibility, value, "TransitionUpToDateVisibility");
            }
        }

        public string TransitionUpToDateText
        {
            get
            {
                return _TransitionUpToDateText;
            }
            set
            {
                SetProperty(ref _TransitionUpToDateText, value, "TransitionUpToDateText");
            }
        }

        public Visibility TransitionUpdateReadyVisibility
        {
            get
            {
                return _TransitionUpdateReadyVisibility;
            }
            set
            {
                SetProperty(ref _TransitionUpdateReadyVisibility, value, "TransitionUpdateReadyVisibility");
            }
        }

        public double TransitionPublisherOpacity
        {
            get
            {
                return _TransitionPublisherOpacity;
            }
            set
            {
                SetProperty(ref _TransitionPublisherOpacity, value, "TransitionPublisherOpacity");
            }
        }

        public Visibility TransitionPublisherVisibility
        {
            get
            {
                return _TransitionPublisherVisibility;
            }
            set
            {
                SetProperty(ref _TransitionPublisherVisibility, value, "TransitionPublisherVisibility");
            }
        }

        public Visibility TransitionContentGridVisibility
        {
            get
            {
                return _TransitionContentGridVisibility;
            }
            set
            {
                SetProperty(ref _TransitionContentGridVisibility, value, "TransitionContentGridVisibility");
            }
        }

        public Visibility TransitionNoneGridVisibility
        {
            get
            {
                return _TransitionNoneGridVisibility;
            }
            set
            {
                SetProperty(ref _TransitionNoneGridVisibility, value, "TransitionNoneGridVisibility");
            }
        }

        public bool TransitionCloseButtonActive
        {
            get
            {
                return _TransitionCloseButtonActive;
            }
            set
            {
                SetProperty(ref _TransitionCloseButtonActive, value, "TransitionCloseButtonActive");
            }
        }

        public string TransitionSourceText
        {
            get
            {
                return _TransitionSourceText;
            }
            set
            {
                SetProperty(ref _TransitionSourceText, value, "TransitionSourceText");
            }
        }

        public string TransitionDonateText
        {
            get
            {
                return _TransitionDonateText;
            }
            set
            {
                SetProperty(ref _TransitionDonateText, value, "TransitionDonateText");
            }
        }

        public double TransitionUpdateBoxOpacity
        {
            get
            {
                return _TransitionUpdateBoxOpacity;
            }
            set
            {
                SetProperty(ref _TransitionUpdateBoxOpacity, value, "TransitionUpdateBoxOpacity");
            }
        }

        public bool TransitionUpdateButtonsActive
        {
            get
            {
                return _TransitionUpdateButtonsActive;
            }
            set
            {
                SetProperty(ref _TransitionUpdateButtonsActive, value, "TransitionUpdateButtonsActive");
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