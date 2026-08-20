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
                        if (Convert.ToUInt32(Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\Winmgmt").GetValue("Start")) == 4)
                        {
                            GUIUtil.EnsureWMI().GetAwaiter().GetResult();
                        }
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

        public static void SetWindowBackdrop(Window window, BackdropType micaType, CornerPreference cornerType = CornerPreference.Round)
        {
            if (GlobalsGUI.WinVer < 22000)
            {
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
            if (window.WindowStyle == WindowStyle.None)
            {
                SetWindowAttribute(windowHandle, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, 2);
            }
            if (GlobalsGUI.WinVer >= 22523)
            {
                SetWindowAttribute(windowHandle, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, (int)micaType);
            }
            _ = ThemeWatcher.CurrentTheme;
            SetWindowAttribute(windowHandle, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, 0);
        }
    }
}