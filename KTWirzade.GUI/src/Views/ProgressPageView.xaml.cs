using Core;
using Core.Actions;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using KTWirzade.GUI.Models;
using KTWirzade.GUI.ViewModels;
using KTWirzade.Shared;
using KTWirzade.Shared.Tasks;

namespace KTWirzade.GUI.Views
{
    public partial class ProgressPageView : Page
    {

        private static bool HasSafelyExitted;

        private Process proc;


        public ProgressPageView()
        {
            InitializeComponent();
            textBox.AppendText("Running playbook..." + Environment.NewLine);
            ComponentDispatcher.ThreadIdle += Begin;
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                mainWindow.Topmost = true;
                mainWindow.TitleSpace.MouseLeftButtonDown -= mainWindow.TitleBar_MouseDown;
            });
        }

        private static int CountDownloadActions(string apbxPath)
        {
            int count = 0;
            try
            {
                if (string.IsNullOrEmpty(apbxPath) || !File.Exists(apbxPath))
                    return 0;
                using (var archive = ZipFile.OpenRead(apbxPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.FullName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) && !entry.FullName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                            continue;
                        string content;
                        using (var reader = new StreamReader(entry.Open()))
                        {
                            content = reader.ReadToEnd();
                        }
                        count += Regex.Matches(content, @"^[ \t]*-[ \t]*(DownloadAction|SoftwareAction)[ \t]*:", RegexOptions.Multiline).Count;
                    }
                }
            }
            catch (Exception)
            {
                return 0;
            }
            return count;
        }

        private async void Begin(object sender, EventArgs e)
        {
            ComponentDispatcher.ThreadIdle -= Begin;
            try
            {
                try
                {
                    bool online = await Task.Run(() => KTWirzade.Shared.Cache.PlaybookCacheManager.CheckOnlineStatus());
                    if (!online)
                    {
                        string playbookPath = ((Playbook)GlobalsGUI.Current.Playbook).Path;
                        int downloads = await Task.Run(() => CountDownloadActions(playbookPath));
                        if (downloads > 0)
                        {
                            MessageBox.Show(typeof(MainWindow), $"Sem conexao com a internet.\n\nEste playbook precisa baixar {downloads} arquivo(s) durante a aplicacao. Essas etapas vao falhar sem rede.\n\nConecte-se e verifique a conexao antes de continuar.", "Conexao necessaria", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                catch (Exception)
                {
                }
                KTWirzade.GUI.Controls.ProgressBarDeterminate installationProgressBar = InstallationProgressBar;
                installationProgressBar.Maximum = await Task.Run(delegate
                {
                    CmdAction val = new CmdAction
                    {
                        Command = "schtasks /delete /tn \"KTWirzade\" /f",
                        Wait = false
                    };
                    CoreActions.SafeRun((ICoreAction)(object)val, false);
                    try
                    {
                        val.RunTask(true);
                    }
                    catch (Exception)
                    {
                    }
                    string machineName = Environment.MachineName;
                    string scope = "\\\\" + machineName + "\\root\\SecurityCenter2";
                    string queryString = "SELECT * FROM AntivirusProduct WHERE displayName = \"Windows Defender\"";
                    try
                    {
                        using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(scope, queryString);
                        foreach (ManagementObject item in managementObjectSearcher.Get())
                        {
                            item.Delete();
                        }
                    }
                    catch (Exception ex4)
                    {
                        Log.WriteExceptionSafe((LogType)1, ex4, "Could not remove Windows Defender Antivirus entry.", Array.Empty<(string, object)>());
                    }
                    return AmeliorationUtil.GetProgressMaximum((List<ITaskAction>)null);
                }) + 10;
                InstallationProgressBar.Value += 10.0;
                string directory = Directory.GetCurrentDirectory();
                proc = new Process();
                proc.StartInfo = new ProcessStartInfo(Path.Combine(directory, "KTWirzade.CLI.exe"));
                proc.StartInfo.Arguments = "\"" + ((Playbook)GlobalsGUI.Current.Playbook).Path + "\"";
                if (GlobalsGUI.SkipBuildCheck)
                    proc.StartInfo.Arguments += " --skip-build";
                if (GlobalsGUI.SkipRequirementsCheck)
                    proc.StartInfo.Arguments += " --skip-requirements";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.WorkingDirectory = directory;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.EnableRaisingEvents = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.ErrorDataReceived += proc_DataReceived;
                proc.OutputDataReceived += proc_DataReceived;
                proc.Exited += proc_UnexpectedExit;
                proc.Start();
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();
            }
            catch (Exception ex)
            {
                Log.WriteExceptionSafe((LogType)1, ex, "Could not initialize amelioration process.", Array.Empty<(string, object)>());
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    string saveLogDir = System.Windows.Forms.Application.StartupPath + "\\KT WIRZADE Logs";
                    if (Directory.Exists(saveLogDir))
                    {
                        Directory.Delete(saveLogDir, recursive: true);
                    }
                    Directory.Move(Directory.GetCurrentDirectory() + "\\Logs", saveLogDir);
                }
                catch (Exception)
                {
                }
                MessageBox.Show(typeof(MainWindow), "Could not initialize amelioration process. Check the error logs and contact the team for more information and assistance.", "Amelioration failed.", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown(-1);
            }
        }

        private void proc_DataReceived(object sender, DataReceivedEventArgs e)
        {
            textBox.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)async delegate
            {
                string data = e.Data;
                if (data == null)
                {
                    textBox.AppendText(Environment.NewLine);
                    textBox.ScrollToEnd();
                }
                else if (!e.Data.StartsWith("Action completed."))
                {
                    if (!(data == "Playbook finished."))
                    {
                        if (data == "Configuration folder is empty, put YAML files in it and restart the application.")
                        {
                            proc.Exited -= proc_UnexpectedExit;
                            if (!(base.DataContext is ViewModelBase viewModel))
                            {
                                return;
                            }
                            viewModel.MainCloseButtonActive = true;
                            viewModel.MainCloseButtonVisibility = Visibility.Visible;
                            viewModel.MainCancelButtonActive = true;
                        }
                        else if (e.Data.StartsWith(":KTWIRZADE-Fatal Error: "))
                        {
                            proc.Exited -= proc_UnexpectedExit;
                            try
                            {
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                string saveLogDir = System.Windows.Forms.Application.StartupPath + "\\KT WIRZADE Logs";
                                if (Directory.Exists(saveLogDir))
                                {
                                    Directory.Delete(saveLogDir, recursive: true);
                                }
                                Directory.Move(Directory.GetCurrentDirectory() + "\\Logs", saveLogDir);
                            }
                            catch (Exception)
                            {
                            }
                            MessageBox.Show(typeof(MainWindow), "Amelioration failed. Check the error logs and contact the team for more information and assistance.", "Amelioration failed.", MessageBoxButton.OK, MessageBoxImage.Error);
                            System.Windows.Application.Current.Shutdown(-1);
                        }
                        else if (e.Data.StartsWith(":KTWIRZADE-ERROR: "))
                        {
                            AmeliorationUtil.ErrorDisplayList.Add(e.Data.Substring(e.Data.IndexOf(' ') + 1).Replace("|NEWLINE|", Environment.NewLine));
                            textBox.AppendText(e.Data.Substring(e.Data.IndexOf('-') + 1).Replace("|NEWLINE|", Environment.NewLine) + ".\r\nCheck the error log for more information." + Environment.NewLine);
                            textBox.ScrollToEnd();
                            return;
                        }
                    }
                    else
                    {
                        proc.Exited -= proc_UnexpectedExit;
                        HasSafelyExitted = true;
                        try
                        {
                            if (!proc.WaitForExit(10000))
                            {
                                proc.Kill();
                            }
                        }
                        catch (Exception)
                        {
                        }
                        InstallationProgressBar.Value = InstallationProgressBar.Maximum;
                        Registry.LocalMachine.CreateSubKey("SOFTWARE\\KTWirzade\\")?.SetValue("KTWirzade", true);
                        try
                        {
                            string sourceDirName = Directory.GetCurrentDirectory() + "\\Logs";
                            string saveDir = Environment.ExpandEnvironmentVariables("%ProgramData%\\KTWirzade\\Logs");
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            if (!Directory.Exists(saveDir))
                            {
                                Directory.CreateDirectory(saveDir);
                            }
                            if (Directory.Exists(saveDir + "\\" + Path.GetDirectoryName(((Playbook)GlobalsGUI.Current.Playbook).Path)))
                            {
                                Directory.Delete(saveDir + "\\" + Path.GetDirectoryName(((Playbook)GlobalsGUI.Current.Playbook).Path), recursive: true);
                            }
                            Directory.Move(sourceDirName, saveDir + "\\" + Path.GetFileName(((Playbook)GlobalsGUI.Current.Playbook).Path));
                        }
                        catch (Exception ex3)
                        {
                            Log.WriteExceptionSafe((LogType)1, ex3, "Error while attempting to clean up process files.", Array.Empty<(string, object)>());
                        }
                        if (!(base.DataContext is ViewModelBase viewModel2))
                        {
                            return;
                        }
                        viewModel2.MainNextButtonContent = new TextBlock
                        {
                            Text = "Reboot"
                        };
                        MainWindow.CurrentDispatcher.Invoke(delegate
                        {
                            MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                            mainWindow.Topmost = false;
                            mainWindow.TitleSpace.MouseLeftButtonDown += mainWindow.TitleBar_MouseDown;
                            if (AmeliorationUtil.ErrorDisplayList.Count > 0)
                            {
                                ((MainWindowViewModel)mainWindow.DataContext).CurrentViewModel = new FinishErrorPageViewModel(new FinishErrorPage());
                            }
                            else
                            {
                                ((MainWindowViewModel)mainWindow.DataContext).CurrentViewModel = new FinishPageViewModel(new FinishPage());
                            }
                        });
                    }
                    textBox.AppendText(e.Data + Environment.NewLine);
                    textBox.ScrollToEnd();
                }
                else if (e.Data.Contains(" Weight:"))
                {
                    string progressValue = e.Data.Substring(e.Data.LastIndexOf(':') + 1);
                    InstallationProgressBar.Value += int.Parse(progressValue);
                }
            });
        }

        private void proc_UnexpectedExit(object sender, EventArgs e)
        {
            Task.Delay(8000).Wait();
            if (HasSafelyExitted)
            {
                return;
            }
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                string saveLogDir = System.Windows.Forms.Application.StartupPath + "\\KT WIRZADE Logs";
                if (Directory.Exists(saveLogDir))
                {
                    Directory.Delete(saveLogDir, recursive: true);
                }
                Directory.Move(Directory.GetCurrentDirectory() + "\\Logs", saveLogDir);
            }
            catch (Exception)
            {
            }
            MessageBox.Show(typeof(MainWindow), "Amelioration failed. Check the error logs and contact the team for more information and assistance.", "Amelioration failed.", MessageBoxButton.OK, MessageBoxImage.Error);
            System.Windows.Application.Current.Shutdown(-1);
        }
    }
}
