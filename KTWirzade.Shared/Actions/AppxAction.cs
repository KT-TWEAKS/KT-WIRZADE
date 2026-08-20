using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
//using Windows.ApplicationModel;
//using Windows.Management.Deployment;
using KTWirzade.Shared.Exceptions;
using KTWirzade.Shared.Tasks;
using YamlDotNet.Serialization;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using Core;

namespace KTWirzade.Shared.Actions
{
    // Integrate ame-assassin later
    internal class AppxAction : Tasks.TaskAction, ITaskAction
    {
        public void RunTaskOnMainThread(Output.OutputWriter output) { throw new NotImplementedException(); }
        
        public enum AppxOperation
        {
            Remove = 0,
            ClearCache = 1,
        }
        public enum Level
        {
            Family = 0,
            Package = 1,
            App = 2
        }
        
        [YamlMember(typeof(string), Alias = "name")]
        public string Name { get; set; }

        [YamlMember(typeof(Level), Alias = "type")]
        public Level? Type { get; set; } = Level.Family;
        
        [YamlMember(typeof(AppxOperation), Alias = "operation")]
        public AppxOperation Operation { get; set; } = AppxOperation.Remove;
        [YamlMember(typeof(bool), Alias = "verboseOutput")]
        public bool Verbose { get; set; } = false;
        
        [YamlMember(typeof(bool), Alias = "unregister")]
        public bool Unregister { get; set; } = false;
        
        [YamlMember(typeof(string), Alias = "weight")]
        public int ProgressWeight { get; set; } = 30;
        public int GetProgressWeight() => ProgressWeight;
        public ErrorAction GetDefaultErrorAction() => Tasks.ErrorAction.Notify;
        public bool GetRetryAllowed() => false;
        
        private bool InProgress { get; set; }
        public void ResetProgress() => InProgress = false;

        public string ErrorString() => $"AppxAction failed to remove '{Name}'.";
        
