using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using Core;

namespace KTWirzade.Shared.SFC
{
    /// <summary>
    /// Interface with sfc.exe to warn against usage of sfc /scannow.
    /// When KT WIRZADE modifies system files, running sfc /scannow can revert those changes.
    /// This component monitors for sfc.exe execution and warns the user.
    /// </summary>
    public static class SFCInterface
    {
        // WMI process-start watcher (not FileSystemWatcher: sfc.exe is a process, not a file).
        private static System.Management.ManagementEventWatcher _watcher;
        private static bool _monitoring;
        private static DateTime _lastWarning = DateTime.MinValue;

        /// <summary>
        /// Event raised when sfc /scannow is detected.
        /// </summary>
        public static event EventHandler<SFCDetectedEventArgs> SFCDetected;

        /// <summary>
        /// Whether SFC monitoring is currently active.
        /// </summary>
        public static bool IsMonitoring => _monitoring;

        /// <summary>
        /// Starts monitoring for sfc.exe execution.
        /// </summary>
        public static void StartMonitoring()
        {
            if (_monitoring)
                return;

            try
            {
                // Use WMI process monitoring for sfc.exe
                var query = new System.Management.WqlEventQuery(
                    "SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName='sfc.exe'");

                _watcher = new System.Management.ManagementEventWatcher(query);
                _watcher.EventArrived += OnSFCProcessStarted;
                _watcher.Start();

                _monitoring = true;
                Log.WriteSafe(LogType.Info, "SFCInterface: Monitoring started.", null);
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SFCInterface: Failed to start monitoring.");
            }
        }

        /// <summary>
        /// Stops monitoring for sfc.exe execution.
        /// </summary>
        public static void StopMonitoring()
        {
            if (!_monitoring)
                return;

            try
            {
                // Stop the watcher
                _watcher?.Dispose();
                _watcher = null;
                _monitoring = false;

                Log.WriteSafe(LogType.Info, "SFCInterface: Monitoring stopped.", null);
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SFCInterface: Failed to stop monitoring.");
            }
        }

        /// <summary>
        /// Shows a warning dialog to the user about sfc /scannow.
        /// </summary>
        public static SFCDialogResult ShowWarning(string parentWindowTitle = "KT WIRZADE")
        {
            // Throttle warnings to avoid spam
            if (DateTime.UtcNow - _lastWarning < TimeSpan.FromSeconds(30))
            {
                return SFCDialogResult.Ignored;
            }

            _lastWarning = DateTime.UtcNow;

            var result = MessageBox.Show(
                "System File Checker (sfc /scannow) was detected.\n\n" +
                "Running sfc /scannow will revert changes made by KT WIRZADE to system files.\n\n" +
                "It is recommended NOT to run this command unless you are troubleshooting.\n\n" +
                "Do you want to continue anyway?\n\n" +
                "Click 'No' to cancel the operation and keep your system modifications.",
                parentWindowTitle + " - SFC Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            return result == MessageBoxResult.Yes ? SFCDialogResult.Confirmed : SFCDialogResult.Cancelled;
        }

        /// <summary>
        /// Checks if sfc.exe is currently running.
        /// </summary>
        public static bool IsSFCRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName("sfc");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the status of the System File Checker.
        /// </summary>
        public static SFCStatus GetSFCStatus()
        {
            try
            {
                var psi = new ProcessStartInfo("cmd", "/c sfc /verifyonly")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                if (output.Contains("did not find any integrity violations"))
                {
                    return SFCStatus.Clean;
                }
                else if (output.Contains("found corrupt files"))
                {
                    return SFCStatus.CorruptionDetected;
                }
                else if (output.Contains("could not perform"))
                {
                    return SFCStatus.CouldNotPerform;
                }

                return SFCStatus.Unknown;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SFCInterface: Failed to get SFC status.");
                return SFCStatus.Unknown;
            }
        }

        private static void OnSFCProcessStarted(object sender, System.Management.EventArrivedEventArgs e)
        {
            try
            {
                var processName = e.NewEvent.Properties["ProcessName"]?.Value?.ToString();
                var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"]?.Value);
                var commandLine = GetProcessCommandLine(processId);

                Log.WriteSafe(LogType.Warning, $"SFCInterface: sfc.exe detected (PID: {processId}, CMD: {commandLine})", null);

                // Raise the event
                SFCDetected?.Invoke(null, new SFCDetectedEventArgs
                {
                    ProcessId = processId,
                    CommandLine = commandLine,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Log.WriteExceptionSafe(ex, "SFCInterface: Error handling SFC detection.");
            }
        }

        private static string GetProcessCommandLine(int processId)
        {
            try
            {
                using var proc = Process.GetProcessById(processId);
                // Note: Getting command line requires additional P/Invoke or WMI
                // This is a simplified version
                return proc.ProcessName;
            }
            catch
            {
                return "unknown";
            }
        }
    }

    public class SFCDetectedEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public string CommandLine { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum SFCDialogResult
    {
        Confirmed,
        Cancelled,
        Ignored
    }

    public enum SFCStatus
    {
        Unknown,
        Clean,
        CorruptionDetected,
        CouldNotPerform
    }
}
