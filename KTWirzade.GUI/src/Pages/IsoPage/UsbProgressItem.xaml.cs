using System.Windows;
using System.Windows.Input;
using static iso_mode.USB;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.Pages.IsoPage
{
    public partial class UsbProgressItem : System.Windows.Controls.CheckBox
    {
        public bool Selected;

        internal UsbDisk USB { get; set; }

        public string UsbName { get; set; }

        public string UsbSize { get; set; }

        internal UsbProgressItem(UsbDisk usb)
        {
            InitializeComponent();
            USB = usb;
            UsbName = usb.FriendlyName.Replace("USB", "").Trim();
            UsbSize = usb.ReadableSize;
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsChecked == true)
            {
                e.Handled = true;
                OnChecked(new RoutedEventArgs(CheckedEvent));
            }
        }

        internal UsbProgressItem()
        {
        }
    }
}