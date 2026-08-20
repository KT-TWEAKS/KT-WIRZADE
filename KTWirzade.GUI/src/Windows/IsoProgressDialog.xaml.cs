using Core;
using Core.Actions;
using Interprocess;
using Microsoft.Win32;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.Utils;
using KTWirzade.GUI.ViewModels;
using KTWirzade.Shared;
using static Core.Log;
using static Core.Win32;
using static Interprocess.InterLink;
using static KTWirzade.Shared.Playbook.FeaturePage;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace KTWirzade.GUI.Windows
{
    public partial class IsoProgressDialog : AcrylicWindow
    {
        private bool _networkDrivers;

        private bool _graphicsDrivers;

        private bool _systemDrivers;

        private string logFolder;

        public IsoProgressDialog(bool networkDrivers, bool graphicsDrivers, bool systemDrivers)
        {
            InitializeComponent();
            base.Loaded += OnLoaded;
            base.ContentRendered += Begin;
            _networkDrivers = networkDrivers;
            _graphicsDrivers = graphicsDrivers;
            _systemDrivers = systemDrivers;
        }

        private static void ExtractPlaybook(string apbx)
        {
            string pbExtDir = Directory.CreateDirectory(Path.Combine(App.ActivePath, "Playbooks")).FullName;
            APBX.ExtractArchive(apbx, Path.Combine(pbExtDir, Path.GetFileNameWithoutExtension(apbx)), "Executables");
            Directory.CreateDirectory(Path.Combine(pbExtDir, Path.GetFileNameWithoutExtension(apbx), "Executables"));
        }

        private async void Begin(object sender, EventArgs e)
        {
            base.ContentRendered -= Begin;
            TaskBar.TaskbarNotifier taskbarProgress = Wrap.ExecuteSafe((() => new TaskBar.TaskbarNotifier()), true, (LogOptions)null).Value;
            try
            {
                string status = "Extracting Playbook";
                bool fatalError = false;
                Wrap.ExecuteSafe((Action)delegate
                {
                    taskbarProgress?.SetProgressValue(this, 0);
                }, true, (LogOptions)null);
                string pbDir = Directory.CreateDirectory(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks")).FullName;
                string apbx = Path.Combine(pbDir, GlobalsGUI.Current.ISO.SelectedPlaybook.FileNameWithoutExtension + ".apbx");
                Task<Exception> extractTask = SafeTask.Run((Action)delegate
                {
                    ExtractPlaybook(apbx);
                    if (File.Exists(Path.Combine(App.ActivePath, "oobe_playbook.apbx")))
                    {
                        File.Delete(Path.Combine(App.ActivePath, "oobe_playbook.apbx"));
                    }
                    File.Copy(apbx, Path.Combine(App.ActivePath, "oobe_playbook.apbx"));
                }, false, (LogOptions)null);
                int i = 0;
                while (!extractTask.IsCompleted && i < 4)
                {
                    ProgressBar.ProgressOffset += 0.4;
                    await Task.Delay(100);
                    i++;
                }
                ProgressBar.ProgressOffset = 4.0;
                Exception extractException = await extractTask;
                if (extractException != null || !Directory.Exists(Path.Combine(Path.Combine(App.ActivePath, "Playbooks"), GlobalsGUI.Current.ISO.SelectedPlaybook.FileNameWithoutExtension)))
                {
                    Log.EnqueueExceptionSafe(extractException, "Could not extract Playbook.", Array.Empty<(string, object)>());
                    base.Topmost = false;
                    CloseButton.IsEnabled = true;
                    StatusText.Text = "Error extracting Playbook";
                    FinishText.Text = "Contact the team for assistance";
                    ProgressBar.Visibility = Visibility.Collapsed;
                    FinishText.Visibility = Visibility.Visible;
                    ShowLogsButton.Visibility = Visibility.Visible;
                    StatusImage.Source = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/warning_circle_yellow_gradient_128.png"));
                    StatusImage.Visibility = Visibility.Visible;
                    Wrap.ExecuteSafe((Action)delegate
                    {
                        taskbarProgress?.SetProgressNone(this);
                    }, true, (LogOptions)null);
                    return;
                }
                string playbookPath = Path.Combine(App.ActivePath, "Playbooks", GlobalsGUI.Current.ISO.SelectedPlaybook.FileNameWithoutExtension);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd (h.mm tt)").Replace(" )", ")");
                string folderName = "[" + timestamp + "] " + RemoveInvalidFilePathCharacters(((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Name, "~");
                logFolder = Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Logs"), folderName);
                bool errorsOccurred = false;
                DispatcherTimer dispatcherTimer = new DispatcherTimer
                {
                    Interval = new TimeSpan(0, 0, 5, 0, 0)
                };
                string imagesFolder = Environment.ExpandEnvironmentVariables("%ProgramData%\\KTWirzade\\Images");
                string isoName = PlaybookGUI.RemoveInvalidFilePathCharacters(((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Name, "~").Replace(" ", "") + "_v" + ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Version.TrimStart('v') + "_Win" + GlobalsGUI.Current.ISO.WinMajorVer + "_" + (GlobalsGUI.Current.ISO.Version ?? GlobalsGUI.Current.ISO.WinVer?.ToString()) + "_" + (GlobalsGUI.Current.ISO.Architecture?.ToString() ?? "Unknown") + ".iso";
                string isoDest = Path.Combine(imagesFolder, isoName);
                try
                {
                    if (File.Exists(isoDest))
                    {
                        File.Delete(isoDest);
                    }
                    if (!Directory.Exists(imagesFolder))
                    {
                        Directory.CreateDirectory(imagesFolder);
                    }
                    ProgressBar.Maximum = 101.5;
                    ProgressBar.Value += 0.5;
                    if ((await SafeTask.Run<int>((Func<int>)(() => InterLink.LaunchNode((TargetLevel)3, (Expression<Func<string, int>>)((string arguments) => NativeProcess.StartProcessAsTI(ProcessEx.GetCurrentProcessFileLocation(), arguments)), (Level)4, (Mode)2, System.Diagnostics.Process.GetCurrentProcess().Id, false)), true, (LogOptions)null)).Failed)
                    {
                        MessageBox.Show(typeof(IsoProgressDialog), "Could not initialize process. Check the error logs and contact the team for more information and assistance.", "Playbook failed.", MessageBoxButton.Exit, MessageBoxImage.Error);
                        System.Windows.Application.Current.Shutdown(-1);
                    }
                    int ticks = 0;
                    dispatcherTimer.Tick += async delegate
                    {
                        ticks++;
                        if (ticks == 1 && StatusText.Text != "Reticulating splines...")
                        {
                            StatusText.Text = "Reticulating splines...";
                            await InterLink.ExecuteSafeAsync((Expression<Action>)(() => WriteGUIOutput("Reticulating splines...")), false, -1);
                        }
                        else if (ticks > 2 && StatusText.Text != "Action taking a long time...")
                        {
                            ShowLogsButton.Visibility = Visibility.Visible;
                            StatusText.Text = "Action taking a long time...";
                            ProgressGrid.UpdateLayout();
                            ProgressBar.Value = ProgressBar.Value;
                            await InterLink.ExecuteSafeAsync((Expression<Action>)(() => WriteGUIOutput("Action taking a long time...")), false, -1);
                        }
                    };
                    dispatcherTimer.Start();
                    ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options = ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options?.Where((string x) => !x.StartsWith("none-") || !int.TryParse(x.Substring(5), out var _)).ToList();
                    string[] allOptions = ((((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).FeaturePages == null) ? new string[0] : (from x in ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).FeaturePages.SelectMany((Playbook.FeaturePage x) => x.Options.Select((Option o) => o.Name))
                                                                                                                                         where !string.IsNullOrEmpty(x)
                                                                                                                                         select x).ToArray());
                    string[] selectedOptions = ((((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options == null) ? new string[0] : ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Options.ToArray());
                    InterMessageReporter reporter = new InterMessageReporter((Action<string>)delegate (string statusText)
                    {
                        dispatcherTimer.Stop();
                        dispatcherTimer.Start();
                        StatusText.Text = (status = statusText.TrimEnd('.') + "...");
                    });
                    try
                    {
                        InterProgress progress = new InterProgress((Action<decimal>)async delegate (decimal value)
                        {
                            ticks = 0;
                            if (StatusText.Text == "Action taking a long time...")
                            {
                                StatusText.Text = "Reticulating splines...";
                                ShowLogsButton.Visibility = Visibility.Collapsed;
                                ProgressGrid.UpdateLayout();
                            }
                            Wrap.ExecuteSafe((Action)delegate
                            {
                                taskbarProgress?.SetProgressValue(this, (int)Math.Round(value) + 1);
                            }, false, (LogOptions)null);
                            ProgressBar.Value = (double)value + 1.0;
                        });
                        try
                        {
                            bool flag = await InterLink.ExecuteAsync<bool>((Expression<Func<Task<bool>>>)(() => AmeliorationUtil.RunPlaybook(playbookPath, _networkDrivers, _graphicsDrivers, _systemDrivers, (int?)GlobalsGUI.Current.ISO.SelectedPlaybook.VerificationStatus == (int?)0, GlobalsGUI.AutoLogon, GlobalsGUI.Username, GlobalsGUI.UserPassword, GlobalsGUI.AdminPassword, false, isoDest, GlobalsGUI.Current.ISO.FilePath, (GlobalsGUI.Current.ISO.WinVer != (int?)null) ? GlobalsGUI.Current.ISO.WinVer.Value.ToString() : null, (GlobalsGUI.Current.ISO.WinUpdateVer != (int?)null) ? GlobalsGUI.Current.ISO.WinUpdateVer.Value.ToString() : null, GlobalsGUI.Current.ISO.Architecture.ToArchitecture(), ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Name, ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Version, selectedOptions, allOptions, logFolder, progress, reporter, false)), false, -1);
                            errorsOccurred = flag;
                        }
                        finally
                        {
                            if (progress != null)
                            {
                                ((IDisposable)progress).Dispose();
                            }
                        }
                    }
                    finally
                    {
                        if (reporter != null)
                        {
                            ((IDisposable)reporter).Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    InterLink.ShutdownNode((Level)4);

                    Exception serializableException = ex;
                    if (ex.GetType().Name == "SerializableException" &&
                        ex.GetType().GetProperty("OriginalType")?.GetValue(ex) is Type originalType &&
                        originalType == typeof(SerializationException))
                    {
                        if (!File.Exists(Path.Combine(logFolder, "Log.yml")))
                        {
                            Log.EnqueueExceptionSafe(ex, "YAML Error.", new LogOptions(Path.Combine(logFolder, "Log.yml")), (string)null, Array.Empty<(string, object)>());
                        }
                        MessageBox.Show(typeof(IsoProgressDialog), ex.Message ?? "", "YAML Error", MessageBoxButton.ShowLogExit, MessageBoxImage.Error, null, Path.Combine(logFolder, "Log.yml"));
                        System.Windows.Application.Current.Shutdown();
                        return;
                    }
                    Log.EnqueueExceptionSafe((LogType)3, ex, "Fatal error.", new LogOptions(Path.Combine(logFolder, "Log.yml")), (string)null, Array.Empty<(string, object)>());
                    Log.EnqueueExceptionSafe((LogType)3, ex, "Fatal Playbook error.", Array.Empty<(string, object)>());
                    fatalError = true;
                    errorsOccurred = true;
                }
                dispatcherTimer.Stop();
                base.Topmost = false;
                ProgressBar.Value = ProgressBar.Maximum;
                CloseButton.IsEnabled = true;
                Wrap.ExecuteSafe((Action)delegate
                {
                    if (!base.IsActive)
                    {
                        if ((int?)Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PushNotifications")?.GetValue("ToastEnabled") == 0)
                        {
                            Activate();
                        }
                        else if (Wrap.ExecuteSafe((Action)delegate
                        {
                            TaskBar.ShowNotification(errorsOccurred ? "ISO creation has completed with errors" : "ISO creation has completed");
                        }, false, (LogOptions)null) != null)
                        {
                            Activate();
                        }
                    }
                    taskbarProgress?.SetProgressNone(this);
                }, true, (LogOptions)null);
                StatusText.Text = (fatalError ? "Error encountered" : (errorsOccurred ? "Completed with errors" : "ISO file modified successfully"));
                if (AmeliorationUtil.ErrorDisplayList.Any() && !fatalError)
                {
                    FinishText.Text = "Contact the team for assistance";
                }
                if (fatalError)
                {
                    FinishText.Text = "Halted at " + status.TrimEnd('.');
                }
                else
                {
                    FinishText.Text = "You can now close this window";
                }
                ProgressBar.Visibility = Visibility.Collapsed;
                FinishText.Visibility = Visibility.Visible;
                if (errorsOccurred || fatalError)
                {
                    ShowLogsButton.Visibility = Visibility.Visible;
                    StatusImage.Source = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/warning_circle_yellow_gradient_128.png"));
                }
                StatusImage.Visibility = Visibility.Visible;
                if (!fatalError)
                {
                    await MainWindow.CurrentDispatcher.Invoke((Func<Task>)async delegate
                    {
                        GlobalsGUI.Current.ISO.CurrentPage = new IsoPageViewModel();
                        await System.Windows.Application.Current.Windows.OfType<MainWindow>().First().LoadISO(isoDest);
                    });
                }
            }
            finally
            {
                if (taskbarProgress != null)
                {
                    ((IDisposable)taskbarProgress).Dispose();
                }
            }
        }

        [InterprocessMethod(Level.TrustedInstaller)]
        private static void WriteGUIOutput(string text)
        {
            Output.WriteAll("GUI", text);
            Output.FlushAll();
        }

        [InterprocessMethod(Level.Administrator)]
        private static void DeleteKPH()
        {
            new RegistryKeyAction
            {
                KeyName = "HKLM\\SYSTEM\\CurrentControlSet\\Services\\KProcessHacker2"
            }.RunTask(true);
        }

        public static string RemoveInvalidFilePathCharacters(string filename, string replaceChar)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars());
            return new Regex($"[{Regex.Escape(regexSearch)}]").Replace(filename, replaceChar);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
            {
                RootWindow.SetResourceReference(BackgroundProperty, "FakeBackgroundBrush");
                PageContainer.SetResourceReference(BackgroundProperty, "FakePageBackgroundBrush");
            }
        }

        public void ShowDialog(Window owner)
        {
            base.Owner = owner;
            ShowDialog();
        }

        public void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWindow(progressscale);
        }

        private async void ShowLogs_OnClick(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(logFolder))
            {
                try
                {
                    System.Diagnostics.Process.Start(logFolder);
                    return;
                }
                catch (Exception ex)
                {
                    try
                    {
                        ShowLogsButton.IsEnabled = false;
                        await Task.Run(delegate
                        {
                            bool flag;
                            do
                            {
                                flag = false;
                                Microsoft.Win32.OpenFileDialog openFileDialog = new()
                                {
                                    DefaultExt = ".txt",
                                    InitialDirectory = logFolder,
                                    Filter = "Text Files|*.txt;*.log;*.yml|All Files|*",
                                    Multiselect = true
                                };
                                bool? flag2 = openFileDialog.ShowDialog();
                                if (flag2.HasValue && flag2.Value && openFileDialog.FileNames.LastOrDefault() != null)
                                {
                                    try
                                    {
                                        flag = true;
                                        System.Diagnostics.Process.Start("notepad.exe", "\"" + openFileDialog.FileNames.LastOrDefault() + "\"");
                                    }
                                    catch (Exception)
                                    {
                                    }
                                }
                            }
                            while (flag);
                        });
                        ShowLogsButton.IsEnabled = true;
                    }
                    catch (Exception)
                    {
                        ShowLogsButton.IsEnabled = false;
                        MessageBox.Show(typeof(IsoProgressDialog), "Error opening log directory: " + ex.Message, "Information");
                    }
                    return;
                }
            }
            MessageBox.Show(typeof(IsoProgressDialog), "Could not find log directory.", "Information");
        }
    }
}
