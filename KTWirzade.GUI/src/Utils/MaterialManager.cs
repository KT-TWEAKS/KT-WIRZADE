using Microsoft.Win32;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using KTWirzade.GUI;
using System;


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

        private static bool? _isVMwareVM;

        private enum AccentState
        {
            Disabled = 0,
            BlurBehind = 3,
            AcrylicBlurBehind = 4
        }

        private enum WindowCompositionAttribute
        {
            AccentPolicy = 19
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState State;
            public int Flags;
            public uint GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

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

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

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
                ApplyWindows10Backdrop(window, micaType);
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

        /// <summary>
        /// Windows 10 exposes acrylic through SetWindowCompositionAttribute rather
        /// than the public Windows 11 backdrop attributes. The tint keeps text
        /// readable while allowing the desktop behind the window to remain visible.
        /// </summary>
        private static void ApplyWindows10Backdrop(Window window, BackdropType backdropType)
        {
            IntPtr windowHandle = new WindowInteropHelper(window).Handle;
            if (windowHandle == IntPtr.Zero)
                return;

            bool enabled = backdropType != BackdropType.None;
            if (enabled)
                window.Background = Brushes.Transparent;

            var policy = new AccentPolicy
            {
                State = enabled
                    ? (GlobalsGUI.WinVer >= 17134 ? AccentState.AcrylicBlurBehind : AccentState.BlurBehind)
                    : AccentState.Disabled,
                Flags = enabled ? 2 : 0,
                // ACCENT_POLICY expects AABBGGRR. A 60% tint keeps the blur visible
                // beneath the semi-transparent WPF surfaces.
                GradientColor = ThemeWatcher.CurrentTheme == ThemeWatcher.WindowsTheme.Dark
                    ? 0x99171514u
                    : 0x99F7F4EEu,
                AnimationId = 0
            };

            int size = Marshal.SizeOf<AccentPolicy>();
            IntPtr policyPointer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, policyPointer, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.AccentPolicy,
                    Data = policyPointer,
                    SizeOfData = size
                };
                SetWindowCompositionAttribute(windowHandle, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(policyPointer);
            }
        }
    }
}
