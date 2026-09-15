using Microsoft.Toolkit.Uwp.Notifications;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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
    public static class TaskBar
    {
        private struct FLASHWINFO
        {
            public uint cbSize;

            public IntPtr hwnd;

            public uint dwFlags;

            public uint uCount;

            public uint dwTimeout;
        }

        internal class TaskbarNotifier : IDisposable
        {
            [ComImport]
            [Guid("56FDF344-FD6D-11D0-958A-006097C9A090")]
            [ClassInterface(ClassInterfaceType.None)]
            private class TaskbarList
            {
            }

            [Flags]
            internal enum TBATFLAG
            {
                TBATF_USEMDITHUMBNAIL = 1,
                TBATF_USEMDILIVEPREVIEW = 2
            }

            [Flags]
            internal enum TBPFLAG
            {
                TBPF_NOPROGRESS = 0,
                TBPF_INDETERMINATE = 1,
                TBPF_NORMAL = 2,
                TBPF_ERROR = 4,
                TBPF_PAUSED = 8
            }

            [StructLayout(LayoutKind.Sequential, Pack = 4)]
            public struct THUMBBUTTON
            {
                public uint dwMask;

                public uint iId;

                public uint iBitmap;

                public IntPtr hIcon;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 260)]
                public ushort[] szTip;

                public uint dwFlags;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 4)]
            public struct RECT
            {
                public int left;

                public int top;

                public int right;

                public int bottom;
            }

            [ComImport]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
            public interface ITaskbarList3
            {
                void HrInit();

                void AddTab(IntPtr hwnd);

                void DeleteTab(IntPtr hwnd);

                void ActivateTab(IntPtr hwnd);

                void SetActivateAlt(IntPtr hwnd);

                void MarkFullscreenWindow(IntPtr hwnd, bool fFullscreen);

                void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);

                void SetProgressState(IntPtr hwnd, TBPFLAG tbpFlags);

                void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);

                void UnregisterTab(IntPtr hwndTab);

                void SetTabOrder(IntPtr hwndTab, int hwndInsertBefore);

                void SetTabActive(IntPtr hwndTab, int hwndMDI, TBATFLAG tbatFlags);

                void ThumbBarAddButtons(IntPtr hwnd, uint cButtons, THUMBBUTTON[] pButton);

                void ThumbBarUpdateButtons(IntPtr hwnd, uint cButtons, THUMBBUTTON[] pButton);

                void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);

                void SetOverlayIcon(IntPtr hwnd, IntPtr hIcon, [MarshalAs(UnmanagedType.LPWStr)] string pszDescription);

                void SetThumbnailTooltip(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string pszTip);

                void SetThumbnailClip(IntPtr hwnd, RECT prcClip);
            }

            private bool _disposed;

            private ITaskbarList3 _taskBarList;

            public void Dispose()
            {
                if (!_disposed && _taskBarList != null)
                {
                    _disposed = true;
                    try
                    {
                        Marshal.ReleaseComObject(_taskBarList);
                        _taskBarList = null;
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            public TaskbarNotifier()
            {
                _taskBarList = (ITaskbarList3)new TaskbarList();
                try
                {
                    _taskBarList.HrInit();
                }
                catch (NotImplementedException)
                {
                    _taskBarList = null;
                }
            }

            public void SetProgressValue(Window window, int value)
            {
                if (_taskBarList == null)
                {
                    return;
                }
                if (value < 0)
                {
                    value = 0;
                }
                if (value > 100)
                {
                    value = 100;
                }
                if (window.IsLoaded)
                {
                    IntPtr handle = new WindowInteropHelper(window).Handle;
                    _taskBarList.SetProgressValue(handle, Convert.ToUInt64(value), Convert.ToUInt64(100));
                    _taskBarList.SetProgressState(handle, TBPFLAG.TBPF_NORMAL);
                    return;
                }
                window.Loaded += delegate
                {
                    IntPtr handle2 = new WindowInteropHelper(window).Handle;
                    _taskBarList.SetProgressValue(handle2, Convert.ToUInt64(value), Convert.ToUInt64(100));
                    _taskBarList.SetProgressState(handle2, TBPFLAG.TBPF_NORMAL);
                };
            }

            public void SetProgressMarquee(Window window)
            {
                if (_taskBarList == null)
                {
                    return;
                }
                if (window.IsLoaded)
                {
                    IntPtr handle = new WindowInteropHelper(window).Handle;
                    _taskBarList.SetProgressState(handle, TBPFLAG.TBPF_INDETERMINATE);
                    return;
                }
                window.Loaded += delegate
                {
                    IntPtr handle2 = new WindowInteropHelper(window).Handle;
                    _taskBarList.SetProgressState(handle2, TBPFLAG.TBPF_INDETERMINATE);
                };
            }

            public void SetProgressNone(Window window)
            {
                if (_taskBarList != null && window.IsLoaded)
                {
                    IntPtr handle = new WindowInteropHelper(window).Handle;
                    _taskBarList.SetProgressState(handle, TBPFLAG.TBPF_NOPROGRESS);
                }
            }
        }

        private const uint FLASHW_STOP = 0u;

        private const uint FLASHW_TRAY = 2u;

        private const uint FLASHW_ALL = 3u;

        private const uint FLASHW_TIMER = 4u;

        private const uint FLASHW_TIMERNOFG = 12u;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        public static void FlashWindow(this Window win, uint count = uint.MaxValue)
        {
            if (!win.IsActive)
            {
                WindowInteropHelper h = new WindowInteropHelper(win);
                FLASHWINFO info = new FLASHWINFO
                {
                    hwnd = h.Handle,
                    dwFlags = 7u,
                    uCount = count,
                    dwTimeout = 0u
                };
                info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
                FlashWindowEx(ref info);
            }
        }

        public static void StopFlashingWindow(this Window win)
        {
            WindowInteropHelper h = new WindowInteropHelper(win);
            FLASHWINFO info = default;
            info.hwnd = h.Handle;
            info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
            info.dwFlags = 0u;
            info.uCount = uint.MaxValue;
            info.dwTimeout = 0u;
            FlashWindowEx(ref info);
        }

        public static void ShowNotification(string text)
        {
            new ToastContentBuilder()
                .AddText(text)
                .Show();
        }
    }
}