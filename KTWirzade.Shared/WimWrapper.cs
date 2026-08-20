using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using System.Xml.XPath;
using Core;
using JetBrains.Annotations;
using ManagedWimLib;
using Microsoft.Wim;
using Microsoft.Win32;

namespace KTWirzade.Shared
{
    public class WimWrapper : IDisposable
    {
        private Wim _wim;
        private WimHandle _wimGHandle;
        private WimHandle _wimGImageHandle;
        private IXPathNavigable _wimMountImageXmlInfo;
        public bool IsESD { get; private set; }
        public uint ImageCount { get; private set; }
        public bool PendingChanges { get; private set; } = false;
        public string WimPath { get; private set; }

        public static WimWrapper OpenWim(string path) => new WimWrapper(Wim.OpenWim(path, OpenFlags.None), path);
        public WimWrapper (Wim wimInstance, string path)
        {
            _wim = wimInstance;

            IsESD = _wim.GetWimInfo().CompressionType == CompressionType.LZMS;
            ImageCount = _wim.GetWimInfo().ImageCount;
            WimPath = path;
        }

        public void WriteChanges()
        {
            _wim.Overwrite(WriteFlags.IgnoreReadOnlyFlag, Wim.DefaultThreads);
            _wim.Dispose();
            _wim = Wim.OpenWim(WimPath, OpenFlags.None);
        }
        public string GetImageName(int image) => _wim.GetImageName(image);
        public string GetXmlData() => _wim.GetXmlData();
        public string GetImageProperty(int image, string property) => _wim.GetImageProperty(image, property);
        
        public void RemoveSuperfluousImages()
        {
            if (IsESD)
                throw new Exception("Cannot modify an ESD wim.");
            if (_wimGHandle != null)
                throw new Exception("Cannot modify mounted wim.");
            
            for (int i = 1; i <= ImageCount; i++)
            {
                var name = _wim.GetImageName(i);
                if ((name.EndsWith(" N") || name.Contains(" for Workstations") || name.Contains(" Education") || name.EndsWith(" Single Language")) && !(i == ImageCount && ImageCount == 1))
                {
                    _wim.DeleteImage(i);
                    ImageCount--;
                    i--;
                }
            }
        }

        public void Mount(int image, string mountPath, string stagingPath)
        {
            if (IsESD)
                throw new Exception("Cannot mount an ESD wim.");
            if (PendingChanges)
                throw new Exception("Cannot mount wim with pending changes.");
            if (_wimGHandle != null)
                throw new Exception("Cannot mount multiple wim images at the same time.");
            
            _wim.Dispose();

            _wimGHandle = WimgApi.CreateFile(
                WimPath,
                WimFileAccess.Mount | WimFileAccess.Write | WimFileAccess.Read,
                WimCreationDisposition.OpenExisting,
                WimCreateFileOptions.None,
                WimCompressionType.Lzx);

            Directory.CreateDirectory(stagingPath);
            WimgApi.SetTemporaryPath(_wimGHandle, stagingPath);

            _wimGImageHandle = WimgApi.LoadImage(_wimGHandle, image);
            
            Directory.CreateDirectory(mountPath);
            WimgApi.MountImage(_wimGImageHandle, mountPath, WimMountImageOptions.None);
        }
        public void Unmount()
        {
            if (_wimGHandle == null)
                throw new Exception("Cannot unmount unmounted wim.");

            WimgApi.CommitImageHandle(_wimGImageHandle, false, WimCommitImageOptions.None);
            WimgApi.UnmountImage(_wimGImageHandle);
            
            _wimMountImageXmlInfo = null;
            _wimGImageHandle.Dispose();
            _wimGImageHandle = null;
            _wimGHandle.Dispose();
            _wimGHandle = null;

            _wim = Wim.OpenWim(WimPath, OpenFlags.None);
        }
        
        
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern int RegLoadKey(IntPtr hKey, string lpSubKey, string lpFile);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern int RegUnLoadKey(IntPtr hKey, string lpSubKey);
        
