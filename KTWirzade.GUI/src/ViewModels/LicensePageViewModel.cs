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
    internal class LicensePageViewModel : ViewModelBase
    {
        public LicensePage LicensePage { get; private set; }

        public LicensePageViewModel(LicensePage licensePage)
        {
            LicensePage = licensePage;
        }

        public override ViewModelBase GetNextPage(ApplicationState state)
        {
            return new ModePageViewModel(state.modePage);
        }

        public override ViewModelBase GetPreviousPage(ApplicationState state)
        {
            return new RequirementsPageViewModel(state.activationPage);
        }

        public override bool HasPreviousPage()
        {
            return true;
        }
    }
}