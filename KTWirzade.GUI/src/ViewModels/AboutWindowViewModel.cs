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
    public class AboutWindowViewModel : ViewModelBase
    {
        public override ViewModelBase GetNextPage(ApplicationState state)
        {
            // The About window is a standalone dialog: it is not part of the page flow.
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