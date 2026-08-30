#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Core;
using Microsoft.Win32;
using KTWirzade.Shared.Exceptions;
using KTWirzade.Shared.Tasks;
using YamlDotNet.Serialization;


namespace KTWirzade.Shared.Actions
{
    public enum RegistryKeyOperation
    {
        Delete = 0,
        Add = 1
    }
    public class RegistryKeyAction : TaskActionWithOutputProcessor, ITaskAction
    {
        public void RunTaskOnMainThread(Output.OutputWriter output) { throw new NotImplementedException(); }
        [YamlMember(typeof(string), Alias = "path")]
        public string KeyName { get; set; }

        [YamlMember(typeof(Scope), Alias = "scope")]
        public Scope Scope { get; set; } = Scope.AllUsers;

        [YamlMember(typeof(RegistryKeyOperation), Alias = "operation")]
        public RegistryKeyOperation Operation { get; set; } = RegistryKeyOperation.Delete;
        
        [YamlMember(typeof(int), Alias = "weight")]
        public int ProgressWeight { get; set; } = 1;
        public int GetProgressWeight() => ProgressWeight;
        public ErrorAction GetDefaultErrorAction() => Tasks.ErrorAction.Notify;
        public bool GetRetryAllowed() => true;
        
        static Dictionary<RegistryHive, UIntPtr> HiveKeys = new Dictionary<RegistryHive, UIntPtr> {
            { RegistryHive.ClassesRoot, new UIntPtr(0x80000000u) },
            { RegistryHive.CurrentConfig, new UIntPtr(0x80000005u) },
            { RegistryHive.CurrentUser, new UIntPtr(0x80000001u) },
            { RegistryHive.DynData, new UIntPtr(0x80000006u) },
            { RegistryHive.LocalMachine, new UIntPtr(0x80000002u) },
            { RegistryHive.PerformanceData, new UIntPtr(0x80000004u) },
            { RegistryHive.Users, new UIntPtr(0x80000003u) }
        };
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegOpenKeyEx(UIntPtr hKey, string subKey, int ulOptions, int samDesired, out UIntPtr hkResult);
  
        [DllImport("advapi32.dll", EntryPoint = "RegDeleteKeyEx", SetLastError = true)]
        private static extern int RegDeleteKeyEx(
            UIntPtr hKey,
            string lpSubKey,
            uint samDesired, // see Notes below
            uint Reserved);
        private static void DeleteKeyTreeWin32(string key, RegistryHive hive)
        {
            using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
            {
                if (baseKey == null) return;
                using (var openedKey = baseKey.OpenSubKey(key))
                {
                    if (openedKey == null)
                        return;

                    var subKeys = openedKey.GetSubKeyNames().ToList();
                    foreach (var subKey in subKeys)
                    {
                        DeleteKeyTreeWin32(key + "\\" + subKey, hive);
                    }
                }
            }

            RegDeleteKeyEx(HiveKeys[hive], key, 0x0100, 0);
        }
        
        private bool InProgress { get; set; }
        public void ResetProgress() => InProgress = false;
        
        public string ErrorString() => $"RegistryKeyAction failed to {Operation.ToString().ToLower()} key '{KeyName}'.";

        /// <summary>Checks for a child subkey while disposing the probe handle (previous code leaked one handle per user SID).</summary>
        private static bool HasSubKey(RegistryKey parent, string childName, string expectedChild)
        {
            using var child = parent.OpenSubKey(childName);
            return child != null && child.GetSubKeyNames().Any(y => y.Equals(expectedChild));
        }
        
