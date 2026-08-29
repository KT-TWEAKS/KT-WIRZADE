using Core;
using Core.Actions;
using Interprocess;
using Microsoft.Win32;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.Utils;
using KTWirzade.Shared;
using KTWirzade.Shared.Customization;
using KTWirzade.Shared.Rollback;
using static Core.Log;
using static Core.Win32;
using static Interprocess.InterLink;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace KTWirzade.GUI.Windows;

public partial class ProgressDialog : AcrylicWindow
{
    private string logFolder;
    private CustomizationProfile _customizationProfile;
    private UserCustomizationChoices _customizationChoices;
    private bool _playbookRunning = false;

    public ProgressDialog()
    {
        InitializeComponent();

        if (GlobalsGUI.Current.Playbook.VerificationStatus == PlaybookGUI.VerificationLevel.Verified
            && (GlobalsGUI.Current.Playbook).Username == "KTWirzade")
        {
            Title = "Ameliorate System";
            TitleSpace.Text = "Ameliorate System";
            PageTitleText.Text = "Ameliorate System";
        }
        else
        {
            Title = "Apply Playbook";
            TitleSpace.Text = "Apply Playbook";
            PageTitleText.Text = "Apply Playbook";
        }

        Loaded += OnLoaded;
        ContentRendered += Begin;
    }

    private static void ExtractPlaybook(string apbx)
    {
        string pbExtDir = Directory.CreateDirectory(Path.Combine(App.ActivePath, "Playbooks")).FullName;
        APBX.ExtractArchive(apbx, Path.Combine(pbExtDir, Path.GetFileNameWithoutExtension(apbx)));
    }

