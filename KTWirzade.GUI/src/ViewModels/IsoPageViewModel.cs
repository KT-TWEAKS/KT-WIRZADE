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
            // ISO navigation is driven directly by the IsoPageView code-behind; this
            // fallback returns null (previously threw) so a stray Next click cannot
            // crash through the global exception handler.
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
