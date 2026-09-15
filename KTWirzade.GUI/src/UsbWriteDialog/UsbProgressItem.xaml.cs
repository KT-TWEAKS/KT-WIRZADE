using System.Windows;
using System.Windows.Media.Animation;
using static Interprocess.InterLink;
using static iso_mode.USB;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.UsbWriteDialog
{
    public partial class UsbProgressItem : System.Windows.Controls.UserControl
    {

        public string Text
        {
            get
            {
                return StatusText.Text;
            }
            set
            {
                StatusText.Text = value;
            }
        }

        public bool Completed
        {
            get
            {
                return CheckImage.Visibility == Visibility.Visible;
            }
            set
            {
                UsbImage.Visibility = (value ? Visibility.Collapsed : Visibility.Visible);
                CheckImage.Visibility = ((!value) ? Visibility.Collapsed : Visibility.Visible);
            }
        }

        public bool Failed
        {
            get
            {
                return FailedImage.Visibility == Visibility.Visible;
            }
            set
            {
                UsbImage.Visibility = (value ? Visibility.Collapsed : Visibility.Visible);
                FailedImage.Visibility = ((!value) ? Visibility.Collapsed : Visibility.Visible);
            }
        }

        public bool Active
        {
            get
            {
                return base.IsEnabled;
            }
            set
            {
                base.IsEnabled = value;
                if (value)
                {
                    DoubleAnimation fadeIn = new DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(0.2)
                    };
                    Storyboard.SetTarget(fadeIn, MainStack);
                    Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
                    Storyboard storyboard = new Storyboard();
                    storyboard.Children.Add(fadeIn);
                    storyboard.Begin(MainStack);
                }
                else
                {
                    DoubleAnimation fadeIn2 = new DoubleAnimation
                    {
                        To = 0.5,
                        Duration = TimeSpan.FromSeconds(0.2)
                    };
                    Storyboard.SetTarget(fadeIn2, MainStack);
                    Storyboard.SetTargetProperty(fadeIn2, new PropertyPath(UIElement.OpacityProperty));
                    Storyboard storyboard2 = new Storyboard();
                    storyboard2.Children.Add(fadeIn2);
                    storyboard2.Begin(MainStack);
                }
            }
        }

        public Task WriteTask { get; set; }

        public InterProgress Progress { get; set; }

        public UsbDisk UsbDisk { get; set; }

        internal UsbProgressItem()
        {
            InitializeComponent();
        }
    }
}
