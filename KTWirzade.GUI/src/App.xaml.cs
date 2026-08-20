using Core;
using DiscUtils.Iso9660;
using Interprocess;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using SharpSevenZip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using KTWirzade.GUI.Utils;
using KTWirzade.GUI.Windows;
using KTWirzade.Shared;
using static Core.Log;

namespace KTWirzade.GUI
{
    public partial class App : System.Windows.Application
    {

        internal static string ActivePath = Environment.ExpandEnvironmentVariables("%TEMP%\\KTWirzade");

        public static bool DeCrippleDefender = false;

        private static Mutex _ktWirzadeMutex;

        public static readonly SemaphoreSlim AdminNodeLaunched = new SemaphoreSlim(0);

        private static int unhandledCount = 0;

        public static event EventHandler PreparationCompleted;

        public static event EventHandler DispatchCompleted;

        private static async System.Threading.Tasks.Task ParseArguments(string[] args)
        {
            SharpSevenZipBase.SetLibraryPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "7z.dll"));
            CommandLine.IArgumentData argumentsData = null;
            try
            {
                argumentsData = CommandLine.ParseArguments(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Command line error: " + ex.Message);
                Environment.Exit(1);
            }
            if (argumentsData is CommandLine.Interprocess interprocessData)
            {
                if ((int)interprocessData.Level != 1 && !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                {
                    throw new SecurityException("Process must be run as an administrator.");
                }
                Directory.SetCurrentDirectory(ActivePath);
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                if ((int)interprocessData.Level == 3)
                {
                    System.Threading.Tasks.Task.Run((System.Action)RemoveKTWirzadeTask);
                }
                await InterLink.InitializeConnection(interprocessData.Level, interprocessData.Mode, interprocessData.Host, interprocessData.Nodes?.Select((CommandLine.Interprocess.NodeData x) => (Level: x.Level, ProcessID: x.ProcessID)).ToArray() ?? null);
                Environment.Exit(376);
            }
        }

        private static void RemoveKTWirzadeTask()
        {
            try
            {
                TaskService.Instance.RootFolder.DeleteTask("KTWirzade", false);
            }
            catch (Exception)
            {
            }
        }

        private void ConfigureCulture()
        {
            CultureInfo culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ".";
            culture.DateTimeFormat.Calendar = new GregorianCalendar();
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            ConfigureCulture();
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length == 3 && arguments[1] == "--apply-package")
            {
                new ApplyPackageDialog().ShowDialog();
                Current.Shutdown(0);
                return;
            }
            if (arguments.Length == 3 && arguments[1] == "--service")
            {
                ServiceBase.Run(new Service());
                return;
            }
            if (arguments.Length == 2 && arguments[1] == "--updated")
            {
                int i = 0;
                while (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)).Length > 1)
                {
                    await System.Threading.Tasks.Task.Delay(100);
                    if (i > 20)
                    {
                        KTWirzade.GUI.MessageBox.Show(null, "Update timed out.", "Error", KTWirzade.GUI.MessageBoxButton.OK, KTWirzade.GUI.MessageBoxImage.Warning, null, null);
                        Environment.Exit(0);
                    }
                    i++;
                }
                if (File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".exe", ".bak")))
                {
                    try
                    {
                        File.Delete(Assembly.GetExecutingAssembly().Location.Replace(".exe", ".bak"));
                    }
                    catch
                    {
                    }
                }
            }
            if (arguments.Length > 2)
            {
                Directory.SetCurrentDirectory(arguments[1]);
                ActivePath = arguments[1];
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                await ParseArguments(arguments.Skip(2).ToArray());
            }
            if (arguments.Length == 2 && arguments[1] == "--de-cripple")
            {
                DeCrippleDefender = true;
            }
            ThemeWatcher.WatchTheme();
            try
            {
                _ktWirzadeMutex = new Mutex(initiallyOwned: true, "KTWirzade.Client");
                if (!_ktWirzadeMutex.WaitOne(0))
                {
                    KTWirzade.GUI.MessageBox.Show(null, "Another instance of KT WIRZADE Beta was detected, a new instance will not be started.", "Warning", KTWirzade.GUI.MessageBoxButton.OK, KTWirzade.GUI.MessageBoxImage.Warning, null, null);
                    Environment.Exit(-1);
                }
                else
                {
                    //try
                    //{
                    //    PipeSecurity pipeSecurity = new PipeSecurity();
                    //    PipeAccessRule adminRule = new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, AccessControlType.Allow);
                    //    pipeSecurity.SetAccessRule(adminRule);
                    //    using (new NamedPipeServerStream("KTWirzade-User-Receiver", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None, 0, 0, pipeSecurity))
                    //    {
                    //    }
                    //}
                    //catch (Exception)
                    //{
                    //    KTWirzade.GUI.MessageBox.Show(null, "Another instance of KT WIRZADE Beta was detected, a new instance will not be started.", "Warning", KTWirzade.GUI.MessageBoxButton.OK, KTWirzade.GUI.MessageBoxImage.Warning, null, null);
                    //    Environment.Exit(-1);
                    //}
                }
            }
            catch (Exception)
            {
            }
            try
            {
                ExtractResourceFolder("WizardFiles", ActivePath, overwrite: true);
            }
            catch (Exception ex3)
            {
                KTWirzade.GUI.MessageBox.Show(null, "Could not extract required files. Contact the team for more information and assistance.\r\n\r\nError: " + ex3.Message, "Could not extract required files.", KTWirzade.GUI.MessageBoxButton.OK, KTWirzade.GUI.MessageBoxImage.Error, null, null);
                Environment.Exit(-1);
            }
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            base.Exit += Application_Exit;
            AppDomain.CurrentDomain.ProcessExit += Process_Exit;
            await Extracted();
        }

        private async System.Threading.Tasks.Task Extracted()
        {
            base.DispatcherUnhandledException -= UnhandledExceptionShowMessageBox;
            base.DispatcherUnhandledException += App_DispatcherUnhandledException;
            Log.MetadataSource = (ILogMetadata)new WizardMetadata();
            if (!Directory.Exists(ActivePath))
            {
                Directory.CreateDirectory(ActivePath);
            }
            Directory.SetCurrentDirectory(ActivePath);
            SharpSevenZipBase.SetLibraryPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "7z.dll"));
            InterLink.NodeExitedUnexpectedly += delegate (object sender, Level level)
            {
                if ((int)level == 3)
                {
                    if (Directory.Exists("KTWirzade"))
                    {
                        foreach (string dir in Directory.EnumerateDirectories("KTWirzade"))
                        {
                            Wrap.ExecuteSafe(delegate
                            {
                                Directory.Delete(dir, recursive: true);
                            }, false, (LogOptions)null);
                        }
                        foreach (string file in Directory.EnumerateFiles("KTWirzade"))
                        {
                            Wrap.ExecuteSafe(delegate
                            {
                                File.Delete(file);
                            }, false, (LogOptions)null);
                        }
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        Wrap.ExecuteSafe(delegate
                        {
                            Directory.Delete("KTWirzade", recursive: true);
                        }, false, (LogOptions)null);
                    }
                    Current.Dispatcher.Invoke(() => MessageBox.Show(null, $"{level} process exited unexpectedly with exit code: " + (uint)sender, "Error", KTWirzade.GUI.MessageBoxButton.OK, KTWirzade.GUI.MessageBoxImage.Information, null, null));
                    Environment.Exit(1);
                }
            };
            System.Threading.Tasks.Task initializeTask = InterLink.InitializeConnection((Level)2, (Mode)2, -1);
            WizardConfig.GetConfig();
            foreach (ISO item in GlobalsGUI.Current.Items.OfType<ISO>().ToList())
            {
                if (!File.Exists(item.FilePath))
                {
                    GlobalsGUI.Current.Items.Remove(item);
                }
            }
            IDragItem firstItem = GlobalsGUI.Current.Items.FirstOrDefault();
            if (firstItem is PlaybookGUI firstPb)
            {
                GlobalsGUI.Current.Playbook = firstPb;
            }
            else if (firstItem is ISO firstISO)
            {
                GlobalsGUI.Current.ISO = firstISO;
            }
            try
            {
                if (WizardConfig.Current.LastSelectedItem.Get() != null)
                {
                    IDragItem currentItem = GlobalsGUI.Current.Items.FirstOrDefault((IDragItem x) => x.FileNameWithoutExtension == WizardConfig.Current.LastSelectedItem.Get());
                    if (currentItem == null)
                    {
                        if (firstItem != null)
                        {
                            firstItem.Selected = true;
                            firstItem.SidebarInitialHeight = 37;
                        }
                        WizardConfig.Current.LastSelectedItem.Set(firstItem?.FileNameWithoutExtension);
                    }
                    else
                    {
                        currentItem.Selected = true;
                        currentItem.SidebarInitialHeight = 37;
                        if (currentItem is PlaybookGUI pb)
                        {
                            GlobalsGUI.Current.Playbook = pb;
                        }
                        else if (currentItem is ISO iso)
                        {
                            GlobalsGUI.Current.ISO = iso;
                        }
                    }
                }
                else
                {
                    if (firstItem != null)
                    {
                        firstItem.Selected = true;
                        firstItem.SidebarInitialHeight = 37;
                    }
                    WizardConfig.Current.LastSelectedItem.Set(firstItem?.FileNameWithoutExtension);
                }
            }
            catch (Exception)
            {
                WizardConfig.Current.Items = new List<WizardConfig.Item>();
                WizardConfig.Current.LastSelectedItem.Set(null);
            }
            System.Threading.Tasks.Task prepareTask = PrepareItems(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks"));
            try
            {
                InterLink.LaunchNode((Func<string, int>)((string arguments) => Process.Start(new ProcessStartInfo(Win32.ProcessEx.GetCurrentProcessFileLocation(), arguments)
                {
                    Verb = "runas",
                    UseShellExecute = true
                }).Id), (Level)3, (Mode)2, Process.GetCurrentProcess().Id, true);
            }
            catch (Win32Exception ex2)
            {
                if (ex2.NativeErrorCode == 1223)
                {
                    Environment.Exit(0);
                }
                throw;
            }
            AdminNodeLaunched.Release();
            WizardConfig.StartConfigThread();
            Wrap.ExecuteSafe(CheckVersion, false, (LogOptions)null);
            Wrap.ExecuteSafe(delegate
            {
                if (Directory.Exists("\\\\?\\" + Path.Combine(ActivePath, "Playbooks")))
                {
                    Directory.Delete("\\\\?\\" + Path.Combine(ActivePath, "Playbooks"), recursive: true);
                }
            }, false, (LogOptions)null);
            if (!File.Exists(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks.ico")))
            {
                InterLink.EnqueueSafe((Expression<System.Action>)(() => SetPBIcon()), 10000, true);
            }
            GlobalsGUI.Current.AppliedPlaybooks = Playbook.GetAppliedPlaybooks();
            new MainWindow().Show();
            await initializeTask;
            await prepareTask;
            App.DispatchCompleted?.Invoke(null, new EventArgs());
            await CheckForWizardUpdate();
            App.PreparationCompleted?.Invoke(null, new EventArgs());
        }

        [InterprocessMethod(Level.Administrator)]
        private static void SetPBIcon()
        {
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("KTWirzade.GUI.Properties.Playbooks.ico"))
            {
                if (resource != null)
                {
                    if (!File.Exists(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks.ico")))
                    {
                        File.Delete(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks.ico"));
                    }
                    using FileStream file = new FileStream(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks.ico"), FileMode.Create, FileAccess.Write);
                    resource.CopyTo(file);
                }
            }
            Registry.ClassesRoot.CreateSubKey(".apbx").CreateSubKey("DefaultIcon").SetValue("", "%PROGRAMDATA%\\KTWirzade\\Playbooks.ico", RegistryValueKind.ExpandString);
        }

        private static void CheckVersion()
        {
            if (WizardConfig.Current.PendingUpdate.Get() != null && VersionNumber.GetVersionNumber(WizardConfig.Current.PendingUpdate.Get()) <= Globals.CurrentVersionNumber)
            {
                WizardConfig.Current.PendingUpdate.Set(null);
            }
        }

        private static async System.Threading.Tasks.Task CheckForWizardUpdate()
        {
            //await System.Threading.Tasks.Task.Run(async delegate
            //{
            //    Thread.Sleep(1000);
            //    try
            //    {
            //        if (WizardConfig.Current.PendingUpdate.Get() == null && (int)DateTime.Now.Subtract(WizardConfig.Current.LastChecked.Get()).TotalMinutes > 30)
            //        {
            //            await new Updater().CheckForWizardUpdates(GlobalsGUI.Current.WizardPlaybook);
            //            if (GlobalsGUI.Current.WizardPlaybook.PendingUpdate != null)
            //            {
            //                WizardConfig.Current.PendingUpdate.Set(GlobalsGUI.Current.WizardPlaybook.PendingUpdate);
            //            }
            //            GlobalsGUI.Current.WizardPlaybook.LastChecked = DateTime.Now;
            //            WizardConfig.Current.LastChecked.Set(DateTime.Now);
            //            GlobalsGUI.Current.WizardPlaybook.UpdatesChecked = true;
            //        }
            //        else if ((int)DateTime.Now.Subtract(WizardConfig.Current.LastChecked.Get()).TotalMinutes <= 30)
            //        {
            //            GlobalsGUI.Current.WizardPlaybook.UpdatesChecked = true;
            //        }
            //    }
            //    catch (Exception)
            //    {
            //    }
            //});
        }

        private static async System.Threading.Tasks.Task PrepareItems(string pbDir)
        {
            List<Task<IDragItem>> tasks = new List<Task<IDragItem>>();
            List<string> apbxFiles = (Directory.Exists(pbDir) ? Directory.GetFiles(pbDir, "*.apbx").ToList() : new List<string>());
            foreach (string apbx in apbxFiles)
            {
                tasks.Add(System.Threading.Tasks.Task.Run((Func<Task<IDragItem>>)(async () => await LoadPlaybook(apbx))));
            }
            foreach (IDragItem iso in GlobalsGUI.Current.Items.Where((IDragItem x) => x.FilePath != null))
            {
                tasks.Add(System.Threading.Tasks.Task.Run((Func<Task<IDragItem>>)(async () => await LoadISO(iso.FilePath))));
            }
            if (GlobalsGUI.Current.Playbook != null)
            {
                GlobalsGUI.Current.Playbook.Selected = true;
                GlobalsGUI.Current.Playbook.SidebarInitialHeight = 37;
            }
            else if (GlobalsGUI.Current.ISO != null)
            {
                GlobalsGUI.Current.ISO.Selected = true;
                GlobalsGUI.Current.ISO.SidebarInitialHeight = 37;
            }
            for (int i = 0; i < tasks.Count; i++)
            {
                IDragItem item = await tasks[i];
                PlaybookGUI pb = item as PlaybookGUI;
                if (pb != null)
                {
                    if (pb.FileNameWithoutExtension + ".apbx" != Path.GetFileName(apbxFiles[i]))
                    {
                        if (File.Exists(Path.Combine(pbDir, pb.FileNameWithoutExtension + ".apbx")))
                        {
                            Log.WriteSafe((LogType)1, "Playbooks directory corruption was detected.", (SerializableTrace)null, Array.Empty<(string, object)>());
                            continue;
                        }
                        if (await InterLink.ExecuteSafeAsync((Expression<System.Action>)(() => RenamePlaybookAdmin(Path.GetFileName(apbxFiles[i]), pb.FileNameWithoutExtension + ".apbx")), true, 10000) != null)
                        {
                            continue;
                        }
                        pb.VerificationTask = System.Threading.Tasks.Task.Run(() => pb.GetStatus());
                    }
                    pb.Checked = true;
                    int index = GlobalsGUI.Current.Items.FindPlaybookIndex((PlaybookGUI x) => x.FileNameWithoutExtension == pb.FileNameWithoutExtension);
                    if (index == -1)
                    {
                        GlobalsGUI.Current.Items.Add(pb);
                        continue;
                    }
                    pb.Selected = GlobalsGUI.Current.Playbook != null && GlobalsGUI.Current.Playbook.FileNameWithoutExtension == pb.FileNameWithoutExtension;
                    pb.SidebarInitialHeight = (pb.Selected ? 37 : 0);
                    GlobalsGUI.Current.Items[index] = pb;
                    if (pb.Selected)
                    {
                        GlobalsGUI.Current.Playbook = pb;
                    }
                    continue;
                }
                ISO iso2 = item as ISO;
                if (iso2 == null)
                {
                    continue;
                }
                iso2.Checked = true;
                int index2 = GlobalsGUI.Current.Items.FindISOIndex((ISO x) => x.FilePath == iso2.FilePath);
                if (index2 == -1)
                {
                    GlobalsGUI.Current.Items.Add(iso2);
                    continue;
                }
                iso2.Selected = GlobalsGUI.Current.ISO != null && GlobalsGUI.Current.ISO.FilePath == iso2.FilePath;
                iso2.SidebarInitialHeight = (iso2.Selected ? 37 : 0);
                GlobalsGUI.Current.Items[index2] = iso2;
                if (iso2.Selected)
                {
                    GlobalsGUI.Current.ISO = iso2;
                }
            }
        }

        [InterprocessMethod(Level.Administrator)]
        private static void RenamePlaybookAdmin(string name, string newName)
        {
            File.Move(Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks"), name), Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks"), newName));
        }

        private static async Task<PlaybookGUI> LoadPlaybook(string apbx)
        {
            string tmpPath = Environment.ExpandEnvironmentVariables(Path.Combine("%TEMP%", Path.GetFileNameWithoutExtension(apbx) + "-" + new Random().Next(10000, 99999)));
            try
            {
                PlaybookGUI pb = await System.Threading.Tasks.Task.Run(() => APBX.GetData(apbx));
                string pbExtDir = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Playbooks")).FullName;
                ((Playbook)pb).Path = Path.Combine(pbExtDir, pb.FileNameWithoutExtension);
                pb.VerificationTask = System.Threading.Tasks.Task.Run(() => pb.GetStatus());
                return pb;
            }
            catch (Exception ex)
            {
                Wrap.ExecuteSafe(delegate
                {
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath);
                    }
                    if (File.Exists(Path.GetFileNameWithoutExtension(apbx) + ".status"))
                    {
                        File.Delete(Path.GetFileNameWithoutExtension(apbx) + ".status");
                    }
                }, false, (LogOptions)null);
                InterLink.EnqueueSafe((Expression<System.Action>)(() => RemovePlaybookAdmin(Path.GetFileName(apbx))), 5000, true);
                Log.EnqueueExceptionSafe(ex, "Could not load a playbook.", new (string, object)[1] { ("Path", apbx) });
                return null;
            }
        }

        [InterprocessMethod(Level.Administrator)]
        public static void RemovePlaybookAdmin(string fileName)
        {
            string pbPath = Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks"), fileName);
            File.Delete(pbPath);
            File.Delete(Path.Combine(Path.GetDirectoryName(pbPath), Path.GetFileNameWithoutExtension(pbPath)) + ".status");
        }

        private static async Task<ISO> LoadISO(string isoPath)
        {
            ISO iso = null;
            Wrap.ExecuteSafe(delegate
            {
                long length = new FileInfo(isoPath).Length;
                FileStream fileStream = File.Open(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                try
                {
                    iso = ImageParsers.Windows.TryGetInfo(fileStream);
                    if (iso == null)
                    {
                        CDReader value = Wrap.ExecuteSafe<CDReader>((Func<CDReader>)(() => new CDReader((Stream)fileStream, true)), false, (LogOptions)null).Value;
                        try
                        {
                            List<ImageParsers.IOSParser> list = new List<ImageParsers.IOSParser>();
                            ImageParsers.IOSParser[] oSParsers = ImageParsers.OSParsers;
                            bool flag = false;
                            ImageParsers.IOSParser[] array = oSParsers;
                            foreach (ImageParsers.IOSParser iOSParser in array)
                            {
                                ISO iSO = iOSParser.MatchFileName(Path.GetFileName(isoPath));
                                if (iSO != null)
                                {
                                    iso = iSO;
                                }
                                if (iSO != null && value != null)
                                {
                                    iSO = iOSParser.TryGetInfo(value, Path.GetFileName(isoPath), iso);
                                    if (iSO != null)
                                    {
                                        iso = iSO;
                                        flag = true;
                                        break;
                                    }
                                    list.Add(iOSParser);
                                }
                            }
                            if (!flag && value != null)
                            {
                                foreach (ImageParsers.IOSParser item in oSParsers.Except(list))
                                {
                                    ISO iSO2 = item.TryGetInfo(value, Path.GetFileName(isoPath), iso);
                                    if (iSO2 != null)
                                    {
                                        iso = iSO2;
                                        break;
                                    }
                                }
                            }
                            if (iso == null && value != null)
                            {
                                iso = ImageParsers.Linux.TryGetInfo(value, Path.GetFileName(isoPath));
                            }
                            if (iso == null)
                            {
                                iso = ImageParsers.Linux.MatchFileName(Path.GetFileName(isoPath));
                            }
                            if (iso == null)
                            {
                                iso = ImageParsers.Unknown.TryGetInfo(Path.GetFileName(isoPath));
                            }
                        }
                        finally
                        {
                            ((IDisposable)value)?.Dispose();
                        }
                    }
                }
                finally
                {
                    if (fileStream != null)
                    {
                        ((IDisposable)fileStream).Dispose();
                    }
                }
                if (iso != null)
                {
                    iso.Size = length;
                }
            }, true, (LogOptions)null);
            if (iso != null)
            {
                iso.FilePath = isoPath;
                iso.Watcher = new FileSystemWatcher(Path.GetDirectoryName(isoPath), Path.GetFileName(isoPath))
                {
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.FileName,
                    IncludeSubdirectories = false
                };
                iso.Watcher.Renamed += delegate (object sender, RenamedEventArgs args)
                {
                    ((FileSystemWatcher)sender).Filter = Path.GetFileName(args.FullPath);
                    KTWirzade.GUI.MainWindow.CurrentDispatcher.Invoke(() => iso.FilePath = args.FullPath);
                };
                iso.Watcher.Deleted += delegate (object sender, FileSystemEventArgs args)
                {
                    KTWirzade.GUI.MainWindow.CurrentDispatcher.Invoke(() => GlobalsGUI.Current.Items.Remove(iso));
                    ((FileSystemWatcher)sender).Dispose();
                };
                iso.Checked = true;
            }
            return iso;
        }

        private async void Process_Exit(object sender, EventArgs e)
        {
            Application_Exit(sender);
        }

        private async void Application_Exit(object sender, ExitEventArgs e = null)
        {
            WizardConfig.EndConfigThread();
        }

        private void UnhandledExceptionShowMessageBox(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            System.Windows.MessageBox.Show("Unexpected error: " + e.Exception);
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            if (unhandledCount == 3)
            {
                return;
            }
            unhandledCount++;
            Log.EnqueueExceptionSafe((LogType)3, e.Exception, Array.Empty<(string, object)>());
            if ((int)InterLink.ApplicationLevel == 2 || (int)InterLink.ApplicationLevel == 0)
            {
                try
                {
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
                    SerializableTrace trace = new SerializableTrace(e.Exception, (string)null, 0, int.MaxValue);
                    KTWirzade.GUI.MessageBox.Show(null, "Please contact the KT WIRZADE team for assistance.", "A critical error occurred", KTWirzade.GUI.MessageBoxButton.Exit, KTWirzade.GUI.MessageBoxImage.Error, "[" + e.Exception.GetType().ToString().Split('.')
                        .Last() + "] " + e.Exception.Message + Environment.NewLine + (object)trace, null);
                }
                catch (Exception)
                {
                    System.Windows.MessageBox.Show("Please contact the KT WIRZADE team for assistance.\r\n\r\n" + e.Exception.ToString(), "A critical error occurred", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Hand);
                }
                Environment.Exit(-1);
            }
        }

        public static void ExtractResourceFolder(string resource, string dir, bool overwrite = false)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (string obj in from res in assembly.GetManifestResourceNames()
                                   where res.StartsWith("KTWirzade.GUI.resources." + resource)
                                   select res)
            {
                using UnmanagedMemoryStream stream = (UnmanagedMemoryStream)assembly.GetManifestResourceStream(obj);
                int MB = 1048576;
                int offset = -MB;
                string file = dir + "\\" + obj.Substring(("KTWirzade.GUI.resources." + resource + ".").Length).Replace("---", "\\");
                if (file.EndsWith(".gitkeep"))
                {
                    continue;
                }
                string fileDir = Path.GetDirectoryName(file);
                if (fileDir != null && !Directory.Exists(fileDir))
                {
                    Directory.CreateDirectory(fileDir);
                }
                if (File.Exists(file) && !overwrite)
                {
                    continue;
                }
                if (File.Exists(file) && overwrite)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception)
                    {
                        goto end_IL_0059;
                    }
                }
                using (FileStream fsDlst = new FileStream(file, FileMode.CreateNew, FileAccess.Write))
                {
                    while (offset + MB < stream.Length)
                    {
                        byte[] buffer = new byte[MB];
                        offset += MB;
                        if (offset + MB > stream.Length)
                        {
                            buffer = new byte[stream.Length - offset];
                        }
                        stream.Seek(offset, SeekOrigin.Begin);
                        stream.Read(buffer, 0, buffer.Length);
                        fsDlst.Seek(offset, SeekOrigin.Begin);
                        fsDlst.Write(buffer, 0, buffer.Length);
                    }
                }
            end_IL_0059:;
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string activePath = ActivePath;
            AssemblyName assyName = new AssemblyName(args.Name);
            string newPath = Path.Combine(activePath, assyName.Name);
            if (!newPath.EndsWith(".dll") && !newPath.EndsWith(".winmd"))
            {
                newPath = ((!newPath.EndsWith("Windows")) ? (newPath + ".dll") : (newPath + ".winmd"));
            }
            if (File.Exists(newPath))
            {
                try
                {
                    return Assembly.LoadFrom(newPath);
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return null;
        }
    }
}