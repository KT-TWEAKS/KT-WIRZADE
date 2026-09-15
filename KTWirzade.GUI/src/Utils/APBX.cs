using Core;
using Interprocess;
using SharpSevenZip;
using System.IO;
using System.Linq.Expressions;
using System.Security;
using System.Windows.Media.Imaging;
using KTWirzade.Shared;
using static Core.Log;
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
    public static class APBX
    {
        public static async Task<PlaybookGUI> ImportAPBX(string file, bool overwrite = false)
        {
            string pbDir = Directory.CreateDirectory(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks")).FullName;
            PlaybookGUI pb = await Task.Run(() => GetData(file));
            string path = Path.Combine(pbDir, pb.FileNameWithoutExtension + ".apbx");
            string altDest = Path.Combine(pbDir, PlaybookGUI.RemoveInvalidFilePathCharacters(pb.Username + "-" + pb.Name, "~") + ".apbx");
            if ((File.Exists(path) || File.Exists(altDest)) && !overwrite)
            {
                return null;
            }
            try
            {
                await InterLink.ExecuteAsync((Expression<Action>)(() => CopyAPBX(file, pb.FileNameWithoutExtension + ".apbx", PlaybookGUI.RemoveInvalidFilePathCharacters(string.Concat(pb.Username + "-", pb.Name), "~") + ".apbx")), false, -1);
            }
            catch (SecurityException)
            {
                pb.Path = null;
                return null;
            }
            pb.Path = "Placeholder";
            Task updTask = Task.CompletedTask;
            if (pb.Git != null && pb.ProductCode != null && pb.PendingUpdate == null)
            {
                updTask = SafeTask.Run((Func<Task>)async delegate
                {
                    string releaseTag = await pb.LatestPlaybookVersion();
                    if (VersionNumber.GetVersionNumber(releaseTag) > pb.GetVersionNumber())
                    {
                        pb.PendingUpdate = releaseTag;
                    }
                    pb.UpdatesChecked = true;
                    pb.LastChecked = DateTime.Now;
                }, false, null);
            }
            await pb.GetVerificationStatus();
            await updTask;
            await pb.WriteEncryptedStatus();
            if (pb.VerificationStatus != PlaybookGUI.VerificationLevel.Malicious)
            {
                pb.Icon = pb.IconCache;
            }
            else
            {
                pb.DisplayUsername = "Malicious";
            }
            if (pb.Username == "KTWirzade")
            {
                pb.DonateLink = null;
            }
            return pb;
        }

        [InterprocessMethod(Level.Administrator)]
        private static void CopyAPBX(string apbx, string destName, string altName)
        {
            string pbPath = Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks");
            string statusFile = Path.Combine(pbPath, Path.GetFileNameWithoutExtension(destName) + ".status");
            string altStatusFile = Path.Combine(pbPath, Path.GetFileNameWithoutExtension(destName) + ".status");
            try
            {
                if (File.Exists(Path.Combine(pbPath, destName)))
                {
                    File.Delete(Path.Combine(pbPath, destName));
                }
                if (File.Exists(Path.Combine(pbPath, altName)))
                {
                    File.Delete(Path.Combine(pbPath, altName));
                }
            }
            catch (Exception)
            {
                throw new SecurityException();
            }
            File.Copy(apbx, Path.Combine(pbPath, destName));
            if (File.Exists(statusFile))
            {
                Wrap.ExecuteSafe(delegate
                {
                    File.Delete(statusFile);
                }, true, null);
            }
            if (File.Exists(altStatusFile))
            {
                Wrap.ExecuteSafe(delegate
                {
                    File.Delete(altStatusFile);
                }, true, null);
            }
        }
        public static void ExtractArchive(string apbx, string targetDir, string exclude = null)
        {
            using (SharpSevenZipExtractor extractor = new SharpSevenZipExtractor(apbx, "malte"))
            {
                if (exclude == null)
                {
                    try
                    {
                        extractor.ExtractArchive(targetDir);
                        return;
                    }
                    catch (IOException e)
                    {
                        WriteExceptionSafe(e, "IOException while extracting '" + apbx + "'", Array.Empty<ValueTuple<string, object>>());
                        return;
                    }
                }
                List<int> toBeExtracted = new List<int>();
                foreach (ArchiveFileInfo entry in extractor.ArchiveFileData)
                {
                    if (!entry.FileName.Equals(exclude, StringComparison.OrdinalIgnoreCase))
                    {
                        toBeExtracted.Add(entry.Index);
                    }
                }
                try
                {
                    extractor.ExtractFiles(targetDir, toBeExtracted.ToArray());
                }
                catch (IOException e2)
                {
                    WriteExceptionSafe(e2, "IOException while extracting '" + apbx + "'", Array.Empty<ValueTuple<string, object>>());
                }
            }
        }

        public static PlaybookGUI GetData(string apbx)
        {
            string tmpPath = Environment.ExpandEnvironmentVariables(Path.Combine("%TEMP%", Path.GetFileNameWithoutExtension(apbx) + "-" + new Random().Next(10000, 99999).ToString()));
            string targetDir = Directory.CreateDirectory(tmpPath).FullName;
            using (SharpSevenZipExtractor extractor = new SharpSevenZipExtractor(apbx, "malte"))
            {
                List<int> toBeExtracted = new List<int>();
                foreach (ArchiveFileInfo entry in extractor.ArchiveFileData)
                {
                    if (entry.FileName.StartsWith("playbook.") || entry.FileName.StartsWith("Images\\", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!entry.Method.Contains("7zAES") && !entry.Method.Contains("ZipCrypto"))
                        {
                            throw new Exception("Playbook must be encrypted using 'malte' as the password.");
                        }
                        toBeExtracted.Add(entry.Index);
                    }
                }
                extractor.ExtractFiles(targetDir, toBeExtracted.ToArray());
            }
            PlaybookGUI result = new PlaybookGUI(AmeliorationUtil.DeserializePlaybook(tmpPath));
            if (Directory.Exists(Path.Combine(tmpPath, "Images")))
            {
                foreach (string image in from x in Directory.GetFiles(Path.Combine(tmpPath, "Images"), "*.png")
                                         where !x.EndsWith("\\playbook.png", StringComparison.OrdinalIgnoreCase)
                                         select x)
                {
                    BitmapImage bmi = new BitmapImage();
                    bmi.BeginInit();
                    bmi.CacheOption = BitmapCacheOption.OnLoad;
                    bmi.UriSource = new Uri(image, UriKind.Absolute);
                    bmi.EndInit();
                    result.Images.Add(bmi);
                    result.Images[result.Images.Count - 1].Freeze();
                }
            }
            string path = null;
            if (File.Exists(Path.Combine(tmpPath, "playbook.png")))
            {
                path = Path.Combine(tmpPath, "playbook.png");
            }
            if (File.Exists(Path.Combine(tmpPath, "Images\\playbook.png")))
            {
                path = Path.Combine(tmpPath, "Images\\playbook.png");
            }
            if (path != null)
            {
                BitmapImage bmi2 = new BitmapImage();
                bmi2.BeginInit();
                bmi2.CacheOption = BitmapCacheOption.OnLoad;
                bmi2.UriSource = new Uri(path, UriKind.Absolute);
                bmi2.EndInit();
                result.IconCache = bmi2;
                result.IconCache.Freeze();
            }
            else
            {
                result.IconCache = result.Icon;
            }
            Directory.Delete(tmpPath, true);
            result.Path = null;
            if (result.Username == "KTWirzade")
            {
                result.DonateLink = null;
            }
            return result;
        }
    }
}