    private async void Begin(object sender, EventArgs e)
    {
        ContentRendered -= Begin;
        _playbookRunning = true;

        TaskBar.TaskbarNotifier taskbarProgress = Wrap.ExecuteSafe(() => new TaskBar.TaskbarNotifier(), true, null).Value;
        try
        {
            string status = "Extracting Playbook";
            bool fatalError = false;

            Wrap.ExecuteSafe(() => taskbarProgress?.SetProgressValue(this, 0), true, null);

            string pbDir = Directory.CreateDirectory(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks")).FullName;

            Task<Exception> extractTask = SafeTask.Run(() =>
            {
                ExtractPlaybook(Path.Combine(pbDir, GlobalsGUI.Current.Playbook.FileNameWithoutExtension + ".apbx"));
            }, false, null);

            int i = 0;
            while (!extractTask.IsCompleted && i < 4)
            {
                ProgressBar.ProgressOffset += 0.4;
                await Task.Delay(100);
                i++;
            }
            ProgressBar.ProgressOffset = 4.0;

            Exception extractException = await extractTask;
            if (extractException != null || !Directory.Exists(Path.Combine(App.ActivePath, "Playbooks", GlobalsGUI.Current.Playbook.FileNameWithoutExtension)))
            {
                Log.EnqueueExceptionSafe(extractException, "Could not extract Playbook.", Array.Empty<(string, object)>());
                Topmost = false;
                RestartTextCheck.Opacity = 0.7;
                CheckBox.Opacity = 0.5;
                CheckBox.IsEnabled = false;
                CloseButton.IsEnabled = true;
                StatusText.Text = "Error extracting Playbook";
                FinishText.Text = "Contact the team for assistance";
                ProgressBar.Visibility = Visibility.Collapsed;
                FinishText.Visibility = Visibility.Visible;
                ShowLogsButton.Visibility = Visibility.Visible;
                StatusImage.Source = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/warning_circle_yellow_gradient_128.png"));
                StatusImage.Visibility = Visibility.Visible;
                Wrap.ExecuteSafe(() => taskbarProgress?.SetProgressNone(this), true, null);
                return;
            }

            string playbookPath = Path.Combine(App.ActivePath, "Playbooks", GlobalsGUI.Current.Playbook.FileNameWithoutExtension);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd (h.mm tt)").Replace(" )", ")");
            string folderName = "[" + timestamp + "] " + RemoveInvalidFilePathCharacters((GlobalsGUI.Current.Playbook).Name, "~");
            logFolder = Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Logs"), folderName);
            Directory.CreateDirectory(logFolder);

            _customizationProfile = null;
            _customizationChoices = null;
            try
            {
                _customizationProfile = CustomizationManager.LoadProfile(playbookPath);
                if (_customizationProfile != null && _customizationProfile.HasCustomizations)
                {
                    StatusText.Text = "Customization options available";
                    var dialog = new CustomizationDialog(_customizationProfile, playbookPath) { Owner = this };
                    if (dialog.ShowDialog() == true)
                    {
                        _customizationChoices = dialog.Choices;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Could not load customizations.", Array.Empty<(string, object)>());
            }

            bool errorsOccurred = false;
            DispatcherTimer dispatcherTimer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 5, 0, 0)
            };

            try
            {
                await Task.Run(() =>
                {
                    Playbook playbook = GlobalsGUI.Current.Playbook;

                    if (!playbook.UseKernelDriver.HasValue)
                    {
                        if (new RegistryValueAction
                        {
                            KeyName = "HKLM\\SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity",
                            Value = "Enabled",
                            Data = 1
                        }.GetStatus() != 0 && new RegistryValueAction
                        {
                            KeyName = "HKLM\\SYSTEM\\CurrentControlSet\\Control\\CI\\Config",
                            Value = "VulnerableDriverBlocklistEnable",
                            Data = 0
                        }.GetStatus() == 0 && GUIUtil.GetDefenderToggles().Result.All((bool toggleOn) => !toggleOn))
                        {
                            AmeliorationUtil.UseKernelDriver = true;
                        }
                    }
                    else
                    {
                        AmeliorationUtil.UseKernelDriver = playbook.UseKernelDriver.Value;
                    }

                    PlaybookResources.ExtractResourceFolder("PlaybookResources", App.ActivePath);
                    PlaybookResources.ExtractArchive(Path.Combine(App.ActivePath, "GUI-Resources.7z"), App.ActivePath);
                    Wrap.ExecuteSafe(() => File.Delete(Path.Combine(App.ActivePath, "GUI-Resources.7z")), false, null);

                    if (AmeliorationUtil.UseKernelDriver)
                    {
                        PlaybookResources.ExtractArchive(Path.Combine(App.ActivePath, "ProcessInformer.7z"), App.ActivePath);
                        Wrap.ExecuteSafe(() => File.Delete(Path.Combine(App.ActivePath, "ProcessInformer.7z")), false, null);
                    }
                });

                ProgressBar.Maximum = 101.5;
                ProgressBar.Value += 0.5;

                if (GlobalsGUI.UserPassword != null || GlobalsGUI.AdminPassword != null)
                {
                    bool hasCustomCreds = _customizationProfile != null && _customizationProfile.HasCustomizations &&
                        (!string.IsNullOrEmpty(_customizationProfile.AccountPassword) || !string.IsNullOrEmpty(_customizationProfile.Username));
                    if (!hasCustomCreds)
                    {
                        status = "Setting credentials";
                        StatusText.Text = "Setting credentials...";

                        if (GlobalsGUI.UserPassword != null)
                        {
                            string fullUsername = WindowsIdentity.GetCurrent().Name;
                            SecurityIdentifier sid = WindowsIdentity.GetCurrent().User;
                            string username = fullUsername.Split('\\').Last();
                            string domain = fullUsername.Split('\\').FirstOrDefault();
                            await ExecuteAsync((Expression<Action>)(() => CredentialManager.SetUserCredentials(username, GlobalsGUI.Username, domain, sid == null ? null : sid.ToString(), GlobalsGUI.UserPassword, GlobalsGUI.AutoLogon)), false, -1);
                        }

                        if (GlobalsGUI.AdminPassword != null)
                        {
                            await ExecuteAsync((Expression<Action>)(() => CredentialManager.SetAdminPassword(GlobalsGUI.AdminPassword)), false, -1);
                        }
                    }

                    ProgressBar.Value += 0.5;
                }

                if ((await SafeTask.Run<int>(() => LaunchNode(TargetLevel.Administrator, (Expression<Func<string, int>>)(arguments => NativeProcess.StartProcessAsTI(ProcessEx.GetCurrentProcessFileLocation(), arguments)), Level.TrustedInstaller, (Mode)2, System.Diagnostics.Process.GetCurrentProcess().Id, false), true, null)).Failed)
                {
                    MessageBox.Show(typeof(ProgressDialog), "Could not initialize process. Check the error logs and contact the team for more information and assistance.", "Playbook failed.", MessageBoxButton.Exit, MessageBoxImage.Error);
                    System.Windows.Application.Current.Shutdown(-1);
                }

                int ticks = 0;
                dispatcherTimer.Tick += async (_, _) =>
                {
                    ticks++;
                    if (ticks == 1 && StatusText.Text != "Reticulating splines...")
                    {
                        StatusText.Text = "Reticulating splines...";
                        await ExecuteSafeAsync((Expression<Action>)(() => WriteGUIOutput("Reticulating splines...")), false, -1);
                    }
                    else if (ticks > 2 && StatusText.Text != "Action taking a long time...")
                    {
                        ShowLogsButton.Visibility = Visibility.Visible;
                        StatusText.Text = "Action taking a long time...";
                        ProgressGrid.UpdateLayout();
                        ProgressBar.Value = ProgressBar.Value;
                        await ExecuteSafeAsync((Expression<Action>)(() => WriteGUIOutput("Action taking a long time...")), false, -1);
                    }
                };
                dispatcherTimer.Start();

                InterMessageReporter reporter = new InterMessageReporter(statusText =>
                {
                    dispatcherTimer.Stop();
                    dispatcherTimer.Start();
                    StatusText.Text = status = statusText.TrimEnd('.') + "...";
                });

                try
                {
                    InterProgress progress = new InterProgress(async value =>
                    {
                        ticks = 0;
                        if (StatusText.Text == "Action taking a long time...")
                        {
                            StatusText.Text = "Reticulating splines...";
                            ShowLogsButton.Visibility = Visibility.Collapsed;
                            ProgressGrid.UpdateLayout();
                        }
                        Wrap.ExecuteSafe(() => taskbarProgress?.SetProgressValue(this, (int)Math.Round(value) + 1), false, null);
                        ProgressBar.Value = (double)value + 1.0;
                    });

                    try
                    {
                        Playbook playbook = GlobalsGUI.Current.Playbook;
                        playbook.Options = playbook.Options?.Where(x => !x.StartsWith("none-") || !int.TryParse(x.Substring(5), out _)).ToList();

                        RollbackManager.BeginSession(playbook.Name);

                        string[] allOptions = playbook.FeaturePages == null
                            ? Array.Empty<string>()
                            : playbook.FeaturePages
                                .SelectMany(x => x.Options.Select(o => o.Name))
                                .Where(x => !string.IsNullOrEmpty(x))
                                .ToArray();

                        string[] selectedOptions = playbook.Options?.ToArray();

                        errorsOccurred = await ExecuteAsync<bool>(
                            (Expression<Func<Task<bool>>>)(() => AmeliorationUtil.RunPlaybook(
                                playbookPath,
                                (int?)GlobalsGUI.Current.Playbook.VerificationStatus == 0,
                                GlobalsGUI.AutoLogon,
                                GlobalsGUI.Username,
                                GlobalsGUI.UserPassword,
                                GlobalsGUI.AdminPassword,
                                playbook.Name,
                                playbook.Version,
                                selectedOptions,
                                allOptions,
                                logFolder,
                                progress,
                                reporter,
                                AmeliorationUtil.UseKernelDriver)),
                            false, -1);
                    }
                    finally
                    {
                        // Must run even when RunPlaybook throws, otherwise the session stays
                        // open forever (CompletedAt == null) and PruneSessions refuses to prune it.
                        try { RollbackManager.EndSession(!errorsOccurred); }
                        catch { /* keep the original exception flowing */ }
                        ((IDisposable)progress)?.Dispose();
                    }
                }
                finally
                {
                    ((IDisposable)reporter)?.Dispose();
                }
            }
            catch (Exception ex)
            {
                ShutdownNode(Level.Administrator);

                Exception serializableException = ex;
                if (ex.GetType().Name == "SerializableException" &&
                    ex.GetType().GetProperty("OriginalType")?.GetValue(ex) is Type originalType &&
                    originalType == typeof(SerializationException))
                {
                    if (!File.Exists(Path.Combine(logFolder, "Log.yml")))
                        Log.EnqueueExceptionSafe(ex, "YAML Error.", new LogOptions(Path.Combine(logFolder, "Log.yml")), null, Array.Empty<(string, object)>());

                    MessageBox.Show(typeof(ProgressDialog), ex.Message ?? "", "YAML Error", MessageBoxButton.ShowLogExit, MessageBoxImage.Error, null, Path.Combine(logFolder, "Log.yml"));
                    System.Windows.Application.Current.Shutdown();
                    return;
                }

                Log.EnqueueExceptionSafe(LogType.Error, ex, "Fatal error.", new LogOptions(Path.Combine(logFolder, "Log.yml")), null, Array.Empty<(string, object)>());
                Log.EnqueueExceptionSafe(LogType.Error, ex, "Fatal Playbook error.", Array.Empty<(string, object)>());
                fatalError = true;
                errorsOccurred = true;
            }

            dispatcherTimer.Stop();

            Playbook appliedPlaybook = GlobalsGUI.Current.Playbook;
            Playbook applied = GlobalsGUI.Current.AppliedPlaybooks.FirstOrDefault(x => x.Username == appliedPlaybook.Username && x.Name == appliedPlaybook.Name);
            if (applied != null && !applied.UniqueId.HasValue)
                await ExecuteSafeAsync((Expression<Action>)(() => DeleteAppliedPlaybook(Path.GetFileName(applied.Path))), true, -1);

            await ExecuteSafeAsync((Expression<Action>)(() => WriteAppliedPlaybook(
                playbookPath,
                appliedPlaybook.UniqueId,
                appliedPlaybook.Name,
                appliedPlaybook.Username,
                appliedPlaybook.Overhaul,
                appliedPlaybook.Version,
                appliedPlaybook.Options == null ? Array.Empty<string>() : appliedPlaybook.Options.ToArray(),
                appliedPlaybook.FeaturePages == null
                    ? Array.Empty<string>()
                    : appliedPlaybook.FeaturePages
                        .SelectMany(x => x.Options.Select(o => o.Name))
                        .Where(x => !string.IsNullOrEmpty(x))
                        .ToArray(),
                errorsOccurred,
                fatalError,
                (int?)GlobalsGUI.Current.Playbook.VerificationStatus == 0)),
                true, -1);

            await ExecuteSafeAsync((Expression<Action>)(() => DeleteKPH()), true, -1);

            if (GlobalsGUI.Username != null && !fatalError && _customizationProfile == null)
            {
                status = "Setting credentials";
                StatusText.Text = "Setting username...";
                string username2 = WindowsIdentity.GetCurrent().Name.Split('\\').Last();
                SecurityIdentifier sid2 = WindowsIdentity.GetCurrent().User;
                Exception exception = await ExecuteSafeAsync((Expression<Action>)(() => CredentialManager.RenameUser(username2, GlobalsGUI.Username, sid2 == null ? null : sid2.ToString())), true, -1);
                if (exception != null)
                {
                    Log.EnqueueExceptionSafe(exception, "Could not rename user.", new LogOptions(Path.Combine(logFolder, "Log.yml")), null, Array.Empty<(string, object)>());
                    errorsOccurred = true;
                }
            }

            if (_customizationProfile != null && !fatalError)
            {
                status = "Applying customizations";
                StatusText.Text = "Applying customizations...";
                try
                {
                    var progress = new Progress<string>(msg => Dispatcher.BeginInvoke(new Action(() => StatusText.Text = msg)));
                    bool customOk = await CustomizationManager.ApplyCustomizations(
                        _customizationProfile, _customizationChoices, playbookPath, progress);
                    if (!customOk)
                    {
                        Log.EnqueueExceptionSafe(new Exception("Some customizations failed."), "Customization errors.", Array.Empty<(string, object)>());
                        errorsOccurred = true;
                    }
                }
                catch (Exception ex)
                {
                    Log.EnqueueExceptionSafe(ex, "Could not apply customizations.", new LogOptions(Path.Combine(logFolder, "Log.yml")), null, Array.Empty<(string, object)>());
                    errorsOccurred = true;
                }
            }

            Topmost = false;
            ProgressBar.Value = ProgressBar.Maximum;

            if (AmeliorationUtil.ErrorDisplayList.Any() || fatalError)
            {
                RestartTextCheck.Opacity = 0.7;
                CheckBox.Opacity = 0.5;
                CheckBox.IsEnabled = false;
            }

            CloseButton.IsEnabled = true;
            Wrap.ExecuteSafe(() => taskbarProgress?.SetProgressNone(this), true, null);

            StatusText.Text = fatalError ? "Error encountered" : errorsOccurred ? "Completed with errors" : "Playbook complete";

            if (AmeliorationUtil.ErrorDisplayList.Any() && !fatalError)
                FinishText.Text = "Contact the team for assistance";

            if (fatalError)
                FinishText.Text = "Halted at " + status.TrimEnd('.');

            ProgressBar.Visibility = Visibility.Collapsed;
            FinishText.Visibility = Visibility.Visible;

            if (errorsOccurred || fatalError)
            {
                ShowLogsButton.Visibility = Visibility.Visible;
                StatusImage.Source = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/warning_circle_yellow_gradient_128.png"));
            }

            StatusImage.Visibility = Visibility.Visible;

            if (errorsOccurred || fatalError)
                return;

            if (!CheckBox.IsChecked.Value)
            {
                RestartTextCheck.Opacity = 0.5;
                CheckBox.Opacity = 0.5;
                CheckBox.IsEnabled = false;
                FinishText.Text = "Restart at the soonest possible time";
                return;
            }

            for (int seconds = 0; seconds < 11; seconds++)
            {
                FinishText.Text = "Windows will restart in " + (10 - seconds) + " seconds";
                await Task.Delay(seconds == 10 ? 400 : 1000);

                if (!CheckBox.IsChecked.Value)
                {
                    RestartTextCheck.Opacity = 0.5;
                    CheckBox.Opacity = 0.5;
                    CheckBox.IsEnabled = false;
                    FinishText.Text = "Restart at the soonest possible time";
                    return;
                }
            }

            CoreActions.SafeRun(new CmdAction
            {
                Command = "timeout /t 1 & shutdown /r /t 0",
                Wait = false
            }, false);

            System.Windows.Application.Current.Shutdown();
        }
        finally
        {
            _playbookRunning = false;
            ((IDisposable)taskbarProgress)?.Dispose();
        }
    }

    [InterprocessMethod(Level.TrustedInstaller)]
    private static void DeleteAppliedPlaybook(string folderName)
    {
        string appliedDir = Environment.ExpandEnvironmentVariables("%ProgramData%\\KTWirzade\\AppliedPlaybooks");
        string fullPath = Path.Combine(appliedDir, folderName);
        if (Directory.Exists(fullPath))
            Directory.Delete(fullPath, recursive: true);
    }

    [InterprocessMethod(Level.TrustedInstaller)]
    private static void WriteAppliedPlaybook(string playbookPath, Guid? uniqueId, string name, string username, bool overhaul, string version, string[] selectedOptions, string[] allOptions, bool hadErrors, bool fatalError, bool isVerified)
    {
        try
        {
            if (uniqueId.HasValue)
            {
                using RegistryKey key = Registry.LocalMachine.CreateSubKey("SOFTWARE\\KTWirzade\\Playbooks\\Applied\\{" + uniqueId.Value.ToString().ToUpper() + "}", writable: true);
                key.SetValue("Name", name, RegistryValueKind.String);
                key.SetValue("Username", username, RegistryValueKind.String);
                key.SetValue("Overhaul", overhaul ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("Version", version, RegistryValueKind.String);
                key.SetValue("ErrorLevel", hadErrors ? (!fatalError ? 1 : 2) : 0, RegistryValueKind.DWord);
                key.SetValue("AvailableOptions", allOptions, RegistryValueKind.MultiString);
                key.SetValue("SelectedOptions", selectedOptions, RegistryValueKind.MultiString);
                key.SetValue("AppliedTimeUTC", DateTime.UtcNow.ToBinary(), RegistryValueKind.QWord);

                if (File.Exists(Path.Combine(playbookPath, "playbook.png")))
                    key.SetValue("Image", File.ReadAllBytes(Path.Combine(playbookPath, "playbook.png")), RegistryValueKind.Binary);
                else if (File.Exists(Path.Combine(playbookPath, "Images\\playbook.png")))
                    key.SetValue("Image", File.ReadAllBytes(Path.Combine(playbookPath, "Images\\playbook.png")), RegistryValueKind.Binary);

                return;
            }

            DirectoryInfo parent = Directory.CreateDirectory(Environment.ExpandEnvironmentVariables("%ProgramData%\\KTWirzade\\AppliedPlaybooks"));
            List<int> indexes = parent.GetDirectories()
                .Where(v => int.TryParse(v.Name, out _))
                .Select(v => int.Parse(v.Name))
                .ToList();

            int currentIndex = indexes.Count > 0 ? indexes.Max() : 0;
            if (currentIndex >= 10)
                // GetDirectories().First() returned the alphabetically-first folder ("1", then
                // "10" before "2" once it existed), deleting the wrong (not oldest) entry.
                Wrap.ExecuteSafe(() =>
                {
                    DirectoryInfo oldest = parent.GetDirectories()
                        .Where(v => int.TryParse(v.Name, out _))
                        .OrderBy(v => int.Parse(v.Name))
                        .FirstOrDefault();
                    oldest?.Delete(recursive: true);
                }, true, null);

            DirectoryInfo target = parent.CreateSubdirectory((currentIndex + 1).ToString());

            if (File.Exists(Path.Combine(playbookPath, "playbook.png")))
                File.Copy(Path.Combine(playbookPath, "playbook.png"), Path.Combine(target.FullName, "playbook.png"));
            else if (File.Exists(Path.Combine(playbookPath, "Images\\playbook.png")))
                File.Copy(Path.Combine(playbookPath, "Images\\playbook.png"), Path.Combine(target.FullName, "playbook.png"));

            File.Copy(Path.Combine(playbookPath, "playbook.conf"), Path.Combine(target.FullName, "playbook.conf"));

            if (hadErrors)
                File.Create(Path.Combine(target.FullName, "errors.txt")).Close();

            if (isVerified)
                File.Create(Path.Combine(target.FullName, "verified.txt")).Close();
        }
        catch (Exception ex)
        {
            Log.EnqueueExceptionSafe(LogType.Error, ex, Array.Empty<(string, object)>());
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
        {
            RootWindow.SetResourceReference(BackgroundProperty, "FakeBackgroundBrush");
            PageContainer.SetResourceReference(BackgroundProperty, "FakePageBackgroundBrush");
        }
    }

    public new void ShowDialog(Window owner, string playbookName)
    {
        Owner = owner;
        base.ShowDialog();
    }

    public void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_playbookRunning)
        {
            var result = KTWirzade.GUI.MessageBox.Show(
                this,
                "O playbook ainda esta em execucao. Tem certeza que deseja fechar?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;
        }
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
                    await Task.Run(() =>
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
                            bool? result = openFileDialog.ShowDialog();
                            if (result.HasValue && result.Value && openFileDialog.FileNames.LastOrDefault() != null)
                            {
                                try
                                {
                                    flag = true;
                                    System.Diagnostics.Process.Start("notepad.exe", "\"" + openFileDialog.FileNames.LastOrDefault() + "\"");
                                }
                                catch (Exception) { }
                            }
                        }
                        while (flag);
                    });
                    ShowLogsButton.IsEnabled = true;
                }
                catch (Exception)
                {
                    ShowLogsButton.IsEnabled = false;
                    MessageBox.Show(typeof(ProgressDialog), "Error opening log directory: " + ex.Message, "Information");
                }
                return;
            }
        }

        MessageBox.Show(typeof(ProgressDialog), "Could not find log directory.", "Information");
    }
}

