using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace iso_mode
{
    internal static class Helper
    {
        [DllImport("client-helper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.BStr)]
        internal static extern string FormatVolume(string letter, string formatType, uint allocationSize, string volumeLabel);

        [DllImport("client-helper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.BStr)]
        internal static extern string DeletePartitions(uint driveIndex);

        /// <summary>
        /// Only supports REG_SZ, REG_MULTI_SZ (I think), DWORD, and QWORD value types.
        /// </summary>
        [DllImport("client-helper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.BStr)]
        internal static extern string GetValue(IntPtr data, string key, string valueName);

        /// <summary>
        /// Gets all REG_SZ, REG_MULTI_SZ (I think), DWORD, and QWORD value types.
        /// The values are delimited by '\n', and the value name is separated from
        /// the value with the following 3 wide string: ":|:"
        /// </summary>
        [DllImport("client-helper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.BStr)]
        internal static extern string GetValues(IntPtr data, string key);

        internal static ulong GetDWordValue(IntPtr data, string key, string valueName) =>
            uint.Parse(GetValue(data, key, valueName), NumberStyles.HexNumber);

        internal static ulong GetQWordValue(IntPtr data, string key, string valueName) =>
            ulong.Parse(GetValue(data, key, valueName), NumberStyles.HexNumber);
    }

    internal static class Win32
    {
        // Win32 Functions

        [Flags]
        internal enum CM_GETIDLIST_FILTER : uint
        {
            ENUMERATOR = 0x00000001,
            SERVICE = 0x00000002,
            EJECTRELATIONS = 0x00000004,
            REMOVALRELATIONS = 0x00000008,
            POWERRELATIONS = 0x00000010,
            BUSRELATIONS = 0x00000020,
            NONE = 0x00000000,
            DONOTGENERATE = 0x10000040,
            TRANSPORTRELATIONS = 0x00000080,
            PRESENT = 0x00000100,
            CLASS = 0x00000200,
            BITS = 0x100003FF,
        }

        internal enum PNP_VETO_TYPE
        {
            Ok,

            TypeUnknown,
            LegacyDevice,
            PendingClose,
            WindowsApp,
            WindowsService,
            OutstandingOpen,
            Device,
            Driver,
            IllegalDeviceRequest,
            InsufficientPower,
            NonDisableable,
            LegacyDriver,
        }
        [DllImport("setupapi.dll")]
        internal static extern int CM_Request_Device_Eject(
            uint dnDevInst,
            out PNP_VETO_TYPE pVetoType,
            StringBuilder pszVetoName,
            int ulNameLength,
            int ulFlags
        );
        
        [DllImport("CfgMgr32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int CM_Get_Device_ID_List_Size(ref int length, string filter, CM_GETIDLIST_FILTER flags);

        [DllImport("CfgMgr32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int CM_Get_Device_ID_List(string filter, byte[] buffer, int bufferLength, CM_GETIDLIST_FILTER flags);

        [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
        internal static extern int CM_Get_Parent(
            out int pdnDevInst,
            int dnDevInst,
            int ulFlags);

        [DllImport("kernel32.dll")]
        internal static extern bool DefineDosDevice(uint dwFlags, string lpDeviceName,
            string lpTargetPath);

        [DllImport("Kernel32.dll")]
        internal static extern uint QueryDosDevice(string lpDeviceName,
            string lpTargetPath, uint ucchMax);

        internal const uint DDD_RAW_TARGET_PATH = 0x00000001;
        internal const uint DDD_REMOVE_DEFINITION = 0x00000002;
        internal const uint DDD_EXACT_MATCH_ON_REMOVE = 0x00000004;
        internal const uint DDD_NO_BROADCAST_SYSTEM = 0x00000008;

        [DllImport("Kernel32.dll", EntryPoint = "RtlZeroMemory", SetLastError = false)]
        internal static extern void ZeroMemory(IntPtr dest, uint size);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid,
            [MarshalAs(UnmanagedType.LPTStr)] string? Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SetupDiGetClassDevs(IntPtr ClassGuid,
            [MarshalAs(UnmanagedType.LPTStr)] string? Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Boolean SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, ref SP_DEVINFO_DATA devInfo,
            ref Guid interfaceClassGuid, UInt32 memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiBuildDriverInfoList(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            SPDIT driverType);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiDestroyDriverInfoList(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            SPDIT driverType);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiBuildDriverInfoList(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            SPDIT driverType);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiDestroyDriverInfoList(
            IntPtr deviceInfoSet,
            ref IntPtr deviceInfoData,
            SPDIT driverType);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr ClassGuid, IntPtr hwndParent);

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Boolean SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData, UInt32 deviceInterfaceDetailDataSize,
            ref UInt32 requiredSize, IntPtr deviceInfoData);

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Boolean SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData,
            UInt32 deviceInterfaceDetailDataSize, ref UInt32 requiredSize, IntPtr deviceInfoData);

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Boolean SetupDiEnumDeviceInfo(IntPtr hDevInfo, UInt32 memberIndex,
            ref SP_DEVINFO_DATA devInfo);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiEnumDriverInfo(
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            SPDIT DriverType,
            int MemberIndex,
            ref SP_DRVINFO_DATA DriverInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiEnumDriverInfo(
            IntPtr DeviceInfoSet,
            IntPtr DeviceInfoData,
            SPDIT DriverType,
            int MemberIndex,
            ref SP_DRVINFO_DATA DriverInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiGetDriverInfoDetail(
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            ref SP_DRVINFO_DATA DriverInfoData,
            ref SP_DRVINFO_DETAIL_DATA DriverInfoDetailData,
            Int32 DriverInfoDetailDataSize,
            out Int32 RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiGetDriverInfoDetail(
            IntPtr DeviceInfoSet,
            IntPtr DeviceInfoData,
            ref SP_DRVINFO_DATA DriverInfoData,
            ref SP_DRVINFO_DETAIL_DATA DriverInfoDetailData,
            Int32 DriverInfoDetailDataSize,
            out Int32 RequiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern Boolean SetupDiGetDeviceRegistryProperty(IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData, UInt32 property, out UInt32 propertyRegDataType, IntPtr propertyBuffer,
            UInt32 propertyBufferSize, out UInt32 requiredSize);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiGetDevicePropertyW(IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData, ref DEVPROPKEY PropertyKey, out uint PropertyType, IntPtr PropertyBuffer,
            uint PropertyBufferSize, out uint RequiredSize, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool SetupDiGetDeviceInstanceId(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData,
            StringBuilder DeviceInstanceId, uint DeviceInstanceIdSize, out uint RequiredSize);

        [DllImport("setupapi.dll", SetLastError = false)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiGetDeviceInstallParams(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref SP_DEVINSTALL_PARAMS DeviceInstallParams);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiSetDeviceInstallParams(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref SP_DEVINSTALL_PARAMS DeviceInstallParams);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupGetInfDriverStoreLocation(
            [MarshalAs(UnmanagedType.LPTStr)] string FileName,
            Int32 AlternatePlatformInfo,
            Int32 LocalName,
            StringBuilder ReturnBuffer,
            Int32 ReturnBufferSize,
            out Int32 RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupCopyOEMInf(
            string SourceInfFileName,
            string OEMSourceMediaLocation,
            SPOST OEMSourceMediaType,
            SP_COPY CopyStyle,
            IntPtr DestinationInfFileName,
            int DestinationInfFileNameSize,
            ref int RequiredSize,
            IntPtr DestinationInfFileNameComponent
        );

        [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
        public static extern int CM_Get_DevNode_Property_Keys(UInt32 dnDevInst, [Out] IntPtr propertyKeyArray,
            ref UInt32 propertyKeyCount, UInt32 flags);

        [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
        public static extern int CM_Get_DevNode_Property(UInt32 dnDevInst, ref DEVPROPKEY propertyKey,
            out UInt32 propertyType, IntPtr propertyBuffer, ref UInt32 propertyBufferSize, UInt32 flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern int CM_Get_Device_ID_Size(out uint pulLen, UInt32 dnDevInst, int flags = 0);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern int CM_Get_Device_ID(uint dnDevInst, StringBuilder Buffer, int BufferLen,
            int ulFlags = 0);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern int CM_Get_Child(out uint pdnDevInst, UInt32 dnDevInst, int ulFlags = 0);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern int CM_Get_Sibling(out uint pdnDevInst, UInt32 dnDevInst, int ulFlags = 0);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern int CM_Locate_DevNodeA(ref uint pdnDevInst, string pDeviceID, int ulFlags = 0);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode,
            IntPtr lpInBuffer, uint nInBufferSize,
            IntPtr lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);


        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
            ref DRIVE_LAYOUT_INFORMATION_EX lpInBuffer, uint nInBufferSize,
            IntPtr lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(
            [MarshalAs(UnmanagedType.LPTStr)] string filename,
            [MarshalAs(UnmanagedType.U4)] FileAccess access,
            [MarshalAs(UnmanagedType.U4)] FileShare share,
            IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll")]
        public static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer,
            uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten,
            [In] ref System.Threading.NativeOverlapped lpOverlapped);

        [DllImport("kernel32.dll")]
        public static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer,
            uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten,
            [In] IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FlushFileBuffers(SafeFileHandle hFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);
        
        [DllImport("kernel32.dll", EntryPoint = "RtlFillMemory", SetLastError = false)]
        public static extern void FillMemory(IntPtr destination, uint length, byte fill);

        [DllImport("newdev.dll", SetLastError = true)]
        public static extern bool UpdateDriverForPlugAndPlayDevices(
            IntPtr hWndParent,
            string hardwareId,
            string fullInfPath,
            INSTALLFLAG installFlags,
            out bool rebootRequired
        );

        [DllImport("newdev.dll", SetLastError = true)]
        public static extern bool DiInstallDriver(
            IntPtr hwndParent,
            string FullInfPath,
            DIIRFLAG Flags, // either ZERO or FORCE_INF
            out bool NeedReboot
        );

        // Win32 Items

        public enum INSTALLFLAG
        {
            FORCE = 0x00000001,
            READONLY = 0x00000002,
            NONINTERACTIVE = 0x00000004,
            BITS = 0x00000007
        }

        [Flags]
        public enum DIIRFLAG
        {
            ZERO = 0x00000000,
            FORCE_INF = 0x00000002
        }

        public enum SPOST : uint
        {
            NONE = 0,
            PATH = 1,
            URL = 2,
            MAX = 3
        }

        [Flags]
        public enum SP_COPY : uint
        {
            DEFAULT = 0x0000000, // just to privide a 0 value
            DELETESOURCE = 0x0000001, // delete source file on successful copy
            REPLACEONLY = 0x0000002, // copy only if target file already present
            NEWER = 0x0000004, // copy only if source newer than or same as target
            NEWER_OR_SAME = NEWER,
            NOOVERWRITE = 0x0000008, // copy only if target doesn't exist
            NODECOMP = 0x0000010, // don't decompress source file while copying
            LANGUAGEAWARE = 0x0000020, // don't overwrite file of different language
            SOURCE_ABSOLUTE = 0x0000040, // SourceFile is a full source path
            SOURCEPATH_ABSOLUTE = 0x0000080, // SourcePathRoot is the full path
            IN_USE_NEEDS_REBOOT = 0x0000100, // System needs reboot if file in use
            FORCE_IN_USE = 0x0000200, // Force target-in-use behavior
            NOSKIP = 0x0000400, // Skip is disallowed for this file or section
            CABINETCONTINUATION = 0x0000800, // Used with need media notification
            FORCE_NOOVERWRITE = 0x0001000, // like NOOVERWRITE but no callback nofitication
            FORCE_NEWER = 0x0002000, // like NEWER but no callback nofitication
            WARNIFSKIP = 0x0004000, // system critical file: warn if user tries to skip
            NOBROWSE = 0x0008000, // Browsing is disallowed for this file or section
            NEWER_ONLY = 0x0010000 // copy only if source file newer than target
        }

        public const string GUID_DEVINTERFACE_USB_HUB = "{F18A0E88-C30C-11D0-8815-00A0C906BED8}";
        public const string GUID_DEVINTERFACE_DISK = "{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}";

        // Common Return Codes
        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        internal static readonly int ERROR_INSUFFICIENT_BUFFER = 122;
        public const int CR_SUCCESS = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVPROPKEY
        {
            public Guid Fmtid;
            public UInt32 Pid;
        }

        internal static class DevicePropertyTypes
        {
            internal const UInt32 DEVPROP_TYPEMOD_ARRAY = 0x00001000;
            internal const UInt32 DEVPROP_TYPEMOD_LIST = 0x00002000;
            internal const UInt32 DEVPROP_TYPE_EMPTY = 0x00000000;
            internal const UInt32 DEVPROP_TYPE_NULL = 0x00000001;
            internal const UInt32 DEVPROP_TYPE_SBYTE = 0x00000002;
            internal const UInt32 DEVPROP_TYPE_BYTE = 0x00000003;
            internal const UInt32 DEVPROP_TYPE_INT16 = 0x00000004;
            internal const UInt32 DEVPROP_TYPE_UINT16 = 0x00000005;
            internal const UInt32 DEVPROP_TYPE_INT32 = 0x00000006;
            internal const UInt32 DEVPROP_TYPE_UINT32 = 0x00000007;
            internal const UInt32 DEVPROP_TYPE_INT64 = 0x00000008;
            internal const UInt32 DEVPROP_TYPE_UINT64 = 0x00000009;
            internal const UInt32 DEVPROP_TYPE_FLOAT = 0x0000000A;
            internal const UInt32 DEVPROP_TYPE_DOUBLE = 0x0000000B;
            internal const UInt32 DEVPROP_TYPE_DECIMAL = 0x0000000C;
            internal const UInt32 DEVPROP_TYPE_GUID = 0x0000000D;
            internal const UInt32 DEVPROP_TYPE_CURRENCY = 0x0000000E;
            internal const UInt32 DEVPROP_TYPE_DATE = 0x0000000F;
            internal const UInt32 DEVPROP_TYPE_FILETIME = 0x00000010;
            internal const UInt32 DEVPROP_TYPE_BOOLEAN = 0x00000011;
            internal const UInt32 DEVPROP_TYPE_STRING = 0x00000012;
            internal const UInt32 DEVPROP_TYPE_STRING_LIST = DEVPROP_TYPE_STRING | DEVPROP_TYPEMOD_LIST;
            internal const UInt32 DEVPROP_TYPE_SECURITY_DESCRIPTOR = 0x00000013;
            internal const UInt32 DEVPROP_TYPE_SECURITY_DESCRIPTOR_STRING = 0x00000014;
            internal const UInt32 DEVPROP_TYPE_DEVPROPKEY = 0x00000015;
            internal const UInt32 DEVPROP_TYPE_DEVPROPTYPE = 0x00000016;
            internal const UInt32 DEVPROP_TYPE_BINARY = DEVPROP_TYPE_BYTE | DEVPROP_TYPEMOD_ARRAY;
            internal const UInt32 DEVPROP_TYPE_ERROR = 0x00000017;
            internal const UInt32 DEVPROP_TYPE_NTSTATUS = 0x00000018;
            internal const UInt32 DEVPROP_TYPE_STRING_INDIRECT = 0x00000019;
            internal const UInt32 MAX_DEVPROP_TYPE = 0x00000019;
            internal const UInt32 MAX_DEVPROP_TYPEMOD = 0x00002000;
            internal const UInt32 DEVPROP_MASK_TYPE = 0x00000FFF;
            internal const UInt32 DEVPROP_MASK_TYPEMOD = 0x0000F000;
        }

        internal static class DevicePropertyKeys
        {
            internal static readonly DEVPROPKEY DEVPKEY_NAME = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xb725f130, 0x47ef, 0x101a, 0xa5, 0xf1, 0x02, 0x60, 0x8c, 0x9e, 0xeb, 0xac), Pid = 10
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DeviceDesc = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_HardwareIds = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 3
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_CompatibleIds = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 4
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Service = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 6
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Class = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 9
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_ClassGuid = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 10
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Driver = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 11
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_ConfigFlags = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 12
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Manufacturer = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 13
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_FriendlyName = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 14
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_LocationInfo = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 15
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_PDOName = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 16
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Capabilities = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 17
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_UINumber = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 18
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_UpperFilters = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 19
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_LowerFilters = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 20
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_BusTypeGuid = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 21
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_LegacyBusType = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 22
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_BusNumber = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 23
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_EnumeratorName = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 24
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Security = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 25
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_SecuritySDS = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 26
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DevType = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 27
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Exclusive = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 28
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Characteristics = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 29
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Address = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 30
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_UINumberDescFormat = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 31
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_PowerData = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 32
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_RemovalPolicy = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 33
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_RemovalPolicyDefault = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 34
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_RemovalPolicyOverride = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 35
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_InstallState = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 36
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_LocationPaths = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 37
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_BaseContainerId = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), Pid = 38
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DevNodeStatus = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_ProblemCode = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), Pid = 3
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_EjectionRelations = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), Pid = 4
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_RemovalRelations = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), Pid = 5
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_PowerRelations = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), Pid = 6
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_BusRelations = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), Pid = 7
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Parent = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), Pid = 8
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Children = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), Pid = 9
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Siblings = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), Pid = 10
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_TransportRelations = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), Pid = 11
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_ProblemStatus = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), Pid = 12
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Reported = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x80497100, 0x8c73, 0x48b9, 0xaa, 0xd9, 0xce, 0x38, 0x7e, 0x19, 0xc5, 0x6e), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Legacy = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x80497100, 0x8c73, 0x48b9, 0xaa, 0xd9, 0xce, 0x38, 0x7e, 0x19, 0xc5, 0x6e), Pid = 3
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_ContainerId = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x8c7ed206, 0x3f8a, 0x4827, 0xb3, 0xab, 0xae, 0x9e, 0x1f, 0xae, 0xfc, 0x6c), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_InLocalMachineContainer = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x8c7ed206, 0x3f8a, 0x4827, 0xb3, 0xab, 0xae, 0x9e, 0x1f, 0xae, 0xfc, 0x6c), Pid = 4
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_ModelId = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_FriendlyNameAttributes = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b), Pid = 3
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_ManufacturerAttributes = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b), Pid = 4
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_PresenceNotForDevice = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b), Pid = 5
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_SignalStrength = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b), Pid = 6
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_IsAssociateableByUserAction = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b), Pid = 7
            };

            internal static readonly DEVPROPKEY DEVPKEY_Numa_Proximity_Domain = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 1
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DHP_Rebalance_Policy = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Numa_Node = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 3
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 4
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_IsPresent = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 5
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_HasProblem = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 6
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_ConfigurationId = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 7
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_ReportedDeviceIdsHash = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 8
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_PhysicalDeviceLocation = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 9
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_BiosDeviceName = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 10
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DriverProblemDesc = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 11
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DebuggerSafe = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 12
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_PostInstallInProgress = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), Pid = 13
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_SessionId = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29), Pid = 6
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_InstallDate = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29), Pid = 100
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_FirstInstallDate = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29), Pid = 101
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_LastArrivalDate = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29), Pid = 102
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_LastRemovalDate = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29), Pid = 103
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DriverDate = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DriverVersion = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 3
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DriverDesc = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 4
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DriverInfPath = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 5
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DriverInfSection = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 6
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DriverInfSectionExt = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 7
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_MatchingDeviceId = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 8
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DriverProvider = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 9
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DriverPropPageProvider = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 10
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DriverCoInstallers = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 11
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_RepropertyBufferPickerTags = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 12
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_RepropertyBufferPickerExceptions = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 13
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DriverRank = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 14
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_DriverLogoLevel = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 15
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_NoConnectSound = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 17
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_GenericDriverInstalled = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 18
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_AdditionalSoftwareRequested = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6), Pid = 19
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_SafeRemovalRequired = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xafd97640, 0x86a3, 0x4210, 0xb6, 0x7c, 0x28, 0x9c, 0x41, 0xaa, 0xbe, 0x55), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_SafeRemovalRequiredOverride = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xafd97640, 0x86a3, 0x4210, 0xb6, 0x7c, 0x28, 0x9c, 0x41, 0xaa, 0xbe, 0x55), Pid = 3
            };

            internal static readonly DEVPROPKEY DEVPKEY_DrvPkg_Model = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xcf73bb51, 0x3abf, 0x44a2, 0x85, 0xe0, 0x9a, 0x3d, 0xc7, 0xa1, 0x21, 0x32), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_DrvPkg_VendorWebSite = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xcf73bb51, 0x3abf, 0x44a2, 0x85, 0xe0, 0x9a, 0x3d, 0xc7, 0xa1, 0x21, 0x32), Pid = 3
            };

            internal static readonly DEVPROPKEY DEVPKEY_DrvPkg_DetailedDescription = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xcf73bb51, 0x3abf, 0x44a2, 0x85, 0xe0, 0x9a, 0x3d, 0xc7, 0xa1, 0x21, 0x32), Pid = 4
            };

            internal static readonly DEVPROPKEY DEVPKEY_DrvPkg_DocumentationLink = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xcf73bb51, 0x3abf, 0x44a2, 0x85, 0xe0, 0x9a, 0x3d, 0xc7, 0xa1, 0x21, 0x32), Pid = 5
            };

            internal static readonly DEVPROPKEY DEVPKEY_DrvPkg_Icon = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xcf73bb51, 0x3abf, 0x44a2, 0x85, 0xe0, 0x9a, 0x3d, 0xc7, 0xa1, 0x21, 0x32), Pid = 6
            };

            internal static readonly DEVPROPKEY DEVPKEY_DrvPkg_BrandingIcon = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xcf73bb51, 0x3abf, 0x44a2, 0x85, 0xe0, 0x9a, 0x3d, 0xc7, 0xa1, 0x21, 0x32), Pid = 7
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_UpperFilters = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b), Pid = 19
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_LowerFilters = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b), Pid = 20
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_Security = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b), Pid = 25
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_SecuritySDS = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b), Pid = 26
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_DevType = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b), Pid = 27
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_Exclusive = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b), Pid = 28
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_Characteristics = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b), Pid = 29
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_Name = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_ClassName = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66), Pid = 3
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_Icon = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66), Pid = 4
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_ClassInstaller = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66), Pid = 5
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_PropPageProvider = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66), Pid = 6
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_NoInstallClass = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66), Pid = 7
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_NoDisplayClass = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66), Pid = 8
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_SilentInstall = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66), Pid = 9
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_NoUseClass = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66), Pid = 10
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_DefaultService = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66), Pid = 11
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_IconPath = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66), Pid = 12
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_DHPRebalanceOptOut = new DEVPROPKEY()
            {
                Fmtid = new Guid(0xd14d3ef3, 0x66cf, 0x4ba2, 0x9d, 0x38, 0x0d, 0xdb, 0x37, 0xab, 0x47, 0x01), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceClass_ClassCoInstallers = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x713d1703, 0xa2e2, 0x49f5, 0x92, 0x14, 0x56, 0x47, 0x2e, 0xf3, 0xda, 0x5c), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceInterface_FriendlyName = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceInterface_Enabled = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22), Pid = 3
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceInterface_ClassGuid = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22), Pid = 4
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceInterface_ReferenceString = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22), Pid = 5
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceInterface_Restricted = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22), Pid = 6
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceInterfaceClass_DefaultInterface = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x14c83a99, 0x0b3f, 0x44b7, 0xbe, 0x4c, 0xa1, 0x78, 0xd3, 0x99, 0x05, 0x64), Pid = 2
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceInterfaceClass_Name = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x14c83a99, 0x0b3f, 0x44b7, 0xbe, 0x4c, 0xa1, 0x78, 0xd3, 0x99, 0x05, 0x64), Pid = 3
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_Model = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 39
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_Address = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 51
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_DiscoveryMethod = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 52
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsEncrypted = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 53
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsAuthenticated = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 54
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsConnected = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 55
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsPaired = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 56
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_Icon = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 57
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_Version = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 65
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_Last_Seen = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 66
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_Last_Connected = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 67
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsShowInDisconnectedState = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 68
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsLocalMachine = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 70
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_MetadataPath = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 71
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsMetadataSearchInProgress = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 72
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_MetadataChecksum = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 73
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsNotInterestingForDisplay = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 74
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_LaunchDeviceStageOnDeviceConnect =
                new DEVPROPKEY()
                {
                    Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57),
                    Pid = 76
                };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_LaunchDeviceStageFromExplorer = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 77
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_BaselineExperienceId = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 78
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsDeviceUniquelyIdentifiable = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 79
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_AssociationArray = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 80
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_DeviceDescription1 = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 81
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_DeviceDescription2 = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 82
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_HasProblem = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 83
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsSharedDevice = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 84
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsNetworkDevice = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 85
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsDefaultDevice = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 86
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_MetadataCabinet = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 87
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_RequiresPairingElevation = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 88
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_ExperienceId = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 89
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_Category = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 90
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_Category_Desc_Singular = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 91
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_Category_Desc_Plural = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 92
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_Category_Icon = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 93
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_CategoryGroup_Desc = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 94
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_CategoryGroup_Icon = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 95
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_PrimaryCategory = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 97
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_UnpairUninstall = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 98
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_RequiresUninstallElevation = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 99
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_DeviceFunctionSubRank = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 100
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_AlwaysShowDeviceAsConnected = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 101
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_ConfigFlags = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 105
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_PrivilegedPackageFamilyNames = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 106
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_CustomPrivilegedPackageFamilyNames =
                new DEVPROPKEY()
                {
                    Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57),
                    Pid = 107
                };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_IsRebootRequired = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 108
            };

            internal static readonly DEVPROPKEY DEVPKEY_Device_InstanceId = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57), Pid = 256
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_FriendlyName = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x656A3BB3, 0xECC0, 0x43FD, 0x84, 0x77, 0x4A, 0xE0, 0x40, 0x4A, 0x96, 0xCD),
                Pid = 12288
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_Manufacturer = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x656A3BB3, 0xECC0, 0x43FD, 0x84, 0x77, 0x4A, 0xE0, 0x40, 0x4A, 0x96, 0xCD), Pid = 8192
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_ModelName = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x656A3BB3, 0xECC0, 0x43FD, 0x84, 0x77, 0x4A, 0xE0, 0x40, 0x4A, 0x96, 0xCD), Pid = 8194
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_ModelNumber = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x656A3BB3, 0xECC0, 0x43FD, 0x84, 0x77, 0x4A, 0xE0, 0x40, 0x4A, 0x96, 0xCD), Pid = 8195
            };

            internal static readonly DEVPROPKEY DEVPKEY_DeviceContainer_InstallInProgress = new DEVPROPKEY()
            {
                Fmtid = new Guid(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29), Pid = 9
            };
        }

        [Flags]
        public enum DiGetClassFlags : uint
        {
            DIGCF_DEFAULT = 0x00000001, // only valid with DIGCF_DEVICEINTERFACE
            DIGCF_PRESENT = 0x00000002,
            DIGCF_ALLCLASSES = 0x00000004,
            DIGCF_PROFILE = 0x00000008,
            DIGCF_DEVICEINTERFACE = 0x00000010,
        }

        [Flags]
        public enum CmFlags : uint
        {
            CM_GETIDLIST_FILTER_SERVICE = 0x00000002,
            CM_GETIDLIST_FILTER_PRESENT = 0x00000100,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public Int32 cbSize;
            public Guid interfaceClassGuid;
            public Int32 flags;
            public UIntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public UInt32 cbSize;
            public Guid ClassGuid;
            public UInt32 DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public int cbSize;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DevicePath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SP_DRVINFO_DATA
        {
            public int cbSize;
            public int DriverType;
            private IntPtr Reserved;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Description;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string MfgName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string ProviderName;
            public FILETIME DriverDate;
            public long DriverVersion;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SP_DRVINFO_DETAIL_DATA
        {
            public int cbSize;
            public FILETIME InfDate;
            public int CompatIDsOffset;
            public int CompatIDsLength;
            public IntPtr Reserved;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string SectionName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string InfFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DrvDescription;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1)]
            public string HardwareID;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SP_DEVINSTALL_PARAMS
        {
            public int cbSize;
            public int Flags;
            public int FlagsEx;
            public readonly IntPtr hwndParent;
            public readonly IntPtr InstallMsgHandler;
            public readonly IntPtr InstallMsgHandlerContext;
            public readonly IntPtr FileQueue;
            public readonly IntPtr ClassInstallReserved;
            public readonly UIntPtr Reserved;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string DriverPath;
            public void Initialize()
            {
                cbSize = Marshal.SizeOf(typeof(SP_DEVINSTALL_PARAMS));
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SP_ORIGINAL_FILE_INFO
        {
            /// <summary>Size of this structure, in bytes.</summary>
            public uint cbSize;

            /// <summary>Original file name of the INF file stored in array of size MAX_PATH.</summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string OriginalInfName;

            /// <summary>Catalog name of the INF file stored in array of size MAX_PATH.</summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string OriginalCatalogName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        public enum SPDIT
        {
            NODRIVER = 0x00000000,
            CLASSDRIVER = 0x00000001,
            COMPATDRIVER = 0x00000002,
        }

        const ulong CM_GETIDLIST_FILTER_PRESENT = 0x00000100;
        const ulong CM_GETIDLIST_FILTER_SERVICE = 0x00000002;

        internal enum SPDRP
        {
            SPDRP_DEVICEDESC = 0,
            SPDRP_HARDWAREID = 0x1,
            SPDRP_COMPATIBLEIDS = 0x2,
            SPDRP_UNUSED0 = 0x3,
            SPDRP_SERVICE = 0x4,
            SPDRP_UNUSED1 = 0x5,
            SPDRP_UNUSED2 = 0x6,
            SPDRP_CLASS = 0x7,
            SPDRP_CLASSGUID = 0x8,
            SPDRP_DRIVER = 0x9,
            SPDRP_CONFIGFLAGS = 0xa,
            SPDRP_MFG = 0xb,
            SPDRP_FRIENDLYNAME = 0xc,
            SPDRP_LOCATION_INFORMATION = 0xd,
            SPDRP_PHYSICAL_DEVICE_OBJECT_NAME = 0xe,
            SPDRP_CAPABILITIES = 0xf,
            SPDRP_UI_NUMBER = 0x10,
            SPDRP_UPPERFILTERS = 0x11,
            SPDRP_LOWERFILTERS = 0x12,
            SPDRP_BUSTYPEGUID = 0x13,
            SPDRP_LEGACYBUSTYPE = 0x14,
            SPDRP_BUSNUMBER = 0x15,
            SPDRP_ENUMERATOR_NAME = 0x16,
            SPDRP_SECURITY = 0x17,
            SPDRP_SECURITY_SDS = 0x18,
            SPDRP_DEVTYPE = 0x19,
            SPDRP_EXCLUSIVE = 0x1a,
            SPDRP_CHARACTERISTICS = 0x1b,
            SPDRP_ADDRESS = 0x1c,
            SPDRP_UI_NUMBER_DESC_FORMAT = 0x1e,
            SPDRP_MAXIMUM_PROPERTY = 0x1f
        }

        [Flags]
        internal enum FileAccess : uint
        {
            AccessSystemSecurity = 0x1000000,
            MaximumAllowed = 0x2000000,

            Delete = 0x10000,
            ReadControl = 0x20000,
            WriteDAC = 0x40000,
            WriteOwner = 0x80000,
            Synchronize = 0x100000,

            StandardRightsRequired = 0xF0000,
            StandardRightsRead = ReadControl,
            StandardRightsWrite = ReadControl,
            StandardRightsExecute = ReadControl,
            StandardRightsAll = 0x1F0000,
            SpecificRightsAll = 0xFFFF,

            FILE_READ_DATA = 0x0001, // file & pipe
            FILE_LIST_DIRECTORY = 0x0001, // directory
            FILE_WRITE_DATA = 0x0002, // file & pipe
            FILE_ADD_FILE = 0x0002, // directory
            FILE_APPEND_DATA = 0x0004, // file
            FILE_ADD_SUBDIRECTORY = 0x0004, // directory
            FILE_CREATE_PIPE_INSTANCE = 0x0004, // named pipe
            FILE_READ_EA = 0x0008, // file & directory
            FILE_WRITE_EA = 0x0010, // file & directory
            FILE_EXECUTE = 0x0020, // file
            FILE_TRAVERSE = 0x0020, // directory
            FILE_DELETE_CHILD = 0x0040, // directory
            FILE_READ_ATTRIBUTES = 0x0080, // all
            FILE_WRITE_ATTRIBUTES = 0x0100, // all

            //
            // Generic Section
            //

            GenericRead = 0x80000000,
            GenericWrite = 0x40000000,
            GenericExecute = 0x20000000,
            GenericAll = 0x10000000,

            SPECIFIC_RIGHTS_ALL = 0x00FFFF,

            FILE_ALL_ACCESS =
                StandardRightsRequired |
                Synchronize |
                0x1FF,

            FILE_GENERIC_READ =
                StandardRightsRead |
                FILE_READ_DATA |
                FILE_READ_ATTRIBUTES |
                FILE_READ_EA |
                Synchronize,

            FILE_GENERIC_WRITE =
                StandardRightsWrite |
                FILE_WRITE_DATA |
                FILE_WRITE_ATTRIBUTES |
                FILE_WRITE_EA |
                FILE_APPEND_DATA |
                Synchronize,

            FILE_GENERIC_EXECUTE =
                StandardRightsExecute |
                FILE_READ_ATTRIBUTES |
                FILE_EXECUTE |
                Synchronize
        }

        [Flags]
        internal enum FileShare : uint
        {
            /// <summary>
            ///
            /// </summary>
            None = 0x00000000,

            /// <summary>
            /// Enables subsequent open operations on an object to request read access.
            /// Otherwise, other processes cannot open the object if they request read access.
            /// If this flag is not specified, but the object has been opened for read access, the function fails.
            /// </summary>
            Read = 0x00000001,

            /// <summary>
            /// Enables subsequent open operations on an object to request write access.
            /// Otherwise, other processes cannot open the object if they request write access.
            /// If this flag is not specified, but the object has been opened for write access, the function fails.
            /// </summary>
            Write = 0x00000002,

            /// <summary>
            /// Enables subsequent open operations on an object to request delete access.
            /// Otherwise, other processes cannot open the object if they request delete access.
            /// If this flag is not specified, but the object has been opened for delete access, the function fails.
            /// </summary>
            Delete = 0x00000004
        }

        internal enum FileMode : uint
        {
            /// <summary>
            /// Creates a new file. The function fails if a specified file exists.
            /// </summary>
            New = 1,

            /// <summary>
            /// Creates a new file, always.
            /// If a file exists, the function overwrites the file, clears the existing attributes, combines the specified file attributes,
            /// and flags with FILE_ATTRIBUTE_ARCHIVE, but does not set the security descriptor that the SECURITY_ATTRIBUTES structure specifies.
            /// </summary>
            CreateAlways = 2,

            /// <summary>
            /// Opens a file. The function fails if the file does not exist.
            /// </summary>
            OpenExisting = 3,

            /// <summary>
            /// Opens a file, always.
            /// If a file does not exist, the function creates a file as if dwCreationDisposition is CREATE_NEW.
            /// </summary>
            OpenAlways = 4,

            /// <summary>
            /// Opens a file and truncates it so that its size is 0 (zero) bytes. The function fails if the file does not exist.
            /// The calling process must open the file with the GENERIC_WRITE access right.
            /// </summary>
            TruncateExisting = 5
        }

        [Flags]
        internal enum FileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        internal struct LARGE_INTEGER
        {
            [FieldOffset(0)] internal int Low;
            [FieldOffset(4)] internal int High;
            [FieldOffset(0)] internal long QuadPart;

            // use only when QuadPart canot be passed
            internal long ToInt64()
            {
                return ((long)this.High << 32) | (uint)this.Low;
            }

            // just for demonstration
            internal static LARGE_INTEGER FromInt64(long value)
            {
                return new LARGE_INTEGER
                {
                    Low = (int)(value),
                    High = (int)((value >> 32)),
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal class STORAGE_DEVICE_NUMBER
        {
            internal uint DeviceType;
            internal uint DeviceNumber;
            internal uint PartitionNumber;
        }

        [StructLayout(LayoutKind.Sequential, Size = 8)]
        internal class DISK_EXTENT
        {
            internal uint DiskNumber;
            internal long StartingOffset;
            internal long ExtentLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal class VOLUME_DISK_EXTENTS
        {
            internal uint NumberOfDiskExtents;
            internal DISK_EXTENT Extents;
        }

        /// <summary>
        /// Describes the geometry of disk devices and media.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        internal struct DISK_GEOMETRY
        {
            /// <summary>
            /// The number of cylinders.
            /// </summary>
            [FieldOffset(0)] internal Int64 Cylinders;

            /// <summary>
            /// The type of media. For a list of values, see MEDIA_TYPE.
            /// </summary>
            [FieldOffset(8)] internal MEDIA_TYPE MediaType;

            /// <summary>
            /// The number of tracks per cylinder.
            /// </summary>
            [FieldOffset(12)] internal uint TracksPerCylinder;

            /// <summary>
            /// The number of sectors per track.
            /// </summary>
            [FieldOffset(16)] internal uint SectorsPerTrack;

            /// <summary>
            /// The number of bytes per sector.
            /// </summary>
            [FieldOffset(20)] internal uint BytesPerSector;
        }

        /*
        /// <summary>
        /// Describes the extended geometry of disk devices and media.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        internal struct DISK_GEOMETRY_EX
        {
            /// <summary>
            /// A DISK_GEOMETRY structure.
            /// </summary>
            [FieldOffset(0)] public DISK_GEOMETRY Geometry;

            /// <summary>
            /// The disk size, in bytes.
            /// </summary>
            [FieldOffset(24)] public Int64 DiskSize;

            /// <summary>
            /// Any additional data.
            /// </summary>
            [FieldOffset(32)] public Byte Data;
        }
        */

        [StructLayout(LayoutKind.Sequential)]
        internal class DISK_GEOMETRY_EX
        {
            internal DISK_GEOMETRY Geometry;
            internal LARGE_INTEGER DiskSize;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            internal byte[] Data;
        }


        internal enum MEDIA_TYPE : int
        {
            Unknown = 0,
            F5_1Pt2_512 = 1,
            F3_1Pt44_512 = 2,
            F3_2Pt88_512 = 3,
            F3_20Pt8_512 = 4,
            F3_720_512 = 5,
            F5_360_512 = 6,
            F5_320_512 = 7,
            F5_320_1024 = 8,
            F5_180_512 = 9,
            F5_160_512 = 10,
            RemovableMedia = 11,
            FixedMedia = 12,
            F3_120M_512 = 13,
            F3_640_512 = 14,
            F5_640_512 = 15,
            F5_720_512 = 16,
            F3_1Pt2_512 = 17,
            F3_1Pt23_1024 = 18,
            F5_1Pt23_1024 = 19,
            F3_128Mb_512 = 20,
            F3_230Mb_512 = 21,
            F8_256_128 = 22,
            F3_200Mb_512 = 23,
            F3_240M_512 = 24,
            F3_32M_512 = 25
        }

        /// <summary>
        /// Represents the format of a partition.
        /// </summary>
        internal enum PARTITION_STYLE : uint
        {
            /// <summary>
            /// Master boot record (MBR) format.
            /// </summary>
            PARTITION_STYLE_MBR = 0,

            /// <summary>
            /// GUID Partition Table (GPT) format.
            /// </summary>
            PARTITION_STYLE_GPT = 1,

            /// <summary>
            /// Partition not formatted in either of the recognized formats—MBR or GPT.
            /// </summary>
            PARTITION_STYLE_RAW = 2
        }

        /// <summary>
        /// Contains partition information specific to master boot record (MBR) disks.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        internal struct PARTITION_INFORMATION_MBR
        {
            #region Constants
            /// <summary>
            /// An unused entry partition.
            /// </summary>
            internal const byte PARTITION_ENTRY_UNUSED = 0x00;

            /// <summary>
            /// A FAT12 file system partition.
            /// </summary>
            internal const byte PARTITION_FAT_12 = 0x01;

            /// <summary>
            /// A FAT16 file system partition.
            /// </summary>
            internal const byte PARTITION_FAT_16 = 0x04;

            /// <summary>
            /// An extended partition.
            /// </summary>
            internal const byte PARTITION_EXTENDED = 0x05;

            /// <summary>
            /// An IFS partition.
            /// </summary>
            internal const byte PARTITION_IFS = 0x07;

            /// <summary>
            /// A FAT32 file system partition.
            /// </summary>
            internal const byte PARTITION_FAT32 = 0x0B;

            /// <summary>
            /// A logical disk manager (LDM) partition.
            /// </summary>
            internal const byte PARTITION_LDM = 0x42;

            /// <summary>
            /// An NTFT partition.
            /// </summary>
            internal const byte PARTITION_NTFT = 0x80;

            /// <summary>
            /// A valid NTFT partition.
            /// 
            /// The high bit of a partition type code indicates that a partition is part of an NTFT mirror or striped array.
            /// </summary>
            internal const byte PARTITION_VALID_NTFT = 0xC0;
            #endregion

            /// <summary>
            /// The type of partition. For a list of values, see Disk Partition Types.
            /// </summary>
            [FieldOffset(0)] [MarshalAs(UnmanagedType.U1)]
            internal byte PartitionType;

            /// <summary>
            /// If this member is TRUE, the partition is bootable.
            /// </summary>
            [FieldOffset(1)] [MarshalAs(UnmanagedType.I1)]
            internal bool BootIndicator;

            /// <summary>
            /// If this member is TRUE, the partition is of a recognized type.
            /// </summary>
            [FieldOffset(2)] [MarshalAs(UnmanagedType.I1)]
            internal bool RecognizedPartition;

            /// <summary>
            /// The number of hidden sectors in the partition.
            /// </summary>
            [FieldOffset(4)] internal uint HiddenSectors;
        }

        /// <summary>
        /// Contains GUID partition table (GPT) partition information.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        internal struct PARTITION_INFORMATION_GPT
        {
            /// <summary>
            /// A GUID that identifies the partition type.
            /// 
            /// Each partition type that the EFI specification supports is identified by its own GUID, which is 
            /// published by the developer of the partition.
            /// </summary>
            [FieldOffset(0)] internal Guid PartitionType;

            /// <summary>
            /// The GUID of the partition.
            /// </summary>
            [FieldOffset(16)] internal Guid PartitionId;

            /// <summary>
            /// The Extensible Firmware Interface (EFI) attributes of the partition.
            /// 
            /// </summary>
            [FieldOffset(32)] internal UInt64 Attributes;

            /// <summary>
            /// A wide-character string that describes the partition.
            /// </summary>
            [FieldOffset(40)] [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 36)]
            internal string Name;
        }

        /// <summary>
        /// Provides information about a drive's master boot record (MBR) partitions.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        internal struct DRIVE_LAYOUT_INFORMATION_MBR
        {
            /// <summary>
            /// The signature of the drive.
            /// </summary>
            [FieldOffset(0)] internal uint Signature;
        }

        /// <summary>
        /// Contains information about a drive's GUID partition table (GPT) partitions.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        internal struct DRIVE_LAYOUT_INFORMATION_GPT
        {
            /// <summary>
            /// The GUID of the disk.
            /// </summary>
            [FieldOffset(0)] internal Guid DiskId;

            /// <summary>
            /// The starting byte offset of the first usable block.
            /// </summary>
            [FieldOffset(16)] internal Int64 StartingUsableOffset;

            /// <summary>
            /// The size of the usable blocks on the disk, in bytes.
            /// </summary>
            [FieldOffset(24)] internal Int64 UsableLength;

            /// <summary>
            /// The maximum number of partitions that can be defined in the usable block.
            /// </summary>
            [FieldOffset(32)] internal uint MaxPartitionCount;
        }


        /// <summary>
        /// Contains information about a disk partition.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        internal struct PARTITION_INFORMATION_EX
        {
            /// <summary>
            /// The format of the partition. For a list of values, see PARTITION_STYLE.
            /// </summary>
            [FieldOffset(0)] internal PARTITION_STYLE PartitionStyle;

            /// <summary>
            /// The starting offset of the partition.
            /// </summary>
            [FieldOffset(8)] internal Int64 StartingOffset;

            /// <summary>
            /// The length of the partition, in bytes.
            /// </summary>
            [FieldOffset(16)] internal Int64 PartitionLength;

            /// <summary>
            /// The number of the partition (1-based).
            /// </summary>
            [FieldOffset(24)] internal uint PartitionNumber;

            /// <summary>
            /// If this member is TRUE, the partition information has changed. When you change a partition (with 
            /// IOCTL_DISK_SET_DRIVE_LAYOUT), the system uses this member to determine which partitions have changed
            /// and need their information rewritten.
            /// </summary>
            [FieldOffset(28)] [MarshalAs(UnmanagedType.I1)]
            internal bool RewritePartition;

            /// <summary>
            /// A PARTITION_INFORMATION_MBR structure that specifies partition information specific to master boot 
            /// record (MBR) disks. The MBR partition format is the standard AT-style format.
            /// </summary>
            [FieldOffset(32)] internal PARTITION_INFORMATION_MBR Mbr;

            /// <summary>
            /// A PARTITION_INFORMATION_GPT structure that specifies partition information specific to GUID partition 
            /// table (GPT) disks. The GPT format corresponds to the EFI partition format.
            /// </summary>
            [FieldOffset(32)] internal PARTITION_INFORMATION_GPT Gpt;
        }


        [StructLayout(LayoutKind.Explicit)]
        internal struct DRIVE_LAYOUT_INFORMATION_UNION
        {
            [FieldOffset(0)] internal DRIVE_LAYOUT_INFORMATION_MBR Mbr;

            [FieldOffset(0)] internal DRIVE_LAYOUT_INFORMATION_GPT Gpt;
        }

        /// <summary>
        /// Contains extended information about a drive's partitions.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        internal struct DRIVE_LAYOUT_INFORMATION_EX
        {
            /// <summary>
            /// The style of the partitions on the drive enumerated by the PARTITION_STYLE enumeration.
            /// </summary>
            [FieldOffset(0)] internal PARTITION_STYLE PartitionStyle;

            /// <summary>
            /// The number of partitions on a drive.
            /// 
            /// On disks with the MBR layout, this value is always a multiple of 4. Any partitions that are unused have
            /// a partition type of PARTITION_ENTRY_UNUSED.
            /// </summary>
            [FieldOffset(4)] internal uint PartitionCount;

            /// <summary>
            /// A DRIVE_LAYOUT_INFORMATION_MBR structure containing information about the master boot record type 
            /// partitioning on the drive.
            /// </summary>
            [FieldOffset(8)] internal DRIVE_LAYOUT_INFORMATION_UNION Mbr;

            /// <summary>
            /// A DRIVE_LAYOUT_INFORMATION_GPT structure containing information about the GUID disk partition type 
            /// partitioning on the drive.
            /// </summary>
            [FieldOffset(8)] internal DRIVE_LAYOUT_INFORMATION_GPT Gpt;

            /// <summary>
            /// A variable-sized array of PARTITION_INFORMATION_EX structures, one structure for each partition on the 
            /// drive.
            /// </summary>
            [FieldOffset(48)] [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 4)]
            internal PARTITION_INFORMATION_EX[] PartitionEntry;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct CREATE_DISK_MBR
        {
            [FieldOffset(0)] internal uint Signature;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct CREATE_DISK_GPT
        {
            [FieldOffset(0)] internal Guid DiskId;

            [FieldOffset(16)] internal uint MaxPartitionCount;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct CREATE_DISK
        {
            [FieldOffset(0)] internal PARTITION_STYLE PartitionStyle;

            [FieldOffset(4)] internal CREATE_DISK_MBR Mbr;

            [FieldOffset(4)] internal CREATE_DISK_GPT Gpt;
        }

        internal const int DRIVE_ACCESS_RETRIES = 10;
        internal const int DRIVE_ACCESS_TIMEOUT = 15000;
        internal const int MIN_EXTRA_PART_SIZE = 1024 * 1024;

        internal class IoCtl /* constants */
        {
            internal const UInt32
                DISK_BASE = 0x00000007,
                VOLUME_BASE = 0x00000056,
                STORAGE_BASE = 0x0000002d,
                FILE_DEVICE_DISK_SYSTEM = 0x00000008,
                FILE_DEVICE_FILE_SYSTEM = 0x00000009,
                METHOD_BUFFERED = 0,
                METHOD_IN_DIRECT = 1,
                METHOD_OUT_DIRECT = 2,
                METHOD_NEITHER = 3,
                FILE_READ_ACCESS = 0x0001,
                FILE_WRITE_ACCESS = 0x0002,
                FILE_ANY_ACCESS = 0;

            internal const UInt32
                GENERIC_READ = 0x80000000,
                FILE_SHARE_WRITE = 0x2,
                FILE_SHARE_READ = 0x1,
                OPEN_EXISTING = 0x3;

            internal static readonly UInt32 DISK_FLUSH_CACHE =
                IoCtl.CTL_CODE(DISK_BASE, 0x715, METHOD_BUFFERED, FILE_ANY_ACCESS);

            internal static readonly UInt32 DISK_GET_DRIVE_LAYOUT_EX =
                IoCtl.CTL_CODE(DISK_BASE, 0x0014, METHOD_BUFFERED, FILE_ANY_ACCESS);

            internal static readonly UInt32 DISK_SET_DRIVE_LAYOUT_EX =
                IoCtl.CTL_CODE(DISK_BASE, 0x0015, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS);

            internal static readonly UInt32 DISK_DELETE_DRIVE_LAYOUT =
                IoCtl.CTL_CODE(DISK_BASE, 0x0040, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS);

            internal static readonly UInt32 DISK_CREATE_DISK =
                IoCtl.CTL_CODE(DISK_BASE, 0x0016, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS);

            internal static readonly UInt32 DISK_UPDATE_PROPERTIES =
                IoCtl.CTL_CODE(DISK_BASE, 0x0050, METHOD_BUFFERED, FILE_ANY_ACCESS);

            internal static readonly UInt32 DISK_GET_DRIVE_GEOMETRY_EX =
                IoCtl.CTL_CODE(DISK_BASE, 0x0028, METHOD_BUFFERED, FILE_ANY_ACCESS);

            internal static readonly UInt32 DISK_GET_DRIVE_GEOMETRY =
                IoCtl.CTL_CODE(DISK_BASE, 0, METHOD_BUFFERED, FILE_ANY_ACCESS);

            internal static readonly UInt32 FSCTL_ALLOW_EXTENDED_DASD_IO =
                IoCtl.CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 32, METHOD_NEITHER, FILE_ANY_ACCESS);

            internal static readonly UInt32 FSCTL_LOCK_VOLUME =
                IoCtl.CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 6, METHOD_BUFFERED, FILE_ANY_ACCESS);

            internal static readonly UInt32 FSCTL_UNLOCK_VOLUME =
                IoCtl.CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 7, METHOD_BUFFERED, FILE_ANY_ACCESS);

            internal static readonly UInt32 FSCTL_DISMOUNT_VOLUME =
                IoCtl.CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 8, METHOD_BUFFERED, FILE_ANY_ACCESS);

            internal static readonly UInt32 VOLUME_ONLINE =
                IoCtl.CTL_CODE(VOLUME_BASE, 2, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS);

            internal static readonly UInt32 VOLUME_OFFLINE =
                IoCtl.CTL_CODE(VOLUME_BASE, 3, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS);

            internal static readonly UInt32 VOLUME_GET_VOLUME_DISK_EXTENTS =
                IoCtl.CTL_CODE(VOLUME_BASE, 0, METHOD_BUFFERED, FILE_ANY_ACCESS);

            internal static readonly UInt32 STORAGE_GET_DEVICE_NUMBER =
                IoCtl.CTL_CODE(STORAGE_BASE, 0x0420, METHOD_BUFFERED, FILE_ANY_ACCESS);

            internal static UInt32 CTL_CODE(UInt32 DeviceType, UInt32 Function, UInt32 Method, UInt32 Access)
            {
                return (((DeviceType) << 16) | ((Access) << 14) | ((Function) << 2) | (Method));
            }
        }

        public class SafeHGlobalHandle : SafeHandle
        {
            public SafeHGlobalHandle(IntPtr handle) : base(IntPtr.Zero, true)
            {
                SetHandle(handle);
            }

            public override bool IsInvalid
            {
                get => handle == IntPtr.Zero;
            }

            protected override bool ReleaseHandle()
            {
                Marshal.FreeHGlobal(handle);
                return true;
            }
        }

        public static class FileLock
        {
            public static bool HasKilledExplorer = false;

            private const int RmRebootReasonNone = 0;
            private const int CCH_RM_MAX_APP_NAME = 255;
            private const int CCH_RM_MAX_SVC_NAME = 63;

            [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
            private static extern int RmRegisterResources(uint pSessionHandle,
                uint nFiles,
                string[] rgsFilenames,
                uint nApplications,
                [In] RM_UNIQUE_PROCESS[] rgApplications,
                uint nServices,
                string[] rgsServiceNames);

            [DllImport("rstrtmgr.dll", CharSet = CharSet.Auto)]
            private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

            [DllImport("rstrtmgr.dll")]
            private static extern int RmEndSession(uint pSessionHandle);

            [DllImport("rstrtmgr.dll")]
            private static extern int RmGetList(uint dwSessionHandle,
                out uint pnProcInfoNeeded,
                ref uint pnProcInfo,
                [In] [Out] RM_PROCESS_INFO[] rgAffectedApps,
                ref uint lpdwRebootReasons);

            public static List<Process> WhoIsLocking(string path)
            {
                string key = Guid.NewGuid().ToString();
                List<Process> processes = new List<Process>();

                int res = RmStartSession(out uint handle, 0, key);
                if (res != 0)
                {
                    throw new Exception("Could not begin restart session.  Unable to determine file locker.");
                }

                try
                {
                    const int ERROR_MORE_DATA = 234;
                    uint pnProcInfoNeeded = 0,
                        pnProcInfo = 0,
                        lpdwRebootReasons = RmRebootReasonNone;

                    string[] resources = new string[] { path }; // Just checking on one resource.

                    res = RmRegisterResources(handle, (uint)resources.Length, resources, 0, null, 0, null);

                    if (res != 0) throw new Exception("Could not register resource.");

                    //Note: there's a race condition here -- the first call to RmGetList() returns
                    //      the total number of process. However, when we call RmGetList() again to get
                    //      the actual processes this number may have increased.
                    res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, null, ref lpdwRebootReasons);

                    if (res == ERROR_MORE_DATA)
                    {
                        // Create an array to store the process results
                        RM_PROCESS_INFO[] processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded + 3];
                        pnProcInfo = pnProcInfoNeeded;

                        // Get the list
                        res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref lpdwRebootReasons);
                        if (res == 0)
                        {
                            processes = new List<Process>((int)pnProcInfo);

                            // Enumerate all of the results and add them to the 
                            // list to be returned
                            for (int i = 0; i < pnProcInfo; i++)
                            {
                                try
                                {
                                    processes.Add(Process.GetProcessById(processInfo[i].Process.dwProcessId));
                                }
                                // catch the error -- in case the process is no longer running
                                catch (ArgumentException) { }
                            }
                        }
                        else throw new Exception("Could not list processes locking resource: " + res);
                    }
                    else if (res != 0)
                        throw new Exception("Could not list processes locking resource. Could not get size of result." + $" Result value: {res}");
                }
                finally
                {
                    RmEndSession(handle);
                }

                return processes;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct RM_UNIQUE_PROCESS
            {
                public readonly int dwProcessId;
                public readonly FILETIME ProcessStartTime;
            }

            private enum RM_APP_TYPE
            {
                RmUnknownApp = 0,
                RmMainWindow = 1,
                RmOtherWindow = 2,
                RmService = 3,
                RmExplorer = 4,
                RmConsole = 5,
                RmCritical = 1000
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct RM_PROCESS_INFO
            {
                public readonly RM_UNIQUE_PROCESS Process;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
                public readonly string strAppName;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
                public readonly string strServiceShortName;

                public readonly RM_APP_TYPE ApplicationType;
                public readonly uint AppStatus;
                public readonly uint TSSessionId;
                [MarshalAs(UnmanagedType.Bool)] public readonly bool bRestartable;
            }
        }
    }
}
