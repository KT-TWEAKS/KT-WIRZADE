using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Documents;
using Core;
using Interprocess;
using JetBrains.Annotations;

namespace iso_mode
{
    public class USB
    {
        public class UsbDevice
        {
            public Guid DeviceInterface { get; set; } = Guid.Empty;
            public string DeviceID { get; set; } = "";
            public string Service { get; set; }
            public DateTime Connected { get; set; } = DateTime.UtcNow;
        }

        public class UsbDisk
        {
            public uint Index { get; set; }
            public long Size { get; set; }
            public string ReadableSize { get; set; }
            public string FriendlyName { get; set; }
            public List<System.IO.DriveInfo> LogicalDrives;
            public bool Card { get; set; }
            public bool UASP { get; set; }
            public bool VHD { get; set; }
            public bool SCSI { get; set; }
            public ushort VID { get; set; }
            public ushort PID { get; set; }


            [CanBeNull] public UsbDevice UsbDevice;
            [CanBeNull] public string UsbDeviceID { get; set; }
            public long ISOPartitionOffset { get; set; } = 0;
            public long DataPartitionOffset { get; set; } = 0;
            public long EFIPartitionOffset { get; set; } = 0;


            #region Lists
            private static Dictionary<string, (int Score, bool Number, bool Contains)> LabelScores = new Dictionary<string, (int Score, bool Number, bool Contains)>
            {
                { "LEXAR", (-15, false, false) },
                { "CORSAIR", (-15, false, false) },
                { "KINGMAX", (-15, false, false) },
                { "PNY", (-15, false, false) },
                { "KINGSTON", (-15, false, false) },
                { "MUSHKIN", (-15, false, false) },
                { "TRANSCEND", (-15, false, false) },
                { "SANDISK", (-15, false, false) },
                { "SD-CARD", (-10, false, true) },
                { "uSD Card", (-10, false, true) },
                { "Flash", (-10, false, true) },
                { "Gadget", (-10, false, true) },
                { "SAMSUNG", (5, false, false) },
                { "TOSHIBA", (5, false, false) },
                { "WDC", (10, false, false) },
                { "IC", (10, true, false) },
                { "HTS", (10, true, false) },
                { "IBM", (10, false, false) },
                { "ST", (10, true, false) },
                { "MX", (10, true, false) },
                { "INTEL", (10, false, false) },
                { "MAXTOR", (10, false, false) },
                { "EXCELSTOR", (10, false, false) },
                { "STM", (10, true, false) },
                { "HDS", (10, true, false) },
                { "QUANTUM", (10, false, false) },
                { "HDP", (10, true, false) },
                { "HDT", (10, true, false) },
                { "HTE", (10, true, false) },
                { "HITACHI", (10, false, false) },
                { "HUA", (10, true, false) },
                { "APPLE", (10, false, false) },
                { "SEAGATE", (10, false, false) },
                { "FUJITSU", (10, false, false) },
                { "SSD", (20, false, true) },
                { "HDD", (20, false, true) },
                { "SCSI", (20, false, true) },
                { "SATA", (20, false, true) },
            };

            private static List<(ushort VID, ushort PID, int Score)> VidPidScores = new List<(ushort VID, ushort PID, int Score)>()
            {
                (0x03f0, 0xbd07, 10),
                (0x0402, 0x5621, 10),
                (0x040d, 0x6204, 10),
                (0x043e, 0x70f1, 10),
                (0x0471, 0x2021, 10),
                (0x05e3, 0x0718, 10),
                (0x05e3, 0x0719, 10),
                (0x05e3, 0x0731, 10),
                (0x05e3, 0x0731, 2),
                (0x0634, 0x0655, 5),
                (0x0718, 0x1000, 7),
                (0x0939, 0x0b16, 10),
                (0x0c0b, 0xb001, 10),
                (0x0c0b, 0xb159, 10),
                (0x0e21, 0x0510, 5),
                (0x11b0, 0x6298, 10),
                (0x125f, 0xa93a, 10),
                (0x125f, 0xa94a, 10),
                (0x14cd, 0x6116, 10),
                (0x18a5, 0x0214, 10),
                (0x18a5, 0x0215, 10),
                (0x18a5, 0x0216, 10),
                (0x18a5, 0x0227, 10),
                (0x18a5, 0x022a, 10),
                (0x18a5, 0x022b, 10),
                (0x18a5, 0x0237, 10),
                (0x1bcf, 0x0c31, 10),
                (0x1f75, 0x0888, 10),
                (0x3538, 0x0902, 10),
                (0x55aa, 0x0015, 10),
                (0x55aa, 0x0102, 8),
                (0x55aa, 0x0103, 10),
                (0x55aa, 0x1234, 8),
                (0x55aa, 0x2b00, 8),
                (0x6795, 0x2756, 2),
                (0x0324, 0xbc06, -20),
                (0x0324, 0xbc08, -20),
                (0x0325, 0xac02, -20),
                (0x0411, 0x01e8, -20),
                (0x04e8, 0x0100, -20),
                (0x04e8, 0x0100, -20),
                (0x04e8, 0x0101, -20),
                (0x04e8, 0x1a23, -20),
                (0x04e8, 0x5120, -20),
                (0x04e8, 0x6818, -20),
                (0x04e8, 0x6845, -20),
                (0x04e8, 0x685E, -20),
                (0x04fc, 0x05d8, -20),
                (0x04fc, 0x5720, -20),
                (0x059f, 0x1027, -20),
                (0x059f, 0x103B, -20),
                (0x059f, 0x1064, -20),
                (0x059f, 0x1079, -20),
                (0x05ac, 0x8400, -20),
                (0x05ac, 0x8401, -20),
                (0x05ac, 0x8402, -20),
                (0x05ac, 0x8403, -20),
                (0x05ac, 0x8404, -20),
                (0x05ac, 0x8405, -20),
                (0x05ac, 0x8406, -20),
                (0x05ac, 0x8407, -20),
                (0x067b, 0x2506, -20),
                (0x067b, 0x2517, -20),
                (0x067b, 0x2528, -20),
                (0x067b, 0x2731, -20),
                (0x067b, 0x2733, -20),
                (0x067b, 0x3400, -10),
                (0x067b, 0x3500, -10),
                (0x0781, 0x5580, -20),
                (0x07ab, 0xfcab, -20),
                (0x090c, 0x1000, -20),
                (0x0930, 0x1400, -20),
                (0x0930, 0x6533, -20),
                (0x0930, 0x653e, -20),
                (0x0930, 0x6544, -20),
                (0x0930, 0x6545, -20),
                (0x0bc2, 0x3312, -20),
                (0x152d, 0x0901, -20),
                (0x18a5, 0x0243, -20),
                (0x18a5, 0x0245, -20),
                (0x18a5, 0x0302, -20),
                (0x18a5, 0x0304, -20),
                (0x18a5, 0x3327, -20),
                (0x1f75, 0x0917, -10),
                (0x23a9, 0xef18, -10),
                (0x6557, 0x0021, -5),
                (0x0011, 0, -5),
                (0x03f0, 0, -5),
                (0x0409, 0, -10),
                (0x0411, 0, 5),
                (0x0420, 0, -5),
                (0x046d, 0, -5),
                (0x0480, 0, 5),
                (0x048d, 0, -10),
                (0x04b4, 0, 10),
                (0x04c5, 0, 7),
                (0x04e8, 0, 5),
                (0x04f3, 0, -5),
                (0x04fc, 0, 5),
                (0x056e, 0, -5),
                (0x058f, 0, -5),
                (0x059b, 0, 7),
                (0x059f, 0, 5),
                (0x05ab, 0, 10),
                (0x05dc, 0, -5),
                (0x05e3, 0, -5),
                (0x067b, 0, 7),
                (0x0718, 0, -2),
                (0x0781, 0, -5),
                (0x07ab, 0, 8),
                (0x090c, 0, -5),
                (0x0928, 0, 10),
                (0x0930, 0, -8),
                (0x093a, 0, -5),
                (0x0951, 0, -5),
                (0x09da, 0, -5),
                (0x0b27, 0, -5),
                (0x0bc2, 0, 10),
                (0x0bda, 0, -10),
                (0x0c76, 0, -5),
                (0x0cf2, 0, -5),
                (0x0d49, 0, 10),
                (0x0dc4, 0, 10),
                (0x1000, 0, -5),
                (0x1002, 0, -5),
                (0x1005, 0, -5),
                (0x1043, 0, -5),
                (0x1058, 0, 10),
                (0x1221, 0, -5),
                (0x12d1, 0, -5),
                (0x125f, 0, -5),
                (0x1307, 0, -5),
                (0x13fd, 0, 10),
                (0x13fe, 0, -5),
                (0x14cd, 0, -5),
                (0x1516, 0, -5),
                (0x152d, 0, 10),
                (0x1687, 0, -5),
                (0x174c, 0, 3),
                (0x1759, 0, 8),
                (0x18a5, 0, -2),
                (0x18ec, 0, -5),
                (0x1908, 0, -5),
                (0x1a4a, 0, 10),
                (0x1b1c, 0, -5),
                (0x1e3d, 0, -5),
                (0x1f75, 0, -2),
                (0x2001, 0, -5),
                (0x201e, 0, -5),
                (0x2109, 0, 10),
                (0x2188, 0, -5),
                (0x3538, 0, -5),
                (0x413c, 0, -5),
                (0x4971, 0, 10),
                (0x5136, 0, -5),
                (0x8564, 0, -5),
                (0x8644, 0, -5),
                (0xeeee, 0, -5),
            };
            #endregion

