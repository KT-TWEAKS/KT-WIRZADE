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
    internal class LoadPageViewModel : ViewModelBase
    {
        public LoadPageViewModel()
        {
            MainNextButtonActive = false;
        }

        public override ViewModelBase GetNextPage(ApplicationState state)
        {
            // Load page is a holding page; navigation is driven by the load task.
            return null;
        }

        public override ViewModelBase GetPreviousPage(ApplicationState state)
        {
            return null;
        }

        public override bool HasPreviousPage()
        {
            return false;
        }
    }
}