        private List<RegistryKey> GetRoots(ref string subKey)
        {
            var hive = KeyName.Split('\\').GetValue(0).ToString().ToUpper();
            var list = new List<RegistryKey>();

            if (hive.Equals("HKCU") || hive.Equals("HKEY_CURRENT_USER"))
            {
                if (AmeliorationUtil.ISO)
                {
                    if ((hive == "HKCU" || hive == "HKEY_CURRENT_USER") && subKey.StartsWith(@"Software\Classes", StringComparison.OrdinalIgnoreCase)) {
                        subKey = GetSubKey(GetSubKey(subKey));
                        return new List<RegistryKey>() { Registry.Users.OpenSubKey("HKCU_Classes-" + AmeliorationUtil.ISOGuid, true) };
                    } else
                        return new List<RegistryKey>() { Registry.Users.OpenSubKey("HKCU-" + AmeliorationUtil.ISOGuid, true) };
                }
                
                RegistryKey usersKey;
                List<string> userKeys;

                switch (Scope)
                {
                    case Scope.AllUsers:
                        WinUtil.RegistryManager.HookUserHives();
                    
                        usersKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);
                        userKeys = usersKey.GetSubKeyNames().
                            Where(x => x.StartsWith("S-") && 
                                HasSubKey(usersKey, x, "Volatile Environment")).ToList();
                    
                        userKeys.AddRange(usersKey.GetSubKeyNames().Where(x => x.StartsWith("AME_UserHive_") && !x.EndsWith("_Classes")).ToList());
                    
                        userKeys.ForEach(x => list.Add(usersKey.OpenSubKey(x, true)));
                        return list;
                    case Scope.ActiveUsers:
                        usersKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);
                        userKeys = usersKey.GetSubKeyNames().
                            Where(x => x.StartsWith("S-") && 
                                HasSubKey(usersKey, x, "Volatile Environment")).ToList();

                        userKeys.ForEach(x => list.Add(usersKey.OpenSubKey(x, true)));
                        return list;
                    case Scope.DefaultUser:
                        usersKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);
                        userKeys = usersKey.GetSubKeyNames().Where(x => x.Equals("AME_UserHive_Default") && !x.EndsWith("_Classes")).ToList();
                        
