using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace KTWirzade.Shared.Integrity
{
    /// <summary>
    /// AME Integrity Check - verifies system integrity after KT WIRZADE modifications.
    /// Checks that expected modifications are in place and system files are not corrupted.
    /// </summary>
    public static class IntegrityCheck
    {
        /// <summary>
        /// Result of an integrity check.
        /// </summary>
        public class IntegrityResult
        {
            public bool OverallSuccess { get; set; }
            public List<CheckResult> Checks { get; set; } = new List<CheckResult>();
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }

        /// <summary>
        /// Individual check result.
        /// </summary>
        public class CheckResult
        {
            public string Name { get; set; }
            public bool Passed { get; set; }
            public string Message { get; set; }
            public string ExpectedValue { get; set; }
            public string ActualValue { get; set; }
            public CheckSeverity Severity { get; set; }
        }

        public enum CheckSeverity
        {
            Info,
            Warning,
            Critical
        }

        /// <summary>
        /// Runs a full integrity check on the system.
        /// </summary>
        public static async Task<IntegrityResult> RunFullCheck(IProgress<string> progress = null)
        {
            var result = new IntegrityResult();

            try
            {
                progress?.Report("Checking registry modifications...");
                result.Checks.AddRange(CheckRegistryModifications());

                progress?.Report("Checking file removals...");
                result.Checks.AddRange(CheckFileRemovals());

                progress?.Report("Checking service states...");
                result.Checks.AddRange(CheckServiceStates());

                progress?.Report("Checking AppX removals...");
                result.Checks.AddRange(CheckAppXRemovals());

                progress?.Report("Checking system file integrity...");
                result.Checks.AddRange(await CheckSystemFileIntegrity());

                progress?.Report("Checking Windows Update configuration...");
                result.Checks.AddRange(CheckWindowsUpdateConfiguration());

                result.OverallSuccess = result.Checks.All(c => c.Passed || c.Severity != CheckSeverity.Critical);
            }
            catch (Exception e)
            {
                result.Checks.Add(new CheckResult
                {
                    Name = "Integrity Check Error",
                    Passed = false,
                    Message = $"Integrity check failed: {e.Message}",
                    Severity = CheckSeverity.Critical
                });
                result.OverallSuccess = false;
            }

            return result;
        }

        /// <summary>
        /// Verifies that expected registry modifications are in place.
        /// </summary>
        public static List<CheckResult> CheckRegistryModifications()
        {
            var results = new List<CheckResult>();

            // Check common registry paths that KT WIRZADE modifies
            var checks = new List<(string Path, string Value, object Expected, string Name)>
            {
                // Example checks - these should be customized based on actual playbook modifications
                (@"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", 1, "Defender Disabled Policy"),
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 0, "UAC Disabled"),
                (@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, "Telemetry Disabled"),
            };

            foreach (var check in checks)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(check.Path);
                    var actualValue = key?.GetValue(check.Value);

                    results.Add(new CheckResult
                    {
                        Name = check.Name,
                        Passed = actualValue != null && actualValue.Equals(check.Expected),
                        ExpectedValue = check.Expected?.ToString(),
                        ActualValue = actualValue?.ToString() ?? "(not set)",
                        Message = actualValue != null && actualValue.Equals(check.Expected)
                            ? "OK"
                            : $"Expected: {check.Expected}, Actual: {actualValue ?? "(not set)"}",
                        Severity = CheckSeverity.Warning
                    });
                }
                catch (Exception e)
                {
                    results.Add(new CheckResult
                    {
                        Name = check.Name,
                        Passed = false,
                        Message = $"Error: {e.Message}",
                        Severity = CheckSeverity.Warning
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Verifies that files expected to be removed are actually gone.
        /// </summary>
        public static List<CheckResult> CheckFileRemovals()
        {
            var results = new List<CheckResult>();

            // Common paths that should be removed by KT WIRZADE
            var pathsToCheck = new List<string>
            {
                @"C:\Windows\System32\Apps\Scanner.exe",
                // Add more paths as needed based on playbook
            };

            foreach (var path in pathsToCheck)
            {
                var exists = File.Exists(path) || Directory.Exists(path);
                results.Add(new CheckResult
                {
                    Name = $"File Removed: {Path.GetFileName(path)}",
                    Passed = !exists,
                    ExpectedValue = "Removed",
                    ActualValue = exists ? "Present" : "Removed",
                    Message = exists ? "File still present" : "File properly removed",
                    Severity = CheckSeverity.Info
                });
            }

            return results;
        }

        /// <summary>
        /// Verifies service states.
        /// </summary>
        public static List<CheckResult> CheckServiceStates()
        {
            var results = new List<CheckResult>();

            var servicesToCheck = new List<(string Name, string ExpectedMode)>
            {
                ("wscsvc", "Manual"), // Security Center
                ("DiagTrack", "Disabled"), // Telemetry
                ("dmwappushservice", "Disabled"), // WAP Push
            };

            foreach (var (name, expectedMode) in servicesToCheck)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT StartMode, State FROM Win32_Service WHERE Name = '{name}'");

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var startMode = obj["StartMode"]?.ToString();
                        var state = obj["State"]?.ToString();

                        var expectedModeStr = expectedMode.ToString();

                        results.Add(new CheckResult
                        {
                            Name = $"Service: {name}",
                            Passed = startMode == expectedModeStr,
                            ExpectedValue = expectedModeStr,
                            ActualValue = startMode,
                            Message = $"Mode: {startMode}, State: {state}",
                            Severity = CheckSeverity.Info
                        });
                    }
                }
                catch (Exception e)
                {
                    results.Add(new CheckResult
                    {
                        Name = $"Service: {name}",
                        Passed = false,
                        Message = $"Error: {e.Message}",
                        Severity = CheckSeverity.Info
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Verifies AppX packages that should be removed.
        /// </summary>
        public static List<CheckResult> CheckAppXRemovals()
        {
            var results = new List<CheckResult>();

            // Common AppX packages to check
            var packagesToRemove = new List<string>
            {
                "Microsoft.BingWeather",
                "Microsoft.BingNews",
                "Microsoft.XboxApp",
                "Microsoft.XboxGameOverlay",
                "Microsoft.MicrosoftSolitaireCollection",
                "Microsoft.WindowsMaps",
                "Microsoft.People",
                "Microsoft.Windows.Photos",
                "Microsoft.WindowsAlarms",
                "Microsoft.Getstarted",
                "Microsoft.YourPhone",
                "Microsoft.MicrosoftOfficeHub",
                "Microsoft.OneConnect",
            };

            try
            {
                // Win32_Product only enumerates MSI installs (and triggers MSI self-repair),
                // so AppX checks always passed vacuously. Query the actual AppX state instead.
                var installedPackages = new List<string>();
                var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes("Get-AppxPackage -AllUsers | Select-Object -ExpandProperty Name"));
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe",
                    $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(30000);
                    if (p.ExitCode == 0)
                        installedPackages = output
                            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim())
                            .ToList();
                }

                foreach (var package in packagesToRemove)
                {
                    var isInstalled = installedPackages.Any(p => p.Contains(package));

                    results.Add(new CheckResult
                    {
                        Name = $"AppX: {package}",
                        Passed = !isInstalled,
                        ExpectedValue = "Not Installed",
                        ActualValue = isInstalled ? "Installed" : "Not Installed",
                        Message = isInstalled ? "Package still installed" : "Package properly removed",
                        Severity = CheckSeverity.Info
                    });
                }
            }
            catch (Exception e)
            {
                results.Add(new CheckResult
                {
                    Name = "AppX Check",
                    Passed = false,
                    Message = $"Error: {e.Message}",
                    Severity = CheckSeverity.Warning
                });
            }

            return results;
        }

        /// <summary>
        /// Checks system file integrity by verifying known hashes.
        /// </summary>
        public static async Task<List<CheckResult>> CheckSystemFileIntegrity()
        {
            var results = new List<CheckResult>();

            // Known-good hashes for critical system files that KT WIRZADE might modify
            // These should be populated based on the actual files your playbooks modify
            var fileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Example: [@"C:\Windows\System32\example.dll", "known-hash-here"]
            };

            foreach (var kvp in fileHashes)
            {
                try
                {
                    if (!File.Exists(kvp.Key))
                    {
                        results.Add(new CheckResult
                        {
                            Name = $"File: {Path.GetFileName(kvp.Key)}",
                            Passed = true,
                            Message = "File not present (expected)",
                            Severity = CheckSeverity.Info
                        });
                        continue;
                    }

                    var actualHash = ComputeFileHash(kvp.Key);
                    var passed = actualHash.Equals(kvp.Value, StringComparison.OrdinalIgnoreCase);

                    results.Add(new CheckResult
                    {
                        Name = $"File: {Path.GetFileName(kvp.Key)}",
                        Passed = passed,
                        ExpectedValue = kvp.Value,
                        ActualValue = actualHash,
                        Message = passed ? "Hash matches" : "Hash mismatch",
                        Severity = CheckSeverity.Warning
                    });
                }
                catch (Exception e)
                {
                    results.Add(new CheckResult
                    {
                        Name = $"File: {Path.GetFileName(kvp.Key)}",
                        Passed = false,
                        Message = $"Error: {e.Message}",
                        Severity = CheckSeverity.Warning
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Checks Windows Update configuration.
        /// </summary>
        public static List<CheckResult> CheckWindowsUpdateConfiguration()
        {
            var results = new List<CheckResult>();

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU");
                var auOptions = key?.GetValue("AUOptions");
                var noAutoUpdate = key?.GetValue("NoAutoUpdate");

                results.Add(new CheckResult
                {
                    Name = "Windows Update Configuration",
                    Passed = auOptions != null || noAutoUpdate != null,
                    ExpectedValue = "Configured",
                    ActualValue = auOptions != null ? $"AUOptions={auOptions}" : (noAutoUpdate != null ? $"NoAutoUpdate={noAutoUpdate}" : "(not configured)"),
                    Message = "Windows Update policy is configured",
                    Severity = CheckSeverity.Info
                });
            }
            catch (Exception e)
            {
                results.Add(new CheckResult
                {
                    Name = "Windows Update Check",
                    Passed = false,
                    Message = $"Error: {e.Message}",
                    Severity = CheckSeverity.Info
                });
            }

            return results;
        }

        private static string ComputeFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
