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
    internal class IntroPageViewModel : ViewModelBase
    {
        public IntroPageViewModel()
        {
            if (!GlobalsGUI.Current.Playbook.VerificationTask.IsCompleted)
            {
                MainNextButtonActive = false;
                MainUpdatesButtonActive = false;
                MainStatusButtonActive = false;
            }
            else if (GlobalsGUI.Current.Playbook.VerificationStatus == PlaybookGUI.VerificationLevel.Unreached || 
                GlobalsGUI.Current.Playbook.VerificationStatus == PlaybookGUI.VerificationLevel.Unverified)
            {
                MainStatusButtonActive = false;
            }
        }

        public override ViewModelBase GetNextPage(ApplicationState state)
        {
            return new RequirementsPageViewModel(state.activationPage);
        }

        public override ViewModelBase GetPreviousPage(ApplicationState state)
        {
            throw new NotImplementedException();
        }

        public override bool HasPreviousPage()
        {
            return false;
        }
    }

}
