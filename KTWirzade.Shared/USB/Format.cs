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
        public static void FormatDrive(string guid, string label, char driveLetter)
        {
            string deleteResult;
            if ((deleteResult = Helper.FormatVolume(driveLetter.ToString(), "NTFS", 0, label)) != "Success")
                throw new Exception(deleteResult);
        }
    }
}
