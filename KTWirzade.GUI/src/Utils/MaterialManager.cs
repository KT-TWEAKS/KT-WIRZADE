using Microsoft.Win32;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using KTWirzade.GUI;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.Utils
{
    public class MaterialManager
    {
        public enum DWMWINDOWATTRIBUTE
        {
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
            DWMWA_SYSTEMBACKDROP_TYPE = 38,
            DWMWA_MICA_EFFECT = 1029,
            DWMWA_WINDOW_CORNER_PREFERENCE = 33
        }

        public enum BackdropType
        {
            None = 1,
            Mica,
            Acrylic,
            Tabbed
        }

        public enum CornerPreference
        {
            Default,
            DoNotRound,
            Round,
            RoundSmall
        }

        private const int True = 1;

        private const int False = 0;

        private static int? Build;

        private static bool? _isVMwareVM;

        public static bool IsVMwareVM
        {
            get
            {
                if (!_isVMwareVM.HasValue)
                {
                    try
                    {
                        // NOTE: do NOT call EnsureWMI() synchronously here - this getter runs on
                        // the UI thread while constructing every AcrylicWindow, and a blocking
                        // IPC call (.GetAwaiter().GetResult()) froze the whole app when opening
                        // any window with the Winmgmt service disabled. A failed WMI query just
                        // means "not a VMware VM" for our purposes.
                        using ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select Manufacturer from Win32_ComputerSystem");
                        using ManagementObjectCollection items = searcher.Get();
                        foreach (ManagementBaseObject item in items)
                        {
                            if (item["Manufacturer"].ToString().ToLower().Contains("vmware"))
                            {
                                _isVMwareVM = true;
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                    if (!_isVMwareVM.HasValue)
                    {
                        _isVMwareVM = false;
                    }
                }
                return _isVMwareVM.Value;
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE dwAttribute, ref int pvAttribute, int cbAttribute);

        private static int SetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, int parameter)
        {
            return DwmSetWindowAttribute(hwnd, attribute, ref parameter, Marshal.SizeOf<int>());
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Windows 10 has no DWMWA_WINDOW_CORNER_PREFERENCE (Win11+ only), so approximate
        /// the rounded look by clipping the window with a GDI round-rect region. The OS
        /// owns the region after SetWindowRgn succeeds. Must be called again when the
        /// window resizes or restores from maximized.
        /// </summary>
        public static void ApplyRoundedCornerRegion(Window window)
        {
            try
            {
                if (GlobalsGUI.WinVer >= 22000)
                {
                    return;
                }
                IntPtr windowHandle = new WindowInteropHelper(window).Handle;
                if (windowHandle == IntPtr.Zero)
                {
                    return;
                }
                if (window.WindowState == WindowState.Maximized)
                {
                    // Fullscreen windows must keep square corners.
                    SetWindowRgn(windowHandle, IntPtr.Zero, true);
                    return;
                }
                double dpiScale = 1.0;
                if (PresentationSource.FromVisual(window) is HwndSource source && source.CompositionTarget != null)
                    dpiScale = source.CompositionTarget.TransformToDevice.M11;
                if (dpiScale <= 0.0)
                    dpiScale = 1.0;
                if (!GetWindowRect(windowHandle, out RECT rect))
                {
                    return;
                }
                int radius = (int)Math.Round(8.0 * dpiScale);
                IntPtr region = CreateRoundRectRgn(0, 0, rect.Right - rect.Left + 1, rect.Bottom - rect.Top + 1, radius * 2, radius * 2);
                SetWindowRgn(windowHandle, region, true);
            }
            catch (Exception)
            {
            }
        }

        public static void SetWindowBackdrop(Window window, BackdropType micaType, CornerPreference cornerType = CornerPreference.Round)
        {
            if (GlobalsGUI.WinVer < 22000)
            {
                // No DWM backdrop/corner APIs before Windows 11 - fall back to a
                // rounded window region so the app keeps its rounded identity.
                ApplyRoundedCornerRegion(window);
                return;
            }
            IntPtr windowHandle = new WindowInteropHelper(window).Handle;
            if (micaType == BackdropType.None)
            {
                if (GlobalsGUI.WinVer >= 22523)
                {
                    SetWindowAttribute(windowHandle, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, (int)micaType);
                }
                SetWindowAttribute(windowHandle, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, (int)cornerType);
                return;
            }
            window.Background = new SolidColorBrush(Colors.Transparent);
            // Always enforce the requested corner preference; it was previously only
            // applied when WindowStyle was None, leaving square corners otherwise.
            SetWindowAttribute(windowHandle, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, (int)cornerType);
            if (GlobalsGUI.WinVer >= 22523)
            {
                SetWindowAttribute(windowHandle, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, (int)micaType);
            }
            _ = ThemeWatcher.CurrentTheme;
            SetWindowAttribute(windowHandle, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, 0);
        }
    }
}