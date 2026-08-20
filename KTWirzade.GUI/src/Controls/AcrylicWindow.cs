using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using KTWirzade.GUI.Utils;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.Controls
{
    public class AcrylicWindow : Window
    {
        private const int GWL_STYLE = -16;

        private const int WS_SYSMENU = 524288;

        public MaterialManager.CornerPreference CornerType { get; set; } = MaterialManager.CornerPreference.Round;

        public bool IsMainWindow { get; set; }

        public AcrylicWindow()
        {
            base.Loaded += delegate
            {
                if (IsMainWindow || !MaterialManager.IsVMwareVM)
                {
                    MaterialManager.SetWindowBackdrop(this, MaterialManager.BackdropType.Acrylic, CornerType);
                }
                else
                {
                    MaterialManager.SetWindowBackdrop(this, MaterialManager.BackdropType.None, CornerType);
                }
                IntPtr handle = new WindowInteropHelper(this).Handle;
                SetWindowLong(handle, -16, GetWindowLong(handle, -16) & -524289);
            };
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        protected override void OnActivated(EventArgs e)
        {
            if (IsMainWindow || !MaterialManager.IsVMwareVM)
            {
                MaterialManager.SetWindowBackdrop(this, MaterialManager.BackdropType.Acrylic, CornerType);
            }
            else
            {
                MaterialManager.SetWindowBackdrop(this, MaterialManager.BackdropType.None, CornerType);
            }
            base.OnActivated(e);
        }

        public override void EndInit()
        {
            if (!MaterialManager.IsVMwareVM)
            {
                WindowChrome.SetWindowChrome(this, new WindowChrome
                {
                    CaptionHeight = 0.0,
                    CornerRadius = new CornerRadius(8.0),
                    GlassFrameThickness = new Thickness(-1.0),
                    ResizeBorderThickness = new Thickness(0.0)
                });
            }
            else
            {
                WindowChrome.SetWindowChrome(this, new WindowChrome
                {
                    CaptionHeight = 0.0,
                    CornerRadius = ((GlobalsGUI.WinVer >= 22000) ? new CornerRadius(8.0) : new CornerRadius(0.0)),
                    GlassFrameThickness = new Thickness(0.0),
                    ResizeBorderThickness = new Thickness(0.0)
                });
            }
            base.EndInit();
        }

        public async Task CloseWindow(ScaleTransform windowscale = null)
        {
            if (IsMainWindow && MaterialManager.IsVMwareVM && GlobalsGUI.WinVer >= 22523 && windowscale != null)
            {
                base.Template = FindResource("FakeWindowCorner") as ControlTemplate;
                await Task.Delay(20);
                MaterialManager.SetWindowBackdrop(this, MaterialManager.BackdropType.None, MaterialManager.CornerPreference.DoNotRound);
                DoubleAnimation animation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(160.0));
                BeginAnimation(UIElement.OpacityProperty, animation);
                DoubleAnimation scale_x = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.9,
                    Duration = TimeSpan.FromMilliseconds(160.0)
                };
                DoubleAnimation scale_y = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.9,
                    Duration = TimeSpan.FromMilliseconds(160.0)
                };
                windowscale.BeginAnimation(ScaleTransform.ScaleXProperty, scale_x);
                windowscale.BeginAnimation(ScaleTransform.ScaleYProperty, scale_y);
                await Task.Delay(160);
                Close();
            }
            else
            {
                SystemCommands.CloseWindow(this);
            }
        }
    }
}