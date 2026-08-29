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

        /// <summary>
        /// Opt-in window resizing. The custom chrome windows ship with a zero resize
        /// border by default (fixed-size dialogs); set this to true on windows whose
        /// layout is responsive and want user resizing.
        /// </summary>
        public bool Resizable { get; set; }

        public AcrylicWindow()
        {
            base.Loaded += delegate
            {
                ApplyBackdrop();
                IntPtr handle = new WindowInteropHelper(this).Handle;
                SetWindowLong(handle, -16, GetWindowLong(handle, -16) & -524289);
            };
            // Windows 10 rounds via a GDI window region; the region does not track
            // geometry changes, so reapply after resize and minimize/restore.
            base.SizeChanged += delegate
            {
                MaterialManager.ApplyRoundedCornerRegion(this);
            };
            base.StateChanged += delegate
            {
                Dispatcher.BeginInvoke(new Action(() => MaterialManager.ApplyRoundedCornerRegion(this)));
            };
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        protected override void OnActivated(EventArgs e)
        {
            ApplyBackdrop();
            base.OnActivated(e);
        }

        public override void EndInit()
        {
            var resizeBorder = Resizable ? 7.0 : 0.0;
            if (!MaterialManager.IsVMwareVM)
            {
                WindowChrome.SetWindowChrome(this, new WindowChrome
                {
                    CaptionHeight = 0.0,
                    CornerRadius = new CornerRadius(8.0),
                    GlassFrameThickness = new Thickness(-1.0),
                    ResizeBorderThickness = new Thickness(resizeBorder)
                });
            }
            else
            {
                WindowChrome.SetWindowChrome(this, new WindowChrome
                {
                    CaptionHeight = 0.0,
                    CornerRadius = ((GlobalsGUI.WinVer >= 22000) ? new CornerRadius(8.0) : new CornerRadius(0.0)),
                    GlassFrameThickness = new Thickness(0.0),
                    ResizeBorderThickness = new Thickness(resizeBorder)
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

        private void ApplyBackdrop()
        {
            if (MaterialManager.IsVMwareVM && !IsMainWindow)
            {
                MaterialManager.SetWindowBackdrop(this, MaterialManager.BackdropType.None, CornerType);
                return;
            }

            if (GlobalsGUI.WinVer >= 22000)
            {
                MaterialManager.SetWindowBackdrop(this, MaterialManager.BackdropType.Acrylic, CornerType);
            }
            else
            {
                MaterialManager.SetWindowBackdrop(this, MaterialManager.BackdropType.None, CornerType);
            }
        }
    }
}
