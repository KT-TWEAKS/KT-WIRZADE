using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Documents;
using Core;
using Microsoft.Win32;
using static iso_mode.Win32;

namespace iso_mode
{
    public class Format
    {
        private enum CallbackCommand
        {
            PROGRESS,
            DONEWITHSTRUCTURE,
            UNKNOWN2,
            UNKNOWN3,
            UNKNOWN4,
            UNKNOWN5,
            INSUFFICIENTRIGHTS,
            UNKNOWN7,
            DISKLOCKEDFORACCESS,
            UNKNOWN9,
            UNKNOWNA,
            DONE,
            UNKNOWNC,
            UNKNOWND,
            OUTPUT,
            STRUCTUREPROGRESS
        }

        private const int FMIFS_HARDDISK = 0xC;

        private delegate Int32 FormatCallBackDelegate(CallbackCommand callBackCommand, int subActionCommand,
            IntPtr action);

        [DllImport("fmifs.dll", EntryPoint = "FormatEx", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void FormatEx(string drivePath, int mediaFlag, string fsType, string label,
            int quickFormat, uint clusterSize, FormatCallBackDelegate callBackDelegate);

        public static void FormatDrive(string guid, string label, char driveLetter)
        {
            string deleteResult;
            if ((deleteResult = Helper.FormatVolume(driveLetter.ToString(), "NTFS", 0, label)) != "Success")
                throw new Exception(deleteResult);
            
            return;
            FormatEx(driveLetter + @":\", 0, "NTFS", label, 1, 0, FormatCallback);

            while (status == FormatStatus.Formatting)
            {
                Thread.Sleep(200);
            }

            if (status == FormatStatus.Failed)
            {
                Console.WriteLine("FAILED!!!: " + driveLetter);
                throw new Exception("Format failed.");
            }

        }


        private enum FormatStatus
        {
            Completed,
            Failed,
            Formatting,
        }

        private static FormatStatus status = FormatStatus.Formatting;

        private static Int32 FormatCallback(CallbackCommand callBackCommand, int subActionCommand, IntPtr action)
        {
            Console.WriteLine(callBackCommand.ToString());

            switch (callBackCommand)
            {
                case CallbackCommand.PROGRESS:
                    int percent = Marshal.ReadInt32(action);
                    Console.WriteLine(percent);
                    break;
                case CallbackCommand.OUTPUT:
                    string output = Marshal.PtrToStringAuto(action);
                    Console.WriteLine(output);
                    break;
                case CallbackCommand.DONE:
                    bool result = Convert.ToBoolean(Marshal.ReadByte(action)); // if False => Error
                    Console.WriteLine(result + " OOF ");

                    status = result ? FormatStatus.Completed : FormatStatus.Failed;


                    break;
            }

            return 1;
        }
    }
}
