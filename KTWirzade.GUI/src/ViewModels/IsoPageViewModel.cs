using KTWirzade.GUI.Models;
using static iso_mode.USB;
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
    internal class IsoPageViewModel : ViewModelBase
    {
        public NotificationContext UsbNotifier;

        public List<UsbDisk> SelectedUSBDisks;

        private bool _Downloading;

        public bool Downloading
        {
            get
            {
                return _Downloading;
            }
            set
            {
                SetProperty(ref _Downloading, value, "Downloading");
            }
        }

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
            return false;
        }
    }
}
