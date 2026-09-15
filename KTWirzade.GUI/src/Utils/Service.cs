using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
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
    public class Service : ServiceBase
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;

            public string lpReserved;

            public string lpDesktop;

            public string lpTitle;

            public int dwX;

            public int dwY;

            public int dwXSize;

            public int dwYSize;

            public int dwXCountChars;

            public int dwYCountChars;

            public int dwFillAttribute;

            public int dwFlags;

            public short wShowWindow;

            public short cbReserved2;

            public IntPtr lpReserved2;

            public IntPtr hStdInput;

            public IntPtr hStdOutput;

            public IntPtr hStdError;
        }

        public struct ProcessInformation
        {
            public IntPtr processHandle;

            public IntPtr threadHandle;

            public int processId;

            public int threadId;
        }

        private static class Win32
        {
            public static class Service
            {
                [StructLayout(LayoutKind.Sequential)]
                public class SERVICE_STATUS
                {
                    public int dwServiceType;

                    public ServiceState dwCurrentState;

                    public int dwControlsAccepted;

                    public int dwWin32ExitCode;

                    public int dwServiceSpecificExitCode;

                    public int dwCheckPoint;

                    public int dwWaitHint;
                }

                public enum ServiceState
                {
                    Unknown = -1,
                    NotFound,
                    Stopped,
                    StartPending,
                    StopPending,
                    Running,
                    ContinuePending,
                    PausePending,
                    Paused
                }

                [Flags]
                public enum ScmAccessRights
                {
                    Connect = 1,
                    CreateService = 2,
                    EnumerateService = 4,
                    Lock = 8,
                    QueryLockStatus = 0x10,
                    ModifyBootConfig = 0x20,
                    StandardRightsRequired = 0xF0000,
                    AllAccess = 0xF003F
                }

                [Flags]
                public enum ServiceAccessRights
                {
                    QueryConfig = 1,
                    ChangeConfig = 2,
                    QueryStatus = 4,
                    EnumerateDependants = 8,
                    Start = 0x10,
                    Stop = 0x20,
                    PauseContinue = 0x40,
                    Interrogate = 0x80,
                    UserDefinedControl = 0x100,
                    Delete = 0x10000,
                    StandardRightsRequired = 0xF0000,
                    AllAccess = 0xF01FF
                }

                public enum ServiceBootFlag
                {
                    Start,
                    SystemStart,
                    AutoStart,
                    DemandStart,
                    Disabled
                }

                public enum ServiceControl
                {
                    Stop = 1,
                    Pause,
                    Continue,
                    Interrogate,
                    Shutdown,
                    ParamChange,
                    NetBindAdd,
                    NetBindRemove,
                    NetBindEnable,
                    NetBindDisable
                }

                public enum ServiceError
                {
                    Ignore,
                    Normal,
                    Severe,
                    Critical
                }

                public struct ENUM_SERVICE_STATUS_PROCESS
                {
                    [MarshalAs(UnmanagedType.LPWStr)]
                    public string lpServiceName;

                    [MarshalAs(UnmanagedType.LPWStr)]
                    public string lpDisplayName;

                    public SERVICE_STATUS_PROCESS ServiceStatusProcess;
                }

                public struct SERVICE_STATUS_PROCESS
                {
                    public SERVICE_TYPE ServiceType;

                    public SERVICE_STATE CurrentState;

                    public SERVICE_ACCEPT ControlsAccepted;

                    public int Win32ExitCode;

                    public int ServiceSpecificExitCode;

                    public int CheckPoint;

                    public int WaitHint;

                    public int ProcessID;

                    public SERVICE_FLAGS ServiceFlags;
                }

                public enum SERVICE_STATE
                {
                    ContinuePending = 5,
                    PausePending = 6,
                    Paused = 7,
                    Running = 4,
                    StartPending = 2,
                    StopPending = 3,
                    Stopped = 1
                }

                public enum SERVICE_ACCEPT
                {
                    NetBindChange = 16,
                    ParamChange = 8,
                    PauseContinue = 2,
                    PreShutdown = 256,
                    Shutdown = 4,
                    Stop = 1,
                    HardwareProfileChange = 32,
                    PowerEvent = 64,
                    SessionChange = 128
                }

                public enum SERVICE_FLAGS
                {
                    None,
                    RunsInSystemProcess
                }

                [Flags]
                public enum SERVICE_TYPE : uint
                {
                    SERVICE_KERNEL_DRIVER = 1u,
                    SERVICE_FILE_SYSTEM_DRIVER = 2u,
                    SERVICE_WIN32_OWN_PROCESS = 0x10u,
                    SERVICE_WIN32_SHARE_PROCESS = 0x20u,
                    SERVICE_INTERACTIVE_PROCESS = 0x100u
                }

                public enum SERVICE_START : uint
                {
                    SERVICE_BOOT_START,
                    SERVICE_SYSTEM_START,
                    SERVICE_AUTO_START,
                    SERVICE_DEMAND_START,
                    SERVICE_DISABLED
                }

                public enum SERVICE_ERROR
                {
                    SERVICE_ERROR_IGNORE,
                    SERVICE_ERROR_NORMAL,
                    SERVICE_ERROR_SEVERE,
                    SERVICE_ERROR_CRITICAL
                }

                [Flags]
                public enum SERVICE_ACCESS : uint
                {
                    STANDARD_RIGHTS_REQUIRED = 0xF0000u,
                    SERVICE_QUERY_CONFIG = 1u,
                    SERVICE_CHANGE_CONFIG = 2u,
                    SERVICE_QUERY_STATUS = 4u,
                    SERVICE_ENUMERATE_DEPENDENTS = 8u,
                    SERVICE_START = 0x10u,
                    SERVICE_STOP = 0x20u,
                    SERVICE_PAUSE_CONTINUE = 0x40u,
                    SERVICE_INTERROGATE = 0x80u,
                    SERVICE_USER_DEFINED_CONTROL = 0x100u,
                    SERVICE_DELETE = 0x10000u,
                    SERVICE_ALL_ACCESS = 0xF01FFu
                }

                [Flags]
                public enum SCM_ACCESS : uint
                {
                    STANDARD_RIGHTS_REQUIRED = 0xF0000u,
                    SC_MANAGER_CONNECT = 1u,
                    SC_MANAGER_CREATE_SERVICE = 2u,
                    SC_MANAGER_ENUMERATE_SERVICE = 4u,
                    SC_MANAGER_LOCK = 8u,
                    SC_MANAGER_QUERY_LOCK_STATUS = 0x10u,
                    SC_MANAGER_MODIFY_BOOT_CONFIG = 0x20u,
                    SC_MANAGER_ALL_ACCESS = 0xF003Fu
                }

                public const int STANDARD_RIGHTS_REQUIRED = 983040;

                public const int SERVICE_WIN32_OWN_PROCESS = 16;

                [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                public static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName, uint dwDesiredAccess, uint dwServiceType, uint dwStartType, uint dwErrorControl, string lpBinaryPathName, [Optional] string lpLoadOrderGroup, [Optional] string lpdwTagId, [Optional] string lpDependencies, [Optional] string lpServiceStartName, [Optional] string lpPassword);

                [DllImport("advapi32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool DeleteService(IntPtr hService);

                [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                public static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, SERVICE_ACCESS dwDesiredAccess);

                [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "OpenSCManagerW", ExactSpelling = true, SetLastError = true)]
                public static extern IntPtr OpenSCManager(string machineName, string databaseName, SCM_ACCESS dwDesiredAccess);

                [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
                public static extern bool QueryServiceStatusEx(IntPtr Service, int InfoLevel, ref SERVICE_STATUS_PROCESS ServiceStatus, int BufSize, out int BytesNeeded);

                [DllImport("advapi32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool CloseServiceHandle(IntPtr hSCObject);

                [DllImport("Advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
                public static extern bool EnumServicesStatusEx(IntPtr hSCManager, uint InfoLevel, SERVICE_TYPE dwServiceType, uint dwServiceState, IntPtr lpServices, int cbBufSize, out int pcbBytesNeeded, out int lpServicesReturned, ref int lpResumeHandle, string pszGroupName);

                [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                public static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName, ServiceAccessRights dwDesiredAccess, int dwServiceType, ServiceBootFlag dwStartType, ServiceError dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId, string lpDependencies, string lp, string lpPassword);

                [DllImport("advapi32.dll")]
                public static extern int QueryServiceStatus(IntPtr hService, SERVICE_STATUS lpServiceStatus);

                [DllImport("advapi32.dll")]
                public static extern int ControlService(IntPtr hService, ServiceControl dwControl, SERVICE_STATUS lpServiceStatus);

                [DllImport("advapi32.dll", SetLastError = true)]
                public static extern int StartService(IntPtr hService, int dwNumServiceArgs, int lpServiceArgVectors);
            }

            public static class Tokens
            {
                [Flags]
                public enum TokenAccessFlags : uint
                {
                    TOKEN_ADJUST_DEFAULT = 0x80u,
                    TOKEN_ADJUST_GROUPS = 0x40u,
                    TOKEN_ADJUST_PRIVILEGES = 0x20u,
                    TOKEN_ADJUST_SESSIONID = 0x100u,
                    TOKEN_ASSIGN_PRIMARY = 1u,
                    TOKEN_DUPLICATE = 2u,
                    TOKEN_EXECUTE = 0x20000u,
                    TOKEN_IMPERSONATE = 4u,
                    TOKEN_QUERY = 8u,
                    TOKEN_QUERY_SOURCE = 0x10u,
                    TOKEN_READ = 0x20008u,
                    TOKEN_WRITE = 0x200E0u,
                    TOKEN_ALL_ACCESS = 0xF01FFu,
                    MAXIMUM_ALLOWED = 0x2000000u
                }

                public enum TOKEN_TYPE
                {
                    TokenPrimary = 1,
                    TokenImpersonation
                }

                public enum TOKEN_INFORMATION_CLASS
                {
                    TokenUser = 1,
                    TokenGroups,
                    TokenPrivileges,
                    TokenOwner,
                    TokenPrimaryGroup,
                    TokenDefaultDacl,
                    TokenSource,
                    TokenType,
                    TokenImpersonationLevel,
                    TokenStatistics,
                    TokenRestrictedSids,
                    TokenSessionId,
                    TokenGroupsAndPrivileges,
                    TokenSessionReference,
                    TokenSandBoxInert,
                    TokenAuditPolicy,
                    TokenOrigin,
                    TokenElevationType,
                    TokenLinkedToken,
                    TokenElevation,
                    TokenHasRestrictions,
                    TokenAccessInformation,
                    TokenVirtualizationAllowed,
                    TokenVirtualizationEnabled,
                    TokenIntegrityLevel,
                    TokenUIAccess,
                    TokenMandatoryPolicy,
                    TokenLogonSid,
                    MaxTokenInfoClass
                }

                public enum SECURITY_IMPERSONATION_LEVEL
                {
                    SecurityAnonymous,
                    SecurityIdentification,
                    SecurityImpersonation,
                    SecurityDelegation
                }

                public struct LUID
                {
                    public uint LowPart;

                    public uint HighPart;
                }

                [DllImport("advapi32.dll", SetLastError = true)]
                public static extern bool OpenProcessToken(SafeProcessHandle hProcess, TokenAccessFlags DesiredAccess, out TokensEx.SafeTokenHandle hToken);

                [DllImport("advapi32.dll", SetLastError = true)]
                public static extern bool GetTokenInformation(TokensEx.SafeTokenHandle TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, int TokenInformationLength, out int ReturnLength);

                [DllImport("advapi32.dll", SetLastError = true)]
                public static extern bool GetTokenInformation(TokensEx.SafeTokenHandle TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, out int TokenInformation, int TokenInformationLength, out int ReturnLength);

                [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool SetTokenInformation(TokensEx.SafeTokenHandle hToken, TOKEN_INFORMATION_CLASS tokenInfoClass, IntPtr pTokenInfo, int tokenInfoLength);

                [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool SetTokenInformation(TokensEx.SafeTokenHandle hToken, TOKEN_INFORMATION_CLASS tokenInfoClass, ref int pTokenInfo, int tokenInfoLength);

                [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                public static extern bool SetTokenInformation(TokensEx.SafeTokenHandle hToken, TOKEN_INFORMATION_CLASS tokenInfoClass, ref uint pTokenInfo, int tokenInfoLength);

                [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                public static extern bool DuplicateTokenEx(TokensEx.SafeTokenHandle hExistingToken, TokenAccessFlags dwDesiredAccess, IntPtr lpTokenAttributes, SECURITY_IMPERSONATION_LEVEL ImpersonationLevel, TOKEN_TYPE TokenType, out TokensEx.SafeTokenHandle phNewToken);

                [DllImport("advapi32.dll", SetLastError = true)]
                public static extern bool ImpersonateLoggedOnUser(TokensEx.SafeTokenHandle hToken);

                [DllImport("advapi32.dll", SetLastError = true)]
                public static extern bool LookupPrivilegeValue(IntPtr lpSystemName, string lpName, out LUID lpLuid);

                [DllImport("ntdll.dll", SetLastError = true)]
                public static extern IntPtr RtlAdjustPrivilege(LUID privilege, bool bEnablePrivilege, bool isThreadPrivilege, out bool previousValue);
            }

            public static class TokensEx
            {
                public sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
                {
                    [DllImport("kernel32.dll", SetLastError = true)]
                    [return: MarshalAs(UnmanagedType.Bool)]
                    private static extern bool CloseHandle(IntPtr hObject);

                    public SafeTokenHandle(IntPtr preexistingHandle)
                        : base(ownsHandle: true)
                    {
                        SetHandle(preexistingHandle);
                    }

                    public SafeTokenHandle()
                        : base(ownsHandle: true)
                    {
                    }

                    protected override bool ReleaseHandle()
                    {
                        return CloseHandle(handle);
                    }
                }

                public static void AdjustCurrentPrivilege(string privilege)
                {
                    Tokens.LookupPrivilegeValue(IntPtr.Zero, privilege, out var luid);
                    Tokens.RtlAdjustPrivilege(luid, bEnablePrivilege: true, isThreadPrivilege: true, out var _);
                }
            }

            public static class WTS
            {
                public enum WTS_INFO_CLASS
                {
                    WTSInitialProgram,
                    WTSApplicationName,
                    WTSWorkingDirectory,
                    WTSOEMId,
                    WTSSessionId,
                    WTSUserName,
                    WTSWinStationName,
                    WTSDomainName,
                    WTSConnectState,
                    WTSClientBuildNumber,
                    WTSClientName,
                    WTSClientDirectory,
                    WTSClientProductId,
                    WTSClientHardwareId,
                    WTSClientAddress,
                    WTSClientDisplay,
                    WTSClientProtocolType,
                    WTSIdleTime,
                    WTSLogonTime,
                    WTSIncomingBytes,
                    WTSOutgoingBytes,
                    WTSIncomingFrames,
                    WTSOutgoingFrames,
                    WTSClientInfo,
                    WTSSessionInfo
                }

                public struct WTS_CLIENT_ADDRESS
                {
                    public uint AddressFamily;

                    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
                    public byte[] Address;
                }

                public struct WTS_SESSION_INFO
                {
                    public int SessionID;

                    [MarshalAs(UnmanagedType.LPStr)]
                    public string pWinStationName;

                    public WTS_CONNECTSTATE_CLASS State;
                }

                public enum WTS_CONNECTSTATE_CLASS
                {
                    WTSActive,
                    WTSConnected,
                    WTSConnectQuery,
                    WTSShadow,
                    WTSDisconnected,
                    WTSIdle,
                    WTSListen,
                    WTSReset,
                    WTSDown,
                    WTSInit
                }

                public struct WTS_PROCESS_INFO
                {
                    public int SessionID;

                    public int ProcessID;

                    public IntPtr ProcessName;

                    public IntPtr UserSid;
                }

                [DllImport("wtsapi32.dll", SetLastError = true)]
                public static extern int WTSEnumerateSessions(IntPtr hServer, int Reserved, int Version, ref IntPtr ppSessionInfo, ref int pCount);

                [DllImport("wtsapi32.dll", SetLastError = true)]
                public static extern bool WTSEnumerateProcesses(IntPtr serverHandle, int reserved, int version, ref IntPtr ppProcessInfo, ref int pCount);

                [DllImport("kernel32.dll")]
                public static extern uint WTSGetActiveConsoleSessionId();

                [DllImport("wtsapi32.dll", SetLastError = true)]
                public static extern bool WTSQueryUserToken(uint sessionId, out TokensEx.SafeTokenHandle Token);

                [DllImport("wtsapi32.dll")]
                public static extern bool WTSQuerySessionInformation(IntPtr hServer, uint sessionId, WTS_INFO_CLASS wtsInfoClass, out IntPtr ppBuffer, out int pBytesReturned);

                [DllImport("wtsapi32.dll")]
                public static extern void WTSFreeMemory(IntPtr pMemory);
            }
        }

        private const int CREATE_NEW_CONSOLE = 16;

        private const int NORMAL_PRIORITY_CLASS = 32;

        public const int CREATE_UNICODE_ENVIRONMENT = 1024;

        public const string SE_ASSIGNPRIMARYTOKEN_NAME = "SeAssignPrimaryTokenPrivilege";

        public const string SE_INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";

        public const string SE_DEBUG_NAME = "SeDebugPrivilege";

        public const string SE_IMPERSONATE_NAME = "SeImpersonatePrivilege";

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool CreateProcess(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, [In] ref STARTUPINFO lpSTARTUPINFO, out ProcessInformation lpProcessInformation);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CreateProcessAsUser(IntPtr hToken, string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, int dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpSTARTUPINFO, out ProcessInformation lpProcessInformation);

        [DllImport("userenv.dll", SetLastError = true)]
        public static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        public static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeProcessHandle GetCurrentProcess();

        protected override void OnStart(string[] args)
        {
            Task.Run(delegate
            {
                ServiceMain();
            });
        }

        private static void ServiceMain()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            Win32.Tokens.OpenProcessToken(GetCurrentProcess(), Win32.Tokens.TokenAccessFlags.MAXIMUM_ALLOWED, out var rawToken);
            Win32.Tokens.DuplicateTokenEx(rawToken, Win32.Tokens.TokenAccessFlags.MAXIMUM_ALLOWED, IntPtr.Zero, Win32.Tokens.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, Win32.Tokens.TOKEN_TYPE.TokenPrimary, out var duplicatedToken);
            rawToken.Dispose();
            uint sessionId = 1u;
            Win32.Tokens.SetTokenInformation(duplicatedToken, Win32.Tokens.TOKEN_INFORMATION_CLASS.TokenSessionId, ref sessionId, 4);
            STARTUPINFO STARTUPINFO = default(STARTUPINFO);
            STARTUPINFO.cb = Marshal.SizeOf(STARTUPINFO);
            STARTUPINFO.lpDesktop = "winsta0\\winlogon";
            STARTUPINFO.dwFlags = 1;
            STARTUPINFO.wShowWindow = 5;
            int dwFlags = 16;
            if (CreateEnvironmentBlock(out var environment, duplicatedToken.DangerousGetHandle(), bInherit: false))
            {
                dwFlags |= 0x400;
            }
            CreateProcessAsUser(duplicatedToken.DangerousGetHandle(), null, "\"" + Assembly.GetExecutingAssembly().Location + "\" --apply-package " + arguments[2], IntPtr.Zero, IntPtr.Zero, bInheritHandles: true, dwFlags, environment, null, ref STARTUPINFO, out var _);
            duplicatedToken.Dispose();
            DestroyEnvironmentBlock(environment);
            try
            {
                RunCommand("bcdedit.exe", "/deletevalue {current} safeboot", null, null);
            }
            catch (Exception)
            {
            }
            UninstallService();
            Environment.Exit(0);
        }

        protected override void OnStop()
        {
            Environment.Exit(1);
        }

        private static int RunCommand(string exe, string arguments, DataReceivedEventHandler outputHandler, DataReceivedEventHandler errorHandler)
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = (outputHandler != null),
                    RedirectStandardError = (errorHandler != null)
                }
            };
            if (outputHandler != null)
            {
                process.OutputDataReceived += outputHandler;
            }
            if (errorHandler != null)
            {
                process.ErrorDataReceived += errorHandler;
            }
            process.Start();
            if (outputHandler != null)
            {
                process.BeginOutputReadLine();
            }
            if (errorHandler != null)
            {
                process.BeginErrorReadLine();
            }
            process.WaitForExit();
            return process.ExitCode;
        }

        public static void UninstallService()
        {
            IntPtr scm = Win32.Service.OpenSCManager(null, null, Win32.Service.SCM_ACCESS.SC_MANAGER_CREATE_SERVICE);
            if (scm == IntPtr.Zero)
            {
                throw new ApplicationException("Could not connect to service control manager.");
            }
            try
            {
                IntPtr service = Win32.Service.OpenService(scm, "KTWirzadePrepare", Win32.Service.SERVICE_ACCESS.SERVICE_DELETE);
                if (!(service == IntPtr.Zero))
                {
                    Win32.Service.DeleteService(service);
                    Win32.Service.CloseServiceHandle(service);
                }
            }
            finally
            {
                Win32.Service.CloseServiceHandle(scm);
            }
        }
    }

}
