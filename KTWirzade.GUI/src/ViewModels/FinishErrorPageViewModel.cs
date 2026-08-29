using Core.Actions;
using System.Windows;
using System.Windows.Controls;
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
    internal class FinishErrorPageViewModel : ViewModelBase
    {
        public FinishErrorPage finishErrorPage { get; set; }

        public FinishErrorPageViewModel(FinishErrorPage finishErrorPage)
        {
            this.finishErrorPage = finishErrorPage;
            MainCancelButtonActive = false;
            MainNextButtonContent = new TextBlock
            {
                Text = "Reboot"
            };
            MainNextButtonStyle = MainNextButtonStyles.Pulse;
            MainPlaybookColumnVisibility = Visibility.Collapsed;
        }

        public override ViewModelBase GetNextPage(ApplicationState state)
        {
            CoreActions.SafeRun(new CmdAction
            {
                Command = "timeout /t 1 & shutdown /r /t 0",
                Wait = false
            }, false);
            System.Windows.Application.Current.Shutdown();
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