        public void MountHives(string guid)
        {
            try
            {
                for (int i = 1; i <= ImageCount; i++)
                {
                    var hiveFolder = Path.Combine(Path.GetTempPath(), $"KTWirzade-WIM-{guid}-{i}");
                    Directory.CreateDirectory(hiveFolder);
                
                    ExtractFileOrFolder(i, @"Users\Default\NTUSER.DAT", hiveFolder);
                
                    if (Wrap.ExecuteSafe(() => ExtractFileOrFolder(i, @"Users\Default\AppData\Local\Microsoft\Windows\UsrClass.dat", hiveFolder)) != null)
                    {
                        var fs = File.Create(Path.Combine(hiveFolder, @"UsrClass.dat"));
                        var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("KTWirzade.Shared.Properties.UsrClass.dat");
                        resource!.CopyTo(fs);
                        fs.Close();
                    }

                    foreach (var hive in new[] { "SOFTWARE", "SYSTEM", "SECURITY", "SAM", "DEFAULT" })
                        ExtractFileOrFolder(i, @"Windows\System32\config\" + hive, hiveFolder);

                    WinUtil.RegistryManager.AcquirePrivileges();
                
                    using (var hku = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default))
                    {
                        if (hku == null)
                            throw new Exception("Could not open HKU key.");
                    
                        if (RegLoadKey(hku.Handle.DangerousGetHandle(), $"HKCU-{guid}-{i}", Path.Combine(hiveFolder, @"NTUSER.DAT")) != 0)
                            throw new Exception($"Failed to mount default user hive hive.");
                        if (RegLoadKey(hku.Handle.DangerousGetHandle(), $"HKCU_Classes-{guid}-{i}", Path.Combine(hiveFolder, @"UsrClass.dat")) != 0)
                            throw new Exception($"Failed to mount default user classes hive.");
                    
    
                        if (RegLoadKey(hku.Handle.DangerousGetHandle(), $"HKU-S-1-5-18-{guid}-{i}", Path.Combine(hiveFolder, @"DEFAULT")) != 0)
                            Log.WriteSafe(LogType.Warning, $"Failed to mount system profile user hive.", null);
      
                        foreach (var hive in new [] { "SOFTWARE", "SYSTEM", "SECURITY", "SAM" })
                        {
                            if (RegLoadKey(hku.Handle.DangerousGetHandle(), "HKLM-" + hive + "-" + guid + "-" + i, Path.Combine(hiveFolder, hive)) != 0)
                            {
                                if (hive == "SECURITY" || hive == "SAM")
                                    Log.EnqueueSafe(LogType.Warning, $"Failed to mount {hive} hive.", null);
                                else 
                                    throw new Exception($"Failed to mount {hive} hive.");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Wrap.ExecuteSafe(() => UnmountHives(guid));
                throw;
            }
        }
        public void UnmountHives(string guid, bool commit = false)
        {
            WinUtil.RegistryManager.AcquirePrivileges();

            for (int i = 1; i <= ImageCount; i++)
            {
                var hiveFolder = Path.Combine(Path.GetTempPath(), $"KTWirzade-WIM-{guid}-{i}");
                if (!Directory.Exists(hiveFolder))
                {
                    Log.EnqueueSafe(LogType.Warning, $"Hive image folder '{hiveFolder}' not found.", null);
                    continue;
                }
                using (var hku = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default))
                {
                    if (RegUnLoadKey(hku.Handle.DangerousGetHandle(), "HKCU-" + guid + "-" + i) != 0)
                        Log.WriteSafe(LogType.Warning, $"Failed to unmount default user hive.", null);
                    if (RegUnLoadKey(hku.Handle.DangerousGetHandle(), "HKCU_Classes-" + guid + "-" + i) != 0)
                        Log.WriteSafe(LogType.Warning, $"Failed to unmount default user classes hive.", null);
                    if (RegUnLoadKey(hku.Handle.DangerousGetHandle(), "HKU-S-1-5-18-" + guid + "-" + i) != 0)
                        Log.WriteSafe(LogType.Warning, $"Failed to unmount system profile user hive.", null);
                    //if (RegUnLoadKey(hku.Handle.DangerousGetHandle(), "HKU-S-1-5-19-" + guid) != 0)
                    //    Log.WriteSafe(LogType.Warning, $"Failed to unmount local service hive hive.", null);
                    //if (RegUnLoadKey(hku.Handle.DangerousGetHandle(), "HKU-S-1-5-20-" + guid) != 0)
                    //    Log.WriteSafe(LogType.Warning, $"Failed to unmount network service hive hive.", null);

                    foreach (var hive in new[] { "SOFTWARE", "SYSTEM", "SECURITY", "SAM" })
                    {
                        if (RegUnLoadKey(hku.Handle.DangerousGetHandle(), "HKLM-" + hive + "-" + guid + "-" + i) != 0)
                            Log.WriteSafe(LogType.Warning, $"Failed to unmount {hive} hive.", null);
                    }
                }
                if (commit)
                {
                    foreach (var hive in new[]
                             {
                                 @"Users\Default\NTUSER.DAT", @"Users\Default\AppData\Local\Microsoft\Windows\UsrClass.dat",
                                 @"Windows\System32\config\SYSTEM", @"Windows\System32\config\SOFTWARE", @"Windows\System32\config\DEFAULT", @"Windows\System32\config\SAM",
                                 @"Windows\System32\config\SECURITY"
                             })
                    {
                        var source = Path.Combine(hiveFolder, Path.GetFileName(hive));
                        if (!File.Exists(source))
                        {
                            Log.EnqueueSafe(LogType.Warning, $"Hive {Path.GetFileName(hive)} not found.", null);
                            continue;
                        }

                        Wrap.ExecuteSafe(() => AddFileOrFolder(i, source, hive), true);
                    }
                }
            }
            if (commit)
                WriteChanges();
            for (int i = 1; i <= ImageCount; i++)
            {
                var hiveFolder = Path.Combine(Path.GetTempPath(), $"KTWirzade-WIM-{guid}-{i}");
                if (Directory.Exists(hiveFolder))
                    Wrap.ExecuteSafe(() => Directory.Delete(hiveFolder, true), true);
            }
        }
        public void ExtractFileOrFolder(int image, string source, string destinationFolder)
        {
            if (!Directory.Exists(destinationFolder))
                throw new DirectoryNotFoundException($"Directory {destinationFolder} not found.");
            _wim.ExtractPath(image, destinationFolder, source, ExtractFlags.NoPreserveDirStructure);
        }

        public void DeleteFileOrFolder(string path)
        {
            for (int i = 1; i <= ImageCount; i++)
                DeleteFileOrFolder(i, path);
        }
        public void DeleteFileOrFolder(int image, string path)
        {
            if (IsESD)
                throw new Exception("Cannot modify an ESD wim.");
            if (_wimGHandle != null)
                throw new Exception("Cannot modify mounted wim.");

            PendingChanges = true;
            _wim.DeletePath(image, path, DeleteFlags.Force | DeleteFlags.Recursive);
        }
        public void AddFileOrFolder(int image, string source, string destination)
        {
            if (IsESD)
                throw new Exception("Cannot modify an ESD wim.");
            if (_wimGHandle != null)
                throw new Exception("Cannot modify mounted wim.");
            
            PendingChanges = true;
            _wim.UpdateImage(
                image,
                UpdateCommand.SetAdd(source, destination, null, AddFlags.None),
                UpdateFlags.None);
        }
        public void AddFileOrFolder(string source, string destination)
        {
            for (int i = 1; i <= ImageCount; i++)
                AddFileOrFolder(i, source, destination);
        }
        public void MoveFileOrFolder(int image, string source, string destination)
        {
            if (IsESD)
                throw new Exception("Cannot modify an ESD wim.");
            if (_wimGHandle != null)
                throw new Exception("Cannot modify mounted wim.");
            
            PendingChanges = true;
            _wim.UpdateImage(
                image,
                UpdateCommand.SetRename(source, destination),
                UpdateFlags.None);
        }
        public void MoveFileOrFolder(string source, string destination)
        {
            for (int i = 1; i <= ImageCount; i++)
                MoveFileOrFolder(i, source, destination);
        }
        public void AddTree(string sourcePath, string destinationPath)
        {
            if (IsESD)
                throw new Exception("Cannot modify an ESD wim.");
            if (_wimGHandle != null)
                throw new Exception("Cannot modify mounted wim.");
            
            for (int i = 1; i <= ImageCount; i++)
            {
                PendingChanges = true;
                
                _wim.AddTree(i, sourcePath, destinationPath, AddFlags.None);
            }
        }
        
        public void WriteToWIM(string destination, string stagingPath)
        {
            if (IsESD)
            {
                _wim.SetOutputCompressionType(CompressionType.LZX);
                _wim.SetOutputPackCompressionType(CompressionType.LZX);
                _wim.SetOutputChunkSize(32768);
                _wim.SetOutputPackChunkSize(32768);

                Write(destination, WriteFlags.IgnoreReadOnlyFlag | WriteFlags.Rebuild | WriteFlags.Recompress);
            } else
                Write(destination, WriteFlags.IgnoreReadOnlyFlag | WriteFlags.Rebuild);
        }
        public void WriteToESD(string destination)
        {
            if (!IsESD)
            {
                _wim.SetOutputCompressionType(CompressionType.LZMS);
                _wim.SetOutputPackCompressionType(CompressionType.LZMS);
                _wim.SetOutputChunkSize(0);
                _wim.SetOutputPackChunkSize(0);
                
                Write(destination, WriteFlags.Rebuild | WriteFlags.Recompress | WriteFlags.Solid | WriteFlags.IgnoreReadOnlyFlag);
            } else
                Write(destination, WriteFlags.Solid | WriteFlags.IgnoreReadOnlyFlag);
        }

        private void Write([CanBeNull] string path, WriteFlags writeFlags)
        {
            bool overwrite = path == null || Path.GetFullPath(WimPath) == Path.GetFullPath(path);
            if (!writeFlags.HasFlag(WriteFlags.Rebuild) || true)
            {
                if (overwrite)
                    _wim.Overwrite(writeFlags, Wim.DefaultThreads);
                else
                    _wim.Write(path, Wim.AllImages, writeFlags, Wim.DefaultThreads);
                return;
            }
            
            if (PendingChanges)
                WriteChanges();
            
            var properties = new Dictionary<int, Dictionary<string, string>>();
            for (var i = 1; i <= ImageCount; i++)
            {
                properties[i] = new Dictionary<string, string>()
                {
                    { @"DISPLAYNAME", null }, 
                    { @"DISPLAYDESCRIPTION", null }, 
                    { @"WINDOWS/PRODUCTNAME", null }
                };
                foreach (var property in properties[i].Keys.ToList())
                {
                    properties[i][property] = _wim.GetImageProperty(i, property);
                    if (string.IsNullOrWhiteSpace(properties[i][property]))
                        properties[i][property] = null;
                }
            }
            
            if (overwrite)
                _wim.Overwrite(writeFlags, Wim.DefaultThreads);
            else
                _wim.Write(path, Wim.AllImages, writeFlags, Wim.DefaultThreads);
            
            _wim.Dispose();
            _wim = Wim.OpenWim(path ?? WimPath, OpenFlags.None);
            
            for (var i = 1; i <= ImageCount; i++)
            {
                foreach (var property in properties[i].Keys.ToList())
                    _wim.SetImageProperty(i, property, properties[i][property]);
            }

            WriteChanges();
        }
        
        public void Dispose() => _wim.Dispose();
    }
}
