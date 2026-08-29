using Core;
using Core.Actions;
using Interprocess;
using System;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using KTWirzade.Shared;


namespace KTWirzade.GUI.Utils
{
    public static class CredentialManager
    {
        [InterprocessMethod(Level.Administrator)]
        public static void SetUserCredentials(string username, string pendingUsername, string domain, string sid, string password, bool autoLogon)
        {
            bool error = false;
            try
            {
                try
                {
                    ServiceController server = new ServiceController("LanmanServer");
                    WinUtil.ChangeStartMode(server, ServiceStartMode.Automatic);
                    ServiceController serviceController = new ServiceController("LanmanWorkstation");
                    WinUtil.ChangeStartMode(serviceController, ServiceStartMode.Automatic);
                    if (server.Status != ServiceControllerStatus.Running)
                    {
                        server.Start();
                    }
                    if (serviceController.Status != ServiceControllerStatus.Running)
                    {
                        serviceController.Start();
                    }
                    server.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(10000.0));
                    serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(10000.0));
                }
                catch (Exception ex)
                {
                    Log.EnqueueExceptionSafe(ex, "Failed to enable Lanman services.", Array.Empty<ValueTuple<string, object>>());
                }
                try
                {
                    new RunAction
                    {
                        BaseDir = true,
                        Exe = "NSudoLC.exe",
                        Arguments = "-ShowWindowMode:Hide -Wait -U:T -P:E -M:S -Priority:RealTime cmd /c \"CONVERT.bat\""
                    }.RunTask(true);
                }
                catch (Exception ex2)
                {
                    Log.EnqueueExceptionSafe(ex2, "Account conversion error.", new ValueTuple<string, object>[]
                    {
                        new ValueTuple<string, object>("Username", username)
                    });
                }
                PrincipalSearcher userPrincipalSearcher = new PrincipalSearcher(new UserPrincipal(new PrincipalContext(ContextType.Machine)));
                if (sid != null)
                {
                    UserObject = (UserPrincipal)userPrincipalSearcher.FindAll().FirstOrDefault((Principal x) => x is UserPrincipal && x.Sid.Value == sid);
                }
                else
                {
                    UserObject = (UserPrincipal)userPrincipalSearcher.FindAll().FirstOrDefault((Principal x) => x is UserPrincipal && x.Name == username);
                }
                if (UserObject == null)
                {
                    throw new Exception("User not found.");
                }
                UserObject.SetPassword(password);
                Wrap.ExecuteSafe(() => UserObject.PasswordNeverExpires = true, true, null);
            }
            catch (Exception ex3)
            {
                Log.EnqueueExceptionSafe(ex3, "Failed to set user password.", Array.Empty<ValueTuple<string, object>>());
                error = true;
            }
            if (autoLogon && !error)
            {
                try
                {
                    string LogonKey = "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon";
                    new RegistryValueAction
                    {
                        KeyName = LogonKey,
                        Value = "DefaultUserName",
                        Data = pendingUsername ?? username,
                        Type = RegistryValueType.REG_SZ
                    }.RunTask(true);
                    new RegistryValueAction
                    {
                        KeyName = LogonKey,
                        Value = "DefaultDomainName",
                        Data = string.IsNullOrEmpty(domain) ? Environment.MachineName : domain,
                        Type = RegistryValueType.REG_SZ
                    }.RunTask(true);
                    new RegistryValueAction
                    {
                        KeyName = LogonKey,
                        Value = "AutoAdminLogon",
                        Data = 1,
                        Type = RegistryValueType.REG_DWORD
                    }.RunTask(true);
                    new RegistryValueAction
                    {
                        KeyName = LogonKey,
                        Value = "AutoLogonCount",
                        Operation = 0
                    }.RunTask(true);
                    new RegistryValueAction
                    {
                        KeyName = LogonKey,
                        Value = "DisableCAD",
                        Data = 1,
                        Type = RegistryValueType.REG_DWORD
                    }.RunTask(true);
                    new RegistryValueAction
                    {
                        KeyName = LogonKey,
                        Value = "DefaultPassword",
                        Operation = 0
                    }.RunTask(true);
                    StoreData("DefaultPassword", password);
                    return;
                }
                catch (Exception ex4)
                {
                    Log.EnqueueExceptionSafe(ex4, "Failed to enable AutoLogon.", Array.Empty<ValueTuple<string, object>>());
                    return;
                }
            }
            Wrap.ExecuteSafe(delegate ()
            {
                string LogonKey2 = "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon";
                new RegistryValueAction
                {
                    KeyName = LogonKey2,
                    Value = "DefaultUserName",
                    Operation = 0
                }.RunTask(true);
                new RegistryValueAction
                {
                    KeyName = LogonKey2,
                    Value = "DefaultDomainName",
                    Operation = 0
                }.RunTask(true);
                new RegistryValueAction
                {
                    KeyName = LogonKey2,
                    Value = "AutoAdminLogon",
                    Operation = 0
                }.RunTask(true);
                new RegistryValueAction
                {
                    KeyName = LogonKey2,
                    Value = "AutoLogonCount",
                    Operation = 0
                }.RunTask(true);
                new RegistryValueAction
                {
                    KeyName = LogonKey2,
                    Value = "DefaultPassword",
                    Operation = 0
                }.RunTask(true);
                StoreData("DefaultPassword", "");
            }, false, null);
        }

        [InterprocessMethod(Level.Administrator)]
        public static void SetAdminPassword(string password)
        {
            try
            {
                UserPrincipal userPrincipal = (UserPrincipal)new PrincipalSearcher(new UserPrincipal(new PrincipalContext(ContextType.Machine))).FindAll().FirstOrDefault((Principal x) => x is UserPrincipal && x.Sid.IsWellKnown(WellKnownSidType.AccountAdministratorSid));
                if (userPrincipal == null)
                {
                    throw new Exception("User not found.");
                }
                userPrincipal.SetPassword(password);
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Admin password set failed.", Array.Empty<ValueTuple<string, object>>());
            }
        }

        [InterprocessMethod(Level.Administrator)]
        public static void RenameUser(string username, string newUsername, string sid)
        {
            //if (UserObject == null)
            //{
            //    PrincipalSearcher userPrincipalSearcher = new PrincipalSearcher(new UserPrincipal(new PrincipalContext(ContextType.Machine)));
            //    if (sid != null)
            //    {
            //        UserObject = (UserPrincipal)userPrincipalSearcher.FindAll().FirstOrDefault((Principal x) => x is UserPrincipal && x.Sid.Value == sid);
            //    }
            //    else
            //    {
            //        UserObject = (UserPrincipal)userPrincipalSearcher.FindAll().FirstOrDefault((Principal x) => x is UserPrincipal && x.Name == username);
            //    }
            //    if (UserObject == null)
            //    {
            //        throw new Exception("User not found.");
            //    }
            //}
            //System.DirectoryServices.DirectoryEntry directoryEntry = (DirectoryEntry)UserObject.GetUnderlyingObject();
            //directoryEntry.Rename(newUsername);
            //directoryEntry.CommitChanges();
        }

        public static long StoreData(string keyName, string Data)
        {
            IntPtr zero = IntPtr.Zero;
            IntPtr pSid = Marshal.AllocHGlobal(0);
            SafeNativeMethods.LSA_UNICODE_STRING systemName = default;
            int access = 32;
            IntPtr policyHandle = IntPtr.Zero;
            SafeNativeMethods.LSA_OBJECT_ATTRIBUTES ObjectAttributes = default;
            ObjectAttributes.Length = 0;
            ObjectAttributes.RootDirectory = IntPtr.Zero;
            ObjectAttributes.Attributes = 0U;
            ObjectAttributes.SecurityDescriptor = IntPtr.Zero;
            ObjectAttributes.SecurityQualityOfService = IntPtr.Zero;
            long winErrorCode = (long)((ulong)SafeNativeMethods.LsaNtStatusToWinError(SafeNativeMethods.LsaOpenPolicy(ref systemName, ref ObjectAttributes, access, out policyHandle)));
            if (winErrorCode != 0L)
            {
                Log.EnqueueSafe(LogType.Error, "Failed to enable AutoLogon: OpenPolicy failed: " + winErrorCode.ToString(), new SerializableTrace(null, 0, int.MaxValue), Array.Empty<ValueTuple<string, object>>());
            }
            else
            {
                SafeNativeMethods.LSA_UNICODE_STRING[] uKeyName =
                {
                    default
                };
                uKeyName[0].Buffer = Marshal.StringToHGlobalUni(keyName);
                uKeyName[0].Length = (ushort)(keyName.Length * 2);
                uKeyName[0].MaximumLength = (ushort)((keyName.Length + 1) * 2);
                SafeNativeMethods.LSA_UNICODE_STRING[] uData =
                {
                    default
                };
                uData[0].Buffer = Marshal.StringToHGlobalUni(Data);
                uData[0].Length = (ushort)(Data.Length * 2);
                uData[0].MaximumLength = (ushort)((Data.Length + 1) * 2);
                winErrorCode = (long)((ulong)SafeNativeMethods.LsaStorePrivateData(policyHandle, uKeyName, uData));
                if (winErrorCode != 0L)
                {
                    Log.EnqueueSafe(LogType.Error, "Failed to enable AutoLogon: LsaStorePrivateData failed: " + winErrorCode.ToString(), new SerializableTrace(null, 0, int.MaxValue), Array.Empty<ValueTuple<string, object>>());
                }
                SafeNativeMethods.LsaClose(policyHandle);
            }
            SafeNativeMethods.FreeSid(pSid);
            return winErrorCode;
        }

        private static UserPrincipal UserObject;

        internal static class SafeNativeMethods
        {
            [DllImport("advapi32")]
            public static extern IntPtr FreeSid(IntPtr pSid);

            [DllImport("advapi32.dll")]
            public static extern uint LsaOpenPolicy(ref LSA_UNICODE_STRING SystemName, ref LSA_OBJECT_ATTRIBUTES ObjectAttributes, int DesiredAccess, out IntPtr PolicyHandle);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern uint LsaStorePrivateData(IntPtr PolicyHandle, LSA_UNICODE_STRING[] KeyName, LSA_UNICODE_STRING[] PrivateData);

            [DllImport("advapi32.dll")]
            public static extern uint LsaRetrievePrivateData(IntPtr PolicyHandle, LSA_UNICODE_STRING[] KeyName, out IntPtr PrivateData);

            [DllImport("advapi32.dll")]
            public static extern uint LsaNtStatusToWinError(uint status);

            [DllImport("advapi32.dll")]
            public static extern uint LsaClose(IntPtr ObjectHandle);

            public struct LSA_UNICODE_STRING : IDisposable
            {
                public void Dispose()
                {
                    this = default;
                }

                public ushort Length;

                public ushort MaximumLength;

                public IntPtr Buffer;
            }

            public struct LSA_OBJECT_ATTRIBUTES
            {
                public int Length;

                public IntPtr RootDirectory;

                public SafeNativeMethods.LSA_UNICODE_STRING ObjectName;

                public uint Attributes;

                public IntPtr SecurityDescriptor;

                public IntPtr SecurityQualityOfService;
            }

            public enum LSA_AccessPolicy : long
            {
                POLICY_VIEW_LOCAL_INFORMATION = 1L,
                POLICY_VIEW_AUDIT_INFORMATION,
                POLICY_GET_PRIVATE_INFORMATION = 4L,
                POLICY_TRUST_ADMIN = 8L,
                POLICY_CREATE_ACCOUNT = 16L,
                POLICY_CREATE_SECRET = 32L,
                POLICY_CREATE_PRIVILEGE = 64L,
                POLICY_SET_DEFAULT_QUOTA_LIMITS = 128L,
                POLICY_SET_AUDIT_REQUIREMENTS = 256L,
                POLICY_AUDIT_LOG_ADMIN = 512L,
                POLICY_SERVER_ADMIN = 1024L,
                POLICY_LOOKUP_NAMES = 2048L,
                POLICY_NOTIFICATION = 4096L
            }
        }
    }
}