        /*
        private Package GetPackage()
        {
            var packageManager = new PackageManager();

            return packageManager.FindPackages().FirstOrDefault(package => package.Id.Name == Name);
        }
        */
        public override string? IsISOCompatible() => "AppxAction does not support iso yet.";
        public UninstallTaskStatus GetStatus(Output.OutputWriter output)
        {
            if (InProgress) return UninstallTaskStatus.InProgress;
            return HasFinished ? UninstallTaskStatus.Completed : UninstallTaskStatus.ToDo;
            //return GetPackage() == null ? UninstallTaskStatus.Completed : UninstallTaskStatus.ToDo;
        }
        private bool HasFinished = false;
        public async Task<bool> RunTask(Output.OutputWriter output)
        {
            if (InProgress) throw new TaskInProgressException("Another Appx action was called while one was in progress.");
            InProgress = true;

            output.WriteLineSafe("Info", $"Removing APPX {Type.ToString().ToLower()} '{Name}'...");

            if (AmeliorationUtil.ISO)
            {
                if (Type == Level.App)
                {
                    foreach (string manifest in Directory.GetFiles(Path.Combine(AmeliorationUtil.WimPath, "Program Files\\WindowsApps"), "AppxManifest.xml", SearchOption.AllDirectories))
                    {
                        var xml = new XmlDocument();
                        xml.Load(manifest);

                        var appName = Name.Trim('*');
                        var appData = xml.SelectSingleNode($"//*[@Id='{appName}']");
                        try
                        {
                            if (appData != null)
                            {
                                output.WriteLineSafe("Info", $"\r\nRemoving application xml with Id {appName} from file {manifest}...");
                                appData.ParentNode!.RemoveChild(appData);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"\r\nError: Could not remove {appName} from xml document {manifest}.\r\nException: {e.Message}");
                        }

                        xml.Save(manifest);
                    }
                    foreach (string manifest in Directory.GetFiles(Path.Combine(AmeliorationUtil.WimPath, "Windows\\SystemApps"), "AppxManifest.xml", SearchOption.AllDirectories))
                    {
                        var xml = new XmlDocument();
                        xml.Load(manifest);

                        var appName = Name.Trim('*');
                        var appData = xml.SelectSingleNode($"//*[@Id='{appName}']");
                        try
                        {
                            if (appData != null)
                            {
                                output.WriteLineSafe("Info", $"\r\nRemoving application xml with Id {appName} from file {manifest}...");
                                appData.ParentNode!.RemoveChild(appData);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.WriteExceptionSafe(e);
                        }

                        xml.Save(manifest);
                    }
                }
                else
                {
                    foreach (string directory in Directory.GetDirectories(Path.Combine(AmeliorationUtil.WimPath, "Program Files\\WindowsApps"), Name))
                    {
                        output.WriteLineSafe("Info", $"\r\nRemoving APPX package folder {directory}...");
                        
                        try
                        {
                            DirectoryInfo dir = new DirectoryInfo(directory);
                            foreach (FileInfo file in dir.GetFiles("*", SearchOption.AllDirectories))
                                Wrap.ExecuteSafe(() => file.Attributes = FileAttributes.Normal);
                            foreach (DirectoryInfo subDir in dir.GetDirectories("*", SearchOption.AllDirectories))
                                Wrap.ExecuteSafe(() => subDir.Attributes = FileAttributes.Normal);
                            Wrap.ExecuteSafe(() => dir.Attributes = FileAttributes.Normal);

                            Directory.Delete(directory, true);
                        }
                        catch (Exception e)
                        {
                            var action = new FileAction() { RawPath = directory };
                            await action.RunTask(output);
                        }
                    }
                    foreach (string directory in Directory.GetDirectories(Path.Combine(AmeliorationUtil.WimPath, "Windows\\SystemApps"), Name))
                    {
                        output.WriteLineSafe("Info", $"\r\nRemoving APPX package folder {directory}...");
                        try
                        {
                            DirectoryInfo dir = new DirectoryInfo(directory);
                            foreach (FileInfo file in dir.GetFiles("*", SearchOption.AllDirectories))
                                Wrap.ExecuteSafe(() => file.Attributes = FileAttributes.Normal);
                            foreach (DirectoryInfo subDir in dir.GetDirectories("*", SearchOption.AllDirectories))
                                Wrap.ExecuteSafe(() => subDir.Attributes = FileAttributes.Normal);
                            Wrap.ExecuteSafe(() => dir.Attributes = FileAttributes.Normal);
                        
                            Directory.Delete(directory, true);
                        }
                        catch (Exception e)
                        {
                            var action = new FileAction() { RawPath = directory };
                            await action.RunTask(output);
                        }
                    }
                }
                HasFinished = true;
                InProgress = false;
                return true;
            }
            
            WinUtil.CheckKph();

            string ameAssassinPath = FindAmeAssassin();
            if (ameAssassinPath == null)
            {
                output.WriteLineSafe("Warning", "ame-assassin.exe not found in playbook, falling back to PowerShell Remove-AppxPackage (multi-strategy).");
                return await RunPowerShellFallback(output);
            }

            output.WriteLineSafe("Info", $"Using ame-assassin at: {ameAssassinPath}");

            string verboseArg = Verbose ? " -Verbose" : "";
            string unregisterArg = Unregister ? " -Verbose" : "";
            string kernelDriverArg = AmeliorationUtil.UseKernelDriver ? " -UseKernelDriver" : "";

            var psi = new ProcessStartInfo()
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $@"-{Type.ToString()} ""{Name}""" + verboseArg + unregisterArg + kernelDriverArg,
                FileName = ameAssassinPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (Operation == AppxOperation.ClearCache)
            {
                psi.Arguments = $@"-ClearCache ""{Name}""";
            }

            this.outputWriter = output;

            Process proc;
            if (AmeliorationUtil.ISO)
            {
                using (var environment = new ProcessEnvironment(
                           ("SYSTEMROOT", Path.Combine(AmeliorationUtil.WimPath, "Windows")),
                           ("WINDIR", Path.Combine(AmeliorationUtil.WimPath, "Windows")),
                           ("SYSTEMDRIVE", AmeliorationUtil.WimPath),
                           ("HOMEDRIVE", AmeliorationUtil.WimPath),
                           ("PROGRAMDATA", Path.Combine(AmeliorationUtil.WimPath, "ProgramData"))
                       ))
                {
                    proc = Process.Start(psi);
                }
            } else
                proc = Process.Start(psi);

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
            while (!exited && ExeRunning("ame-assassin", proc.Id))
            {
                exited = proc.WaitForExit(30000);
            }

            HasFinished = true;

            InProgress = false;
            return true;
        }

        private static string FindAmeAssassin()
        {
            if (AmeliorationUtil.Playbook?.Path == null) return null;

            string[] candidates = new[]
            {
                Path.Combine(AmeliorationUtil.Playbook.Path, "Executables", "ame-assassin", "ame-assassin.exe"),
                Path.Combine(AmeliorationUtil.Playbook.Path, "Executables", "ame-assassin.exe"),
                Path.Combine(AmeliorationUtil.Playbook.Path, "ame-assassin", "ame-assassin.exe"),
                Path.Combine(AmeliorationUtil.Playbook.Path, "Tools", "ame-assassin", "ame-assassin.exe"),
                Path.Combine(AmeliorationUtil.Playbook.Path, "Tools", "ame-assassin.exe"),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        private Output.OutputWriter outputWriter = null;
        private void ProcOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (!string.IsNullOrWhiteSpace(outLine.Data))
                outputWriter.WriteLineSafe("Process", outLine.Data);
        }

        private static bool ExeRunning(string name, int id)
        {
            try
            {
                return Process.GetProcessesByName(name).Any(x => x.Id == id);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void RemoveISOAppx(Output.OutputWriter output)
        {

        }

        private async Task<bool> RunPowerShellFallback(Output.OutputWriter output)
        {
            string escapedName = Name.Replace("'", "''");
            string nameFilter = Name.Contains("*") ? escapedName : $"*{escapedName}*";
            string typeFilter = Type switch
            {
                Level.Family => "PackageFullName",
                Level.Package => "PackageFullName",
                Level.App => "AppId",
                _ => "PackageFullName"
            };

            this.outputWriter = output;

            output.WriteLineSafe("Info", $"Step 1/5: Stopping AppXSvc and ClipSvc services...");
            await RunPowerShellCommand(output,
                "Stop-Service -Name AppXSvc -Force -ErrorAction SilentlyContinue; " +
                "Stop-Service -Name ClipSVC -Force -ErrorAction SilentlyContinue; " +
                "Set-Service -Name AppXSvc -StartupType Manual; " +
                "Get-Service -Name AppXSvc,ClipSVC | Select-Object Name, Status | Format-Table -AutoSize");

            output.WriteLineSafe("Info", $"Step 2/5: Stopping explorer.exe (releases locked AppX packages)...");
            await RunPowerShellCommand(output,
                "Get-Process -Name explorer -ErrorAction SilentlyContinue | Stop-Process -Force; " +
                "Start-Sleep -Seconds 1");

            output.WriteLineSafe("Info", $"Step 3/5: Removing Appx packages matching '{nameFilter}'...");
            string removeCommand = Operation == AppxOperation.ClearCache
                ? $"Get-AppxPackage -AllUsers | Where-Object {{ $_.PackageFullName -like '{nameFilter}' -or $_.Name -like '{nameFilter}' }} | ForEach-Object {{ [IO.Path]::GetFullPath($_.InstallLocation) | ForEach-Object {{ if (Test-Path $_) {{ Remove-Item -Path $_ -Recurse -Force -ErrorAction Continue }} }} }}; Write-Host 'ClearCache completed'"
                : (Type == Level.App
                    ? $"Get-AppxPackage -AllUsers | Where-Object {{ $_.{typeFilter} -like '{nameFilter}' }} | Remove-AppxPackage -ErrorAction Continue; Write-Host 'Remove App completed'"
                    : $"Get-AppxPackage -AllUsers | Where-Object {{ $_.{typeFilter} -like '{nameFilter}' }} | Remove-AppxPackage -AllUsers -ErrorAction Continue; Write-Host 'Remove Family/Package completed'");
            await RunPowerShellCommand(output, removeCommand);

            if (Operation != AppxOperation.ClearCache)
            {
                output.WriteLineSafe("Info", $"Step 4/5: Removing provisioned packages...");
                string provisionCommand = Type == Level.App
                    ? $"Get-AppxProvisionedPackage -Online | Where-Object {{ $_.{typeFilter} -like '{nameFilter}' }} | Remove-AppxProvisionedPackage -Online -ErrorAction Continue; Write-Host 'Provisioned remove completed'"
                    : $"Get-AppxProvisionedPackage -Online | Where-Object {{ $_.PackageFullName -like '{nameFilter}' }} | Remove-AppxProvisionedPackage -Online -ErrorAction Continue; Write-Host 'Provisioned remove completed'";
                await RunPowerShellCommand(output, provisionCommand);
            }

            output.WriteLineSafe("Info", $"Step 5/5: Restarting AppXSvc...");
            await RunPowerShellCommand(output,
                "Set-Service -Name AppXSvc -StartupType Manual; " +
                "Start-Service -Name AppXSvc -ErrorAction SilentlyContinue; " +
                "Start-Service -Name ClipSVC -ErrorAction SilentlyContinue");

            HasFinished = true;
            InProgress = false;
            return true;
        }

        private async Task RunPowerShellCommand(Output.OutputWriter output, string command)
        {
            var psi = new ProcessStartInfo()
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $"-NoProfile -NonInteractive -NoLogo -ExecutionPolicy Bypass -Command \"{command}\"",
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                output.WriteLineSafe("Warning", "Failed to start powershell.exe for Appx removal.");
                return;
            }

            proc.OutputDataReceived += ProcOutputHandler;
            proc.ErrorDataReceived += ProcOutputHandler;
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await Task.Run(() => proc.WaitForExit());
        }


        
        private class ProcessEnvironment : IDisposable
        {
            private List<(string Variable, string Value)> oldVariables;
            public ProcessEnvironment(params (string Variable, string Value)[] variables)
            {
                oldVariables = new List<(string Variable, string Value)>();
                foreach (var pair in variables)
                {
                    oldVariables.Add((pair.Variable, Environment.GetEnvironmentVariable(pair.Variable)));
                    Environment.SetEnvironmentVariable(pair.Variable, pair.Value, EnvironmentVariableTarget.Process);
                }
            }
            
            public void Dispose()
            {
                foreach (var oldVariable in oldVariables)
                {
                    Environment.SetEnvironmentVariable(oldVariable.Variable, oldVariable.Value, EnvironmentVariableTarget.Process);
                }
            }
        }
    }
}
