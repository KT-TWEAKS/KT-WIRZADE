using Core;
using Interprocess;
using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

namespace KTWirzade.GUI
{
    public class RegistryManager
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegLoadKey(IntPtr hKey, string lpSubKey, string lpFile);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegSaveKey(IntPtr hKey, string lpFile, uint securityAttrPtr = 0u);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegUnLoadKey(IntPtr hKey, string lpSubKey);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern IntPtr RtlAdjustPrivilege(int Privilege, bool bEnablePrivilege, bool IsThreadPrivilege, out bool PreviousValue);

        [DllImport("advapi32.dll")]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, ref ulong lpLuid);

        [DllImport("advapi32.dll")]
        private static extern bool LookupPrivilegeValue(IntPtr lpSystemName, string lpName, ref ulong lpLuid);

        private static void AcquirePrivileges()
        {
            ulong luid = 0uL;
            LookupPrivilegeValue(IntPtr.Zero, "SeRestorePrivilege", ref luid);
            RtlAdjustPrivilege((int)luid, bEnablePrivilege: true, IsThreadPrivilege: false, out var throwaway);
            LookupPrivilegeValue(IntPtr.Zero, "SeBackupPrivilege", ref luid);
            RtlAdjustPrivilege((int)luid, bEnablePrivilege: true, IsThreadPrivilege: false, out throwaway);
        }

        private static void ReturnPrivileges()
        {
            ulong luid = 0uL;
            LookupPrivilegeValue(IntPtr.Zero, "SeRestorePrivilege", ref luid);
            RtlAdjustPrivilege((int)luid, bEnablePrivilege: false, IsThreadPrivilege: false, out var throwaway);
            LookupPrivilegeValue(IntPtr.Zero, "SeBackupPrivilege", ref luid);
            RtlAdjustPrivilege((int)luid, bEnablePrivilege: false, IsThreadPrivilege: false, out throwaway);
        }

        [InterprocessMethod(Level.Administrator)]
        public static void HookHive(string hivePath, string hiveName)
        {
            try
            {
                AcquirePrivileges();
                using RegistryKey parentKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);
                RegLoadKey(parentKey.Handle.DangerousGetHandle(), hiveName, hivePath);
                ReturnPrivileges();
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Critical error while attempting to mount hive: " + hivePath, Array.Empty<(string, object)>());
            }
        }

        [InterprocessMethod(Level.Administrator)]
        public static void UnhookHive(string hiveName)
        {
            try
            {
                using RegistryKey usersKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);
                AcquirePrivileges();
                int result = RegUnLoadKey(usersKey.Handle.DangerousGetHandle(), hiveName);
                if (result != 0)
                {
                    ReturnPrivileges();
                    throw new Exception($"Failed to unhook hive from Registry ({Marshal.GetLastWin32Error()}): " + result);
                }
                ReturnPrivileges();
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Critical error while attempting to unmount hive: " + hiveName, Array.Empty<(string, object)>());
            }
        }
    }
}