                        userKeys.ForEach(x => list.Add(usersKey.OpenSubKey(x, true)));
                        return list;
                }
            }
            if (AmeliorationUtil.ISO)
            {
                if ((hive == "HKLM" || hive == "HKEY_LOCAL_MACHINE") && subKey.StartsWith(@"SAM\", StringComparison.OrdinalIgnoreCase)) {
                    subKey = GetSubKey(subKey);
                    list.AddRange(Registry.Users.GetSubKeyNames().Where(x => x.StartsWith("HKLM-SAM-" + AmeliorationUtil.ISOGuid)).Select(x => Registry.Users.OpenSubKey(x, true)));
                }
                if ((hive == "HKLM" || hive == "HKEY_LOCAL_MACHINE") && subKey.StartsWith(@"SECURITY\", StringComparison.OrdinalIgnoreCase)) {
                    subKey = GetSubKey(subKey);
                    list.AddRange(Registry.Users.GetSubKeyNames().Where(x => x.StartsWith("HKLM-SECURITY-" + AmeliorationUtil.ISOGuid)).Select(x => Registry.Users.OpenSubKey(x, true)));
                }
                if ((hive == "HKLM" || hive == "HKEY_LOCAL_MACHINE") && subKey.StartsWith(@"SOFTWARE\", StringComparison.OrdinalIgnoreCase)) {
                    subKey = GetSubKey(subKey);
                    list.AddRange(Registry.Users.GetSubKeyNames().Where(x => x.StartsWith("HKLM-SOFTWARE-" + AmeliorationUtil.ISOGuid)).Select(x => Registry.Users.OpenSubKey(x, true)));
                }
                if ((hive == "HKLM" || hive == "HKEY_LOCAL_MACHINE") && subKey.StartsWith(@"SYSTEM\CurrentControlSet\", StringComparison.OrdinalIgnoreCase)) {
                    subKey = GetSubKey(GetSubKey(subKey));
                    list.AddRange(Registry.Users.GetSubKeyNames().Where(x => x.StartsWith("HKLM-SYSTEM-" + AmeliorationUtil.ISOGuid)).Select(x => Registry.Users.OpenSubKey(x + @"\ControlSet001", true)));
                }
                else if ((hive == "HKLM" || hive == "HKEY_LOCAL_MACHINE") && subKey.StartsWith(@"SYSTEM\", StringComparison.OrdinalIgnoreCase)) {
                    subKey = GetSubKey(subKey);
                    list.AddRange(Registry.Users.GetSubKeyNames().Where(x => x.StartsWith("HKLM-SYSTEM-" + AmeliorationUtil.ISOGuid)).Select(x => Registry.Users.OpenSubKey(x, true)));
                }
                if ((hive == "HKCR" || hive == "HKEY_CLASSES_ROOT")) {
                    list.AddRange(Registry.Users.GetSubKeyNames().Where(x => x.StartsWith("HKLM-SOFTWARE-" + AmeliorationUtil.ISOGuid)).Select(x => Registry.Users.OpenSubKey(x + @"\Classes", true)));
                }
                if ((hive == "HKU" || hive == "HKEY_USERS") && (subKey.StartsWith(@"S-1-5-18\", StringComparison.OrdinalIgnoreCase) || subKey.StartsWith(@".DEFAULT\", StringComparison.OrdinalIgnoreCase))) {
                    subKey = GetSubKey(subKey);
                    list.AddRange(Registry.Users.GetSubKeyNames().Where(x => x.StartsWith("HKU-S-1-5-18-" + AmeliorationUtil.ISOGuid)).Select(x => Registry.Users.OpenSubKey(x, true)));
                }
                if ((hive == "HKU" || hive == "HKEY_USERS") && subKey.StartsWith(@"S-1-5-19", StringComparison.OrdinalIgnoreCase)) {
                    subKey = GetSubKey(subKey);
                    list.AddRange(Registry.Users.GetSubKeyNames().Where(x => x.StartsWith("HKU-S-1-5-19-" + AmeliorationUtil.ISOGuid)).Select(x => Registry.Users.OpenSubKey(x, true)));
                }
                if ((hive == "HKU" || hive == "HKEY_USERS") && subKey.StartsWith(@"S-1-5-20", StringComparison.OrdinalIgnoreCase)) {
                    subKey = GetSubKey(subKey);
                    list.AddRange(Registry.Users.GetSubKeyNames().Where(x => x.StartsWith("HKU-S-1-5-20-" + AmeliorationUtil.ISOGuid)).Select(x => Registry.Users.OpenSubKey(x, true)));
                }
            }
            else
            {
                list.Add(hive switch
                {
                    "HKCU" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default),
                    "HKEY_CURRENT_USER" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default),
                    "HKLM" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default),
                    "HKEY_LOCAL_MACHINE" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default),
                    "HKCR" => RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Default),
                    "HKEY_CLASSES_ROOT" => RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Default),
                    "HKU" => RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default),
                    "HKEY_USERS" => RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default),
                    _ => throw new ArgumentException($"Key '{KeyName}' does not specify a valid registry hive.")
                });
            }
            return list;
        }

        public string GetSubKey(string key) => key.Replace("\\\\", "\\").Substring(key.IndexOf('\\') + 1);

        public UninstallTaskStatus GetStatus(Output.OutputWriter output)
        {
            try
            {
                
                var subKey = GetSubKey(KeyName);
                var roots = GetRoots(ref subKey);

                foreach (var _root in roots)
                {
                    var root = _root;
                    try
                    {
                        if (root.Name.Contains("AME_UserHive_") && subKey.StartsWith("SOFTWARE\\Classes", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var usersKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);

                            var name = (root.Name.Contains("AME_UserHive_Default") ? "AME_UserHive_Default" : root.Name.Substring(11)) + "_Classes";
                            root.Dispose();
                            root = usersKey.OpenSubKey(name, true);
                            subKey = Regex.Replace(subKey, @"^SOFTWARE\\*Classes\\*", "", RegexOptions.IgnoreCase);

                            if (root == null)
                            {
                                continue;
                            }
                        }
                    
                        using var openedSubKey = root.OpenSubKey(subKey);

                        if (Operation == RegistryKeyOperation.Delete && openedSubKey != null)
                        {
                            return UninstallTaskStatus.ToDo;
                        }
                        if (Operation == RegistryKeyOperation.Add && openedSubKey == null)
                        {
                            return UninstallTaskStatus.ToDo;
                        }
                    }
                    finally
                    {
                        root?.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(LogType.Warning, e, output.LogOptions);
                return UninstallTaskStatus.ToDo;
            }
            return UninstallTaskStatus.Completed;
        }

        public async Task<bool> RunTask(Output.OutputWriter output)
        {
            output.WriteLineSafe("Info", $"{Operation.ToString().TrimEnd('e')}ing registry key '{KeyName}'...");
            
            var subKey = GetSubKey(KeyName);
            var roots = GetRoots(ref subKey);

            foreach (var _root in roots)
            {
                var root = _root;
                try
                {
                    try
                    {
                        if (root.Name.Contains("AME_UserHive_") && subKey.StartsWith("SOFTWARE\\Classes", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var usersKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);

                            var name = root.Name.Substring(11) + "_Classes";
                            root.Dispose();
                            root = usersKey.OpenSubKey(name, true);
                            subKey = Regex.Replace(subKey, @"^SOFTWARE\\*Classes\\*", "", RegexOptions.IgnoreCase);
                        
                            if (root == null)
                            {
                                Log.WriteSafe(LogType.Warning, "User classes hive not found for hive {_root.Name}.", new SerializableTrace(), output.LogOptions);
                                continue;
                            }
                        }

                        // Only log a rollback entry when the operation will actually change state:
                        // logging Add for a pre-existing key makes rollback delete the pre-existing tree;
                        // logging Delete for a missing key makes rollback restore data that was never removed.
                        bool keyExistedBefore;
                        using (var probe = root.OpenSubKey(subKey))
                            keyExistedBefore = probe != null;

                        if (Operation == RegistryKeyOperation.Add)
                        {
                            using var _ = root.CreateSubKey(subKey);

                            if (!AmeliorationUtil.ISO && !keyExistedBefore)
                            {
                                Rollback.RollbackManager.LogEntry(new Rollback.RollbackEntry
                                {
                                    TaskName = "RegistryKeyAction",
                                    ActionType = Rollback.RollbackActionType.RegistryKey,
                                    Operation = Rollback.RollbackOperation.Add,
                                    Target = root.Name + "\\" + subKey
                                });
                            }
                        }
                        if (Operation == RegistryKeyOperation.Delete)
                        {
                            if (!AmeliorationUtil.ISO && keyExistedBefore)
                            {
                                // Export the full key tree before deleting so rollback can restore
                                // the values, not just re-create an empty key.
                                var regBackup = Rollback.RollbackManager.BackupRegistryKeyForRollback(root.Name + "\\" + subKey);

                                var entry = new Rollback.RollbackEntry
                                {
                                    TaskName = "RegistryKeyAction",
                                    ActionType = Rollback.RollbackActionType.RegistryKey,
                                    Operation = Rollback.RollbackOperation.Delete,
                                    Target = root.Name + "\\" + subKey
                                };
                                if (regBackup != null)
                                    entry.ExtraData["regBackup"] = regBackup;

                                Rollback.RollbackManager.LogEntry(entry);
                            }
                            try
                            {
                                root.DeleteSubKeyTree(subKey, false);
                            }
                            catch (Exception e)
                            {
                                if (AmeliorationUtil.ISO)
                                    throw;
                                
                                Log.WriteExceptionSafe(LogType.Warning, e, output.LogOptions);

                                var rootHive = root.Name.Split('\\').First() switch
                                {
                                    "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
                                    "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
                                    "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
                                    "HKEY_USERS" => RegistryHive.Users,
                                    _ => throw new ArgumentException($"Unable to parse: " + root.Name.Split('\\').First())
                                };
                            
                                DeleteKeyTreeWin32(root.Name.StartsWith("HKEY_USERS") ? root.Name.Split('\\')[1] + "\\" + subKey: subKey, rootHive);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.WriteExceptionSafe(LogType.Warning, e, output.LogOptions);
                    
                        if (e is UnauthorizedAccessException)
                        {
                            try
                            {
                                var tempPath = Environment.ExpandEnvironmentVariables(@"%TEMP%\AME");
                                var regPath = Environment.ExpandEnvironmentVariables(@"%SYSTEMROOT%\System32\reg.exe");
                                var ameRegPath = Path.Combine(tempPath, "amereg.exe");
                                if (File.Exists(regPath))
                                {
                                    if (!Directory.Exists(tempPath))
                                        Directory.CreateDirectory(tempPath);
                                
                                    File.Copy(regPath, ameRegPath);
                                
                                    RegAddKey(output, ameRegPath, root!.Name + "\\" + subKey);
                                
                                    File.Delete(ameRegPath);
                                }
                                else
                                {
                                    output.WriteLineSafe("Info", "reg.exe not found, cannot try alternate method.");
                                }
                            }
                            catch (Exception exception)
                            {
                                Log.WriteExceptionSafe(LogType.Warning, exception, output.LogOptions);
                            }
                        }
                    }
                }
                finally
                {
                    root?.Dispose();
                }
            }
            return true;
        }
        
        
        private void RegAddKey(Output.OutputWriter output, string exePath, string key)
        {
            var arguments = @$"add ""{key}"" /f";
            
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Normal,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                FileName = exePath,
                Arguments = arguments,
            };
            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            using (var handler = new OutputHandler("Process", process, output))
            {
                handler.StartProcess();

                bool exited = process.WaitForExit(30000);

                // WaitForExit alone seems to not be entirely reliable
                while (!exited && ExeRunning(process.ProcessName, process.Id))
                {
                    exited = process.WaitForExit(30000);
                }
            }

            int exitCode = Wrap.ExecuteSafe(() => process.ExitCode, true, output.LogOptions).Value;
            if (exitCode != 0)
                output.WriteLineSafe("Warning", $"Reg exited with a non-zero exit code: {exitCode}");
        }
    }
}
