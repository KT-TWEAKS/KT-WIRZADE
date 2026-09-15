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
    internal class IsoOptionsPageViewModel : ViewModelBase
    {
        public List<UsbDisk> SelectedUSBDisks;

        public IsoModePage ModePage { get; private set; }

        public IsoOptionsPageViewModel(List<UsbDisk> selectedDisks)
        {
            SelectedUSBDisks = selectedDisks;
        }

        public override ViewModelBase GetPreviousPage(ApplicationState state)
        {
            return new IsoPageViewModel();
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