using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;

namespace KTWirzade.Shared.Rollback
{
    public static class AdvancedRollback
    {
        public static RollbackServiceResult RollbackService(string serviceName, ServiceOperation originalOp,
            string regBackupPath = null, string previousStartType = null, string wasRunning = null)
        {
            try
            {
                if (originalOp == ServiceOperation.Delete)
                    return RollbackDeletedService(serviceName, regBackupPath, wasRunning);

                using (var sc = new ServiceController(serviceName))
                {
                    switch (originalOp)
                    {
                        case ServiceOperation.Stop:
                            if (sc.Status != ServiceControllerStatus.Running)
                                sc.Start();
                            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                            return new RollbackServiceResult { Success = true };

                        case ServiceOperation.Disable:
                        case ServiceOperation.Change:
                        {
                            // Restore the start type the service had before the playbook
                            // changed it; fall back to Automatic when nothing was captured.
                            var mode = ServiceStartMode.Automatic;
                            if (!string.IsNullOrEmpty(previousStartType) &&
                                Enum.TryParse(previousStartType, out ServiceStartMode parsed))
                                mode = parsed;
                            ChangeStartType(serviceName, mode);

                            // A captured Stop left the machine without the service running;
                            // bring it back when it was running before the playbook touched it.
                            if (string.Equals(wasRunning, "True", StringComparison.OrdinalIgnoreCase))
                            {
                                sc.Refresh();
                                if (sc.Status == ServiceControllerStatus.Stopped)
                                {
                                    sc.Start();
                                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                                }
                            }
                            return new RollbackServiceResult { Success = true };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new RollbackServiceResult { Success = false, Error = ex.Message };
            }

            return new RollbackServiceResult { Success = true };
        }

        private static RollbackServiceResult RollbackDeletedService(string serviceName, string regBackupPath, string wasRunning = null)
        {
            if (!string.IsNullOrEmpty(regBackupPath) && File.Exists(regBackupPath))
            {
                try
                {
                    // Service keys are owned by SYSTEM/TrustedInstaller and deny write to
                    // Administrators; fix the ACL of the whole tree (key + subkeys + parent)
                    // first or reg import fails with access denied.
                    RollbackManager.EnsureKeyTreeWritableForRollback($@"HKLM\SYSTEM\CurrentControlSet\Services\{serviceName}");

                    var psi = new ProcessStartInfo("reg.exe", $"import \"{regBackupPath}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        p.WaitForExit(15000);
                        if (p.ExitCode == 0)
                        {
                            // The playbook stopped/killed it before deleting; restore run state.
                            if (string.Equals(wasRunning, "True", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    using (var sc = new ServiceController(serviceName))
                                    {
                                        if (sc.Status == ServiceControllerStatus.Stopped)
                                            sc.Start();
                                    }
                                }
                                catch { /* best effort */ }
                            }

                            return new RollbackServiceResult
                            {
                                Success = true,
                                Message = "Servico restaurado do backup do registro: " + serviceName
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    return new RollbackServiceResult { Success = false, Error = "Falha ao importar backup do servico: " + ex.Message };
                }
            }

            // Honest failure: reporting success here hid services that were never restored.
            return new RollbackServiceResult
            {
                Success = false,
                Message = "Servico precisa ser restaurado manualmente. Nome: " + serviceName
            };
        }

        private static void ChangeStartType(string serviceName, ServiceStartMode mode)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"config \"{serviceName}\" start= {(mode == ServiceStartMode.Automatic ? "auto" : mode == ServiceStartMode.Disabled ? "disabled" : "demand")}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(5000);
            }
            catch { }
        }

        public static RollbackResult RollbackScheduledTask(string taskPath, TaskOperation originalOp)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                switch (originalOp)
                {
                    case TaskOperation.Delete:
                        psi.Arguments = $"/Create /TN \"{taskPath}\" /XML \"{taskPath}.bak.xml\"";
                        using (var proc1 = Process.Start(psi)) { proc1?.WaitForExit(5000); }
                        return new RollbackResult { Success = true };

                    case TaskOperation.Disable:
                        psi.Arguments = $"/Change /TN \"{taskPath}\" /Enable";
                        using (var proc2 = Process.Start(psi)) { proc2?.WaitForExit(5000); }
                        return new RollbackResult { Success = true };

                    case TaskOperation.Enable:
                        psi.Arguments = $"/Change /TN \"{taskPath}\" /Disable";
                        using (var proc3 = Process.Start(psi)) { proc3?.WaitForExit(5000); }
                        return new RollbackResult { Success = true };
                }
            }
            catch (Exception ex)
            {
                return new RollbackResult { Success = false, Error = ex.Message };
            }

            return new RollbackResult { Success = true };
        }

        public static RollbackResult RollbackAppx(string packageName, AppxOperation originalOp)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                if (originalOp == AppxOperation.Remove)
                {
                    psi.Arguments = $"-NoProfile -Command \"Get-AppxPackage -AllUsers *{packageName}* | ForEach-Object {{ Add-AppxPackage -DisableDevelopmentMode -Register '$($_.InstallLocation)\\AppXManifest.xml' }}\"";
                    using (var proc = Process.Start(psi)) { proc?.WaitForExit(30000); }
                    return new RollbackResult { Success = true };
                }
            }
            catch (Exception ex)
            {
                return new RollbackResult { Success = false, Error = ex.Message };
            }

            return new RollbackResult { Success = true };
        }
    }

    public enum ServiceOperation
    {
        Stop,
        Continue,
        Start,
        Pause,
        Delete,
        Change,
        Disable
    }

    public enum TaskOperation
    {
        Delete,
        Enable,
        Disable,
        DeleteFolder
    }

    public enum AppxOperation
    {
        Remove,
        ClearCache
    }

    public class RollbackServiceResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string Message { get; set; }
    }
}
