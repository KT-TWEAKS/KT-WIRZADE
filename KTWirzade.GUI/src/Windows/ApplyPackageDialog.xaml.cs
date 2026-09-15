using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace KTWirzade.GUI.Windows
{
    public partial class ApplyPackageDialog : Window
    {
        public struct LUID
        {
            public uint LowPart;

            public uint HighPart;
        }

        private class Win32
        {
            public struct TOKEN_PRIVILEGES(int privilegeCount)
            {
                public int PrivilegeCount = privilegeCount;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
                public LUID_AND_ATTRIBUTES[] Privileges = new LUID_AND_ATTRIBUTES[36];
            }

            public struct LUID
            {
                public uint LowPart;

                public uint HighPart;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 4)]
            public struct LUID_AND_ATTRIBUTES
            {
                public LUID Luid;

                public uint Attributes;
            }

            public const uint TOKEN_ADJUST_PRIVILEGES = 32u;

            public const uint TOKEN_QUERY = 8u;

            public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";

            [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool InitiateSystemShutdownEx(string lpMachineName, string lpMessage, uint dwTimeout, bool bForceAppsClosed, bool bRebootAfterShutdown, uint dwReason);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLengthPointer);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);
        }

        private enum MachineType : ushort
        {
            IMAGE_FILE_MACHINE_UNKNOWN = 0,
            IMAGE_FILE_MACHINE_ALPHA = 388,
            IMAGE_FILE_MACHINE_AM33 = 467,
            IMAGE_FILE_MACHINE_AMD64 = 34404,
            IMAGE_FILE_MACHINE_ARM = 448,
            IMAGE_FILE_MACHINE_ARMV7 = 452,
            IMAGE_FILE_MACHINE_ARM64 = 43620,
            IMAGE_FILE_MACHINE_EBC = 3772,
            IMAGE_FILE_MACHINE_I386 = 332,
            IMAGE_FILE_MACHINE_I860 = 333,
            IMAGE_FILE_MACHINE_IA64 = 512,
            IMAGE_FILE_MACHINE_M68K = 616,
            IMAGE_FILE_MACHINE_M32R = 36929,
            IMAGE_FILE_MACHINE_MIPS16 = 614,
            IMAGE_FILE_MACHINE_MIPSFPU = 870,
            IMAGE_FILE_MACHINE_MIPSFPU16 = 1126,
            IMAGE_FILE_MACHINE_POWERPC = 496,
            IMAGE_FILE_MACHINE_POWERPCFP = 497,
            IMAGE_FILE_MACHINE_POWERPCBE = 498,
            IMAGE_FILE_MACHINE_R3000 = 354,
            IMAGE_FILE_MACHINE_R4000 = 358,
            IMAGE_FILE_MACHINE_R10000 = 360,
            IMAGE_FILE_MACHINE_SH3 = 418,
            IMAGE_FILE_MACHINE_SH3DSP = 419,
            IMAGE_FILE_MACHINE_SH4 = 422,
            IMAGE_FILE_MACHINE_SH5 = 424,
            IMAGE_FILE_MACHINE_TRICORE = 1312,
            IMAGE_FILE_MACHINE_THUMB = 450,
            IMAGE_FILE_MACHINE_WCEMIPSV2 = 361,
            IMAGE_FILE_MACHINE_ALPHA64 = 644
        }

        private static Architecture? _arch;


        public static Architecture Arch
        {
            get
            {
                if (_arch.HasValue)
                {
                    return _arch.Value;
                }
                MachineType processType = MachineType.IMAGE_FILE_MACHINE_UNKNOWN;
                MachineType hostType = MachineType.IMAGE_FILE_MACHINE_UNKNOWN;
                IsWow64Process2(GetCurrentProcess(), out processType, out hostType);
                switch (hostType)
                {
                    case MachineType.IMAGE_FILE_MACHINE_ARM:
                    case MachineType.IMAGE_FILE_MACHINE_ARMV7:
                        _arch = Architecture.Arm;
                        break;
                    case MachineType.IMAGE_FILE_MACHINE_ARM64:
                        _arch = Architecture.Arm64;
                        break;
                    case MachineType.IMAGE_FILE_MACHINE_I386:
                        _arch = Architecture.X86;
                        break;
                    case MachineType.IMAGE_FILE_MACHINE_I860:
                    case MachineType.IMAGE_FILE_MACHINE_AMD64:
                        _arch = Architecture.X64;
                        break;
                    default:
                        _arch = RuntimeInformation.OSArchitecture;
                        break;
                }
                return _arch.Value;
            }
        }

        public ApplyPackageDialog()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ProgressBar.Maximum = 100.0;
            Exception exception = await Task.Run(delegate
            {
                Exception result = null;
                string text = null;
                try
                {
                    StatusText.Dispatcher.Invoke(() => StatusText.Text = "Extracting service package...");
                    try
                    {
                        text = ExtractCab();
                    }
                    catch (Exception result2)
                    {
                        return result2;
                    }
                    Thread.Sleep(750);
                    ProgressBar.Dispatcher.Invoke(() => ProgressBar.Value = 3.0);
                    StatusText.Dispatcher.Invoke(() => StatusText.Text = "Adding certificate...");
                    string tempFileName = Path.GetTempFileName();
                    int num = RunPSCommand("try {$cert = (Get-AuthenticodeSignature '" + text + "').SignerCertificate; [System.IO.File]::WriteAllBytes('" + tempFileName + "', $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)); Import-Certificate '" + tempFileName + "' -CertStoreLocation 'Cert:\\LocalMachine\\Root' | Out-Null; Copy-Item -Path \"HKLM:\\Software\\Microsoft\\SystemCertificates\\ROOT\\Certificates\\$($cert.Thumbprint)\" \"HKLM:\\Software\\Microsoft\\SystemCertificates\\ROOT\\Certificates\\8A334AA8052DD244A647306A76B8178FA215F344\" -Force | Out-Null; EXIT 0; } catch {EXIT 1}", null, null);
                    Thread.Sleep(500);
                    if (num == 1)
                    {
                        throw new Exception("Could not add certificate.");
                    }
                    ProgressBar.Dispatcher.Invoke(() => ProgressBar.Value = 10.0);
                    StatusText.Dispatcher.Invoke(() => StatusText.Text = "Applying service package...");
                    string err = null;
                    double lastDismProgress = 0.0;
                    num = RunCommand("DISM.exe", "/Online /Add-Package /PackagePath:\"" + text + "\" /NoRestart /IgnoreCheck", delegate (object obj, DataReceivedEventArgs args)
                    {
                        if (args.Data != null && args.Data.Contains("%"))
                        {
                            int num2 = args.Data.IndexOf('%') - 1;
                            while (args.Data[num2] == '.' || char.IsDigit(args.Data[num2]))
                            {
                                num2--;
                            }
                            if (double.TryParse(args.Data.Substring(num2 + 1, args.Data.IndexOf('%') - num2 - 1), out var dismProgress) && lastDismProgress != dismProgress)
                            {
                                ProgressBar.Dispatcher.Invoke(() => ProgressBar.Value = dismProgress / 100.0 * 80.0 + 10.0);
                                lastDismProgress = dismProgress;
                            }
                        }
                    }, delegate (object obj, DataReceivedEventArgs args)
                    {
                        if (err == null && args.Data != null)
                        {
                            err = args.Data;
                        }
                        else if (err != null && args.Data != null)
                        {
                            err = err + Environment.NewLine + args.Data;
                        }
                    });
                    if (num != 0 && num != 3010)
                    {
                        throw new Exception("Failed to install package: " + err);
                    }
                }
                catch (Exception ex)
                {
                    result = ex;
                }
                try
                {
                    ProgressBar.Dispatcher.Invoke(() => ProgressBar.Value = 90.0);
                    StatusText.Dispatcher.Invoke(() => StatusText.Text = "Removing certificate...");
                    RunPSCommand("$cert = (Get-AuthenticodeSignature '" + text + "').SignerCertificate; Get-ChildItem 'Cert:\\LocalMachine\\Root\\$($cert.Thumbprint)' | Remove-Item -Force | Out-Null; Remove-Item \"HKLM:\\Software\\Microsoft\\SystemCertificates\\ROOT\\Certificates\\8A334AA8052DD244A647306A76B8178FA215F344\" -Force -Recurse | Out-Null", null, null);
                }
                catch (Exception)
                {
                }
                Thread.Sleep(500);
                try
                {
                    ProgressBar.Dispatcher.Invoke(() => ProgressBar.Value = 95.0);
                    StatusText.Dispatcher.Invoke(() => StatusText.Text = "Restoring settings...");
                    if (bool.TryParse(Environment.GetCommandLineArgs()[2], out var result3) && !result3)
                    {
                        DisableDontDisplayLastUsername();
                    }
                }
                catch (Exception)
                {
                }
                try
                {
                    RemoveServiceSafeBoot();
                }
                catch (Exception)
                {
                }
                try
                {
                    File.Delete(text);
                }
                catch (Exception)
                {
                }
                Thread.Sleep(250);
                ProgressBar.Dispatcher.Invoke(() => ProgressBar.Value = 100.0);
                return result;
            });
            StatusText.Text = ((exception == null) ? "System preparation complete" : "System preparation failed");
            ProgressBar.Visibility = Visibility.Collapsed;
            FinishText.Visibility = Visibility.Visible;
            if (exception != null)
            {
                ErrorImage.Visibility = Visibility.Visible;
            }
            else
            {
                CheckImage.Visibility = Visibility.Visible;
            }
            int seconds = 0;
            while (seconds <= 5)
            {
                FinishText.Text = "Windows will restart in " + (5 - seconds) + " seconds";
                seconds++;
                if (seconds > 5)
                {
                    await Task.Delay(500);
                }
                else
                {
                    await Task.Delay(1000);
                }
            }
            DoubleAnimation animation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(160.0));
            DoubleAnimation scale_x = new DoubleAnimation
            {
                From = 1.0,
                To = 0.9,
                Duration = TimeSpan.FromMilliseconds(160.0)
            };
            DoubleAnimation scale_y = new DoubleAnimation
            {
                From = 1.0,
                To = 0.9,
                Duration = TimeSpan.FromMilliseconds(160.0)
            };
            WindowGrid.BeginAnimation(UIElement.OpacityProperty, animation);
            WindowTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scale_x);
            WindowTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scale_y);
            await Task.Delay(200);
            RestartSystem();
        }

        private static int RunPSCommand(string command, DataReceivedEventHandler outputHandler, DataReceivedEventHandler errorHandler)
        {
            var cleaned = command.Replace("\"\"\"", "\"");
            var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(cleaned));
            return RunCommand("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -NonInteractive -EncodedCommand {encoded}", outputHandler, errorHandler);
        }

        private static int RunCommand(string exe, string arguments, DataReceivedEventHandler outputHandler, DataReceivedEventHandler errorHandler)
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = (outputHandler != null),
                    RedirectStandardError = (errorHandler != null)
                }
            };
            if (outputHandler != null)
            {
                process.OutputDataReceived += outputHandler;
            }
            if (errorHandler != null)
            {
                process.ErrorDataReceived += errorHandler;
            }
            process.Start();
            if (outputHandler != null)
            {
                process.BeginOutputReadLine();
            }
            if (errorHandler != null)
            {
                process.BeginErrorReadLine();
            }
            process.WaitForExit();
            return process.ExitCode;
        }

        private static string ExtractCab()
        {
            string cabArch = ((Arch == Architecture.Arm || Arch == Architecture.Arm64) ? "arm64" : "amd64");
            string fileDir = Environment.ExpandEnvironmentVariables("%ProgramData%\\KTWirzade");
            if (!Directory.Exists(fileDir))
            {
                Directory.CreateDirectory(fileDir);
            }
            string destination = Path.Combine(fileDir, "Z-KTWirzade-NoDefender-Package31bf3856ad364e35" + cabArch + "1.0.0.0.cab");
            if (File.Exists(destination))
            {
                return destination;
            }
            using UnmanagedMemoryStream stream = (UnmanagedMemoryStream)Assembly.GetEntryAssembly().GetManifestResourceStream("KTWirzade.GUI.Resources.Z-AME-NoDefender-Package31bf3856ad364e35" + cabArch + "1.0.0.0.cab");
            if (stream == null || stream.Length == 0)
            {
                throw new InvalidOperationException("Pacote de remocao do Defender nao incluido neste build - recurso embutido ausente ou vazio.");
            }
            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            File.WriteAllBytes(destination, buffer);
            return destination;
        }

        public void DisableDontDisplayLastUsername()
        {
            using (var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System", writable: true))
            {
                key?.SetValue("dontdisplaylastusername", 0, RegistryValueKind.DWord);
            }
        }

        private static void RemoveServiceSafeBoot()
        {
            using (var key = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\SafeBoot\\Minimal", writable: true))
            {
                key?.DeleteSubKeyTree("KTWirzadePrepare", throwOnMissingSubKey: false);
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool LookupPrivilegeValue(IntPtr lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern IntPtr RtlAdjustPrivilege(LUID privilege, bool bEnablePrivilege, bool isThreadPrivilege, out bool previousValue);

        public static void AdjustCurrentPrivilege(string privilege)
        {
            LookupPrivilegeValue(IntPtr.Zero, privilege, out var luid);
            RtlAdjustPrivilege(luid, bEnablePrivilege: true, isThreadPrivilege: false, out var _);
        }

        private void RestartSystem()
        {
            AdjustCurrentPrivilege("SeShutdownPrivilege");
            if (!Win32.InitiateSystemShutdownEx(null, null, 0u, bForceAppsClosed: true, bRebootAfterShutdown: true, 0u))
            {
                Close();
            }
            Thread.Sleep(5000);
            Close();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process2(IntPtr process, out MachineType processMachine, out MachineType nativeMachine);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
    }
}
