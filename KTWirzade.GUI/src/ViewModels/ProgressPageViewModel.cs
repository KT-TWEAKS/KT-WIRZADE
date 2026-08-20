using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    internal class ProgressPageViewModel : ViewModelBase
    {
        public ProgressPage ProgressPage { get; set; }

        public ProgressPageViewModel(ProgressPage progressPage)
        {
            ProgressPage = progressPage;
            MainNextButtonActive = false;
            MainPreviousButtonActive = false;
            MainCloseButtonActive = false;
            MainCancelButtonActive = false;
            MainUpdatesButtonActive = false;
            MainPlaybookColumnVisibility = Visibility.Collapsed;
        }

        public override ViewModelBase GetNextPage(ApplicationState state)
        {
            if (AmeliorationUtil.ErrorDisplayList.Count > 0)
            {
                return new FinishErrorPageViewModel(state.finishErrorPage);
            }
            return new FinishPageViewModel(state.finishPage);
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
