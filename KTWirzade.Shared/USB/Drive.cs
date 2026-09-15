using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Core;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using static iso_mode.Win32;
using FileMode = System.IO.FileMode;
using FileShare = System.IO.FileShare;

namespace iso_mode
{
    internal class Drive
    {
        internal class VolumeNameAndLetter
        {
            internal string VolumeName = null;
            internal char? Letter = null;
        }

        private static char GetNextAvailableDriveLetter()
        {
            var letters = new List<char>() { 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'A', 'B' };

            foreach (var driveInfo in DriveInfo.GetDrives())
            {
                letters.Remove(driveInfo.Name[0]);
            }

            foreach (var userKey in Registry.Users.GetSubKeyNames())
            {
                var networkKey = Registry.Users.OpenSubKey(userKey + @"\Network");
                if (networkKey == null)
                    continue;

                foreach (var networkLetter in networkKey.GetSubKeyNames().Where(x => x.Length == 1))
                {
                    if (letters.Count > 1)
                        letters.Remove(networkLetter[0]);
                }
            }

            return letters.First();
        }

        private static MEDIA_TYPE _mediaType = 0;


        //Returns true if drive Index is successfully created
        //Returns false if not created successfully
        internal static bool CreatePartition(uint driveIndex, long size, long secondsize = 0)
        {
            SafeFileHandle handle = GetDriveHandle(driveIndex);

            if (handle.DangerousGetHandle() == INVALID_HANDLE_VALUE)
            {
                return false;
            }

            uint returned;
            //Console.WriteLine("FSCTL_LOCK_VOLUME: " + DeviceIoControl(handle, 0x00090018, IntPtr.Zero, 0, IntPtr.Zero,
            //0, out uint returned, IntPtr.Zero));
            //Console.WriteLine("FSCTL_DISMOUNT_VOLUME: " + DeviceIoControl(handle, IoCtl.FSCTL_DISMOUNT_VOLUME,
            //IntPtr.Zero, 0, IntPtr.Zero, 0, out returned, IntPtr.Zero));
            Console.WriteLine("DISK_DELETE_DRIVE_LAYOUT: " + DeviceIoControl(handle, IoCtl.DISK_DELETE_DRIVE_LAYOUT,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out returned, IntPtr.Zero));
            Console.WriteLine("DISK_UPDATE_PROPERTIES: " + DeviceIoControl(handle, IoCtl.DISK_UPDATE_PROPERTIES,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out returned, IntPtr.Zero));

            //Step 2: IoCtl.DISK_GET_DRIVE_GEOMETRY_EX to get the physical disk's geometry ( we need some information in it to fill partition data)
            //The number of surfaces (or heads, which is the same thing), cylinders, and sectors vary a lot; the specification of the number of each is called the geometry of a hard disk. 
            //The geometry is usually stored in a special, battery-powered memory location called the CMOS RAM , from where the operating system can fetch it during bootup or driver initialization.

            var geometry = GetGeometry(handle);

            //Step 3: IoCtl.DISK_CREATE_DISK is used to initialize a disk with an empty partition table. 
            CREATE_DISK createDisk = new CREATE_DISK();
            createDisk.PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_MBR;
            createDisk.Mbr.Signature = 0x80;


            using (var createDiskBuffer = new SafeHGlobalHandle(Marshal.AllocHGlobal(Marshal.SizeOf(typeof(CREATE_DISK)))))
            {
                Marshal.StructureToPtr(createDisk, createDiskBuffer.DangerousGetHandle(), false);

                FillMemory(createDiskBuffer.DangerousGetHandle(), (uint)Marshal.SizeOf(typeof(CREATE_DISK)), 0);

                byte[] arr1 = new byte[Marshal.SizeOf(typeof(CREATE_DISK))];
                Marshal.Copy(createDiskBuffer.DangerousGetHandle(), arr1, 0, Marshal.SizeOf(typeof(CREATE_DISK)));

                DeviceIoControl(handle, IoCtl.DISK_CREATE_DISK, createDiskBuffer.DangerousGetHandle(), (uint)Marshal.SizeOf(typeof(CREATE_DISK)),
                    IntPtr.Zero, 0, out _, IntPtr.Zero);
            }

            DeviceIoControl(handle, IoCtl.DISK_UPDATE_PROPERTIES, IntPtr.Zero, 0, IntPtr.Zero, 0, out returned,
                IntPtr.Zero);


            //Step 4: IoCtl.DISK_SET_DRIVE_LAYOUT_EX to repartition a disk as specified.
            //Note: use IoCtl.DISK_UPDATE_PROPERTIES to synchronize system view after IoCtl.DISK_CREATE_DISK and IoCtl.DISK_SET_DRIVE_LAYOUT_EX
            /* DWORD driveLayoutSize = sizeof(DRIVE_LAYOUT_INFORMATION_EX) + sizeof(PARTITION_INFORMATION_EX) * 4 * 25;
             DRIVE_LAYOUT_INFORMATION_EX *DriveLayoutEx = (DRIVE_LAYOUT_INFORMATION_EX *) new BYTE[driveLayoutSize];*/

            Console.WriteLine(Marshal.SizeOf(typeof(DRIVE_LAYOUT_INFORMATION_EX)));
            var layoutSize = Marshal.SizeOf(typeof(DRIVE_LAYOUT_INFORMATION_EX));

            //var driveLayoutBuffer = Marshal.AllocHGlobal(layoutSize + 4);

            //FillMemory(driveLayoutBuffer, (uint)layoutSize * 4, 0);


            DRIVE_LAYOUT_INFORMATION_EX driveLayoutEx = new DRIVE_LAYOUT_INFORMATION_EX();
            int pn = 0;
            driveLayoutEx.PartitionEntry = new PARTITION_INFORMATION_EX[4];


            _mediaType = (MEDIA_TYPE)geometry.Geometry.MediaType;
            Int64 bytes_per_track = (geometry.Geometry.SectorsPerTrack) * (geometry.Geometry.BytesPerSector);

            Console.WriteLine("MediaType: '" + _mediaType + "'");
            Console.WriteLine("SectorsPerTrack: '" + geometry.Geometry.SectorsPerTrack + "'");
            Console.WriteLine("BytesPerSector: '" + geometry.Geometry.BytesPerSector + "'");
            Console.WriteLine("SectorsPerTrack * BytesPerSector: '" + bytes_per_track + "'");
            Console.WriteLine("" + "(^ bytes_per_track)" + "");


            driveLayoutEx.PartitionEntry[pn].StartingOffset = bytes_per_track;

            Console.WriteLine("\n0x123 Offset UINT: '" + driveLayoutEx.PartitionEntry[pn].StartingOffset + "'");

            Int64 main_part_size_in_sectors, extra_part_size_in_sectors = 0;
            main_part_size_in_sectors = (geometry.DiskSize.QuadPart - driveLayoutEx.PartitionEntry[pn].StartingOffset) /
                                        geometry.Geometry.BytesPerSector;

            Console.WriteLine("\nDiskSize: '" + geometry.DiskSize.QuadPart + "'");

            Console.WriteLine("DiskSize - Offset: '" +
                              (geometry.DiskSize.QuadPart - driveLayoutEx.PartitionEntry[pn].StartingOffset) + "'");
            Console.WriteLine("DiskSize - Offset / BytesPerSector: '" + main_part_size_in_sectors + "'");
            Console.WriteLine("" + "(^ main_part_size_in_sectors)" + "");

            if (main_part_size_in_sectors <= 0)
            {
                return false;
            }

            extra_part_size_in_sectors = (MIN_EXTRA_PART_SIZE + bytes_per_track - 1) / bytes_per_track;

            Console.WriteLine("\nMIN_EXTRA_PART_SIZE: '" + MIN_EXTRA_PART_SIZE + "'");

            Console.WriteLine("MIN_EXTRA + BytesPerTrack - 1 / BytesPerTrack: '" + extra_part_size_in_sectors + "'");
            Console.WriteLine("" + "(^ extra_part_size_in_sectors)" + "");


            main_part_size_in_sectors = ((main_part_size_in_sectors / geometry.Geometry.SectorsPerTrack) -
                                         extra_part_size_in_sectors) * geometry.Geometry.SectorsPerTrack;

            Console.WriteLine("\n((main_part_size / SectorsPerTrack) - extra_part_size) * SectorsPerTrack: '" +
                              main_part_size_in_sectors + "'");
            Console.WriteLine("" + "(^ main_part_size_in_sectors (overwrite))" + "");


            if (main_part_size_in_sectors <= 0)
            {
                return false;
            }

            long partlength = (uint)Math.Ceiling((double)(size + (size / 10)) / 4096);
            Console.WriteLine("PartLength: " + partlength);
            Console.WriteLine("Size: " + size);
            Console.WriteLine("Size 4096: " + (partlength * 4096));

            partlength = partlength * 4096;
            while (partlength % bytes_per_track != 0)
            {
                partlength++;
            }

            Console.WriteLine(partlength);
            Console.WriteLine("DiskSize Divided Stats: " + $"{geometry.DiskSize.QuadPart} / {bytes_per_track}");
            Console.WriteLine("DiskSize Divided: " + geometry.DiskSize.QuadPart / bytes_per_track);
            Console.WriteLine("DiskSize Divided Stats 2: " + $"({geometry.DiskSize.QuadPart} - {bytes_per_track} - {partlength}) / {bytes_per_track}");
            Console.WriteLine("DiskSize Divided 2: " + (geometry.DiskSize.QuadPart - bytes_per_track - partlength) / bytes_per_track);

            driveLayoutEx.PartitionEntry[pn].PartitionLength = ((partlength));
            driveLayoutEx.PartitionEntry[pn].PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_MBR;
            driveLayoutEx.PartitionEntry[pn].Mbr.BootIndicator = true;
            driveLayoutEx.PartitionEntry[pn].Mbr.PartitionType = 0x07;


            driveLayoutEx.PartitionEntry[pn].PartitionNumber = (uint)pn + 1;
            driveLayoutEx.PartitionEntry[pn].PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_MBR;
            driveLayoutEx.PartitionEntry[pn].RewritePartition = true;
            driveLayoutEx.PartitionEntry[pn].Mbr.HiddenSectors =
                (uint)(bytes_per_track / geometry.Geometry.BytesPerSector);
            driveLayoutEx.PartitionEntry[pn].Mbr.RecognizedPartition = true;

            var partlength2 = (geometry.DiskSize.QuadPart / 2) - bytes_per_track - partlength;
            if (partlength2 - bytes_per_track >= 10000000)
            {
                pn++;

                // Set the optional extra partition
                // Should end on a track boundary
                while (partlength2 % bytes_per_track != 0)
                {
                    partlength2--;
                }

                driveLayoutEx.PartitionEntry[pn].StartingOffset = bytes_per_track + partlength;
                driveLayoutEx.PartitionEntry[pn].PartitionLength = partlength2; //TODO: Has to change
                driveLayoutEx.PartitionEntry[pn].Mbr.PartitionType = 0x07;

                driveLayoutEx.PartitionEntry[pn].PartitionNumber = (uint)pn + 1;
                driveLayoutEx.PartitionEntry[pn].PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_MBR;
                driveLayoutEx.PartitionEntry[pn].RewritePartition = true;
                driveLayoutEx.PartitionEntry[pn].Mbr.HiddenSectors =
                    (uint)(bytes_per_track / geometry.Geometry.BytesPerSector);
                driveLayoutEx.PartitionEntry[pn].Mbr.RecognizedPartition = true;
            }

            driveLayoutEx.PartitionEntry[2].PartitionNumber = 3;
            driveLayoutEx.PartitionEntry[2].PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_MBR;
            driveLayoutEx.PartitionEntry[2].Mbr.PartitionType = 0x00;

            driveLayoutEx.PartitionEntry[3].PartitionNumber = 4;
            driveLayoutEx.PartitionEntry[3].PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_MBR;
            driveLayoutEx.PartitionEntry[3].Mbr.PartitionType = 0x00;


            Console.WriteLine(pn);

            driveLayoutEx.PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_MBR;
            driveLayoutEx.PartitionCount = 4; //It should be a multiple of 4
            driveLayoutEx.Mbr.Mbr.Signature = 0x80;

            var bytes = new byte[layoutSize];
            var layoutHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            ZeroMemory(layoutHandle.AddrOfPinnedObject(), (uint)layoutSize);
            Marshal.StructureToPtr(driveLayoutEx, layoutHandle.AddrOfPinnedObject(), false);

            Console.WriteLine("\nDISK_SET_DRIVE_LAYOUT_EX: " +
                              DeviceIoControl(handle, IoCtl.DISK_SET_DRIVE_LAYOUT_EX, layoutHandle.AddrOfPinnedObject(), 624, IntPtr.Zero, 0, out returned,
                                  IntPtr.Zero) + ": " + Marshal.GetLastWin32Error());
            layoutHandle.Free();

            Console.WriteLine("DISK_UPDATE_PROPERTIES: " +
                              DeviceIoControl(handle, IoCtl.DISK_UPDATE_PROPERTIES, IntPtr.Zero, 0, IntPtr.Zero, 0,
                                  out returned, IntPtr.Zero) + ": " + Marshal.GetLastWin32Error());
            //Console.WriteLine("FSCTL_UNLOCK_VOLUME: " + DeviceIoControl(handle, IoCtl.FSCTL_UNLOCK_VOLUME, IntPtr.Zero,
            //0, IntPtr.Zero, (uint)Marshal.SizeOf(typeof(DISK_GEOMETRY_EX)), out returned, IntPtr.Zero));

            handle.Dispose();

            return true;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetVolumePathNamesForVolumeNameW([MarshalAs(UnmanagedType.LPWStr)] string lpszVolumeName,
            [MarshalAs(UnmanagedType.LPWStr)] [Out]
            StringBuilder lpszVolumeNamePaths, uint cchBuferLength,
            ref UInt32 lpcchReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetVolumeNameForVolumeMountPoint(string
                lpszVolumeMountPoint, [Out] StringBuilder lpszVolumeName,
            uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetVolumeMountPoint(string lpszVolumeMountPoint,
            string lpszVolumeName);

        [DllImport("kernel32.dll")]
        static extern bool DeleteVolumeMountPoint(string lpszVolumeMountPoint);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr FindFirstVolume([Out] StringBuilder lpszVolumeName,
            uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FindNextVolume(IntPtr hFindVolume, [Out] StringBuilder lpszVolumeName, uint cchBufferLength);

        [DllImport("kernel32.dll")]
        internal static extern bool DefineDosDevice(uint dwFlags, string lpDeviceName,
            string lpTargetPath);

        [DllImport("Kernel32.dll")]
        internal static extern uint QueryDosDevice(string lpDeviceName,
            StringBuilder lpTargetPath, uint ucchMax);

        internal const uint DDD_RAW_TARGET_PATH = 0x00000001;
        internal const uint DDD_REMOVE_DEFINITION = 0x00000002;
        internal const uint DDD_EXACT_MATCH_ON_REMOVE = 0x00000004;
        internal const uint DDD_NO_BROADCAST_SYSTEM = 0x00000008;


        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindVolumeClose(IntPtr findVolumeHandle);

        public static VolumeNameAndLetter GetVolumeMount(USB.UsbDisk usb, long partitionOffset,
            bool getDriveLetter = true, bool throwIfNoLetterFound = true)
        {
            
            var result = new VolumeNameAndLetter();
            for (int i = 0; i < 20; i++)
            {
                Thread.Sleep(500);

                var volumeName = new StringBuilder(1024, 1024);
                var volumeHandle = FindFirstVolume(volumeName, 1024);
                do
                {
                    var volume = volumeName.ToString();

                    VOLUME_DISK_EXTENTS diskExtents = null;
                    IntPtr outBuffer = IntPtr.Zero;

                    Wrap.RetryExponential.Execute(() =>
                    {
                        using (var handle = GetHandle(volumeName.ToString().TrimEnd('\\')))
                        {
                            diskExtents = new VOLUME_DISK_EXTENTS();
                            var outBufferSize = (UInt32)Marshal.SizeOf(diskExtents);
                            outBuffer = Marshal.AllocHGlobal((int)outBufferSize);


                            if (!DeviceIoControl(handle, IoCtl.VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0, outBuffer,
                                    outBufferSize, out uint returned, IntPtr.Zero))
                            {
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            }
                        }
                    });

                    Marshal.PtrToStructure(outBuffer, diskExtents);


                    if (diskExtents!.Extents.DiskNumber == 0 || diskExtents.Extents.DiskNumber != usb.Index)
                    {
                        continue;
                    }

                    if (diskExtents.Extents.StartingOffset == partitionOffset)
                    {
                        result.VolumeName = volume;

                        if (!getDriveLetter)
                        {
                            FindVolumeClose(volumeHandle);
                            return result;
                        }

                        var mountPoint = new StringBuilder(1024, 1024);
                        var returnLength = (uint)0;

                        if (GetVolumePathNamesForVolumeNameW(volume, mountPoint, 1024, ref returnLength) &&
                            mountPoint.Length > 0 && VerifyLetterMatches(mountPoint[0], usb, partitionOffset))
                        {
                            result.Letter = mountPoint[0];

                            FindVolumeClose(volumeHandle);
                            return result;
                        }

                        var availableLetter = GetNextAvailableDriveLetter();

                        if (volume.StartsWith(@"\\?\GLOBALROOT", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!DefineDosDevice(DDD_RAW_TARGET_PATH | DDD_NO_BROADCAST_SYSTEM, availableLetter + ":",
                                    volume.Substring(14)))
                                continue;

                            result.Letter = availableLetter;
                            FindVolumeClose(volumeHandle);
                            return result;
                        }

                        if (!SetVolumeMountPoint(availableLetter + @":\", volume))
                        {
                            if (Marshal.GetLastWin32Error() != 145)
                                continue;

                            if (GetVolumePathNamesForVolumeNameW(volume, mountPoint, 1024, ref returnLength) &&
                                mountPoint.Length > 0 && VerifyLetterMatches(mountPoint[0], usb, partitionOffset))
                            {
                                result.Letter = mountPoint[0];
                            }
                        }
                        else
                            result.Letter = availableLetter;

                        FindVolumeClose(volumeHandle);
                        return result;
                    }
                } while (FindNextVolume(volumeHandle, volumeName, 1024));

                FindVolumeClose(volumeHandle);
            }

            if (result.VolumeName == null)
                throw new FileNotFoundException("Volume not found for partition.");

            if (throwIfNoLetterFound)
                throw new FileNotFoundException("Drive letter not found for partition.");

            return result;
        }

        public static void RemoveMounts(USB.UsbDisk usb)
        {
            for (int i = 0; i < 20; i++)
            {
                var volumeName = new StringBuilder(1024, 1024);
                var volumeHandle = FindFirstVolume(volumeName, 1024);
                do
                {
                    var volume = volumeName.ToString();

                    VOLUME_DISK_EXTENTS diskExtents;
                    IntPtr outBuffer;
                    
                    using (var handle = GetHandle(volumeName.ToString().TrimEnd('\\')))
                    {
                        diskExtents = new Win32.VOLUME_DISK_EXTENTS();
                        var outBufferSize = (UInt32)Marshal.SizeOf(diskExtents);
                        outBuffer = Marshal.AllocHGlobal((int)outBufferSize);

                        if (!DeviceIoControl(handle, Win32.IoCtl.VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0, outBuffer,
                                outBufferSize, out uint returned, IntPtr.Zero))
                        {
                            continue;
                        }
                    }

                    Marshal.PtrToStructure(outBuffer, diskExtents);

                    if (diskExtents.Extents.DiskNumber == 0 || diskExtents.Extents.DiskNumber != usb.Index)
                        continue;

                    var mountPoint = new StringBuilder(1024, 1024);
                    var returnLength = (uint)0;

                    if (GetVolumePathNamesForVolumeNameW(volume, mountPoint, 1024, ref returnLength) &&
                        mountPoint.Length > 0 && VerifyLetterMatches(mountPoint[0], usb, 0))
                    {
                        var letter = mountPoint[0];

                        DefineDosDevice(DDD_REMOVE_DEFINITION, letter + ":", null);
                        DeleteVolumeMountPoint(letter + @":\");
                    }
                } while (FindNextVolume(volumeHandle, volumeName, 1024));

                FindVolumeClose(volumeHandle);
            }
        }

        public static SafeFileHandle GetVolumeHandle(USB.UsbDisk usb, long partitionOffset)
        {
            for (int i = 0; i < 20; i++)
            {
                var volumeName = new StringBuilder(1024, 1024);
                var volumeHandle = FindFirstVolume(volumeName, 1024);
                do
                {
                    var volume = volumeName.ToString();

                    var handle = GetHandle(volume.TrimEnd('\\'));
                    bool returnHandle = false;
                    try
                    {
                        var DiskExtents = new VOLUME_DISK_EXTENTS();
                        var outBufferSize = (UInt32)Marshal.SizeOf(DiskExtents);
                        var outBuffer = Marshal.AllocHGlobal((int)outBufferSize);

                        if (!DeviceIoControl(handle, IoCtl.VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0, outBuffer,
                                outBufferSize, out _, IntPtr.Zero))
                        {
                            continue;
                        }
                        

                        Marshal.PtrToStructure(outBuffer, DiskExtents);

                        if (DiskExtents.Extents.DiskNumber == 0 || DiskExtents.Extents.DiskNumber != usb.Index)
                        {
                            continue;
                        }


                        if (DiskExtents.Extents.StartingOffset == partitionOffset)
                        {
                            FindVolumeClose(volumeHandle);

                            //Console.WriteLine("FSCTL_ALLOW_EXTENDED_DASD_IO: " + DeviceIoControl(handle, IoCtl.FSCTL_ALLOW_EXTENDED_DASD_IO,
                            //    IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero));

                            int i2 = 0;
                            
                            while (!DeviceIoControl(handle, IoCtl.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero,
                                       0, out _, IntPtr.Zero))
                            {
                                Thread.Sleep(100);

                                if (i2 > 10)
                                    break;
                                i2++;
                            }

                            returnHandle = true;
                        }
                    }
                    finally
                    {
                        if (!returnHandle)
                            handle.Dispose();
                    }
                    if (returnHandle)
                        return handle;
                } while (FindNextVolume(volumeHandle, volumeName, 1024));

                FindVolumeClose(volumeHandle);
            }

            throw new Exception();
        }

        internal static bool VerifyLetterMatches(char letter, USB.UsbDisk usb, long partitionOffset)
        {
            using var hLogicalDrive = CreateFile($@"\\.\{letter}:",
                Win32.FileAccess.GenericRead | Win32.FileAccess.GenericWrite,
                Win32.FileShare.Read | Win32.FileShare.Write, IntPtr.Zero,
                Win32.FileMode.OpenExisting, Win32.FileAttributes.Normal, IntPtr.Zero);
            if (hLogicalDrive.DangerousGetHandle() == INVALID_HANDLE_VALUE)
                return false;

            var DiskExtents = new Win32.VOLUME_DISK_EXTENTS();
            var outBufferSize = (UInt32)Marshal.SizeOf(DiskExtents);
            var outBuffer = Marshal.AllocHGlobal((int)outBufferSize);

            if (!DeviceIoControl(hLogicalDrive, Win32.IoCtl.VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0, outBuffer,
                    outBufferSize, out uint returned, IntPtr.Zero))
            {
                return false;
            }

            Marshal.PtrToStructure(outBuffer, DiskExtents);
            if (DiskExtents.NumberOfDiskExtents != 1)
                return false;

            if (usb.Index == DiskExtents.Extents.DiskNumber && (partitionOffset == 0 || DiskExtents.Extents.StartingOffset == partitionOffset))
                return true;

            return false;
        }
        /*
        internal static bool VerifyLetterMatches(char letter, USB.UsbDisk usb, long partitionOffset)
        {
            char logicalLetter = ' ';
            foreach (var logicalDrive in System.IO.DriveInfo.GetDrives())
            {
                if ((logicalDrive.DriveType == DriveType.Network || logicalDrive.DriveType == DriveType.Ram || logicalDrive.DriveType == DriveType.CDRom) ||
                    (usb.SCSI && !usb.UASP && !usb.VHD && (logicalDrive.DriveType == DriveType.Fixed)))
                    continue;

                var hLogicalDrive = CreateFile($@"\\.\{logicalDrive.Name.TrimEnd('\\')}",
                    Win32.FileAccess.GenericRead | Win32.FileAccess.GenericWrite, Win32.FileShare.Read | Win32.FileShare.Write, IntPtr.Zero,
                    Win32.FileMode.OpenExisting, Win32.FileAttributes.Normal, IntPtr.Zero);
                if (hLogicalDrive == INVALID_HANDLE_VALUE)
                {
                    hLogicalDrive.Dispose();
                    return false;
                }

                var DiskExtents = new Win32.VOLUME_DISK_EXTENTS();
                var outBufferSize = (UInt32)Marshal.SizeOf(DiskExtents);
                var outBuffer = Marshal.AllocHGlobal((int)outBufferSize);

                if (!DeviceIoControl(hLogicalDrive, Win32.IoCtl.VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0, outBuffer,
                        outBufferSize, out uint returned, IntPtr.Zero))
                    continue;

                Marshal.PtrToStructure(outBuffer, DiskExtents);
                if (DiskExtents.NumberOfDiskExtents != 1)
                    continue;

                if (usb.Index == DiskExtents.Extents.DiskNumber && DiskExtents.Extents.StartingOffset == partitionOffset)
                {
                    return true;
                }
            }

            if (logicalLetter == ' ')
                throw new FileNotFoundException("Could not find volume for partition.");

            return null;
        }
        */

        internal static void PrepareDrive(USB.UsbDisk usb)
        {
            RemoveMounts(usb);


            using (var handle = Wrap.Retry().Execute(() => GetDriveHandle(usb.Index)))
            {
                GetGeometry(handle);
                GetLayout(handle);
            }

            Wrap.HelperRetry().ExecuteSafe(() => Helper.DeletePartitions(usb.Index), true);
        }

        internal static DISK_GEOMETRY_EX GetGeometry(SafeFileHandle handle)
        {
            // IoCtl.DISK_GET_DRIVE_GEOMETRY_EX to get the physical disk's geometry ( we need some information in it to fill partition data)
            //The number of surfaces (or heads, which is the same thing), cylinders, and sectors vary a lot; the specification of the number of each is called the geometry of a hard disk. 
            //The geometry is usually stored in a special, battery-powered memory location called the CMOS RAM , from where the operating system can fetch it during bootup or driver initialization.
            using (var geometryBuffer = new SafeHGlobalHandle(Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DISK_GEOMETRY_EX)))))
            {
                DISK_GEOMETRY_EX geometry = new DISK_GEOMETRY_EX();
                Marshal.StructureToPtr(geometry, geometryBuffer.DangerousGetHandle(), false);
                DeviceIoControl(handle, IoCtl.DISK_GET_DRIVE_GEOMETRY_EX, IntPtr.Zero, 0, geometryBuffer.DangerousGetHandle(),
                    (uint)Marshal.SizeOf(typeof(DISK_GEOMETRY_EX)),
                    out uint returned, IntPtr.Zero);
                return (DISK_GEOMETRY_EX)Marshal.PtrToStructure(geometryBuffer.DangerousGetHandle(), typeof(DISK_GEOMETRY_EX));
            }
        }

        internal static DRIVE_LAYOUT_INFORMATION_EX GetLayout(SafeFileHandle handle)
        {
            using (var layoutBuffer = new SafeHGlobalHandle(Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DRIVE_LAYOUT_INFORMATION_EX)))))
            {
                DRIVE_LAYOUT_INFORMATION_EX layout = new DRIVE_LAYOUT_INFORMATION_EX();
                Marshal.StructureToPtr(layout, layoutBuffer.DangerousGetHandle(), false);
                if (!DeviceIoControl(handle, IoCtl.DISK_GET_DRIVE_LAYOUT_EX, IntPtr.Zero, 0, layoutBuffer.DangerousGetHandle(),
                        (uint)Marshal.SizeOf(typeof(DRIVE_LAYOUT_INFORMATION_EX)),
                        out _, IntPtr.Zero))
                {
                    throw new Exception("DISK_GET_DRIVE_LAYOUT_EX Failed");
                }
                
                
                return (DRIVE_LAYOUT_INFORMATION_EX)Marshal.PtrToStructure(layoutBuffer.DangerousGetHandle(), typeof(DRIVE_LAYOUT_INFORMATION_EX));
            }
        }

        internal static void LockLogicalHandles(USB.UsbDisk usb, SafeFileHandle handle, out List<SafeFileHandle> logicalHandles)
        {
            logicalHandles = new List<SafeFileHandle>();

            var layout = GetLayout(handle);

            foreach (var partitionInformationExe in layout.PartitionEntry.Where(x => x.StartingOffset != 0))
            {
                logicalHandles.Add(Wrap.ExecuteSafe(() =>
                {
                    var logicalHandle = GetVolumeHandle(usb, partitionInformationExe.StartingOffset);                    
                    return logicalHandle;
                }).Value);
            }
        }

        private const long _efiPartitionSize = 1048576;
        public const ulong GPT_BASIC_DATA_ATTRIBUTE_NO_DRIVE_LETTER = 0x8000000000000000;
        public const ulong GPT_BASIC_DATA_ATTRIBUTE_HIDDEN = 0x4000000000000000;
        public const ulong GPT_BASIC_DATA_ATTRIBUTE_READ_ONLY = 0x1000000000000000;
        internal static int CreatePartitionGPT(USB.UsbDisk usb, long size, bool dataPartition)
        {
            Wrap.Retry().ExecuteSafe(() => PrepareDrive(usb), true);
            
            SafeFileHandle handle = Wrap.Retry().Execute(() => GetDriveHandle(usb.Index, Win32.FileAttributes.NoBuffering));
            try
            {
                // Lock any remaining logical handles in case we
                // couldn't delete them all with PrepareDrive
                LockLogicalHandles(usb, handle, out List<SafeFileHandle> logicalHandles);

                var geometry = GetGeometry(handle);

                //IoCtl.DISK_SET_DRIVE_LAYOUT_EX to repartition a disk as specified.
                //
                // Use IoCtl.DISK_UPDATE_PROPERTIES to synchronize system view after IoCtl.DISK_CREATE_DISK and IoCtl.DISK_SET_DRIVE_LAYOUT_EX

                /* DWORD driveLayoutSize = sizeof(DRIVE_LAYOUT_INFORMATION_EX) + sizeof(PARTITION_INFORMATION_EX) * 4 * 25;
                DRIVE_LAYOUT_INFORMATION_EX *DriveLayoutEx = (DRIVE_LAYOUT_INFORMATION_EX *) new BYTE[driveLayoutSize]; */

                var layoutSize = Marshal.SizeOf(typeof(DRIVE_LAYOUT_INFORMATION_EX));

                //var driveLayoutBuffer = Marshal.AllocHGlobal(layoutSize + 4);

                //FillMemory(driveLayoutBuffer, (uint)layoutSize * 4, 0);

                DRIVE_LAYOUT_INFORMATION_EX driveLayoutEx = new DRIVE_LAYOUT_INFORMATION_EX();
                int pn = 0;
                driveLayoutEx.PartitionEntry = new PARTITION_INFORMATION_EX[4];


                _mediaType = (MEDIA_TYPE)geometry.Geometry.MediaType;
                Int64 bytes_per_track = (geometry.Geometry.SectorsPerTrack) * (geometry.Geometry.BytesPerSector);

                driveLayoutEx.PartitionEntry[pn].StartingOffset = bytes_per_track;
                usb.ISOPartitionOffset = bytes_per_track;

                Int64 main_part_size_in_sectors, extra_part_size_in_sectors = 0;
                main_part_size_in_sectors = (geometry.DiskSize.QuadPart - driveLayoutEx.PartitionEntry[pn].StartingOffset) /
                                            geometry.Geometry.BytesPerSector;

                if (main_part_size_in_sectors <= 0)
                {
                    return -1;
                }

                extra_part_size_in_sectors = (MIN_EXTRA_PART_SIZE + bytes_per_track - 1) / bytes_per_track;


                main_part_size_in_sectors = ((main_part_size_in_sectors / geometry.Geometry.SectorsPerTrack) -
                                             extra_part_size_in_sectors) * geometry.Geometry.SectorsPerTrack;


                if (main_part_size_in_sectors <= 0)
                {
                    return -1;
                }

                var availableExcessSize = geometry.DiskSize.QuadPart - bytes_per_track - bytes_per_track - size - (_efiPartitionSize + bytes_per_track);
                var excessSize = dataPartition ? Math.Min(size / 8, availableExcessSize) : availableExcessSize;


                long partlength = (size + (excessSize));
                while (partlength % bytes_per_track != 0)
                {
                    partlength--;
                }

                var isoGuid = Guid.NewGuid();

                driveLayoutEx.PartitionEntry[pn].PartitionLength = ((partlength));
                driveLayoutEx.PartitionEntry[pn].PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_GPT;
                driveLayoutEx.PartitionEntry[pn].Gpt.PartitionType = new Guid("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7");
                driveLayoutEx.PartitionEntry[pn].Gpt.Name = "ISO";
                driveLayoutEx.PartitionEntry[pn].Gpt.PartitionId = isoGuid;

                driveLayoutEx.PartitionEntry[pn].PartitionNumber = (uint)pn + 1;
                driveLayoutEx.PartitionEntry[pn].PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_GPT;
                driveLayoutEx.PartitionEntry[pn].RewritePartition = true;

                var dataGuid = Guid.NewGuid();

                bool dataPartitionSet = false;
                var partlength2 = (geometry.DiskSize.QuadPart - bytes_per_track - bytes_per_track - partlength) - (_efiPartitionSize + bytes_per_track);
                if (dataPartition && partlength2 - bytes_per_track >= 1000000)
                {
                    dataPartitionSet = true;
                    pn++;

                    // Set the optional extra partition
                    // Should end on a track boundary
                    while (partlength2 % bytes_per_track != 0)
                    {
                        partlength2--;
                    }

                    driveLayoutEx.PartitionEntry[pn].StartingOffset = bytes_per_track + partlength;
                    usb.DataPartitionOffset = bytes_per_track + partlength;
                    driveLayoutEx.PartitionEntry[pn].PartitionLength = partlength2;
                    driveLayoutEx.PartitionEntry[pn].PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_GPT;
                    driveLayoutEx.PartitionEntry[pn].Gpt.PartitionType = new Guid("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7");
                    driveLayoutEx.PartitionEntry[pn].Gpt.Name = "Data";
                    driveLayoutEx.PartitionEntry[pn].Gpt.PartitionId = dataGuid;

                    driveLayoutEx.PartitionEntry[pn].PartitionNumber = (uint)pn + 1;
                    driveLayoutEx.PartitionEntry[pn].PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_GPT;
                    driveLayoutEx.PartitionEntry[pn].RewritePartition = true;
                }

                var efiGuid = Guid.NewGuid();
                var efiLength = (_efiPartitionSize + bytes_per_track);
                if (true)
                {
                    pn++;

                    // Set the optional extra partition
                    // Should end on a track boundary
                    while (partlength2 % bytes_per_track != 0)
                    {
                        partlength2--;
                    }

                    driveLayoutEx.PartitionEntry[pn].StartingOffset = dataPartitionSet ? bytes_per_track + partlength + partlength2 : bytes_per_track + partlength;
                    usb.EFIPartitionOffset = dataPartitionSet ? bytes_per_track + partlength + partlength2 : bytes_per_track + partlength;
                    driveLayoutEx.PartitionEntry[pn].PartitionLength = efiLength;
                    driveLayoutEx.PartitionEntry[pn].PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_GPT;
                    driveLayoutEx.PartitionEntry[pn].Gpt.PartitionType = new Guid("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7");
                    //driveLayoutEx.PartitionEntry[pn].Gpt.PartitionType = new Guid("C12A7328-F81F-11D2-BA4B-00A0C93EC93B");
                    driveLayoutEx.PartitionEntry[pn].Gpt.Name = "UEFI:NTFS";
                    driveLayoutEx.PartitionEntry[pn].Gpt.PartitionId = efiGuid;
                    driveLayoutEx.PartitionEntry[pn].Gpt.Attributes = GPT_BASIC_DATA_ATTRIBUTE_READ_ONLY | GPT_BASIC_DATA_ATTRIBUTE_NO_DRIVE_LETTER; // | GPT_BASIC_DATA_ATTRIBUTE_HIDDEN; HIDDEN causes 24H2+ Windows setup error :(

                    driveLayoutEx.PartitionEntry[pn].PartitionNumber = (uint)pn + 1;
                    driveLayoutEx.PartitionEntry[pn].PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_GPT;
                    driveLayoutEx.PartitionEntry[pn].RewritePartition = true;

                    ISO.DD_EFI_ISO(ref handle, usb.Index, usb.EFIPartitionOffset);
                }


                driveLayoutEx.PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_GPT;
                driveLayoutEx.PartitionCount = dataPartition ? (uint)3 : 2;


                //Step 3: IoCtl.DISK_CREATE_DISK is used to initialize a disk with an empty partition table. 
                CREATE_DISK createDisk = new CREATE_DISK();
                createDisk.PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_GPT;
                createDisk.Gpt.MaxPartitionCount = 16;
                var diskGuid = Guid.NewGuid();

                createDisk.Gpt.DiskId = diskGuid;

                // createDisk.PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_GPT;
                // createDisk.Gpt.DiskId = new Guid("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7");
                // createDisk.Gpt.MaxPartitionCount = 1;
                using (var createDiskBuffer = new SafeHGlobalHandle(Marshal.AllocHGlobal(Marshal.SizeOf(typeof(CREATE_DISK)))))
                {
                    Marshal.StructureToPtr(createDisk, createDiskBuffer.DangerousGetHandle(), false);
                    
                    FillMemory(createDiskBuffer.DangerousGetHandle(), (uint)Marshal.SizeOf(typeof(CREATE_DISK)), 0);

                    byte[] arr1 = new byte[Marshal.SizeOf(typeof(CREATE_DISK))];
                    Marshal.Copy(createDiskBuffer.DangerousGetHandle(), arr1, 0, Marshal.SizeOf(typeof(CREATE_DISK)));

                    DeviceIoControl(handle, IoCtl.DISK_CREATE_DISK, createDiskBuffer.DangerousGetHandle(), (uint)Marshal.SizeOf(typeof(CREATE_DISK)),
                        IntPtr.Zero, 0, out _, IntPtr.Zero);
                }

                var bytes = new byte[layoutSize];
                var layoutHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                ZeroMemory(layoutHandle.AddrOfPinnedObject(), (uint)layoutSize);
                Marshal.StructureToPtr(driveLayoutEx, layoutHandle.AddrOfPinnedObject(), false);


                if (!DeviceIoControl(handle, IoCtl.DISK_SET_DRIVE_LAYOUT_EX, layoutHandle.AddrOfPinnedObject(), 624, IntPtr.Zero, 0, out _, IntPtr.Zero))
                {
                    throw new Win32Exception("DISK_SET_DRIVE_LAYOUT_EX Failed");
                }

                layoutHandle.Free();

                //Console.WriteLine("FSCTL_UNLOCK_VOLUME: " + DeviceIoControl(handle, IoCtl.FSCTL_UNLOCK_VOLUME, IntPtr.Zero,
                //0, IntPtr.Zero, (uint)Marshal.SizeOf(typeof(DISK_GEOMETRY_EX)), out returned, IntPtr.Zero));

                if (!DeviceIoControl(handle, IoCtl.DISK_UPDATE_PROPERTIES, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
                {
                    throw new Win32Exception("DISK_SET_DRIVE_LAYOUT_EX Failed");
                }


                handle.Dispose();

                logicalHandles.ForEach(x => x.Dispose());

                return 1;
            }
            finally
            {
                handle.Dispose();
            }
        }

        internal static void Chkdsk(char driveLetter)
        {
            ChkdskCallbackDelegate callback = new ChkdskCallbackDelegate(ChkdskCallback);
            Chkdsk(@$"{driveLetter}:\", "NTFS", false, false, false, false, IntPtr.Zero, IntPtr.Zero, callback);
        }

        public static bool ChkdskCallback(FmifsCallbackCommand Command, uint Action, IntPtr pData)
        {
            switch (Command)
            {
                case FmifsCallbackCommand.Progress:
                case FmifsCallbackCommand.CheckDiskProgress:
                    int percent = Marshal.ReadInt32(pData); // Read DWORD as int
                    //Log.WriteSafe(LogType.Info, "Chkdsk progress: " + percent, null);
                    break;

                case FmifsCallbackCommand.Done:
                    bool success = Marshal.ReadByte(pData) != 0; // BOOLEAN as byte
                    if (!success)
                    {
                        Log.WriteSafe(LogType.Warning, "Chkdsk callback failed", null);
                        return false;
                    }
                    break;

                case FmifsCallbackCommand.Unknown1A:
                case FmifsCallbackCommand.Output:
                case FmifsCallbackCommand.DoneWithStructure:
                    break;

                case FmifsCallbackCommand.NoMediaInDrive:
                case FmifsCallbackCommand.AccessDenied:
                case FmifsCallbackCommand.IncompatibleFileSystem:
                case FmifsCallbackCommand.MediaWriteProtected:
                case FmifsCallbackCommand.VolumeInUse:
                    Log.WriteSafe(LogType.Warning, "Chkdsk callback failed with result: " + Command, null);
                    return false;
                case FmifsCallbackCommand.ReadOnlyMode:
                    break;
            }
            return true;
        }
        
        
        [DllImport("fmifs.dll", CharSet = CharSet.Unicode)]
        public static extern void Chkdsk(
            string DriveRoot,              // Drive path, e.g., "C:\\"
            string Format,                 // File system format, e.g., "NTFS"
            bool FixErrors,                // Whether to fix errors
            bool VigorousIndexCheck,       // Thorough index check
            bool SkipFolderCycle,          // Skip folder cycle checking
            bool ForceDismount,            // Force dismount of the volume
            IntPtr Unused1,
            IntPtr Unused2,
            ChkdskCallbackDelegate Callback // Callback function
        );
        public delegate bool ChkdskCallbackDelegate(FmifsCallbackCommand Command, uint Action, IntPtr pData);        // Enum for callback commands (partial list)
        public enum FmifsCallbackCommand
        {
            Progress,
            DoneWithStructure,
            Unknown2,
            IncompatibleFileSystem,
            Unknown4,
            Unknown5,
            AccessDenied,
            MediaWriteProtected,
            VolumeInUse,
            CantQuickFormat,
            UnknownA,
            Done,
            BadLabel,
            UnknownD,
            Output,
            StructureProgress,
            ClusterSizeTooSmall,
            ClusterSizeTooBig,
            VolumeTooSmall,
            VolumeTooBig,
            NoMediaInDrive,
            Unknown15,
            Unknown16,
            Unknown17,
            DeviceNotReady,
            CheckDiskProgress,
            Unknown1A,
            Unknown1B,
            Unknown1C,
            Unknown1D,
            Unknown1E,
            Unknown1F,
            ReadOnlyMode,
            Unknown21,
            Unknown22,
            Unknown23,
            Unknown24,
            AlignmentViolation
        }

        internal static void UnmountUEFINTFS()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => Wrap.ExecuteSafe(() => d.VolumeLabel).Value == "UEFI_NTFS");

            foreach (var drive in drives)
            {
                string driveLetter = drive.Name.Substring(0, 1);
                if (!DeleteVolumeMountPoint($@"{driveLetter}:\"))
                {
                    Log.WriteSafe(LogType.Warning, "DeleteVolumeMountPoint failed during UEFI_NTFS unmount attempt: " + Marshal.GetLastWin32Error(), null);
                    return;
                } else
                    Log.WriteSafe(LogType.Info, "Unmounted UEFI_NTFS volume on " + $@"{driveLetter}:\", null);
            }
        }
        
        internal static void RemountSafe(char driveLetter)
        {
            StringBuilder volumeName = new StringBuilder(52);
            if (!GetVolumeNameForVolumeMountPoint($@"{driveLetter}:\", volumeName, (uint)volumeName.Capacity))
            {
                Log.WriteSafe(LogType.Warning, "GetVolumeNameForVolumeMountPoint Failed: " + Marshal.GetLastWin32Error(), null);
                return;
            }

            if (!SetVolumeMountPoint($@"{driveLetter}:\", volumeName.ToString()))
            {
                var error = Marshal.GetLastWin32Error();
                // 145 = DIR_NOT_EMPTY
                if (error != 145)
                {
                    Log.WriteSafe(LogType.Warning, "SetVolumeMountPoint failed during remount attempt: " + error, null);
                    return;
                }
                if (!DeleteVolumeMountPoint($@"{driveLetter}:\"))
                {
                    Log.WriteSafe(LogType.Warning, "DeleteVolumeMountPoint failed during remount attempt: " + error, null);
                    return;
                }
                if (!SetVolumeMountPoint($@"{driveLetter}:\", volumeName.ToString()))
                {
                    Log.WriteSafe(LogType.Warning, "SetVolumeMountPoint failed after delete during remount attempt: " + error, null);
                    return;
                }
                
            } else
                Log.WriteSafe(LogType.Warning, "Unexpected success of SetVolumeMountPoint when remounting.", null);
        }
        
        internal static void TryFlushDrive(uint driveIndex)
        {
            Wrap.ExecuteSafe(() =>
            {
                using var handle = GetHandle($@"\\.\PhysicalDrive{driveIndex}", Win32.FileAttributes.Normal, Win32.FileShare.Read | Win32.FileShare.Write | Win32.FileShare.Delete);
                Win32.FlushFileBuffers(handle);
            }, true);
        }
        internal static void TryFlushDrive(char driveLetter)
        {
            Wrap.ExecuteSafe(() =>
            {
                using var handle = GetHandle($@"\\.\{driveLetter}:", Win32.FileAttributes.Normal, Win32.FileShare.Read | Win32.FileShare.Write | Win32.FileShare.Delete);
                Win32.FlushFileBuffers(handle);
            }, true);
        }
        internal static void FlushDrive(SafeFileHandle handle) => Win32.FlushFileBuffers(handle);

        internal static SafeFileHandle GetHandle(string path, Win32.FileAttributes attributes = Win32.FileAttributes.Normal, Win32.FileShare share = Win32.FileShare.Read | Win32.FileShare.Write)
        {
            var handle = CreateFile(path, Win32.FileAccess.GenericRead | Win32.FileAccess.GenericWrite,
                share,
                IntPtr.Zero, Win32.FileMode.OpenExisting, attributes, IntPtr.Zero);
            if (handle.DangerousGetHandle() == INVALID_HANDLE_VALUE)
            {
                handle = CreateFile(path, Win32.FileAccess.GenericRead | Win32.FileAccess.GenericWrite,
                    Win32.FileShare.Read | Win32.FileShare.Write,
                    IntPtr.Zero, Win32.FileMode.OpenExisting, attributes, IntPtr.Zero);
                if (handle.DangerousGetHandle() == INVALID_HANDLE_VALUE)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Error opening handle to path: {path}");
                
                Log.EnqueueSafe(LogType.Warning, $"Could not open drive handle with exclusive write access: " + Marshal.GetLastWin32Error(), null);
            }

            return handle;
        }
        
        internal static SafeFileHandle GetDriveHandle(uint driveIndex, Win32.FileAttributes attributes = Win32.FileAttributes.Normal) => GetHandle($@"\\.\PhysicalDrive{driveIndex}", Win32.FileAttributes.Normal, Win32.FileShare.Read);
    }
}