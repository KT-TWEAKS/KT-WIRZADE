using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.TweakDialog.TweakModules
{
    public static class DevicePathMapper
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint QueryDosDevice([In] string lpDeviceName, [Out] StringBuilder lpTargetPath, [In] int ucchMax);

        public static string FromDevicePath(string devicePath)
        {
            if (devicePath == null)
            {
                return null;
            }
            DriveInfo drive = Array.Find(DriveInfo.GetDrives(), (DriveInfo d) => devicePath.StartsWith(GetDevicePath(d), StringComparison.InvariantCultureIgnoreCase));
            if (drive == null)
            {
                return null;
            }
            return ReplaceFirst(devicePath, GetDevicePath(drive), GetDriveLetter(drive));
        }

        private static string GetDevicePath(this DriveInfo driveInfo)
        {
            StringBuilder devicePathBuilder = new StringBuilder(128);
            if (QueryDosDevice(GetDriveLetter(driveInfo), devicePathBuilder, devicePathBuilder.Capacity + 1) == 0)
            {
                return null;
            }
            return devicePathBuilder.ToString();
        }

        private static string GetDriveLetter(this DriveInfo driveInfo)
        {
            return driveInfo.Name.Substring(0, 2);
        }

        private static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search, StringComparison.Ordinal);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
    }
}
