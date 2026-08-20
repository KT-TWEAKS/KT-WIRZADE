using System;
using System.Linq;
using System.Threading.Tasks;
using KTWirzade.Shared.Exceptions;
using KTWirzade.Shared.Tasks;
using YamlDotNet.Serialization;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Core;

namespace KTWirzade.Shared.Actions
{
    // Integrate ame-assassin later
    internal class SystemPackageAction : Tasks.TaskAction, ITaskAction
    {
        public void RunTaskOnMainThread(Output.OutputWriter output) { throw new NotImplementedException(); }
        public enum Architecture
        {
            amd64 = 0,
            wow64 = 1,
            msil = 2,
            x86 = 3,
            All = 4
        }
        
        [YamlMember(typeof(string), Alias = "name")]
        public string Name { get; set; }

        [YamlMember(typeof(string), Alias = "arch")]
        public Architecture Arch { get; set; } = Architecture.All;

        [YamlMember(typeof(string), Alias = "language")]
        public string Language { get; set; } = "*";

        [YamlMember(typeof(string), Alias = "regexExcludeFiles")]
        public string[]? RegexExcludeList { get; set; }
        [YamlMember(typeof(string), Alias = "excludeDependents")]
        public string[]? ExcludeDependentsList { get; set; }

        [YamlMember(typeof(string[]), Alias = "weight")]
        public int ProgressWeight { get; set; } = 15;
        public int GetProgressWeight() => ProgressWeight;
        public ErrorAction GetDefaultErrorAction() => Tasks.ErrorAction.Notify;
        public bool GetRetryAllowed() => false;
        
        private bool InProgress { get; set; }
        public void ResetProgress() => InProgress = false;

        public string ErrorString() => $"SystemPackageAction failed to remove '{Name}'.";
        
        public override string? IsISOCompatible() => "SystemPackageAction does not support iso.";
        public UninstallTaskStatus GetStatus(Output.OutputWriter output)
        {
            if (InProgress) return UninstallTaskStatus.InProgress;
            return HasFinished ? UninstallTaskStatus.Completed : UninstallTaskStatus.ToDo;
        }
        private bool HasFinished = false;
        public async Task<bool> RunTask(Output.OutputWriter output)
        {                
            if (InProgress) throw new TaskInProgressException("Another Appx action was called while one was in progress.");
            InProgress = true;

            output.WriteLineSafe("Info", $"Removing system package '{Name}'...");

            var excludeArgs = new StringBuilder("");
            if (RegexExcludeList != null)
            {
                foreach (var regex in RegexExcludeList)
                {
                    excludeArgs.Append(@$" -xf ""{regex}""");
                }
            }
            
            var excludeDependsArgs = new StringBuilder("");
            if (ExcludeDependentsList != null)
            {
                foreach (var dependent in ExcludeDependentsList)
                {
                    excludeDependsArgs.Append(@$" -xdependent ""{dependent}""");
                }
            }

            string kernelDriverArg = AmeliorationUtil.UseKernelDriver ? " -UseKernelDriver" : "";

            string ameAssassinPath = Path.Combine(AmeliorationUtil.Playbook.Path, "Executables", "ame-assassin", "ame-assassin.exe");

            if (!File.Exists(ameAssassinPath))
            {
                output.WriteLineSafe("Warning", "ame-assassin.exe not found in playbook, falling back to PowerShell Get-WindowsPackage.");
                return await RunPowerShellFallback(output);
            }

            var psi = new ProcessStartInfo()
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $@"-SystemPackage ""{Name}"" -Arch {Arch.ToString()} -Language ""{Language}""" + excludeArgs + excludeDependsArgs + kernelDriverArg,
                FileName = ameAssassinPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            outputWriter = output;

            var proc = Process.Start(psi);

            if (proc == null)
            {
                HasFinished = true;
                InProgress = false;
                throw new InvalidOperationException("Failed to start ame-assassin process.");
            }

            proc.OutputDataReceived += ProcOutputHandler;
            proc.ErrorDataReceived += ProcOutputHandler;

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            bool exited = proc.WaitForExit(30000);

            // WaitForExit alone seems to not be entirely reliable
            while (!exited && ExeRunning(proc))
            {
                exited = proc.WaitForExit(30000);
            }

            HasFinished = true;

            InProgress = false;
            return true;
        }

        private async Task<bool> RunPowerShellFallback(Output.OutputWriter output)
        {
            string escapedName = Name.Replace("'", "''");
            string psCommand = $"Get-WindowsPackage -Online | Where-Object {{ $_.PackageName -like '*{escapedName}*' }} | Remove-WindowsPackage -Online -NoRestart -ErrorAction SilentlyContinue";

            var psi = new ProcessStartInfo()
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $"-NoProfile -NonInteractive -NoLogo -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            outputWriter = output;

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                HasFinished = true;
                InProgress = false;
                return false;
            }

            proc.OutputDataReceived += ProcOutputHandler;
            proc.ErrorDataReceived += ProcOutputHandler;
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await Task.Run(() => proc.WaitForExit());

            HasFinished = true;
            InProgress = false;
            return true;
        }

        private Output.OutputWriter outputWriter = null;
        private void ProcOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (!string.IsNullOrWhiteSpace(outLine.Data))
                outputWriter.WriteLineSafe("Process", outLine.Data);
        }

        private static bool ExeRunning(Process process)
        {
            try
            {
                return Process.GetProcessesByName(process.ProcessName).Any(x => x.Id == process.Id);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
