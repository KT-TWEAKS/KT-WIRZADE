using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace KTWirzade.Shared.DebugLog
{
    public static class DebugLogger
    {
        private static readonly object _lock = new object();
        private static string _logPath;
        private static bool _enabled;
        private static int _actionIndex;
        private static readonly ConcurrentQueue<DebugEntry> _entries = new ConcurrentQueue<DebugEntry>();

        public static bool Enabled => _enabled;
        public static string LogPath => _logPath;

        public static event Action<DebugEntry> OnEntry;

        public static void Enable(string logFolder)
        {
            _logPath = Path.Combine(logFolder, "Debug.txt");
            _enabled = true;
            _actionIndex = 0;

            var dir = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_logPath, $"KT WIRZADE DEBUG LOG - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
            File.WriteAllText(_logPath + ".tail", "");
        }

        public static void Disable()
        {
            _enabled = false;
        }

        public static void LogActionStart(string actionType, string properties, string status = null)
        {
            if (!_enabled) return;

            var entry = new DebugEntry
            {
                Index = Interlocked.Increment(ref _actionIndex),
                Timestamp = DateTime.Now,
                ActionType = actionType,
                Properties = properties,
                Status = status,
                Phase = DebugPhase.Start
            };

            _entries.Enqueue(entry);
            WriteEntry(entry);
            OnEntry?.Invoke(entry);
        }

        public static void LogActionEnd(string actionType, int exitCode, long elapsedMs, string error = null)
        {
            if (!_enabled) return;

            var entry = new DebugEntry
            {
                Index = _actionIndex,
                Timestamp = DateTime.Now,
                ActionType = actionType,
                ExitCode = exitCode,
                ElapsedMs = elapsedMs,
                Error = error,
                Phase = DebugPhase.End
            };

            _entries.Enqueue(entry);
            WriteEntry(entry);
            OnEntry?.Invoke(entry);
        }

        public static void LogActionSkipped(string actionType, string reason)
        {
            if (!_enabled) return;

            var entry = new DebugEntry
            {
                Index = Interlocked.Increment(ref _actionIndex),
                Timestamp = DateTime.Now,
                ActionType = actionType,
                Properties = reason,
                Phase = DebugPhase.Skipped
            };

            _entries.Enqueue(entry);
            WriteEntry(entry);
            OnEntry?.Invoke(entry);
        }

        public static void LogMessage(string message)
        {
            if (!_enabled) return;

            var entry = new DebugEntry
            {
                Index = _actionIndex,
                Timestamp = DateTime.Now,
                ActionType = "DEBUG",
                Properties = message,
                Phase = DebugPhase.Info
            };

            _entries.Enqueue(entry);
            WriteEntry(entry);
            OnEntry?.Invoke(entry);
        }

        private static void WriteEntry(DebugEntry entry)
        {
            try
            {
                lock (_lock)
                {
                    var dir = Path.GetDirectoryName(_logPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var sb = new StringBuilder();
                    sb.Append($"[{entry.Timestamp:HH:mm:ss.fff}] ");
                    sb.Append($"#{entry.Index:D4} ");

                    switch (entry.Phase)
                    {
                        case DebugPhase.Start:
                            sb.Append($"START  ");
                            sb.Append($"[{entry.ActionType}] ");
                            if (!string.IsNullOrEmpty(entry.Status))
                                sb.Append($"{entry.Status} ");
                            sb.Append($"\n         {entry.Properties}");
                            break;
                        case DebugPhase.End:
                            sb.Append($"END    ");
                            sb.Append($"[{entry.ActionType}] ");
                            sb.Append($"exit={entry.ExitCode} ");
                            sb.Append($"time={entry.ElapsedMs}ms ");
                            if (!string.IsNullOrEmpty(entry.Error))
                                sb.Append($"ERROR={entry.Error}");
                            break;
                        case DebugPhase.Skipped:
                            sb.Append($"SKIP   ");
                            sb.Append($"[{entry.ActionType}] ");
                            sb.Append(entry.Properties);
                            break;
                        case DebugPhase.Info:
                            sb.Append($"INFO   ");
                            sb.Append(entry.Properties);
                            break;
                    }

                    File.AppendAllText(_logPath, sb.ToString() + "\n");
                    File.AppendAllText(_logPath + ".tail", sb.ToString() + "\n");
                }
            }
            catch { }
        }

        public static string GetActionProperties(KTWirzade.Shared.Tasks.ITaskAction action)
        {
            var sb = new StringBuilder();
            var typeName = action.GetType().Name;

            try
            {
                if (action is Actions.RunAction run)
                {
                    sb.Append($"exe=\"{run.Exe}\"");
                    if (run.Arguments != null) sb.Append($" args=\"{run.Arguments}\"");
                    if (run.ExeDir) sb.Append(" exeDir=true");
                    if (run.BaseDir) sb.Append(" baseDir=true");
                    sb.Append($" runas={run.RunAs}");
                    if (run.Timeout.HasValue) sb.Append($" timeout={run.Timeout}");
                    sb.Append($" wait={run.Wait}");
                    if (run.HideWindow) sb.Append(" hideWindow=true");
                    if (run.CreateWindow) sb.Append(" createWindow=true");
                    if (!run.ShowOutput) sb.Append(" showOutput=false");
                    if (run.HandleExitCodes != null)
                        sb.Append($" handleExitCodes=[{string.Join(",", run.HandleExitCodes.Keys)}]");
                }
                else if (action is Actions.PowerShellAction ps)
                {
                    var cmd = ps.Command ?? "";
                    if (cmd.Length > 200) cmd = cmd.Substring(0, 200) + "...";
                    sb.Append($"command=\"{cmd}\"");
                    sb.Append($" runas={ps.RunAs}");
                    if (ps.ExeDir) sb.Append(" exeDir=true");
                    if (ps.Timeout.HasValue) sb.Append($" timeout={ps.Timeout}");
                    sb.Append($" wait={ps.Wait}");
                    if (ps.HandleExitCodes != null)
                        sb.Append($" handleExitCodes=[{string.Join(",", ps.HandleExitCodes.Keys)}]");
                }
                else if (action is Actions.RegistryValueAction reg)
                {
                    sb.Append($"key=\"{reg.KeyName}\"");
                    sb.Append($" value=\"{reg.Value}\"");
                    sb.Append($" data=\"{reg.Data}\"");
                    sb.Append($" type={reg.Type}");
                    if (reg.Operation != null) sb.Append($" op={reg.Operation}");
                }
                else if (action is Actions.RegistryKeyAction regKey)
                {
                    sb.Append($"key=\"{regKey.KeyName}\"");
                    sb.Append($" operation={regKey.Operation}");
                    sb.Append($" scope={regKey.Scope}");
                }
                else if (action is Actions.FileAction file)
                {
                    sb.Append($"path=\"{file.RawPath}\"");
                    if (file.TrustedInstaller) sb.Append(" useNSudoTI=true");
                    if (file.ExeFirst) sb.Append(" prioritizeExe=true");
                }
                else if (action is Actions.ScheduledTaskAction task)
                {
                    sb.Append($"operation={task.Operation}");
                    if (task.Path != null) sb.Append($" path=\"{task.Path}\"");
                    if (task.RawTask != null) sb.Append($" data=\"{task.RawTask}\"");
                }
                else if (action is Actions.ServiceAction svc)
                {
                    sb.Append($"name=\"{svc.ServiceName}\"");
                    sb.Append($" operation={svc.Operation}");
                    if (svc.Startup.HasValue) sb.Append($" startup={svc.Startup}");
                    sb.Append($" deleteStop={svc.DeleteStop}");
                    sb.Append($" registryDelete={svc.RegistryDelete}");
                }
                else if (action is Actions.AppxAction appx)
                {
                    sb.Append($"operation={appx.Operation}");
                    if (appx.Name != null) sb.Append($" name=\"{appx.Name}\"");
                    sb.Append($" type={appx.Type}");
                    if (appx.Unregister) sb.Append(" unregister=true");
                }
                else if (action is Actions.LineInFileAction line)
                {
                    sb.Append($"path=\"{line.RawPath}\"");
                    if (line.RawLines != null) sb.Append($" line=\"{line.RawLines}\"");
                    sb.Append($" operation={line.Operation}");
                }
                else if (action is Actions.UserAction user)
                {
                    if (user.Username != null) sb.Append($" username=\"{user.Username}\"");
                    sb.Append($" admin={user.IsAdmin}");
                }
                else if (action is Actions.ShortcutAction shortcut)
                {
                    sb.Append($"path=\"{shortcut.RawPath}\"");
                    if (shortcut.Destination != null) sb.Append($" destination=\"{shortcut.Destination}\"");
                    if (shortcut.Name != null) sb.Append($" name=\"{shortcut.Name}\"");
                    if (shortcut.Description != null) sb.Append($" desc=\"{shortcut.Description}\"");
                }
                else if (action is Actions.WriteStatusAction status)
                {
                    sb.Append($"status=\"{status.Status}\"");
                }
                else if (action is Actions.CmdAction cmd)
                {
                    sb.Append($"command=\"{cmd.Command}\"");
                    sb.Append($" runas={cmd.RunAs}");
                    if (cmd.ExeDir) sb.Append(" exeDir=true");
                    if (cmd.Timeout.HasValue) sb.Append($" timeout={cmd.Timeout}");
                    sb.Append($" wait={cmd.Wait}");
                    if (cmd.HandleExitCodes != null)
                        sb.Append($" handleExitCodes=[{string.Join(",", cmd.HandleExitCodes.Keys)}]");
                }
                else if (action is Actions.DownloadAction dl)
                {
                    if (dl.Package != null) sb.Append($" package=\"{dl.Package}\"");
                    if (dl.Destination != null) sb.Append($" destination=\"{dl.Destination}\"");
                    if (dl.Url != null) sb.Append($" url=\"{dl.Url}\"");
                    if (dl.Git != null) sb.Append($" git=\"{dl.Git}\"");
                    if (dl.Regex != null) sb.Append($" regex=\"{dl.Regex}\"");
                    if (dl.Overwrite) sb.Append(" overwrite=true");
                }
                else if (action is Actions.LanguageAction lang)
                {
                    if (lang.Tag != null) sb.Append($" tag=\"{lang.Tag}\"");
                    sb.Append($" display={lang.Display}");
                }
                else if (action is Actions.RegexFileAction regex)
                {
                    sb.Append($"path=\"{regex.TargetDirectory}\"");
                    if (regex.RegexPattern != null) sb.Append($" regex=\"{regex.RegexPattern}\"");
                    if (regex.SearchPattern != null) sb.Append($" searchPattern=\"{regex.SearchPattern}\"");
                    if (regex.SearchOption != null) sb.Append($" searchOption={regex.SearchOption}");
                    sb.Append($" action={regex.Operation}");
                    if (regex.DestinationPath != null) sb.Append($" destination=\"{regex.DestinationPath}\"");
                    if (regex.TrustedInstaller) sb.Append(" useNSudoTI=true");
                    if (regex.PreserveStructure) sb.Append(" preserveStructure=true");
                }
                else if (action is Actions.SoftwareAction sw)
                {
                    if (sw.Package != null) sb.Append($" package=\"{sw.Package}\"");
                    if (sw.Name != null) sb.Append($" name=\"{sw.Name}\"");
                    sb.Append($" upgrade={sw.Upgrade}");
                    sb.Append($" source={sw.Source}");
                    if (sw.Fallback != null) sb.Append($" fallback=\"{sw.Fallback.Name}\"");
                }
                else if (action is Actions.SystemPackageAction sys)
                {
                    if (sys.Name != null) sb.Append($" name=\"{sys.Name}\"");
                    sb.Append($" arch={sys.Arch}");
                    if (sys.Language != null) sb.Append($" language=\"{sys.Language}\"");
                    if (sys.RegexExcludeList != null) sb.Append($" regexExclude=[{string.Join(",", sys.RegexExcludeList)}]");
                    if (sys.ExcludeDependentsList != null) sb.Append($" excludeDependents=[{string.Join(",", sys.ExcludeDependentsList)}]");
                }
                else if (action is Actions.TaskKillAction kill)
                {
                    if (kill.ProcessName != null) sb.Append($" name=\"{kill.ProcessName}\"");
                    if (kill.PathContains != null) sb.Append($" pathContains=\"{kill.PathContains}\"");
                }
                else if (action is Actions.UpdateAction upd)
                {
                    if (upd.PackageName != null) sb.Append($" name=\"{upd.PackageName}\"");
                }
                else if (action is Tasks.TaskAction baseAction)
                {
                    sb.Append($"(base TaskAction)");
                }
                else
                {
                    sb.Append($"(unknown type: {typeName})");
                }

                var taskAction = action as Tasks.TaskAction;
                if (taskAction != null)
                {
                    if (taskAction.Option != null) sb.Append($" option=\"{taskAction.Option}\"");
                    if (taskAction.OnUpgrade.HasValue) sb.Append($" onUpgrade={taskAction.OnUpgrade}");
                    if (taskAction.Builds != null) sb.Append($" builds=[{string.Join(",", taskAction.Builds)}]");
                    if (taskAction.Arch != null) sb.Append($" arch={taskAction.Arch}");
                    if (taskAction.ISO != null) sb.Append($" iso={taskAction.ISO}");
                    if (taskAction.OOBE != null) sb.Append($" oobe={taskAction.OOBE}");
                    if (taskAction.IgnoreErrors) sb.Append(" ignoreErrors=true");
                    if (taskAction.ErrorAction != null) sb.Append($" errorAction={taskAction.ErrorAction}");
                    if (taskAction.Status != null) sb.Append($" status=\"{taskAction.Status}\"");
                }
            }
            catch (Exception ex)
            {
                sb.Append($"(error reading props: {ex.Message})");
            }

            return sb.ToString();
        }
    }

    public enum DebugPhase
    {
        Start,
        End,
        Skipped,
        Info
    }

    public class DebugEntry
    {
        public int Index { get; set; }
        public DateTime Timestamp { get; set; }
        public string ActionType { get; set; }
        public string Properties { get; set; }
        public string Status { get; set; }
        public int ExitCode { get; set; }
        public long ElapsedMs { get; set; }
        public string Error { get; set; }
        public DebugPhase Phase { get; set; }
    }
}
