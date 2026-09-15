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
    internal class IsoModePageViewModel : ViewModelBase
    {
        public IsoModePage ModePage { get; private set; }

        public IsoModePageViewModel(IsoModePage modePage)
        {
            ModePage = modePage;
        }

        public override ViewModelBase GetPreviousPage(ApplicationState state)
        {
            return new IsoLicensePageViewModel(state.licensePage);
        }

        public override bool HasPreviousPage()
        {
            return true;
        }

        public override ViewModelBase GetNextPage(ApplicationState state)
        {
            return null;
        }
    }
}
