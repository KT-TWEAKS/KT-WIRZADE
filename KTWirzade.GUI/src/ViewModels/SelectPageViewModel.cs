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
    internal class SelectPageViewModel : ViewModelBase
    {
        public SelectPageViewModel()
        {
            MainNextButtonActive = false;
        }

        public override ViewModelBase GetNextPage(ApplicationState state)
        {
            // Terminal page: the Next button is disabled and items drive navigation.
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