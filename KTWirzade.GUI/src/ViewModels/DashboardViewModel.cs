using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KTWirzade.GUI.Models;

namespace KTWirzade.GUI.ViewModels
{
    internal class DashboardViewModel : ViewModelBase
    {
        public override ViewModelBase GetNextPage(ApplicationState state)
        {
            return new SelectPageViewModel();
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