            public bool IsDisk()
            {
                int sus = 0;

                foreach (var score in LabelScores.Where(label => label.Value.Contains ? FriendlyName.Contains(label.Key) : FriendlyName.StartsWith(label.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    sus += score.Value.Score;
                }

                if (Size < 137438953472)
                    sus -= 15;
                else if (Size < 274877906944)
                    sus -= 3;
                else if (Size > 1825361100800)
                    sus += 30;
                else if (Size >= 751619276800)
                    sus += 15;

                foreach (var vidPidMatch in VidPidScores.Where(x => x.VID == VID && (x.PID == 0 || x.PID == PID)))
                {
                    sus += vidPidMatch.Score;
                }

                if (LogicalDrives.Any(x => x.DriveType == DriveType.Fixed))
                    sus += 3;

                return sus > 0;
            }
        }

        private static readonly string[] _usbDriverNames =
            { "USBSTOR", "EUCR", "RTSUER", "CMIUCR", "UASPSTOR", "ASUSSTPT", "VUSBSTOR", "ETRONSTOR" };

        private static readonly string[] _genericDriverNames =
        {
            "SCSI", "SD", "PCISTOR", "GLREADER", "RTSOR", "JMCR", "JMCF", "RISD", "RIMMPTSK", "RIMSPTSK", "ESD7SK",
            "RIXDPTSK", "TI21SONY", "VIACR", "O2MD", "O2SD"
        };

        private static readonly string[] _scsiCardNames =
            { "_SDHC", "_SD", "_xDPicture", "_O2Media", "_MMC", "_MS", "_MSPro" };

        [InterprocessMethod(Level.Administrator)]
        public static List<UsbDisk> GetDevices(bool includeDisks, bool log)
        {
            Log.WriteIf(log, LogType.Info, "GetDevices Begin");
            
            List<UsbDevice> deviceList = new List<UsbDevice>();
            List<UsbDisk> diskList = new List<UsbDisk>();

            foreach (var usbDriver in _usbDriverNames)
            {
                int deviceListLength = 0;
                if (Win32.CM_Get_Device_ID_List_Size(
                        ref deviceListLength,
                        usbDriver,
                        Win32.CM_GETIDLIST_FILTER.SERVICE | Win32.CM_GETIDLIST_FILTER.PRESENT) == Win32.CR_SUCCESS)
                {
                    Log.WriteIf(log, LogType.Info, "GetDevices --> CM_Get_Device_ID_List_Size True");
                    byte[] buffer = new byte[deviceListLength * sizeof(char) + 2];
                    if (Win32.CM_Get_Device_ID_List(
                            usbDriver,
                            buffer,
                            deviceListLength,
                            Win32.CM_GETIDLIST_FILTER.SERVICE | Win32.CM_GETIDLIST_FILTER.PRESENT) == Win32.CR_SUCCESS)
                    {
                        Log.WriteIf(log, LogType.Info, "GetDevices --> CM_Get_Device_ID_List True");
                        string[] deviceIds = Encoding.Unicode.GetString(buffer).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string deviceId in deviceIds)
                        {
                            deviceList.Add(new UsbDevice()
                            {
                                DeviceID = deviceId,
                                Service = usbDriver,
                                Connected = default,
                            });
                            
                            Log.WriteIf(log, LogType.Info, "GetDevices Device Found 1", null, ("DeviceID", deviceId), ("Service", usbDriver));

                            try
                            {
                                uint usbInst = 0;
                                if (Win32.CM_Locate_DevNodeA(ref usbInst, deviceId) != Win32.CR_SUCCESS)
                                {
                                    Log.WriteIf(log, LogType.Info, "GetDevices CM_Locate_DevNodeA False", null, ("DeviceID", deviceId), ("Service", usbDriver));
                                    continue;
                                }

                                var properties = GetProperties(usbInst, new[]
                                {
                                    new KeyValuePair<string, Win32.DEVPROPKEY>("DEVPKEY_Device_LastArrivalDate",
                                        Win32.DevicePropertyKeys.DEVPKEY_Device_LastArrivalDate),
                                });
                                deviceList.Last().Connected =
                                    (DateTime)properties["DEVPKEY_Device_LastArrivalDate"];
                                
                                Log.WriteIf(log, LogType.Info, $"GetDevices Found Device Connected at {deviceList.Last().Connected} 1", null, ("DeviceID", deviceId), ("Service", usbDriver));
                            }
                            catch (Exception e) { }
                        }

                    } else
                        Log.WriteIf(log, LogType.Info, "GetDevices --> CM_Get_Device_ID_List False");
                } else
                    Log.WriteIf(log, LogType.Info, "GetDevices --> CM_Get_Device_ID_List_Size False");
            }

            Guid usbGUID = new Guid(Win32.GUID_DEVINTERFACE_USB_HUB);

            Win32.SP_DEVINFO_DATA infoData = new Win32.SP_DEVINFO_DATA();

            IntPtr usbInterface = Win32.SetupDiGetClassDevs(ref usbGUID, null, IntPtr.Zero,
                (uint)(Win32.DiGetClassFlags.DIGCF_PRESENT | Win32.DiGetClassFlags.DIGCF_DEVICEINTERFACE));
            if (usbInterface != Win32.INVALID_HANDLE_VALUE)
            {
                Log.WriteIf(log, LogType.Info, "GetDevices --> SetupDiGetClassDevs True");
                infoData.cbSize = (uint)Marshal.SizeOf(infoData);

                for (uint i = 0; Win32.SetupDiEnumDeviceInfo(usbInterface, i, ref infoData); i++)
                {
                    Log.WriteIf(log, LogType.Info, "GetDevices --> SetupDiEnumDeviceInfo Loop 1");
                    Win32.SP_DEVICE_INTERFACE_DATA interfaceData = new Win32.SP_DEVICE_INTERFACE_DATA();
                    interfaceData.cbSize = Marshal.SizeOf(interfaceData);
                    Win32.SP_DEVICE_INTERFACE_DETAIL_DATA interfaceDetails = new Win32.SP_DEVICE_INTERFACE_DETAIL_DATA();

                    if (IntPtr.Size == 8) //64 bit
                        interfaceDetails.cbSize = 8;
                    else //32 bit
                        interfaceDetails.cbSize = 4 + Marshal.SystemDefaultCharSize;

                    uint size = 0;
                    if (Win32.SetupDiEnumDeviceInterfaces(usbInterface, ref infoData, ref usbGUID, 0, ref interfaceData)
                        && (!Win32.SetupDiGetDeviceInterfaceDetail(usbInterface, ref interfaceData, IntPtr.Zero, 0, ref size,
                            IntPtr.Zero))
                        && (Marshal.GetLastWin32Error() == 122))
                    {
                        Log.WriteIf(log, LogType.Info, "GetDevices --> SetupDiEnumDeviceInterfaces True");
                        if (Win32.SetupDiGetDeviceInterfaceDetail(usbInterface, ref interfaceData, ref interfaceDetails, size,
                                ref size, IntPtr.Zero))
                        {
                            Log.WriteIf(log, LogType.Info, "GetDevices --> SetupDiGetDeviceInterfaceDetail True");
                            if (Win32.CM_Get_Child(out uint usbDevice, infoData.DevInst, 0) == 0)
                            {
                                Log.WriteIf(log, LogType.Info, "GetDevices --> CM_Get_Child True");
                                StringBuilder usbId = new StringBuilder((int)256);
                                if (Win32.CM_Get_Device_ID(usbDevice, usbId, (int)256, 0) == 0)
                                {
                                    Log.WriteIf(log, LogType.Info, "GetDevices --> CM_Get_Device_ID 1 True");
                                    try
                                    {
#if DEBUG
                                        var allProperties = GetProperties(usbDevice);

                                        Console.WriteLine("\nDEBUG START\n");
                                        foreach (var property in allProperties)
                                        {
                                            Console.WriteLine(property.Key + ": " + property.Value);
                                        }
                                        Console.WriteLine("\nDEBUG END\n");
#endif


                                        var properties = GetProperties(usbDevice, new[]
                                        {
                                            new KeyValuePair<string, Win32.DEVPROPKEY>("DEVPKEY_Device_Service",
                                                Win32.DevicePropertyKeys.DEVPKEY_Device_Service),
                                        });
                                        var service = ((string)properties["DEVPKEY_Device_Service"]).ToUpper();

                                        deviceList.Add(new UsbDevice()
                                        {
                                            DeviceInterface = interfaceData.interfaceClassGuid,
                                            DeviceID = usbId.ToString(),
                                            Service = service,
                                            Connected = default,
                                        });

                                        Log.WriteIf(log, LogType.Info, "GetDevices Device Found 2", null, ("Interface", interfaceData.interfaceClassGuid.ToString()), ("DeviceID", usbId.ToString()), ("Service", service));

                                        properties = GetProperties(usbDevice, new[]
                                        {
                                            new KeyValuePair<string, Win32.DEVPROPKEY>("DEVPKEY_Device_LastArrivalDate",
                                                Win32.DevicePropertyKeys.DEVPKEY_Device_LastArrivalDate),
                                        });
                                        deviceList.Last().Connected =
                                            (DateTime)properties["DEVPKEY_Device_LastArrivalDate"];
                                        
                                        Log.WriteIf(log, LogType.Info, $"GetDevices Found Device Connected at {deviceList.Last().Connected} 2", null, ("DeviceID", usbId.ToString()), ("Service", service));
                                    }
                                    catch (Exception e) { }

                                    while (Win32.CM_Get_Sibling(out usbDevice, usbDevice, 0) == 0)
                                    {
                                        Log.WriteIf(log, LogType.Info, "GetDevices --> CM_Get_Sibling Loop");
                                        usbId = new StringBuilder((int)256);
                                        if (Win32.CM_Get_Device_ID(usbDevice, usbId, (int)256, 0) == 0)
                                        {
                                            Log.WriteIf(log, LogType.Info, "GetDevices --> CM_Get_Device_ID 2 True");
                                            try
                                            {
#if DEBUG
                                            var allProperties = GetProperties(usbDevice);

                                            Console.WriteLine("\nDEBUG START\n");
                                            foreach (var property in allProperties)
                                            {
                                                Console.WriteLine(property.Key + ": " + property.Value);
                                            }
                                            Console.WriteLine("\nDEBUG END\n");
#endif

                                                var properties = GetProperties(usbDevice, new[]
                                                {
                                                    new KeyValuePair<string, Win32.DEVPROPKEY>("DEVPKEY_Device_Service",
                                                        Win32.DevicePropertyKeys.DEVPKEY_Device_Service),
                                                });

                                                deviceList.Add(new UsbDevice()
                                                {
                                                    DeviceInterface = interfaceData.interfaceClassGuid,
                                                    DeviceID = usbId.ToString(),
                                                    Service = ((string)properties["DEVPKEY_Device_Service"]).ToUpper(),
                                                    Connected = default,
                                                });

                                                Log.WriteIf(log, LogType.Info, "GetDevices Device Found 3", null, ("Interface", interfaceData.interfaceClassGuid.ToString()), ("DeviceID", usbId.ToString()),
                                                    ("Service", ((string)properties["DEVPKEY_Device_Service"]).ToUpper()));

                                                properties = GetProperties(usbDevice, new[]
                                                {
                                                    new KeyValuePair<string, Win32.DEVPROPKEY>(
                                                        "DEVPKEY_Device_LastArrivalDate",
                                                        Win32.DevicePropertyKeys.DEVPKEY_Device_LastArrivalDate),
                                                });
                                                deviceList.Last().Connected =
                                                    (DateTime)properties["DEVPKEY_Device_LastArrivalDate"];

                                                Log.WriteIf(log, LogType.Info, $"GetDevices Found Device Connected at {deviceList.Last().Connected} 3", null, ("DeviceID", usbId.ToString()),
                                                    ("Service", ((string)properties["DEVPKEY_Device_Service"]).ToUpper()));
                                            }
                                            catch (Exception e)
                                            {
                                                Log.WriteExceptionIf(log, e, "GetDevices Exception 1");
                                            }
                                        } else
                                            Log.WriteIf(log, LogType.Info, "GetDevices --> CM_Get_Device_ID 2 False");
                                    }
                                } else
                                    Log.WriteIf(log, LogType.Info, "GetDevices --> CM_Get_Device_ID 1 False");
                            } else
                                Log.WriteIf(log, LogType.Info, "GetDevices --> CM_Get_Child False");
                        } else
                            Log.WriteIf(log, LogType.Info, "GetDevices --> SetupDiGetDeviceInterfaceDetail False");
                    } else
                        Log.WriteIf(log, LogType.Info, "GetDevices --> SetupDiEnumDeviceInterfaces False");

                    Win32.SetupDiDestroyDeviceInfoList(usbInterface);
                }
            } else 
                Log.WriteIf(log, LogType.Info, "GetDevices --> SetupDiGetClassDevs False");

            var diskGUID = new Guid(Win32.GUID_DEVINTERFACE_DISK);

            IntPtr diskInterface = Win32.SetupDiGetClassDevs(ref diskGUID, null, IntPtr.Zero,
                (uint)(Win32.DiGetClassFlags.DIGCF_PRESENT | Win32.DiGetClassFlags.DIGCF_DEVICEINTERFACE));
            if (diskInterface == Win32.INVALID_HANDLE_VALUE)
                throw new Exception("Invalid disk interface handle.");

            infoData = new Win32.SP_DEVINFO_DATA();
            infoData.cbSize = (uint)Marshal.SizeOf(infoData);

            for (uint i = 0; Win32.SetupDiEnumDeviceInfo(diskInterface, i, ref infoData); i++)
            {
                Log.WriteIf(log, LogType.Info, "GetDevices --> SetupDiEnumDeviceInfo Loop 2");
                try
                {
                    var disk = GetUSBDisk(diskInterface, infoData, deviceList, log);
                    if (disk != null)
                        diskList.Add(disk);
                }
                catch (Exception e)
                {
                    Log.EnqueueExceptionSafe(e);
                }
            }

            diskList = diskList.OrderByDescending(x => x.UsbDevice?.Connected ?? default(DateTime)).ToList();
            diskList.ForEach(x => x.UsbDeviceID = x.UsbDevice?.DeviceID);
            if (!includeDisks)
            {
                diskList = diskList.Where(x =>
                {
                    if (x.IsDisk())
                    {
                        Log.EnqueueSafe(LogType.Info, $"Disk '{x.FriendlyName}' was detected as an external disk and was skipped.", null);
                        return false;
                    }
                    return true;
                }).ToList();
            }

            return diskList;
        }

