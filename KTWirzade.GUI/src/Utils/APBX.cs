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
            string destPath = Path.Combine(pbPath, destName);
            string altPath = Path.Combine(pbPath, altName);
            string statusFile = Path.Combine(pbPath, Path.GetFileNameWithoutExtension(destName) + ".status");
            string altStatusFile = Path.Combine(pbPath, Path.GetFileNameWithoutExtension(altName) + ".status");
            string tempPath = Path.Combine(pbPath, "." + Guid.NewGuid().ToString("N") + ".apbx.tmp");
            try
            {
                File.Copy(apbx, tempPath, overwrite: false);
                if (File.Exists(destPath))
                {
                    // The temporary file is on the same volume, so replacing it keeps
                    // the old APBX available until the new copy is complete.
                    File.Replace(tempPath, destPath, null);
                }
                else
                {
                    File.Move(tempPath, destPath);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new SecurityException("Could not safely replace the cached APBX file.", ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch (Exception ex)
                {
                    WriteExceptionSafe(ex, "Could not remove the temporary APBX file.", new (string, object)[1] { ("Path", tempPath) });
                }
            }

            if (!string.Equals(destPath, altPath, StringComparison.OrdinalIgnoreCase))
            {
                DeleteCacheSidecar(altPath);
            }
            DeleteCacheSidecar(statusFile);
            if (!string.Equals(statusFile, altStatusFile, StringComparison.OrdinalIgnoreCase))
            {
                DeleteCacheSidecar(altStatusFile);
            }
        }

        private static void DeleteCacheSidecar(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                WriteExceptionSafe(ex, "Could not remove an obsolete APBX cache file.", new (string, object)[1] { ("Path", path) });
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
            string tmpPath = Path.Combine(Path.GetTempPath(), "KTWirzade-APBX-" + Guid.NewGuid().ToString("N"));
            string targetDir = Directory.CreateDirectory(tmpPath).FullName;
            try
            {
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
                result.Path = null;
                if (result.Username == "KTWirzade")
                {
                    result.DonateLink = null;
                }
                return result;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tmpPath)) Directory.Delete(tmpPath, recursive: true);
                }
                catch (Exception cleanupException)
                {
                    WriteExceptionSafe(cleanupException, "Could not clean the temporary APBX extraction directory.", new (string, object)[1] { ("Path", tmpPath) });
                }
            }
        }
    }
}

