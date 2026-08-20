using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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
    public class AcrylicWindowLite : Window
    {
        private const int GWL_STYLE = -16;

        private const int WS_SYSMENU = 524288;

        public MaterialManager.CornerPreference CornerType { get; set; } = MaterialManager.CornerPreference.Round;

        public AcrylicWindowLite()
        {
            base.Loaded += delegate
            {
                MaterialManager.SetWindowBackdrop(this, MaterialManager.BackdropType.Acrylic, CornerType);
                IntPtr handle = new WindowInteropHelper(this).Handle;
                SetWindowLong(handle, -16, GetWindowLong(handle, -16) & -524289);
            };
        }
        protected override void OnActivated(EventArgs e)
        {
            MaterialManager.SetWindowBackdrop(this, MaterialManager.BackdropType.Acrylic, CornerType);
            base.OnActivated(e);
        }

        public override void EndInit()
        {
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 0.0,
                CornerRadius = new CornerRadius(0.0),
                GlassFrameThickness = new Thickness(-1.0),
                ResizeBorderThickness = new Thickness(0.0)
            });
            base.EndInit();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