        private static UsbDisk GetUSBDisk(IntPtr diskInterface, Win32.SP_DEVINFO_DATA infoData, List<UsbDevice> deviceList, bool log)
        {
            Log.WriteIf(log, LogType.Info, $"GetUSBDisk Begin");
            
            bool vhd = false;
            bool usb = false;
            bool card = false;
            bool scsi = false;
            bool uasp = false;
            bool removable = false;

            UInt16 vid = 0;
            UInt16 pid = 0;

            string instanceId = "";
            uint regDataType = 0;
            if (Win32.SetupDiGetDeviceRegistryProperty(diskInterface, ref infoData, (uint)Win32.SPDRP.SPDRP_ENUMERATOR_NAME,
                    out regDataType, IntPtr.Zero, 0, out uint size))
                throw new Exception("SetupDiGetDeviceRegistryProperty ENUMERATOR_NAME size check failed.");

            IntPtr buffer = Marshal.AllocHGlobal((Int32)size);

            if (!Win32.SetupDiGetDeviceRegistryProperty(diskInterface, ref infoData, (uint)Win32.SPDRP.SPDRP_ENUMERATOR_NAME,
                    out regDataType, buffer, size, out size))
                throw new Exception("SetupDiGetDeviceRegistryProperty ENUMERATOR_NAME get failed.");

            var enumerator = Marshal.PtrToStringAuto(buffer);

            if (_genericDriverNames.Contains(enumerator, StringComparer.OrdinalIgnoreCase))
            {
                Log.WriteIf(log, LogType.Info, $"GetUSBDisk --> (_genericDriverNames.Contains(enumerator, StringComparer.OrdinalIgnoreCase)) scsi = true", null, ("Enumerator", enumerator));
                scsi = true;
            }
            if (_usbDriverNames.Contains(enumerator, StringComparer.OrdinalIgnoreCase))
            {
                Log.WriteIf(log, LogType.Info, $"GetUSBDisk --> (_usbDriverNames.Contains(enumerator, StringComparer.OrdinalIgnoreCase)) usb = true", null, ("Enumerator", enumerator));
                usb = true;
            }

            if (!usb && !scsi)
            {
                Log.WriteIf(log, LogType.Info, $"GetUSBDisk --> (!usb && !scsi) return null", null, ("Enumerator", enumerator));
                return null;
            }

            if (_genericDriverNames.Skip(Array.IndexOf(_genericDriverNames, "SD")).Contains(enumerator, StringComparer.OrdinalIgnoreCase))
            {
                Log.WriteIf(log, LogType.Info, $@"GetUSBDisk --> (_genericDriverNames.Skip(Array.IndexOf(_genericDriverNames, ""SD"")).Contains(enumerator, StringComparer.OrdinalIgnoreCase)) card = true", null, ("Enumerator", enumerator));
                card = true;
            }

            // Array.IndexOf is zero based, so we only need to subtract one
            if (_usbDriverNames.Skip(1).Take(Array.IndexOf(_usbDriverNames, "UASPSTOR") - 1).Contains(enumerator, StringComparer.OrdinalIgnoreCase))
            {
                Log.WriteIf(log, LogType.Info, $@"GetUSBDisk --> (_usbDriverNames.Skip(1).Take(Array.IndexOf(_usbDriverNames, ""UASPSTOR"") - 1).Contains(enumerator, StringComparer.OrdinalIgnoreCase)) card = true", null, ("Enumerator", enumerator));
                card = true;
            }


            if (Win32.SetupDiGetDeviceRegistryProperty(diskInterface, ref infoData, (uint)Win32.SPDRP.SPDRP_HARDWAREID,
                    out regDataType, IntPtr.Zero, 0, out size))
                throw new Exception("SetupDiGetDeviceRegistryProperty HARDWAREID size check failed.");

            buffer = Marshal.AllocHGlobal((Int32)size);

            if (!Win32.SetupDiGetDeviceRegistryProperty(diskInterface, ref infoData, (uint)Win32.SPDRP.SPDRP_HARDWAREID,
                    out regDataType, buffer, size, out size))
                throw new Exception("SetupDiGetDeviceRegistryProperty HARDWAREID get failed.");

            var hwid = Marshal.PtrToStringAuto(buffer)!;
            var vhdHwids = new[]
            {
                "VMware__VMware_Virtual_S",
                "Arsenal_________Virtual_",
                "KernSafeVirtual_________",
                "Msft____Virtual_Disk____",
            };
            if (vhdHwids.Any(x => hwid.Contains(x)))
            {
                Log.WriteIf(log, LogType.Info, $"GetUSBDisk --> (vhdHwids.Any(x => hwid.Contains(x))) vhd = true", null, ("HardwareID", hwid));
                vhd = true;
            }


            if (!card && hwid.StartsWith(@"SCSI\Disk") &&
                _scsiCardNames.Any(x => hwid.Contains(x + "_") || hwid.Contains(x + "&")))
            {
                Log.WriteIf(log, LogType.Info, $@"GetUSBDisk --> (!card && hwid.StartsWith(@""SCSI\Disk"") && _scsiCardNames.Any(x => hwid.Contains(x + ""_"") || hwid.Contains(x + ""&""))) card = true", null, ("HardwareID", hwid));
                card = true;
            }


            if (Win32.SetupDiGetDeviceRegistryProperty(diskInterface, ref infoData, (uint)0x1f, out regDataType, IntPtr.Zero,
                    0, out size))
                throw new Exception("SetupDiGetDeviceRegistryProperty REMOVABLE size check failed.");

            buffer = Marshal.AllocHGlobal((Int32)size);

            if (!Win32.SetupDiGetDeviceRegistryProperty(diskInterface, ref infoData, (uint)0x1f, out regDataType, buffer,
                    size, out size))
                throw new Exception("SetupDiGetDeviceRegistryProperty REMOVABLE get failed.");


            removable = new[] { 2, 3 }.Contains(Marshal.ReadInt32(buffer));
            if (!removable)
            {
                Log.WriteIf(log, LogType.Info, $"GetUSBDisk --> (!removable) return null", null, ("Removable Bit", Marshal.ReadInt32(buffer)));
                return null;
            }


            /* TODO: Investigate if needed
            if (!SetupDiGetDeviceInstanceId(diskInterface, ref infoData, new StringBuilder(), 0, out size))
            {
                var instanceBuffer = new StringBuilder((int)size);
                SetupDiGetDeviceInstanceId(diskInterface, ref infoData, instanceBuffer, size, out size);

                instanceId = instanceBuffer.ToString();
            }
            */

            UsbDevice matchingDevice = null;
            var friendlyName = "USB Storage Device (Generic)";
            if (!Win32.SetupDiGetDeviceRegistryProperty(diskInterface, ref infoData, (uint)Win32.SPDRP.SPDRP_FRIENDLYNAME,
                    out regDataType, IntPtr.Zero, 0, out size))
            {
                Log.WriteIf(log, LogType.Info, "GetUSBDisk --> SetupDiGetDeviceRegistryProperty 1 True");
                buffer = Marshal.AllocHGlobal((Int32)size);

                if (Win32.SetupDiGetDeviceRegistryProperty(diskInterface, ref infoData, (uint)Win32.SPDRP.SPDRP_FRIENDLYNAME,
                        out regDataType, buffer, size, out size))
                {
                    Log.WriteIf(log, LogType.Info, "GetUSBDisk --> SetupDiGetDeviceRegistryProperty 2 True");
                    friendlyName = Marshal.PtrToStringAuto(buffer);
                    Log.WriteIf(log, LogType.Info, $"GetUSBDisk Friendly Name \"{friendlyName}\" Found");

                    foreach (var device in deviceList)
                    {
                        uint usbInst = 0;
                        if (Win32.CM_Locate_DevNodeA(ref usbInst, device.DeviceID) != Win32.CR_SUCCESS)
                        {
                            Log.WriteIf(log, LogType.Info, "GetUSBDisk --> CM_Locate_DevNodeA False", null, ("DeviceID", device.DeviceID));
                            continue;
                        }
                        if (Win32.CM_Get_Child(out uint diskInst, usbInst) != Win32.CR_SUCCESS) {
                            Log.WriteIf(log, LogType.Info, "GetUSBDisk --> CM_Get_Child False", null, ("DeviceID", device.DeviceID));
                            continue;
                        }


                        if (diskInst != infoData.DevInst)
                        {
                            while (Win32.CM_Get_Sibling(out diskInst, diskInst) == Win32.CR_SUCCESS)
                            {
                                Log.WriteIf(log, LogType.Info, "GetUSBDisk --> CM_Get_Sibling Loop", null, ("DeviceID", device.DeviceID));
                                if (diskInst == infoData.DevInst)
                                {
                                    break;
                                }
                            }

                            if (diskInst != infoData.DevInst)
                            {
                                Log.WriteIf(log, LogType.Info, "GetUSBDisk --> (diskInst != infoData.DevInst) continue", null, ("DeviceID", device.DeviceID));
                                continue;
                            }
                        }

                        if (_usbDriverNames.Skip(Array.IndexOf(_usbDriverNames, "UASPSTOR")).Contains(device.Service!))
                        {
                            Log.WriteIf(log, LogType.Info, $@"GetUSBDisk --> (_usbDriverNames.Skip(Array.IndexOf(_usbDriverNames, ""UASPSTOR"")).Contains(device.Service!)) uasp = true", null, ("Service", device.Service), ("DeviceID", device.DeviceID));
                            uasp = true;
                        }

                        try
                        {
                            var vid_pid = device.DeviceID.Split('\\')[1].Split('&');
                            UInt16.TryParse(vid_pid[0].Split('_')[1], NumberStyles.AllowHexSpecifier,
                                CultureInfo.InvariantCulture, out vid);
                            UInt16.TryParse(vid_pid[1].Split('_')[1], NumberStyles.AllowHexSpecifier,
                                CultureInfo.InvariantCulture, out pid);
                        }
                        catch (Exception e)
                        {
                            Log.WriteExceptionSafe(LogType.Warning, e, $"Failed to parse device ID {device.DeviceID}");
                        }
                        
                        Log.WriteIf(log, LogType.Info, $@"GetUSBDisk Device Match Found for ""{friendlyName}""", null, ("DeviceID", device.DeviceID));

                        matchingDevice = device;
                        break;
                    }
                } else
                    Log.WriteIf(log, LogType.Info, "GetUSBDisk --> SetupDiGetDeviceRegistryProperty 2 False");
            } else
                Log.WriteIf(log, LogType.Info, "GetUSBDisk --> SetupDiGetDeviceRegistryProperty 1 False");


            if (!vhd && !((vid + pid == 0 || !usb) && card) && ((vid + pid == 0 && !usb) ||
                    (vid == 0x0525 && pid == 0x622b) ||
                    (vid == 0x0781 && pid == 0x75a0) ||
                    (vid == 0x10d6 && pid == 0x1101)))
            {
                Log.WriteIf(log, LogType.Info, $"GetUSBDisk --> ({{USB Conditions}}) return null", null, ("VHD", vhd), ("USB", usb), ("CARD", card), ("VID", vid), ("PID", pid));
                return null;
            }

            if (uasp)
                friendlyName = friendlyName.Replace("SCSI Disk Device", "UAS Device");

            var diskGUID = new Guid(Win32.GUID_DEVINTERFACE_DISK);

            var interfaceData = new Win32.SP_DEVICE_INTERFACE_DATA();
            interfaceData.cbSize = Marshal.SizeOf(interfaceData);

            for (uint i = 0;
                 Win32.SetupDiEnumDeviceInterfaces(diskInterface, ref infoData, ref diskGUID, i, ref interfaceData);
                 i++)
            {
                Log.WriteIf(log, LogType.Info, "GetUSBDisk --> SetupDiEnumDeviceInterfaces Loop", null, ("Count", i), ("FriendlyName", friendlyName));
                
                Win32.SP_DEVICE_INTERFACE_DETAIL_DATA interfaceDetails = new Win32.SP_DEVICE_INTERFACE_DETAIL_DATA();

                if (IntPtr.Size == 8) //64 bit
                    interfaceDetails.cbSize = 8;
                else //32 bit
                    interfaceDetails.cbSize = 4 + Marshal.SystemDefaultCharSize;

                if (Win32.SetupDiGetDeviceInterfaceDetail(diskInterface, ref interfaceData, IntPtr.Zero, 0, ref size,
                        IntPtr.Zero))
                {
                    Log.WriteIf(log, LogType.Info, "GetUSBDisk --> SetupDiGetDeviceInterfaceDetail False 1", null, ("Count", i), ("FriendlyName", friendlyName));
                    continue;
                }
                //throw new Exception("SetupDiGetDeviceInterfaceDetail disk interface size check failed.");

                if (!Win32.SetupDiGetDeviceInterfaceDetail(diskInterface, ref interfaceData, ref interfaceDetails, size,
                        ref size, IntPtr.Zero))
                {
                    Log.WriteIf(log, LogType.Info, "GetUSBDisk --> SetupDiGetDeviceInterfaceDetail False 2", null, ("Count", i), ("FriendlyName", friendlyName));
                    continue;
                }

                uint diskIndex = 0;

                Win32.STORAGE_DEVICE_NUMBER DeviceNumber = new Win32.STORAGE_DEVICE_NUMBER();
                Win32.VOLUME_DISK_EXTENTS DiskExtents = new Win32.VOLUME_DISK_EXTENTS();

                var hDrive = Win32.CreateFile(interfaceDetails.DevicePath,
                    Win32.FileAccess.GenericRead, Win32.FileShare.Read | Win32.FileShare.Write, IntPtr.Zero,
                    Win32.FileMode.OpenExisting, Win32.FileAttributes.Normal, IntPtr.Zero);
                if (hDrive.DangerousGetHandle() == Win32.INVALID_HANDLE_VALUE)
                    throw new Exception("CreateFile invalid disk handle.");

                var outBufferSize = (UInt32)Marshal.SizeOf(DiskExtents);
                var outBuffer = Marshal.AllocHGlobal((int)outBufferSize);
                if (!Win32.DeviceIoControl(hDrive, Win32.IoCtl.VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0, outBuffer,
                        outBufferSize, out size, IntPtr.Zero))
                {
                    Log.WriteIf(log, LogType.Info, "GetUSBDisk --> DeviceIoControl VOLUME_GET_VOLUME_DISK_EXTENTS 1 False", null, ("Count", i), ("FriendlyName", friendlyName));
                    outBufferSize = (UInt32)Marshal.SizeOf(DeviceNumber);
                    outBuffer = Marshal.AllocHGlobal((int)outBufferSize);
                    if (!Win32.DeviceIoControl(hDrive, Win32.IoCtl.STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, outBuffer,
                            outBufferSize, out size, IntPtr.Zero))
                    {
                        Log.WriteIf(log, LogType.Info, "GetUSBDisk --> DeviceIoControl STORAGE_GET_DEVICE_NUMBER 1 False", null, ("Count", i), ("FriendlyName", friendlyName));
                        hDrive.Dispose();
                        continue;
                        throw new Exception("DeviceIoControl STORAGE_GET_DEVICE_NUMBER failed: " +
                            Marshal.GetLastWin32Error());
                    }

                    Marshal.PtrToStructure(outBuffer, DeviceNumber);
                    diskIndex = DeviceNumber.DeviceNumber;
                }
                else
                {
                    Log.WriteIf(log, LogType.Info, "GetUSBDisk --> DeviceIoControl VOLUME_GET_VOLUME_DISK_EXTENTS 1 True", null, ("Count", i), ("FriendlyName", friendlyName));
                    Marshal.PtrToStructure(outBuffer, DiskExtents);
                    if (DiskExtents.NumberOfDiskExtents >= 2)
                    {
                        Log.WriteIf(log, LogType.Info, "GetUSBDisk --> (DiskExtents.NumberOfDiskExtents >= 2) continue 1", null, ("NumberOfDiskExtents", DiskExtents.NumberOfDiskExtents), ("Count", i), ("FriendlyName", friendlyName));
                        hDrive.Dispose();
                        continue;
                    }

                    diskIndex = DiskExtents.Extents.DiskNumber;
                }

                if (diskIndex == 0)
                {
                    Log.WriteIf(log, LogType.Info, "GetUSBDisk --> (diskIndex == 0) continue", null, ("Count", i), ("FriendlyName", friendlyName));
                    hDrive.Dispose();
                    continue;
                }


                var geometry = new Win32.DISK_GEOMETRY_EX();

                outBufferSize = (UInt32)Marshal.SizeOf(geometry);
                outBuffer = Marshal.AllocHGlobal((int)outBufferSize);
                if (!Win32.DeviceIoControl(hDrive, Win32.IoCtl.DISK_GET_DRIVE_GEOMETRY_EX, IntPtr.Zero, 0, outBuffer,
                        outBufferSize, out size, IntPtr.Zero))
                {
                    Log.WriteIf(log, LogType.Info, "GetUSBDisk --> DeviceIoControl DISK_GET_DRIVE_GEOMETRY_EX False", null, ("Count", i), ("FriendlyName", friendlyName));
                    hDrive.Dispose();
                    continue;
                    throw new Exception("DeviceIoControl DISK_GET_DRIVE_GEOMETRY_EX failed: " +
                        Marshal.GetLastWin32Error());
                }

                hDrive.Dispose();

                Marshal.PtrToStructure(outBuffer, geometry);

                var diskSize = geometry.DiskSize.QuadPart;

                // 10 Mb
                if (diskSize / 1000 < 10000)
                    return null;
                var readableDiskSize = HumanReadableDiskSize(diskSize);

                List<System.IO.DriveInfo> logicalDrives = new List<System.IO.DriveInfo>();

                foreach (var logicalDrive in Wrap.ExecuteSafe(() => System.IO.DriveInfo.GetDrives(), Array.Empty<System.IO.DriveInfo>(), true).Value)
                {
                    Log.WriteIf(log, LogType.Info, "GetUSBDisk --> GetDrives Loop", null, ("DriveName", logicalDrive.Name), ("DriveType", logicalDrive.DriveType), ("Count", i), ("FriendlyName", friendlyName));
                    try
                    {
                        if ((logicalDrive.DriveType == DriveType.Network || logicalDrive.DriveType == DriveType.Ram || logicalDrive.DriveType == DriveType.CDRom) ||
                            (scsi && !uasp && !vhd && (logicalDrive.DriveType == DriveType.Fixed)))
                        {
                            Log.WriteIf(log, LogType.Info, @"GetUSBDisk --> ((logicalDrive.DriveType == DriveType.Network || logicalDrive.DriveType == DriveType.Ram || logicalDrive.DriveType == DriveType.CDRom) ||
                            (scsi && !uasp && !vhd && (logicalDrive.DriveType == DriveType.Fixed))) continue", null, ("SCSI", scsi), ("UASP", uasp), ("VHD", vhd), ("DriveName", logicalDrive.Name), ("DriveType", logicalDrive.DriveType), ("Count", i), ("FriendlyName", friendlyName));
                            continue;
                        }

                        var hLogicalDrive = Win32.CreateFile($@"\\.\{logicalDrive.Name.TrimEnd('\\')}",
                            Win32.FileAccess.GenericRead, Win32.FileShare.Read | Win32.FileShare.Write,
                            IntPtr.Zero, Win32.FileMode.OpenExisting, Win32.FileAttributes.Normal, IntPtr.Zero);
                        if (hLogicalDrive.DangerousGetHandle() == Win32.INVALID_HANDLE_VALUE)
                        {
                            Log.WriteIf(log, LogType.Info, "GetUSBDisk --> CreateFile False", null, ("Error", Marshal.GetLastWin32Error()), ("DriveName", logicalDrive.Name), ("DriveType", logicalDrive.DriveType), ("Count", i), ("FriendlyName", friendlyName));
                            hLogicalDrive.Dispose();
                            continue;
                            throw new Exception("CreateFile invalid logical disk handle.");
                        }

                        uint logicalDriveIndex = 0;

                        DiskExtents = new Win32.VOLUME_DISK_EXTENTS();
                        outBufferSize = (UInt32)Marshal.SizeOf(DiskExtents);
                        outBuffer = Marshal.AllocHGlobal((int)outBufferSize);

                        if (!Win32.DeviceIoControl(hLogicalDrive, Win32.IoCtl.VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0,
                                outBuffer, outBufferSize, out size, IntPtr.Zero))
                        {
                            Log.WriteIf(log, LogType.Info, "GetUSBDisk --> DeviceIoControl VOLUME_GET_VOLUME_DISK_EXTENTS 2 False", null, ("DriveName", logicalDrive.Name), ("DriveType", logicalDrive.DriveType), ("Count", i), ("FriendlyName", friendlyName));
                            outBufferSize = (UInt32)Marshal.SizeOf(DeviceNumber);
                            outBuffer = Marshal.AllocHGlobal((int)outBufferSize);
                            if (!Win32.DeviceIoControl(hLogicalDrive, Win32.IoCtl.STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0,
                                    outBuffer, outBufferSize, out size, IntPtr.Zero))
                            {
                                Log.WriteIf(log, LogType.Info, "GetUSBDisk --> DeviceIoControl STORAGE_GET_DEVICE_NUMBER 2 False", null, ("DriveName", logicalDrive.Name), ("DriveType", logicalDrive.DriveType), ("Count", i), ("FriendlyName", friendlyName));
                                hLogicalDrive.Dispose();
                                continue;
                                throw new Exception("DeviceIoControl STORAGE_GET_DEVICE_NUMBER failed: " +
                                    Marshal.GetLastWin32Error());
                            }

                            hLogicalDrive.Dispose();

                            Marshal.PtrToStructure(outBuffer, DeviceNumber);
                            logicalDriveIndex = DeviceNumber.DeviceNumber;
                        }
                        else
                        {
                            Log.WriteIf(log, LogType.Info, "GetUSBDisk --> DeviceIoControl VOLUME_GET_VOLUME_DISK_EXTENTS 2 True", null, ("DriveName", logicalDrive.Name), ("DriveType", logicalDrive.DriveType), ("Count", i), ("FriendlyName", friendlyName));
                            hLogicalDrive.Dispose();

                            Marshal.PtrToStructure(outBuffer, DiskExtents);
                            if (DiskExtents.NumberOfDiskExtents >= 2)
                            {
                                Log.WriteIf(log, LogType.Info, "GetUSBDisk --> (DiskExtents.NumberOfDiskExtents >= 2) continue 2", null, ("NumberOfDiskExtents", DiskExtents.NumberOfDiskExtents), ("DriveName", logicalDrive.Name), ("DriveType", logicalDrive.DriveType), ("Count", i), ("FriendlyName", friendlyName));
                                continue;
                            }
                            
                            Log.WriteIf(log, LogType.Info, $@"GetUSBDisk Disk Number Found for ""{logicalDrive.Name}""", null, ("DiskNumber", DiskExtents.Extents.DiskNumber), ("DriveType", logicalDrive.DriveType), ("Count", i), ("FriendlyName", friendlyName));

                            logicalDriveIndex = DiskExtents.Extents.DiskNumber;
                        }

                        if (diskIndex == logicalDriveIndex)
                        {
                            Log.WriteIf(log, LogType.Info, $@"GetUSBDisk Logical Drive Disk Number ""{logicalDriveIndex}"" Matched with Target Disk ""{diskIndex}""", null, ("DriveName", logicalDrive.Name), ("DriveType", logicalDrive.DriveType), ("Count", i), ("FriendlyName", friendlyName));

                            if (!System.IO.File.Exists(System.IO.Path.Combine(logicalDrive.Name,
                                    @"EFI\Rufus\ntfs_x64.efi")))
                            {
                                logicalDrives.Add(logicalDrive);
                            }
                            else
                            {
                                Log.WriteIf(log, LogType.Info, $@"GetUSBDisk EFI Logical Drive Detected and Skipped", null, ("DriveName", logicalDrive.Name), ("DriveType", logicalDrive.DriveType), ("Count", i), ("FriendlyName", friendlyName));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.WriteExceptionSafe(e);
                    }
                }

                Log.WriteIf(log, LogType.Info, $"GetUSBDisk Returning " + friendlyName.Replace(" USB Device", ""));
                return new UsbDisk()
                {
                    Index = diskIndex,
                    Size = diskSize,
                    ReadableSize = readableDiskSize,
                    FriendlyName = friendlyName.Replace(" USB Device", ""),
                    LogicalDrives = logicalDrives,
                    Card = card,
                    UASP = uasp,
                    VHD = vhd,
                    SCSI = scsi,
                    UsbDevice = matchingDevice,
                    VID = vid,
                    PID = pid,
                };
            }

            Log.WriteIf(log, LogType.Info, "GetUSBDisk No Matching Disk Found", null, ("FriendlyName", friendlyName));
            return null;
        }

        public static string HumanReadableDiskSize(long input)
        {
            double dividedSize = input;
            string suffix = "";
            foreach (var sizeSuffix in new[] { "KB", "MB", "GB", "TB", "PB" })
            {
                dividedSize /= 1000;
                if (dividedSize < 1000)
                {
                    suffix = sizeSuffix;
                    break;
                }
            }

            if (dividedSize < 8)
            {
                var result = (Math.Abs((dividedSize * 10.0) - (Math.Floor(dividedSize + 0.5) * 10.0)) < 0.5)
                    ? Math.Truncate((double)dividedSize)
                    : Math.Truncate((double)(dividedSize * 10)) / 10;
                return result + " " + suffix;
            }
            else
            {
                uint t = (uint)dividedSize;

                t--;
                t |= t >> 1;
                t |= t >> 2;
                t |= t >> 4;
                t |= t >> 8;
                t |= t >> 16;
                t++;

                var result = (Math.Abs(1.0f - (dividedSize / (double)t)) < 0.05f) ? (long)t : (long)dividedSize;
                return result + " " + suffix;
            }
        }

        private static Dictionary<string, object> GetProperties(uint usbDevice,
            KeyValuePair<string, Win32.DEVPROPKEY>[] properties = null)
        {
            var result = new Dictionary<string, object>();

            if (properties != null)
            {
                for (int i = 0; i < properties.Length; i++)
                {
                    var property = properties[i].Value;

                    UInt32 propertyType;
                    UInt32 requiredSize = 0;
                    Win32.CM_Get_DevNode_Property(usbDevice,
                        ref property, out propertyType, IntPtr.Zero, ref requiredSize, 0);

                    var buffer = Marshal.AllocHGlobal((Int32)requiredSize);

                    if (Win32.CM_Get_DevNode_Property(usbDevice,
                            ref property, out propertyType, buffer, ref requiredSize, 0) == 0)
                    {
                        object value;
                        switch (propertyType)
                        {
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_UINT32:
                                value = (UInt32)Marshal.ReadInt32(buffer);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_GUID:
                                value = MarshalEx.ReadGuid(buffer, (Int32)requiredSize);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_FILETIME:
                                value = MarshalEx.ReadFileTime(buffer);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_BOOLEAN:
                                value = Marshal.ReadByte(buffer) != 0;
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_STRING:
                                value = Marshal.PtrToStringUni(buffer);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_SECURITY_DESCRIPTOR:
                                value = MarshalEx.ReadSecurityDescriptor(buffer, (Int32)requiredSize);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_SECURITY_DESCRIPTOR_STRING:
                                value = Marshal.PtrToStringUni(buffer);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_BINARY:
                                value = MarshalEx.ReadByteArray(buffer, (Int32)requiredSize);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_STRING_LIST:
                                value = MarshalEx.ReadMultiSzStringList(buffer, (Int32)requiredSize);
                                break;
                            default:
                                value = "Unknown";
                                break;
                        }

                        result.Add(properties[i].Key, value);

                        Marshal.FreeHGlobal(buffer);
                    }
                    else
                        throw new Exception("Property " + properties[i].Key + " not found for device.");
                }

                return result;
            }

            uint propertyCount = 0;
            Win32.CM_Get_DevNode_Property_Keys(usbDevice, IntPtr.Zero, ref propertyCount, 0);

            var propertyKeys = new Win32.DEVPROPKEY[propertyCount];
            GCHandle propertyKeyArrayPinned = GCHandle.Alloc(propertyKeys, GCHandleType.Pinned);

            IntPtr propertyBuffer = propertyKeyArrayPinned.AddrOfPinnedObject();

            if (Win32.CM_Get_DevNode_Property_Keys(usbDevice, propertyBuffer, ref propertyCount, 0) == 0)
            {
                for (UInt32 propertyKeyIndex = 0; propertyKeyIndex < propertyCount; propertyKeyIndex++)
                {
                    UInt32 propertyType;
                    UInt32 requiredSize = 0;
                    Win32.CM_Get_DevNode_Property(usbDevice,
                        ref propertyKeys[propertyKeyIndex], out propertyType, IntPtr.Zero, ref requiredSize, 0);

                    propertyBuffer = Marshal.AllocHGlobal((Int32)requiredSize);
                    if (Win32.CM_Get_DevNode_Property(usbDevice,
                            ref propertyKeys[propertyKeyIndex], out propertyType, propertyBuffer, ref requiredSize,
                            0) == 0)
                    {
                        string valueName = "";

                        foreach (var field in typeof(Win32.DevicePropertyKeys).GetFields(BindingFlags.Static |
                                     BindingFlags.NonPublic))
                        {
                            Win32.DEVPROPKEY fieldKey = (Win32.DEVPROPKEY)field.GetValue(null);
                            if ((propertyKeys[propertyKeyIndex].Fmtid == fieldKey.Fmtid) &&
                                (propertyKeys[propertyKeyIndex].Pid == fieldKey.Pid))
                            {
                                valueName = field.Name;
                                break;
                            }
                            else
                                valueName = String.Format("{0:B}, {1}", propertyKeys[propertyKeyIndex].Fmtid,
                                    propertyKeys[propertyKeyIndex].Pid);
                        }

                        object value;
                        switch (propertyType)
                        {
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_UINT32:
                                value = (UInt32)Marshal.ReadInt32(propertyBuffer);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_GUID:
                                value = MarshalEx.ReadGuid(propertyBuffer, (Int32)requiredSize);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_FILETIME:
                                value = MarshalEx.ReadFileTime(propertyBuffer);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_BOOLEAN:
                                value = Marshal.ReadByte(propertyBuffer) != 0;
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_STRING:
                                value = Marshal.PtrToStringUni(propertyBuffer);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_SECURITY_DESCRIPTOR:
                                value = MarshalEx.ReadSecurityDescriptor(propertyBuffer, (Int32)requiredSize);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_SECURITY_DESCRIPTOR_STRING:
                                value = Marshal.PtrToStringUni(propertyBuffer);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_BINARY:
                                value = MarshalEx.ReadByteArray(propertyBuffer, (Int32)requiredSize);
                                break;
                            case Win32.DevicePropertyTypes.DEVPROP_TYPE_STRING_LIST:
                                value = MarshalEx.ReadMultiSzStringList(propertyBuffer, (Int32)requiredSize);
                                break;
                            default:
                                value = "Unknown";
                                break;
                        }

                        result.Add(valueName, value);

                        Marshal.FreeHGlobal(propertyBuffer);
                    }
                }
            }

            return result;
        }

        public static void Eject(string deviceId)
        {
            uint usbInst = 0;
            if (Win32.CM_Locate_DevNodeA(ref usbInst, deviceId) != Win32.CR_SUCCESS)
                throw new Exception("Unable to locate USB device for eject.");

            StringBuilder sb = new StringBuilder(1024);
            var result = Win32.CM_Request_Device_Eject(usbInst, out Win32.PNP_VETO_TYPE pVetoType, sb, sb.Capacity, 0);
            if (result != 0)
                throw new Exception($"Unable to request USB device eject ({pVetoType + ": " + sb.ToString()}): " + result);
        }

        public class PositionComparer : IComparer<string>
        {
            private ArrayList Keys { get; set; }

            public PositionComparer(ArrayList keys)
            {
                Keys = keys;
            }

            public int Compare(string s1, string s2)
            {
                return Keys.IndexOf(s1).CompareTo(Keys.IndexOf(s2));
            }
        }

        private static class MarshalEx
        {
            internal static Byte[] ReadByteArray(IntPtr source, Int32 startIndex, Int32 length)
            {
                Byte[] byteArray = new Byte[length];
                Marshal.Copy(source, byteArray, startIndex, length);
                return byteArray;
            }

            internal static Byte[] ReadByteArray(IntPtr source, Int32 length)
            {
                return ReadByteArray(source, 0, length);
            }

            internal static Guid ReadGuid(IntPtr source, Int32 length)
            {
                Byte[] byteArray = ReadByteArray(source, 0, length);
                return new Guid(byteArray);
            }

            internal static String[] ReadMultiSzStringList(IntPtr source, Int32 length)
            {
                Byte[] byteArray = ReadByteArray(source, 0, length);
                String multiSz = Encoding.Unicode.GetString(byteArray, 0, length);

                List<String> strings = new List<String>();

                Int32 start = 0;
                Int32 end = multiSz.IndexOf('\0', start);

                while (end > start)
                {
                    strings.Add(multiSz.Substring(start, end - start));

                    start = end + 1;
                    end = multiSz.IndexOf('\0', start);
                }

                return strings.ToArray();
            }

            internal static DateTime ReadFileTime(IntPtr source)
            {
                Int64 fileTime = Marshal.ReadInt64(source);
                return DateTime.FromFileTimeUtc(fileTime);
            }

            internal static RawSecurityDescriptor ReadSecurityDescriptor(IntPtr source, Int32 length)
            {
                Byte[] byteArray = ReadByteArray(source, 0, length);
                return new RawSecurityDescriptor(byteArray, 0);
            }
        }


        public class NotificationContext
        {
            private CM_NOTIFY_CALLBACK _callback;
            
            public void Register(CM_NOTIFY_CALLBACK callback)
            {
                _callback = callback;
                var usbFilter = new CM_NOTIFY_FILTER
                {
                    Flags = CM_NOTIFY_FILTER_FLAGS.None,
                    FilterType = CM_NOTIFY_FILTER_TYPE.CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE,
                    u = new CM_NOTIFY_FILTER.UNION()
                        { DeviceInterface_ClassGuid = new Guid("{A5DCBF10-6530-11D2-901F-00C04FB951ED}") },
                    cbSize = (uint)Marshal.SizeOf<CM_NOTIFY_FILTER>(),
                };

                CM_Register_Notification(usbFilter, IntPtr.Zero, _callback, out _contextHandle);
            }


            private IntPtr _contextHandle;

            public void Unregister()
            {
                if (_contextHandle != IntPtr.Zero)
                {
                    CM_Unregister_Notification(_contextHandle);
                    _contextHandle = IntPtr.Zero;
                    _callback = null;
                }
            }

            ~NotificationContext()
            {
                Unregister();
            }

            [DllImport("CfgMgr32.dll")]
            private static extern int CM_Register_Notification(
                CM_NOTIFY_FILTER pFilter,
                IntPtr pContext,
                CM_NOTIFY_CALLBACK pCallback,
                [Out] out IntPtr pNotifyContext
            );

            [DllImport("CfgMgr32.dll")]
            private static extern int CM_Unregister_Notification(IntPtr pContext);

            const int MAX_DEVICE_ID_LEN = 200;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
            internal struct CM_NOTIFY_FILTER
            {
                internal uint cbSize;
                internal CM_NOTIFY_FILTER_FLAGS Flags;
                internal CM_NOTIFY_FILTER_TYPE FilterType;
                internal uint Reserved;
                internal UNION u;

                [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
                public unsafe struct UNION
                {
                    /// <summary>The GUID of the device interface class for which to receive notifications.</summary>
                    [FieldOffset(0)]
                    public Guid DeviceInterface_ClassGuid;

                    /// <summary>A handle to the device for which to receive notifications.</summary>
                    [FieldOffset(0)]
                    public IntPtr DeviceHandle_hTarget;

                    [FieldOffset(0)]
                    private fixed char iid[MAX_DEVICE_ID_LEN];

                    /// <summary>The device instance ID for the device for which to receive notifications.</summary>
                    public string DeviceInstance_InstanceId
                    {
                        get
                        {
                            fixed (char* p = iid)
                                return new string(p);
                        }
                        set
                        {
                            if (value is null) throw new ArgumentNullException(nameof(DeviceInstance_InstanceId));
                            if (value.Length >= MAX_DEVICE_ID_LEN) throw new ArgumentException($"String length exceeds maximum of {MAX_DEVICE_ID_LEN - 1} characters.", nameof(DeviceInstance_InstanceId));
                            for (int i = 0; i < value.Length; i++)
                                iid[i] = value[i];
                            iid[value.Length] = '\0';
                        }
                    }
                }
            }

            [Flags]
            internal enum CM_NOTIFY_FILTER_FLAGS
            {
                None = 0,
                CM_NOTIFY_FILTER_FLAG_ALL_INTERFACE_CLASSES = 0x00000001,
                CM_NOTIFY_FILTER_FLAG_ALL_DEVICE_INSTANCES = 0x00000002
            }

            internal enum CM_NOTIFY_FILTER_TYPE
            {
                CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE = 0,
                CM_NOTIFY_FILTER_TYPE_DEVICEHANDLE,
                CM_NOTIFY_FILTER_TYPE_DEVICEINSTANCE,
            }

            public delegate int CM_NOTIFY_CALLBACK(
                IntPtr hNotify,
                IntPtr Context,
                CM_NOTIFY_ACTION Action,
                IntPtr EventData,
                int EventDataSize
            );

            public struct CM_NOTIFY_EVENT_DATA
            {
                internal CM_NOTIFY_FILTER_TYPE FilterType;

                int Reserved;

                // union
                Guid ClassOrEventGuid;
                int NameOffset;

                int DataSize;
                // more data added after struct
            }

            public enum CM_NOTIFY_ACTION
            {
                /* Filter type: CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE */

                CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL = 0,
                CM_NOTIFY_ACTION_DEVICEINTERFACEREMOVAL,

                /* Filter type: CM_NOTIFY_FILTER_TYPE_DEVICEHANDLE */

                CM_NOTIFY_ACTION_DEVICEQUERYREMOVE,
                CM_NOTIFY_ACTION_DEVICEQUERYREMOVEFAILED,
                CM_NOTIFY_ACTION_DEVICEREMOVEPENDING,
                CM_NOTIFY_ACTION_DEVICEREMOVECOMPLETE,
                CM_NOTIFY_ACTION_DEVICECUSTOMEVENT,

                /* Filter type: CM_NOTIFY_FILTER_TYPE_DEVICEINSTANCE */

                CM_NOTIFY_ACTION_DEVICEINSTANCEENUMERATED,
                CM_NOTIFY_ACTION_DEVICEINSTANCESTARTED,
                CM_NOTIFY_ACTION_DEVICEINSTANCEREMOVED,
            }
        }
    }
}
