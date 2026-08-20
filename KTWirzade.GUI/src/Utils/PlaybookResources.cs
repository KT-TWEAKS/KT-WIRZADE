using Core;
using SharpSevenZip;
using System.IO;
using System.Reflection;
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
    public static class PlaybookResources
    {
        public static void ExtractArchive(string file, string targetDir)
        {
            try
            {
                using (SharpSevenZipExtractor extractor = new SharpSevenZipExtractor(file, "wizard", 0))
                {
                    extractor.ExtractArchive(targetDir);
                }
            }
            catch (IOException e)
            {
                Log.WriteExceptionSafe(LogType.Warning, e, "IOException while extracting '" + file + "'", []);
            }
        }

        public static void ExtractResourceFolder(string resource, string dir, bool overwrite = false)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (string obj in from res in assembly.GetManifestResourceNames()
                                   where res.StartsWith("KTWirzade.GUI.Resources." + resource + ".")
                                   select res)
            {
                using (UnmanagedMemoryStream stream = (UnmanagedMemoryStream)assembly.GetManifestResourceStream(obj))
                {
                    int MB = 1048576;
                    int offset = -MB;
                    string file = dir + "\\" + obj.Substring(("KTWirzade.GUI.Resources." + resource + ".").Length).Replace("---", "\\");
                    if (!file.EndsWith(".gitkeep"))
                    {
                        string fileDir = Path.GetDirectoryName(file);
                        if (fileDir != null && !Directory.Exists(fileDir))
                        {
                            Directory.CreateDirectory(fileDir);
                        }
                        if (!File.Exists(file) || overwrite)
                        {
                            if (File.Exists(file) && overwrite)
                            {
                                try
                                {
                                    File.Delete(file);
                                }
                                catch (Exception ex)
                                {
                                    Log.EnqueueExceptionSafe(ex, []);
                                    continue;
                                }
                            }
                            using (FileStream fsDlst = new(file, FileMode.CreateNew, FileAccess.Write))
                            {
                                while ((offset + MB) < stream.Length)
                                {
                                    byte[] buffer = new byte[MB];
                                    offset += MB;
                                    if ((offset + MB) > stream.Length)
                                    {
                                        buffer = new byte[stream.Length - offset];
                                    }
                                    stream.Seek(offset, SeekOrigin.Begin);
                                    stream.Read(buffer, 0, buffer.Length);
                                    fsDlst.Seek(offset, SeekOrigin.Begin);
                                    fsDlst.Write(buffer, 0, buffer.Length);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}