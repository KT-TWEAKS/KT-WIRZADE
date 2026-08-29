using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KTWirzade.Shared.Tasks;
using YamlDotNet.Serialization;
using Core;

namespace KTWirzade.Shared.Actions
{
    /// <summary>
    /// Enhanced file action that supports both wildcard and regex patterns for file matching.
    /// When RegexPattern is set, it uses full regex matching instead of simple wildcards.
    /// </summary>
    public class RegexFileAction : Tasks.TaskAction, ITaskAction
    {
        public void RunTaskOnMainThread(Output.OutputWriter output) { throw new NotImplementedException(); }

        [YamlMember(typeof(string), Alias = "path")]
        public string TargetDirectory { get; set; }

        [YamlMember(typeof(string), Alias = "regex")]
        public string RegexPattern { get; set; }

        [YamlMember(typeof(string), Alias = "searchPattern")]
        public string SearchPattern { get; set; } = "*.*";

        [YamlMember(typeof(string), Alias = "searchOption")]
        public string SearchOption { get; set; } = "TopDirectoryOnly"; // TopDirectoryOnly or AllDirectories

        [YamlMember(typeof(int), Alias = "weight")]
        public int ProgressWeight { get; set; } = 2;

        [YamlMember(typeof(bool), Alias = "useNSudoTI")]
        public bool TrustedInstaller { get; set; } = false;

        [YamlMember(typeof(string), Alias = "action")]
        public string Operation { get; set; } = "delete"; // delete, move, rename

        [YamlMember(typeof(string), Alias = "destination")]
        public string DestinationPath { get; set; }

        [YamlMember(typeof(bool), Alias = "preserveStructure")]
        public bool PreserveStructure { get; set; } = false;

        public int GetProgressWeight() => ProgressWeight;
        public ErrorAction GetDefaultErrorAction() => Tasks.ErrorAction.Notify;
        public bool GetRetryAllowed() => true;
        public void ResetProgress() { }

        public string ErrorString() => $"RegexFileAction failed to process files matching '{RegexPattern}' in '{TargetDirectory}'.";

        private string GetRealPath(string path)
        {
            return AmeliorationUtil.ISO
                ? Environment.ExpandEnvironmentVariables(path).Replace("C:", AmeliorationUtil.WimPath).Replace("c:", AmeliorationUtil.WimPath)
                : Environment.ExpandEnvironmentVariables(path);
        }

        public UninstallTaskStatus GetStatus(Output.OutputWriter output)
        {
            if (AmeliorationUtil.ISO)
                return UninstallTaskStatus.Completed;

            var realPath = GetRealPath(TargetDirectory);
            if (!Directory.Exists(realPath))
                return UninstallTaskStatus.Completed;

            var files = GetMatchingFiles(realPath);
            return files.Any() ? UninstallTaskStatus.ToDo : UninstallTaskStatus.Completed;
        }

        private List<string> GetMatchingFiles(string directory)
        {
            var option = SearchOption.Equals("AllDirectories", StringComparison.OrdinalIgnoreCase)
                ? System.IO.SearchOption.AllDirectories
                : System.IO.SearchOption.TopDirectoryOnly;

            var allFiles = Directory.GetFiles(directory, SearchPattern, option);

            if (string.IsNullOrEmpty(RegexPattern))
                return allFiles.ToList();

            var regex = new Regex(RegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return allFiles.Where(f => regex.IsMatch(Path.GetFileName(f)) || regex.IsMatch(f)).ToList();
        }

        public async Task<bool> RunTask(Output.OutputWriter output)
        {
            var realPath = GetRealPath(TargetDirectory);
            if (!Directory.Exists(realPath))
            {
                output.WriteLineSafe("Info", $"Directory not found: {realPath}");
                return true;
            }

            var matchingFiles = GetMatchingFiles(realPath);

            if (!matchingFiles.Any())
            {
                output.WriteLineSafe("Info", $"No files matched regex '{RegexPattern}' in {realPath}");
                return true;
            }

            output.WriteLineSafe("Info", $"Found {matchingFiles.Count} file(s) matching pattern '{RegexPattern}'");

            int processed = 0;
            foreach (var file in matchingFiles)
            {
                try
                {
                    switch (Operation.ToLowerInvariant())
                    {
                        case "delete":
                            await DeleteFileSafe(file, output);
                            break;
                        case "move":
                            await MoveFileSafe(file, output);
                            break;
                        default:
                            output.WriteLineSafe("Warning", $"Unknown operation: {Operation}");
                            break;
                    }

                    processed++;
                    output.WriteLineSafe("Info", $"Processed ({processed}/{matchingFiles.Count}): {Path.GetFileName(file)}");
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(LogType.Warning, e, output.LogOptions);
                    output.WriteLineSafe("Warning", $"Failed to process {file}: {e.Message}");
                }
            }

            return true;
        }

        private async Task DeleteFileSafe(string file, Output.OutputWriter output)
        {
            if (!TrustedInstaller)
            {
                try { File.Delete(file); }
                catch (Exception e)
                {
                    // Try with cmd
                    var cmdAction = new CmdAction { Command = $"del /q /f \"{file}\"" };
                    cmdAction.RunTaskOnMainThread(output);
                }
            }
            else if (File.Exists("NSudoLC.exe"))
            {
                var tiDelAction = new RunAction
                {
                    Exe = "NSudoLC.exe",
                    Arguments = $"-U:T -P:E -M:S -Priority:RealTime -UseCurrentConsole -Wait cmd /c \"del /q /f \"{file}\"\"",
                    BaseDir = true,
                    CreateWindow = false
                };
                tiDelAction.RunTaskOnMainThread(output);
            }
            else
            {
                // Without this branch the file was silently counted as processed
                // while never being deleted.
                output.WriteLineSafe("Warning", $"NSudoLC.exe not found next to the application; could not delete '{file}' as TrustedInstaller.");
            }
        }

        private async Task MoveFileSafe(string file, Output.OutputWriter output)
        {
            if (string.IsNullOrEmpty(DestinationPath))
            {
                output.WriteLineSafe("Warning", "Destination path not set for move operation.");
                return;
            }

            var destDir = GetRealPath(DestinationPath);
            if (PreserveStructure)
            {
                // relativePath still contains the file name; only its directory part
                // belongs in destDir, otherwise the destination gains a bogus folder
                // named after the file and File.Move fails.
                var relativePath = file.Replace(GetRealPath(TargetDirectory), "").TrimStart('\\', '/');
                var relativeDir = Path.GetDirectoryName(relativePath);
                if (!string.IsNullOrEmpty(relativeDir))
                    destDir = Path.Combine(destDir, relativeDir);
            }

            var destFile = Path.Combine(destDir, Path.GetFileName(file));

            if (!Directory.Exists(Path.GetDirectoryName(destFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
            }

            if (File.Exists(destFile))
            {
                File.Delete(destFile);
            }

            File.Move(file, destFile);
        }
    }
}
