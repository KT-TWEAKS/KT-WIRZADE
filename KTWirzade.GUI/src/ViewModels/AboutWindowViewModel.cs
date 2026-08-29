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