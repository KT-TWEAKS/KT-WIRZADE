using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
using Core;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace KTWirzade.Shared.Rollback
{
    public enum RollbackActionType
    {
        RegistryKey,
        RegistryValue,
        File,
        Service,
        ScheduledTask,
        Appx,
        SystemPackage,
        Unknown
    }

    public enum RollbackOperation
    {
        Add,
        Delete,
        Modify,
        Execute
    }

    public class RollbackEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string PlaybookName { get; set; }
        public string TaskName { get; set; }
        public RollbackActionType ActionType { get; set; }
        public RollbackOperation Operation { get; set; }
        public string Target { get; set; }
        public string SubTarget { get; set; }
        public string PreviousValue { get; set; }
        public string NewValue { get; set; }
        public string RegistryKind { get; set; }
        public Dictionary<string, string> ExtraData { get; set; } = new Dictionary<string, string>();
        public bool RollbackCompleted { get; set; }
        public DateTime? RollbackTimestamp { get; set; }
    }

    public class RollbackSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string PlaybookName { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public List<RollbackEntry> Entries { get; set; } = new List<RollbackEntry>();
        public bool WasSuccessful { get; set; }
        public bool WasRolledBack { get; set; }
    }

    public static class RollbackPaths
    {
        public const string BaseDir = @"C:\ProgramData\AME\Rollbacks";

        public static string GetSessionDir(string sessionId)
        {
            return Path.Combine(BaseDir, sessionId);
        }

        public static string GetSessionFile(string sessionId)
        {
            return Path.Combine(BaseDir, sessionId, "session.json");
        }

        public static string GetSessionBackupDir(string sessionId)
        {
            return Path.Combine(BaseDir, sessionId, "files");
        }

        public static IEnumerable<RollbackSession> ListSessions()
        {
            if (!Directory.Exists(BaseDir))
                yield break;

            foreach (var dir in Directory.GetDirectories(BaseDir))
            {
                var sessionFile = Path.Combine(dir, "session.json");
                if (!File.Exists(sessionFile))
                    continue;

                RollbackSession session = null;
                try
                {
                    var json = File.ReadAllText(sessionFile);
                    session = JsonConvert.DeserializeObject<RollbackSession>(json);
                }
                catch { }

                if (session != null)
                    yield return session;
            }
        }
    }

    public static class RollbackManager
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegUnLoadKey(IntPtr hKey, string lpSubKey);

        private const int KeepDays = 30;
        private const int KeepMinimum = 5;

        private static readonly object SaveLock = new object();

        public static RollbackSession CurrentSession { get; private set; }

        public static RollbackSession BeginSession(string playbookName)
        {
            var session = new RollbackSession { PlaybookName = playbookName };
            CurrentSession = session;

            var dir = RollbackPaths.GetSessionDir(session.SessionId);
            Directory.CreateDirectory(dir);
            SaveSession();

            Wrap.ExecuteSafe(PruneSessions, true);

            return session;
        }

        public static void EndSession(bool wasSuccessful)
        {
            if (CurrentSession == null)
            {
                var active = FindActiveSession();
                if (active == null) return;
                CurrentSession = active;
            }
            else
            {
                // Actions run in a separate TrustedInstaller process that writes LogEntry data
                // straight into the session.json on disk, while this process' in-memory session
                // still holds an empty Entries list. Merging from disk here prevents the final
                // save from wiping everything the child recorded.
                // Merge is done by entry Id (union), not by count: both processes may have
                // appended entries since the last save, and count comparison would discard
                // the child's records whenever the parent happened to hold more entries.
                try
                {
                    RollbackSession onDisk = null;
                    for (int retry = 0; retry < 3; retry++)
                    {
                        try
                        {
                            onDisk = LoadSession(CurrentSession.SessionId);
                            break;
                        }
                        catch { Thread.Sleep(100 * (retry + 1)); }
                    }
                    if (onDisk != null)
                    {
                        var known = new HashSet<string>(CurrentSession.Entries.Select(e => e.Id));
                        foreach (var e in onDisk.Entries)
                        {
                            if (known.Add(e.Id))
                                CurrentSession.Entries.Add(e);
                        }
                        CurrentSession.Entries = CurrentSession.Entries.OrderBy(e => e.Timestamp).ToList();
                    }
                }
                catch (Exception)
                {
                    // Keep the in-memory copy if the disk copy cannot be read after retries.
                }
            }

            CurrentSession.WasSuccessful = wasSuccessful;
            CurrentSession.CompletedAt = DateTime.UtcNow;
            SaveSession();
            CurrentSession = null;
        }

        /// <summary>
        /// Records an action about to be performed by a playbook so it can be reverted later.
        /// Actions may execute in a different process (TrustedInstaller node) than the session
        /// owner, so the active session is resolved from disk when the static state is empty.
        /// </summary>
        public static void LogEntry(RollbackEntry entry)
        {
            Wrap.ExecuteSafe(() =>
            {
                var session = CurrentSession ?? FindActiveSession();
                if (session == null)
                {
                    session = BeginSession("Manual");
                }
                CurrentSession = session;

                entry.PlaybookName = session.PlaybookName;
                session.Entries.Add(entry);
                SaveSession();
            }, true);
        }

        /// <summary>
        /// Copies a file about to be deleted into the session backup folder and returns
        /// the backup path (null when no session is active or the file does not exist).
        /// </summary>
        public static string BackupFileForRollback(string filePath)
        {
            try
            {
                var session = CurrentSession ?? FindActiveSession();
                if (session == null || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return null;

                var backupDir = RollbackPaths.GetSessionBackupDir(session.SessionId);
                Directory.CreateDirectory(backupDir);

                var backupName = Guid.NewGuid().ToString("N").Substring(0, 12) + "_" + Path.GetFileName(filePath);
                var backupPath = Path.Combine(backupDir, backupName);
                File.Copy(filePath, backupPath, true);

                // Create checksum manifest for integrity verification on rollback
                try
                {
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    using var stream = File.OpenRead(filePath);
                    var hash = sha.ComputeHash(stream);
                    var hashHex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    var manifestPath = backupPath + ".sha256";
                    File.WriteAllText(manifestPath, hashHex);
                }
                catch { /* checksum is best effort */ }

                Log.WriteSafe(LogType.Info, $"[Rollback] File backed up: {filePath} -> {backupPath}", null);
                return backupPath;
            }
            catch (Exception ex)
            {
                Log.WriteSafe(LogType.Warning, $"Could not backup file for rollback: {filePath} — {ex.Message}", null);
                return null;
            }
        }

        /// <summary>
        /// Exports a registry key about to be deleted (e.g. a service) into the session
        /// backup folder so it can be restored with reg import. Returns the .reg path or null.
        /// </summary>
        public static string BackupRegistryKeyForRollback(string keyPath)
        {
            try
            {
                var session = CurrentSession ?? FindActiveSession();
                if (session == null || string.IsNullOrEmpty(keyPath))
                    return null;

                var backupDir = Path.Combine(RollbackPaths.GetSessionDir(session.SessionId), "registry");
                Directory.CreateDirectory(backupDir);

                var dest = Path.Combine(backupDir, Guid.NewGuid().ToString("N").Substring(0, 10) + ".reg");
                var psi = new System.Diagnostics.ProcessStartInfo("reg.exe", $"export \"{keyPath}\" \"{dest}\" /y")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    p.WaitForExit(10000);
                }
                return File.Exists(dest) ? dest : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static RollbackSession FindActiveSession()
        {
            try
            {
                return RollbackPaths.ListSessions()
                    .Where(s => s.CompletedAt == null)
                    .OrderByDescending(s => s.StartedAt)
                    .FirstOrDefault();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Removes old rollback sessions. Handles every case that previously leaked
        /// directories forever: (1) sessions whose JSON is missing/corrupt (orphan
        /// folders are invisible to ListSessions), (2) sessions stuck open because
        /// the process died mid-run (CompletedAt == null forever), (3) read-only or
        /// in-use files making Directory.Delete throw, which previously aborted the
        /// whole loop through the outer swallow-all catch, and (4) stray *.tmp saves.
        /// Returns how many session directories were actually removed.
        /// </summary>
        public static int CleanupOldSessions()
        {
            int removed = 0;

            try
            {
                if (!Directory.Exists(RollbackPaths.BaseDir))
                    return 0;

                var cutoff = DateTime.UtcNow.AddDays(-KeepDays);
                var sessions = RollbackPaths.ListSessions()
                    .OrderByDescending(s => s.StartedAt)
                    .ToList();

                for (int i = KeepMinimum; i < sessions.Count; i++)
                {
                    var session = sessions[i];

                    // Never prune a recent open session: it may be the playbook
                    // currently being applied. An open session older than the cutoff,
                    // however, belongs to a dead process and is safe to remove.
                    if (session.CompletedAt == null && session.StartedAt >= cutoff)
                        continue;
                    if (session.CompletedAt != null && session.StartedAt >= cutoff)
                        continue;

                    if (TryDeleteSessionDir(session.SessionId))
                        removed++;
                }

                // Orphan folders: no readable session.json means ListSessions never
                // sees them, so age them by folder write time instead.
                try
                {
                    foreach (var dir in Directory.GetDirectories(RollbackPaths.BaseDir))
                    {
                        try
                        {
                            var hasJson = File.Exists(Path.Combine(dir, "session.json"));
                            if (!hasJson && Directory.GetLastWriteTimeUtc(dir) < cutoff)
                            {
                                if (ForceDeleteDirectory(dir))
                                    removed++;
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                // Leftover atomic-save temporaries at the base level.
                try
                {
                    foreach (var tmp in Directory.GetFiles(RollbackPaths.BaseDir, "*.tmp"))
                    {
                        try { File.SetAttributes(tmp, FileAttributes.Normal); File.Delete(tmp); } catch { }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                Log.WriteExceptionSafe(LogType.Warning, ex, "Rollback prune pass failed.", null);
            }

            return removed;
        }

        /// <summary>Prune entry point for explicit calls (GUI button, post-run).</summary>
        public static int CleanupOldSessionsNow() => CleanupOldSessions();

        /// <summary>
        /// Deletes a single rollback session regardless of age or state. Used by the
        /// GUI per-row delete button.
        /// </summary>
        public static bool DeleteSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return false;
            return TryDeleteSessionDir(sessionId);
        }

        private static void PruneSessions()
        {
            CleanupOldSessions();
        }

        private static bool TryDeleteSessionDir(string sessionId)
        {
            try
            {
                var dir = RollbackPaths.GetSessionDir(sessionId);
                return Directory.Exists(dir) && ForceDeleteDirectory(dir);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Directory.Delete throws on read-only/system/hidden files (File.Copy preserves
        /// source attributes, so backups of protected system files inherit them). Strip
        /// attributes recursively first; fall back to attrib.exe before giving up.
        /// </summary>
        private static bool ForceDeleteDirectory(string dir)
        {
            try
            {
                ClearFileAttributesRecursive(dir);
                Directory.Delete(dir, recursive: true);
                return !Directory.Exists(dir);
            }
            catch
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/C attrib -r -s -h \"{dir}\\*\" /s /d & rmdir /s /q \"{dir}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (var p = System.Diagnostics.Process.Start(psi))
                        p?.WaitForExit(30000);
                    return !Directory.Exists(dir);
                }
                catch
                {
                    return false;
                }
            }
        }

        private static void ClearFileAttributesRecursive(string path)
        {
            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
                foreach (var sub in Directory.GetDirectories(path))
                {
                    try { File.SetAttributes(sub, File.GetAttributes(sub) & ~(FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden)); } catch { }
                    ClearFileAttributesRecursive(sub);
                }
            }
            catch { }
        }

        private static void SaveSession()
        {
            if (CurrentSession == null) return;

            lock (SaveLock)
            {
                Directory.CreateDirectory(RollbackPaths.GetSessionDir(CurrentSession.SessionId));
                WriteSessionFile(CurrentSession.SessionId, CurrentSession);
            }
        }

        /// <summary>
        /// Atomic session save shared by SaveSession and the post-rollback rewrite.
        /// Unique temp suffix: parent and TrustedInstaller child share this file, and a
        /// fixed ".tmp" name made concurrent saves collide and throw. The replace step
        /// retries briefly because a transient IOException here would silently drop
        /// rollback entries (LogEntry swallows).
        /// </summary>
        private static void WriteSessionFile(string sessionId, RollbackSession session)
        {
            var dir = RollbackPaths.GetSessionDir(sessionId);
            Directory.CreateDirectory(dir);
            var target = RollbackPaths.GetSessionFile(sessionId);
            var temp = target + "." + System.Diagnostics.Process.GetCurrentProcess().Id + ".tmp";

            File.WriteAllText(temp, JsonConvert.SerializeObject(session, Formatting.Indented));

            const int attempts = 3;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(target))
                        File.Replace(temp, target, null);
                    else
                        File.Move(temp, target);
                    break;
                }
                catch (IOException) when (attempt < attempts)
                {
                    System.Threading.Thread.Sleep(50 * attempt);
                }
                catch
                {
                    try { File.Delete(temp); } catch { }
                    throw;
                }
            }
        }

        public static RollbackSession LoadSession(string sessionId)
        {
            var file = RollbackPaths.GetSessionFile(sessionId);
            if (!File.Exists(file))
                return null;

            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            var content = sr.ReadToEnd();
            return JsonConvert.DeserializeObject<RollbackSession>(content);
        }

        public static RollbackResult RollbackSession(string sessionId)
        {
            var session = LoadSession(sessionId);
            if (session == null)
                return new RollbackResult { Success = false, Error = "Sessao nao encontrada." };

            if (session.WasRolledBack)
                return new RollbackResult { Success = true, Message = "Sessao ja foi revertida anteriormente." };

            int succeeded = 0;
            int failed = 0;
            int skipped = 0;
            var errors = new List<string>();

            bool hiveMounted = false;
            try
            {
                hiveMounted = EnsureDefaultUserHiveLoaded(session);
            }
            catch (Exception ex)
            {
                Log.WriteSafe(LogType.Warning, $"Could not load Default user hive for rollback: {ex.Message}", null);
            }

            try
            {
            for (int i = session.Entries.Count - 1; i >= 0; i--)
            {
                var entry = session.Entries[i];
                if (entry.RollbackCompleted)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    if (RollbackEntry(entry))
                    {
                        entry.RollbackCompleted = true;
                        entry.RollbackTimestamp = DateTime.UtcNow;
                        succeeded++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"[{entry.ActionType}] {entry.Target}: {ex.Message}");
                }

                if (i % 5 == 0)
                {
                    try { lock (SaveLock) { WriteSessionFile(sessionId, session); } }
                    catch { }
                }
            }

            session.WasRolledBack = failed == 0;
            WriteSessionFile(sessionId, session);

            return new RollbackResult
            {
                Success = failed == 0,
                TotalEntries = session.Entries.Count,
                Succeeded = succeeded,
                Failed = failed,
                Skipped = skipped,
                Error = errors.Count > 0 ? string.Join("\n", errors) : null,
                Message = $"Rollback: {succeeded} revertidos, {failed} erros, {skipped} ignorados"
            };
            }
            finally
            {
                if (hiveMounted)
                    UnloadDefaultUserHive();
            }
        }

        private static bool EnsureDefaultUserHiveLoaded(RollbackSession session)
        {
            bool needsDefault = session.Entries.Any(e =>
                e.Target != null && e.Target.StartsWith("HKEY_USERS\\AME_UserHive_Default", StringComparison.OrdinalIgnoreCase));

            if (!needsDefault)
                return false;

            if (Registry.Users.GetSubKeyNames().Any(x => x.Equals("AME_UserHive_Default", StringComparison.OrdinalIgnoreCase)))
                return false;

            string defaultDir = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMDRIVE"), @"Users\Default");
            string ntdat = Path.Combine(defaultDir, "NTUSER.DAT");
            string usrclass = Path.Combine(defaultDir, @"AppData\Local\Microsoft\Windows\UsrClass.dat");

            WinUtil.RegistryManager.AcquirePrivileges();

            if (File.Exists(ntdat))
                WinUtil.RegistryManager.LoadFromFile(ntdat);

            if (File.Exists(usrclass))
            {
                try { WinUtil.RegistryManager.LoadFromFile(usrclass, true); }
                catch { }
            }

            bool mounted = Registry.Users.GetSubKeyNames()
                .Any(x => x.Equals("AME_UserHive_Default", StringComparison.OrdinalIgnoreCase));

            if (mounted)
                Log.WriteSafe(LogType.Info, "Mounted Default user hive for rollback.", null);

            return mounted;
        }

        private static void UnloadDefaultUserHive()
        {
            foreach (var name in new[] { "AME_UserHive_Default_Classes", "AME_UserHive_Default" })
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        using var hku = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);
                        int rc = RegUnLoadKey(hku.Handle.DangerousGetHandle(), name);
                        if (rc == 0) break;
                        Thread.Sleep(200 * (attempt + 1));
                    }
                    catch { Thread.Sleep(200 * (attempt + 1)); }
                }
            }
            Log.WriteSafe(LogType.Info, "Attempted to unmount Default user hive after rollback.", null);
        }

        private static bool RollbackEntry(RollbackEntry entry)
        {
            switch (entry.ActionType)
            {
                case RollbackActionType.RegistryKey:
                    return RollbackRegistryKey(entry);
                case RollbackActionType.RegistryValue:
                    return RollbackRegistryValue(entry);
                case RollbackActionType.File:
                    return RollbackFile(entry);
                case RollbackActionType.Service:
                    return RollbackService(entry);
                case RollbackActionType.ScheduledTask:
                    return RollbackScheduledTask(entry);
                case RollbackActionType.Appx:
                case RollbackActionType.SystemPackage:
                    // No package state is captured at action time yet, so there is
                    // nothing to revert; report honestly instead of fake success.
                    return false;
                default:
                    return false;
            }
        }

        private static bool RunSchtasks(string arguments)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                if (p == null) return false;
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit(30000);
                if (p.ExitCode != 0)
                    Log.WriteSafe(LogType.Warning, $"schtasks {arguments} failed ({p.ExitCode}): {(error ?? output).Trim()}", null);
                return p.ExitCode == 0;
            }
        }

        private static bool RollbackScheduledTask(RollbackEntry entry)
        {
            try
            {
                string xmlBackup = null;
                entry.ExtraData?.TryGetValue("xmlBackup", out xmlBackup);

                // Task deleted by the playbook: re-create it from the exported XML.
                if (entry.Operation == RollbackOperation.Delete)
                {
                    if (string.IsNullOrEmpty(xmlBackup) || !File.Exists(xmlBackup))
                        return false;
                    return RunSchtasks($"/Create /F /TN \"{entry.Target}\" /XML \"{xmlBackup}\"");
                }

                // Enable/Disable was toggled by the playbook: restore the previous state.
                if (entry.Operation == RollbackOperation.Modify)
                {
                    bool wasEnabled = string.Equals(entry.PreviousValue, "True", StringComparison.OrdinalIgnoreCase);
                    return RunSchtasks(wasEnabled
                        ? $"/Change /TN \"{entry.Target}\" /Enable"
                        : $"/Change /TN \"{entry.Target}\" /Disable");
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.WriteExceptionSafe(LogType.Warning, ex, $"Rollback of scheduled task '{entry.Target}' failed.", null);
                return false;
            }
        }

        private static RegistryKey OpenBase(RegistryHive hive)
        {
            switch (hive)
            {
                case RegistryHive.CurrentUser:
                    return Registry.CurrentUser;
                case RegistryHive.ClassesRoot:
                    return Registry.ClassesRoot;
                case RegistryHive.Users:
                    return Registry.Users;
                default:
                    return Registry.LocalMachine;
            }
        }

        private static RegistryKey OpenHive(string path, bool writable)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (!TrySplitHivePath(path, out var hive, out var subKey))
                return Registry.LocalMachine.OpenSubKey(path, writable);

            if (!writable)
                return OpenBase(hive).OpenSubKey(subKey, false);

            try
            {
                return OpenBase(hive).OpenSubKey(subKey, true);
            }
            catch (UnauthorizedAccessException)
            {
                if (!EnsureKeyWritable(path))
                    throw;
                return OpenBase(hive).OpenSubKey(subKey, true);
            }
            catch (System.Security.SecurityException)
            {
                if (!EnsureKeyWritable(path))
                    throw;
                return OpenBase(hive).OpenSubKey(subKey, true);
            }
        }

        private static RegistryKey CreateHive(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (!TrySplitHivePath(path, out var hive, out var subKey))
                return Registry.LocalMachine.CreateSubKey(path, true);

            try
            {
                return OpenBase(hive).CreateSubKey(subKey, true);
            }
            catch (UnauthorizedAccessException)
            {
                if (!EnsureKeyWritable(path))
                    throw;
                return OpenBase(hive).CreateSubKey(subKey, true);
            }
            catch (System.Security.SecurityException)
            {
                if (!EnsureKeyWritable(path))
                    throw;
                return OpenBase(hive).CreateSubKey(subKey, true);
            }
        }

        private static bool TrySplitHivePath(string path, out RegistryHive hive, out string subKey)
        {
            hive = RegistryHive.LocalMachine;
            subKey = null;

            if (string.IsNullOrEmpty(path))
                return false;

            var upper = path.ToUpperInvariant();
            int separator = upper.IndexOf('\\');
            var hiveName = separator < 0 ? upper : upper.Substring(0, separator);
            subKey = separator < 0 ? string.Empty : path.Substring(separator + 1);

            switch (hiveName)
            {
                case "HKLM":
                case "HKEY_LOCAL_MACHINE":
                    hive = RegistryHive.LocalMachine;
                    return true;
                case "HKCU":
                case "HKEY_CURRENT_USER":
                    hive = RegistryHive.CurrentUser;
                    return true;
                case "HKCR":
                case "HKEY_CLASSES_ROOT":
                    hive = RegistryHive.ClassesRoot;
                    return true;
                case "HKU":
                case "HKEY_USERS":
                    hive = RegistryHive.Users;
                    return true;
                default:
                    return false;
            }
        }

        #region Rollback ACL helpers

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES_SINGLE
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string systemName, string name, out LUID luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges, ref TOKEN_PRIVILEGES_SINGLE newState, uint bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        private static bool _rollbackPrivilegesEnabled;

        private static void EnableRollbackPrivileges()
        {
            if (_rollbackPrivilegesEnabled)
                return;
            _rollbackPrivilegesEnabled = true;

            const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
            const uint TOKEN_QUERY = 0x0008;
            const uint SE_PRIVILEGE_ENABLED = 0x0002;

            foreach (var privilege in new[] { "SeTakeOwnershipPrivilege", "SeBackupPrivilege", "SeRestorePrivilege" })
            {
                IntPtr token = IntPtr.Zero;
                try
                {
                    if (!LookupPrivilegeValue(null, privilege, out var luid))
                        continue;
                    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token))
                        continue;

                    var newState = new TOKEN_PRIVILEGES_SINGLE
                    {
                        PrivilegeCount = 1,
                        Luid = luid,
                        Attributes = SE_PRIVILEGE_ENABLED
                    };
                    AdjustTokenPrivileges(token, false, ref newState, 0, IntPtr.Zero, IntPtr.Zero);
                }
                catch
                {
                }
                finally
                {
                    if (token != IntPtr.Zero)
                        CloseHandle(token);
                }
            }
        }

        /// <summary>
        /// Keys protected by the system (service keys, Diagnostics, Windows Search, etc.) are owned
        /// by SYSTEM or TrustedInstaller and deny write access to Administrators, which made the
        /// rollback fail with "acesso ao registro solicitado nao e permitido". Enables the ownership
        /// privileges, takes ownership of the key for Administrators and grants FullControl so the
        /// rollback can write to it.
        /// </summary>
        private static bool EnsureKeyWritable(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                if (!TrySplitHivePath(path, out var hive, out var subKey) || string.IsNullOrEmpty(subKey))
                    return false;

                EnableRollbackPrivileges();

                var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                {
                    using (var key = baseKey.OpenSubKey(subKey, RegistryKeyPermissionCheck.ReadSubTree, RegistryRights.TakeOwnership))
                    {
                        if (key == null)
                            return false;

                        var security = key.GetAccessControl(AccessControlSections.Owner);
                        if (!Equals(security.GetOwner(typeof(SecurityIdentifier)), admins))
                        {
                            security.SetOwner(admins);
                            key.SetAccessControl(security);
                        }
                    }

                    using (var key = baseKey.OpenSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions | RegistryRights.ReadKey))
                    {
                        if (key == null)
                            return false;

                        var security = key.GetAccessControl(AccessControlSections.Access);
                        var alreadyGranted = security
                            .GetAccessRules(true, false, typeof(SecurityIdentifier))
                            .Cast<RegistryAccessRule>()
                            .Any(r => r.IdentityReference.Equals(admins)
                                      && r.AccessControlType == AccessControlType.Allow
                                      && (r.RegistryRights & RegistryRights.FullControl) != 0);

                        if (!alreadyGranted)
                        {
                            security.AddAccessRule(new RegistryAccessRule(
                                admins,
                                RegistryRights.FullControl,
                                InheritanceFlags.ContainerInherit,
                                PropagationFlags.None,
                                AccessControlType.Allow));
                            key.SetAccessControl(security);
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        private static bool RollbackRegistryKey(RollbackEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Target))
                return false;

            // The playbook created the key: remove it again.
            if (entry.Operation == RollbackOperation.Add)
            {
                var parentPath = entry.Target.Substring(0, entry.Target.LastIndexOf('\\'));

                // Service/protected keys deny delete access to Administrators; fix the
                // ACL on the key and all of its children first.
                EnsureKeyTreeWritableForRollback(entry.Target);

                using (var parent = OpenHive(parentPath, writable: true))
                {
                    if (parent == null)
                        return true;

                    var keyName = entry.Target.Substring(entry.Target.LastIndexOf('\\') + 1);
                    parent.DeleteSubKeyTree(keyName, false);
                }
                return true;
            }

            // The playbook deleted the key: restore it. When a .reg export was captured at
            // delete time, import it to bring back the full value tree; otherwise fall back
            // to re-creating the (empty) key.
            if (entry.Operation == RollbackOperation.Delete)
            {
                string regBackup = null;
                entry.ExtraData?.TryGetValue("regBackup", out regBackup);
                if (!string.IsNullOrEmpty(regBackup) && File.Exists(regBackup))
                {
                    // The import runs as a child process sharing this token; it still
                    // needs every nested key DACL to allow the write, so fix the whole
                    // tree (plus parent) before importing.
                    EnsureKeyTreeWritableForRollback(entry.Target);

                    var psi = new System.Diagnostics.ProcessStartInfo("reg.exe", $"import \"{regBackup}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (var p = System.Diagnostics.Process.Start(psi))
                    {
                        p.WaitForExit(15000);
                        if (p.ExitCode == 0)
                            return true;
                    }
                    // Import failed (permissions, etc.): fall through to at least re-create the key.
                }

                using (CreateHive(entry.Target))
                {
                }
                return true;
            }

            return false;
        }

        private static bool RollbackRegistryValue(RollbackEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Target) || string.IsNullOrEmpty(entry.SubTarget))
                return false;

            // The playbook deleted the value: restore the previous data.
            if (entry.Operation == RollbackOperation.Delete)
            {
                if (string.IsNullOrEmpty(entry.PreviousValue))
                    return false;

                using (var key = OpenHive(entry.Target, writable: true) ?? CreateHive(entry.Target))
                {
                    if (key == null)
                        return false;

                    WriteRegistryValue(key, entry);
                }
                return true;
            }

            // The playbook added or changed the value.
            if (entry.Operation == RollbackOperation.Add || entry.Operation == RollbackOperation.Modify)
            {
                if (string.IsNullOrEmpty(entry.PreviousValue))
                {
                    // The value did not exist before: remove it.
                    using (var key = OpenHive(entry.Target, writable: true))
                    {
                        if (key == null)
                            return true;

                        if (key.GetValue(entry.SubTarget) != null)
                            key.DeleteValue(entry.SubTarget, false);
                    }
                    return true;
                }

                using (var key = OpenHive(entry.Target, writable: true) ?? CreateHive(entry.Target))
                {
                    if (key == null)
                        return false;

                    WriteRegistryValue(key, entry);
                }
                return true;
            }

            return false;
        }

        private static void WriteRegistryValue(RegistryKey key, RollbackEntry entry)
        {
            var kind = entry.RegistryKind ?? "REG_SZ";
            switch (kind)
            {
                case "REG_DWORD":
                case "DWord":
                    if (long.TryParse(entry.PreviousValue, out long dword))
                        key.SetValue(entry.SubTarget, unchecked((int)dword), RegistryValueKind.DWord);
                    break;
                case "REG_QWORD":
                case "QWord":
                    if (ulong.TryParse(entry.PreviousValue, out ulong qword))
                        key.SetValue(entry.SubTarget, qword, RegistryValueKind.QWord);
                    break;
                case "REG_BINARY":
                case "Binary":
                    key.SetValue(entry.SubTarget, HexToBytes(entry.PreviousValue), RegistryValueKind.Binary);
                    break;
                case "REG_MULTI_SZ":
                case "MultiString":
                    key.SetValue(entry.SubTarget, entry.PreviousValue.Split(new[] { "\\0" }, StringSplitOptions.None), RegistryValueKind.MultiString);
                    break;
                case "REG_EXPAND_SZ":
                case "ExpandString":
                    key.SetValue(entry.SubTarget, entry.PreviousValue, RegistryValueKind.ExpandString);
                    break;
                default:
                    key.SetValue(entry.SubTarget, entry.PreviousValue, RegistryValueKind.String);
                    break;
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
                return Array.Empty<byte>();

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        private static string BytesToHex(byte[] bytes)
        {
            return bytes == null ? "" : BitConverter.ToString(bytes).Replace("-", "");
        }

        private static bool RollbackFile(RollbackEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Target))
                return false;

            // The playbook deleted the file: restore it from the session backup.
            if (entry.Operation == RollbackOperation.Delete)
            {
                var backup = entry.PreviousValue;
                if (string.IsNullOrEmpty(backup) || !File.Exists(backup))
                    return false;

                // Verify checksum if manifest exists
                var manifestPath = backup + ".sha256";
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var expectedHash = File.ReadAllText(manifestPath).Trim();
                        using var sha = System.Security.Cryptography.SHA256.Create();
                        using var stream = File.OpenRead(backup);
                        var actualHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                        {
                            Log.WriteSafe(LogType.Warning, $"[Rollback] Checksum mismatch for backup '{backup}'. Expected {expectedHash}, got {actualHash}. Skipping restore.", null);
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteSafe(LogType.Warning, $"[Rollback] Checksum verification failed for '{backup}': {ex.Message}", null);
                    }
                }

                var dir = Path.GetDirectoryName(entry.Target);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                if (File.Exists(entry.Target))
                    MakeFileWritable(entry.Target);
                File.Copy(backup, entry.Target, true);
                Log.WriteSafe(LogType.Info, $"[Rollback] File restored: {entry.Target}", null);
                return true;
            }

            // The playbook created the file or directory: remove it again.
            if (entry.Operation == RollbackOperation.Add)
            {
                if (File.Exists(entry.Target))
                {
                    MakeFileWritable(entry.Target);
                    File.Delete(entry.Target);
                }
                else if (Directory.Exists(entry.Target) && !Directory.EnumerateFileSystemEntries(entry.Target).Any())
                {
                    ClearFileAttributesRecursive(entry.Target);
                    Directory.Delete(entry.Target);
                }
                return true;
            }

            // The playbook overwrote the file: restore the previous content.
            if (entry.Operation == RollbackOperation.Modify)
            {
                var backup = entry.PreviousValue;
                if (string.IsNullOrEmpty(backup) || !File.Exists(backup))
                    return false;

                // Verify checksum if manifest exists
                var manifestPath = backup + ".sha256";
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var expectedHash = File.ReadAllText(manifestPath).Trim();
                        using var sha = System.Security.Cryptography.SHA256.Create();
                        using var stream = File.OpenRead(backup);
                        var actualHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                        {
                            Log.WriteSafe(LogType.Warning, $"[Rollback] Checksum mismatch for backup '{backup}'. Skipping restore.", null);
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteSafe(LogType.Warning, $"[Rollback] Checksum verification failed for '{backup}': {ex.Message}", null);
                    }
                }

                MakeFileWritable(entry.Target);
                File.Copy(backup, entry.Target, true);
                Log.WriteSafe(LogType.Info, $"[Rollback] File modified restored: {entry.Target}", null);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Restoring over protected targets (WinSxS, service exes, etc.) fails with
        /// UnauthorizedAccess when the file is ReadOnly/System/Hidden or owned by
        /// TrustedInstaller. Enables ownership privileges, takes ownership for
        /// Administrators, grants FullControl and strips restrictive attributes.
        /// </summary>
        private static void MakeFileWritable(string path)
        {
            try
            {
                var attrs = File.GetAttributes(path);
                if ((attrs & (FileAttributes.ReadOnly | FileAttributes.Hidden)) != 0)
                    File.SetAttributes(path, attrs & ~(FileAttributes.ReadOnly | FileAttributes.Hidden));
                return;
            }
            catch (UnauthorizedAccessException) { }
            catch (FileNotFoundException) { return; }
            catch { return; }

            try
            {
                EnableRollbackPrivileges();
                var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                var security = File.GetAccessControl(path, AccessControlSections.Owner | AccessControlSections.Access);
                security.SetOwner(admins);
                security.AddAccessRule(new FileSystemAccessRule(
                    admins,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));
                File.SetAccessControl(path, security);

                var finalAttrs = File.GetAttributes(path);
                File.SetAttributes(path, finalAttrs & ~(FileAttributes.ReadOnly | FileAttributes.Hidden));
            }
            catch { }
        }

        private static bool RollbackService(RollbackEntry entry)
        {
            string regBackup = null;
            string startType = null;
            string wasRunning = null;
            entry.ExtraData?.TryGetValue("regBackup", out regBackup);
            entry.ExtraData?.TryGetValue("startType", out startType);
            entry.ExtraData?.TryGetValue("wasRunning", out wasRunning);

            var result = AdvancedRollback.RollbackService(
                entry.Target,
                entry.Operation == RollbackOperation.Delete
                    ? ServiceOperation.Delete
                    : ServiceOperation.Change,
                regBackup,
                // PreviousValue carries the original start mode for Modify entries.
                entry.Operation == RollbackOperation.Modify ? entry.PreviousValue : startType,
                wasRunning);
            return result.Success;
        }

        /// <summary>
        /// ACL fix shared with AdvancedRollback: protected keys (services, WSearch, etc.)
        /// deny write access to Administrators until ownership/DACL is adjusted.
        /// </summary>
        internal static bool EnsureKeyWritableForRollback(string keyPath)
        {
            return EnsureKeyWritable(keyPath);
        }

        /// <summary>
        /// Maximum-permission variant: unlocks the key itself AND its parent chain AND
        /// every existing subkey recursively. reg.exe import writes into several nested
        /// keys that can each carry their own restrictive DACL (e.g. SAM Domains\Account\Users),
        /// and DeleteSubKeyTree enumerates children that individually deny delete access.
        /// </summary>
        internal static bool EnsureKeyTreeWritableForRollback(string keyPath)
        {
            bool ok = !string.IsNullOrEmpty(keyPath);

            // Parent must allow create/delete of children (import recreates the key).
            int lastSep = keyPath != null ? keyPath.LastIndexOf('\\') : -1;
            if (lastSep > 0)
                ok &= EnsureKeyWritable(keyPath.Substring(0, lastSep));

            ok &= EnsureKeyWritable(keyPath);

            try
            {
                if (TrySplitHivePath(keyPath, out var hive, out var subKey) && !string.IsNullOrEmpty(subKey))
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                    {
                        using (var root = OpenSubKeyBestEffort(baseKey, subKey))
                        {
                            if (root != null)
                                GrantSubtreeWritable(root, 0);
                        }
                    }
                }
            }
            catch { }

            return ok;
        }

        private static RegistryKey OpenSubKeyBestEffort(RegistryKey parent, string name)
        {
            var sub = parent.OpenSubKey(name, true);
            if (sub != null)
                return sub;

            // Denied outright: take ownership of this single node, then retry.
            if (EnsureKeyWritable(parent.Name + "\\" + name))
                return parent.OpenSubKey(name, true);
            return null;
        }

        private static void GrantSubtreeWritable(RegistryKey key, int depth)
        {
            if (key == null || depth > 10)
                return;

            GrantFullControl(key);

            string[] names;
            try { names = key.GetSubKeyNames(); }
            catch { return; }

            foreach (var name in names)
            {
                RegistryKey sub = null;
                try { sub = key.OpenSubKey(name, true); }
                catch
                {
                    if (EnsureKeyWritable(key.Name + "\\" + name))
                    {
                        try { sub = key.OpenSubKey(name, true); } catch { }
                    }
                }
                if (sub == null)
                    continue;

                using (sub)
                    GrantSubtreeWritable(sub, depth + 1);
            }
        }

        /// <summary>Grants Administrators FullControl on an already-open key (no owner change).</summary>
        private static void GrantFullControl(RegistryKey key)
        {
            try
            {
                var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                var security = key.GetAccessControl(AccessControlSections.Access);
                var alreadyGranted = security
                    .GetAccessRules(true, false, typeof(SecurityIdentifier))
                    .Cast<RegistryAccessRule>()
                    .Any(r => r.IdentityReference.Equals(admins)
                              && r.AccessControlType == AccessControlType.Allow
                              && (r.RegistryRights & RegistryRights.FullControl) != 0);
                if (!alreadyGranted)
                {
                    security.AddAccessRule(new RegistryAccessRule(
                        admins,
                        RegistryRights.FullControl,
                        InheritanceFlags.ContainerInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));
                    key.SetAccessControl(security);
                }
            }
            catch { }
        }
    }

    public class RollbackResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public int TotalEntries { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
    }
}
