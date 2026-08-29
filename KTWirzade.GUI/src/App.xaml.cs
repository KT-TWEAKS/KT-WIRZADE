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

        internal static string ActivePath = Environment.ExpandEnvironmentVariables("%TEMP%\\AME");

        public static bool DeCrippleDefender = false;

        private static Mutex _ktWirzadeMutex;

        public static readonly SemaphoreSlim AdminNodeLaunched = new SemaphoreSlim(0);

        private static int unhandledCount = 0;

        public static event EventHandler PreparationCompleted;

        public static event EventHandler DispatchCompleted;

        private static async System.Threading.Tasks.Task ParseArguments(string[] args)
        {
            // Distribuicao single-exe: 7z.dll pode nao existir ao lado do exe.
            // Ordem: pasta do exe -> ActivePath (ja extraido) -> extrair do proprio exe.
            string sevenZip = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "7z.dll");
            if (!File.Exists(sevenZip))
            {
                string activeSevenZip = Path.Combine(ActivePath, "7z.dll");
                if (File.Exists(activeSevenZip))
                {
                    sevenZip = activeSevenZip;
                }
                else
                {
                    try
                    {
                        if (!Directory.Exists(ActivePath)) Directory.CreateDirectory(ActivePath);
                        ExtractEmbeddedResource("KTWirzade.GUI.Resources.7z.dll", activeSevenZip);
                        sevenZip = activeSevenZip;
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            if (File.Exists(sevenZip))
                SharpSevenZipBase.SetLibraryPath(sevenZip);
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
                // Join the same IPC session (pipe namespace + DACL owner) as the root node.
                InterLink.InitializeSession(interprocessData.Secret, interprocessData.OwnerSid);
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

        /// <summary>
        /// Blocks startup with an actionable message when the installed .NET Framework is
        /// too old. Without this, machines below 4.7.1 crash inside the WPF BAML loader with
        /// "NotImplementedException: The method or operation is not implemented":
        /// FluentIcons.Common is netstandard2.0 and needs the in-box netstandard.dll facade
        /// that only ships with .NET Framework 4.7.1+, and the supportedRuntime sku does not
        /// prevent the app from starting on older frameworks.
        /// </summary>
        private static void PreflightCheckFramework()
        {
            const int net48Release = 528040; // .NET Framework 4.8
            int release = 0;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    release = (key?.GetValue("Release") as int?) ?? 0;
                }
            }
            catch (Exception)
            {
            }
            if (release > 0 && release < net48Release)
            {
                System.Windows.MessageBox.Show(
                    "KT WIRZADE requires .NET Framework 4.8 (or newer), which is not installed on this computer.\r\n\r\n" +
                    "Download and install it, then run KT WIRZADE again:\r\n" +
                    "https://dotnet.microsoft.com/download/dotnet-framework/net48\r\n\r\n" +
                    "(Detected .NET Framework release: " + release + ")",
                    "KT WIRZADE", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Hand);
                Environment.Exit(-1);
            }
        }

        /// <summary>
        /// Forces resolution of the assemblies referenced by the window BAML before the first
        /// window is created. If one fails to load, the BAML loader would surface it as
        /// "NotImplementedException: The method or operation is not implemented"
        /// (Baml2006SchemaContext.ResolveBamlType), hiding the real cause. Loading them here
        /// turns that into an actionable error message.
        /// </summary>
        private static void PreflightLoadUiAssemblies()
        {
            foreach (string assemblyName in new[] { "FluentIcons.Common", "FluentIcons.Wpf" })
            {
                try
                {
                    Assembly assembly = Assembly.Load(assemblyName);
                    _ = assembly.DefinedTypes;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        "Failed to load a required UI component (" + assemblyName + ").\r\n\r\n" +
                        "Common causes:\r\n" +
                        "- Outdated .NET Framework: install .NET Framework 4.8 from https://dotnet.microsoft.com/download/dotnet-framework/net48\r\n" +
                        "- Incomplete installation: re-extract the full KT WIRZADE zip instead of running the exe alone\r\n\r\n" +
                        "Details:\r\n" + ex,
                        "KT WIRZADE", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Hand);
                    Environment.Exit(-1);
                }
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            ConfigureCulture();
            PreflightCheckFramework();
            // Extract FluentIcons to disk BEFORE any XAML — BAML needs LoadFrom context
            ExtractFluentIconsToDisk();
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
                // initiallyOwned must be false: with true (the old behavior) WaitOne(0) always
                // succeeded for the creating process, so the duplicate-instance check never fired.
                _ktWirzadeMutex = new Mutex(initiallyOwned: false, "KTWirzade.Client");
                bool acquired;
                try
                {
                    acquired = _ktWirzadeMutex.WaitOne(0);
                }
                catch (System.Threading.AbandonedMutexException)
                {
                    // A previous instance crashed without releasing it; we own it now.
                    acquired = true;
                }
                if (!acquired)
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
                // Extracts every embedded runtime dependency (KTWirzade.GUI.Resources.*)
                // into the active directory. The AssemblyResolve handler and the CLI
                // partner process both resolve their dependencies from there — without
                // this step a clean machine fails to load Core.Log (YamlDotNet missing).
                ExtractRuntimeDependencies(ActivePath, overwrite: true);
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
            PreflightLoadUiAssemblies();
            Log.MetadataSource = (ILogMetadata)new WizardMetadata();
            if (!Directory.Exists(ActivePath))
            {
                Directory.CreateDirectory(ActivePath);
            }
            Directory.SetCurrentDirectory(ActivePath);

            // Extract 7z.dll from embedded resources before loading it
            string sevenZipPath = Path.Combine(ActivePath, "7z.dll");
            if (!File.Exists(sevenZipPath))
            {
                try
                {
                    ExtractEmbeddedResource("KTWirzade.GUI.Resources.7z.dll", sevenZipPath);
                }
                catch (Exception ex)
                {
                    Log.WriteExceptionSafe(ex, "Failed to extract 7z.dll from embedded resources.");
                }
            }
            SharpSevenZipBase.SetLibraryPath(sevenZipPath);

            // The playbook is applied by launching KTWirzade.CLI.exe from the current
            // directory (ProgressPageView). Nothing else puts it there, so extract the
            // embedded copy - without this, a clean machine cannot start amelioration.
            string cliExePath = Path.Combine(ActivePath, "KTWirzade.CLI.exe");
            if (!File.Exists(cliExePath))
            {
                try
                {
                    ExtractEmbeddedResource("KTWirzade.GUI.Resources.KTWirzade.CLI.exe", cliExePath);
                    string cliConfigPath = Path.Combine(ActivePath, "KTWirzade.CLI.exe.config");
                    ExtractEmbeddedResource("KTWirzade.GUI.Resources.KTWirzade.CLI.exe.config", cliConfigPath);
                }
                catch (Exception ex)
                {
                    Log.WriteExceptionSafe(ex, "Failed to extract KTWirzade.CLI.exe from embedded resources.");
                }
            }
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
                    // Invoke with a hard 5s timeout: this handler runs on an IPC pipe thread;
                    // an unbound dispatcher wait could block it forever if the UI is busy.
                    // If the UI doesn't answer in 5s, we still show nothing extra and exit.
                    Current.Dispatcher.Invoke(
                        new System.Action(() => MessageBox.Show(null, $"{level} process exited unexpectedly with exit code: " + (uint)sender, "Error", KTWirzade.GUI.MessageBoxButton.OK, KTWirzade.GUI.MessageBoxImage.Information, null, null)),
                        System.Windows.Threading.DispatcherPriority.Normal,
                        default,
                        TimeSpan.FromSeconds(5));
                    Environment.Exit(1);
                }
            };
            // Root of the IPC session: generate the per-run pipe secret before any node launch.
            InterLink.InitializeSession();
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
            // Remove playbooks whose .apbx files no longer exist (deleted externally)
            string pbDir = Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks");
            foreach (PlaybookGUI pb in GlobalsGUI.Current.Items.OfType<PlaybookGUI>().ToList())
            {
                string apbxPath = Path.Combine(pbDir, pb.FileNameWithoutExtension + ".apbx");
                if (!File.Exists(apbxPath))
                {
                    Current.Dispatcher.Invoke(() => GlobalsGUI.Current.Items.Remove(pb));
                }
            }
            // Watch for externally deleted .apbx files
            try
            {
                if (Directory.Exists(pbDir))
                {
                    var apbxWatcher = new FileSystemWatcher(pbDir, "*.apbx")
                    {
                        NotifyFilter = NotifyFilters.FileName,
                        EnableRaisingEvents = true
                    };
                    apbxWatcher.Deleted += (sender, args) =>
                    {
                        var pb = GlobalsGUI.Current.Items.OfType<PlaybookGUI>().FirstOrDefault(x => pbDir + "\\" + x.FileNameWithoutExtension + ".apbx" == args.FullPath || string.Equals(x.FileNameWithoutExtension + ".apbx", args.Name, StringComparison.OrdinalIgnoreCase));
                        if (pb != null)
                        {
                            Current.Dispatcher.Invoke(() => GlobalsGUI.Current.Items.Remove(pb));
                        }
                    };
                    apbxWatcher.Created += (sender, args) =>
                    {
                        // Optionally reload if a new .apbx appears
                    };
                }
            }
            catch (Exception)
            {
                // Ignore watcher setup failures
            }
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
            new WelcomeWindow().Show();
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
                    // Was "!File.Exists(...)", which only deleted the icon when it was absent (no-op).
                    if (File.Exists(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks.ico")))
                    {
                        File.Delete(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks.ico"));
                    }
                    using FileStream file = new FileStream(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks.ico"), FileMode.Create, FileAccess.Write);
                    resource.CopyTo(file);
                }
            }
            using (var apbxKey = Registry.ClassesRoot.CreateSubKey(".apbx"))
            using (var iconKey = apbxKey?.CreateSubKey("DefaultIcon"))
            {
                iconKey?.SetValue("", "%PROGRAMDATA%\\KTWirzade\\Playbooks.ico", RegistryValueKind.ExpandString);
            }
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
                // The BAML loader reports assembly/type resolution failures (e.g. missing
                // netstandard facade or a DLL not shipped next to the exe) as a bare
                // NotImplementedException. Point the user at the actual remedy.
                string hint = string.Empty;
                if (e.Exception is System.Windows.Markup.XamlParseException && e.Exception.InnerException is NotImplementedException)
                {
                    hint = Environment.NewLine + Environment.NewLine +
                        "This error usually means this computer cannot load a UI dependency." + Environment.NewLine +
                        "1. Install .NET Framework 4.8: https://dotnet.microsoft.com/download/dotnet-framework/net48" + Environment.NewLine +
                        "2. Re-extract the full KT WIRZADE zip (do not run the exe alone).";
                }
                try
                {
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
                    SerializableTrace trace = new SerializableTrace(e.Exception, (string)null, 0, int.MaxValue);
                    KTWirzade.GUI.MessageBox.Show(null, "Please contact the KT WIRZADE team for assistance.", "A critical error occurred", KTWirzade.GUI.MessageBoxButton.Exit, KTWirzade.GUI.MessageBoxImage.Error, "[" + e.Exception.GetType().ToString().Split('.')
                        .Last() + "] " + e.Exception.Message + hint + Environment.NewLine + (object)trace, null);
                }
                catch (Exception)
                {
                    System.Windows.MessageBox.Show("Please contact the KT WIRZADE team for assistance.\r\n\r\n" + e.Exception.ToString() + hint, "A critical error occurred", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Hand);
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

        /// <summary>
        /// Extracts all embedded runtime dependencies (managed dlls, native helpers,
        /// CLI executable and configs) into the given directory. The Defender .cab
        /// packages stay embedded — ApplyPackageDialog streams them on demand.
        /// </summary>
        public static void ExtractRuntimeDependencies(string dir, bool overwrite = false)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            const string prefix = "KTWirzade.GUI.Resources.";
            foreach (string res in assembly.GetManifestResourceNames())
            {
                if (!res.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string name = res.Substring(prefix.Length).Replace("---", "\\");
                if (name.EndsWith(".cab", StringComparison.OrdinalIgnoreCase))
                    continue;

                string file = dir + "\\" + name;
                string fileDir = Path.GetDirectoryName(file);
                if (fileDir != null && !Directory.Exists(fileDir))
                {
                    Directory.CreateDirectory(fileDir);
                }

                if (File.Exists(file) && !overwrite)
                {
                    continue;
                }

                try
                {
                    using Stream stream = assembly.GetManifestResourceStream(res);
                    if (stream == null)
                        continue;

                    using FileStream fileStream = new FileStream(file, FileMode.Create, FileAccess.Write);
                    stream.CopyTo(fileStream);
                }
                catch (Exception)
                {
                    // A dependency locked by a running partner process keeps its existing copy.
                }
            }
        }

        /// <summary>
        /// Extracts a single embedded resource to a file path.
        /// </summary>
        public static void ExtractEmbeddedResource(string resourceName, string outputPath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fileStream);
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string activePath = ActivePath;
            AssemblyName assyName = new AssemblyName(args.Name);
            string baseName = assyName.Name;

            // 1) Try ActivePath (%TEMP%\AME)
            string candidate = Path.Combine(activePath, baseName);
            if (!candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !candidate.EndsWith(".winmd", StringComparison.OrdinalIgnoreCase))
                candidate = (!candidate.EndsWith("Windows", StringComparison.OrdinalIgnoreCase) ? candidate + ".dll" : candidate + ".winmd");
            if (File.Exists(candidate))
            {
                try { return Assembly.LoadFrom(candidate); } catch { }
            }

            // 2) Try alongside the exe (same as non-single probing path)
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(exeDir))
            {
                string exeCandidate = Path.Combine(exeDir, baseName + ".dll");
                if (File.Exists(exeCandidate))
                    try { return Assembly.LoadFrom(exeCandidate); } catch { }
            }

            return null;
        }

        /// <summary>
        /// Extracts FluentIcons.Common.dll and FluentIcons.Wpf.dll to the APPLICATION BASE DIRECTORY
        /// (same folder as the exe) before any XAML is parsed.
        /// 
        /// WHY: Baml2006SchemaContext.ResolveBamlType does NOT fire AppDomain.AssemblyResolve.
        /// It uses CLR probing: GAC → app base directory → probe subdirs.
        /// For single-exe, FluentIcons is only in embedded resources — BAML can't find it.
        /// Non-single works because FluentIcons.dll sits next to the exe (app base probing).
        /// 
        /// This method replicates that: extract FluentIcons beside the exe so BAML's own
        /// probing finds them naturally, exactly like the non-single distribution.
        /// </summary>
        private static void ExtractFluentIconsToDisk()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                // PRIMARY: extract to app base directory (beside the exe) — where BAML probes
                string dir = Path.GetDirectoryName(asm.Location);
                if (string.IsNullOrEmpty(dir)) return;

                foreach (string resSuffix in new[] { "FluentIcons.Common.dll", "FluentIcons.WPF.dll" })
                {
                    string resName = "KTWirzade.GUI.Resources." + resSuffix;
                    string diskName = resSuffix.Replace("WPF", "Wpf");
                    string outPath = Path.Combine(dir, diskName);
                    if (File.Exists(outPath)) continue;
                    foreach (var rn in asm.GetManifestResourceNames())
                    {
                        if (rn.Equals(resName, StringComparison.OrdinalIgnoreCase))
                        {
                            using (var stream = asm.GetManifestResourceStream(rn))
                            {
                                if (stream != null)
                                {
                                    using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                                        stream.CopyTo(fs);
                                }
                            }
                            break;
                        }
                    }
                }
            }
            catch { }
        }
    }
}