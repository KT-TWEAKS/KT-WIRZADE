using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using Core;
using Core.Actions;
using Core.Exceptions;
using Interprocess;
using iso_mode;
using JetBrains.Annotations;
using ManagedWimLib;
using Microsoft.Wim;
using Microsoft.Win32;
using KTWirzade.Shared.Actions;
using KTWirzade.Shared.Exceptions;
using KTWirzade.Shared.Parser;
using KTWirzade.Shared.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using RegistryKeyAction = KTWirzade.Shared.Actions.RegistryKeyAction;
using RegistryValueAction = Core.Actions.RegistryValueAction;
using RegistryValueType = KTWirzade.Shared.Actions.RegistryValueType;
using RunAction = KTWirzade.Shared.Actions.RunAction;
using TaskAction = KTWirzade.Shared.Tasks.TaskAction;
using UninstallTaskStatus = KTWirzade.Shared.Tasks.UninstallTaskStatus;
using Win32 = Core.Win32;

namespace KTWirzade.Shared
{

    public static class AmeliorationUtil
    {

        private static readonly HttpClient Client = new HttpClient();

        public static Playbook Playbook { set; get; } = new Playbook();

        public static bool UseKernelDriver = false;

        public static readonly List<string> ErrorDisplayList = new List<string>();

        public static int GetProgressMaximum(List<ITaskAction> actions) => actions.Sum(action => action.GetProgressWeight());

        public static bool LiveISO = false;
        public static bool ISO = false;
        public static string ISOGuid;
        public static string WimPath;
        public static WimWrapper WimInstance;

        private static bool IsApplicable([CanBeNull] Playbook upgradingFrom, bool? onUpgrade, [CanBeNull] string[] onUpgradeVersions, [CanBeNull] string option)
        {
            if (upgradingFrom == null)
                return !onUpgrade.GetValueOrDefault();

            bool isApplicable = true;
            bool? versionApplicable = null;
            if (onUpgradeVersions != null)
            {
                if (onUpgrade == null)
                    throw new YamlException("onUpgrade must be defined when using onUpgradeVersions");

                versionApplicable = 
                    onUpgradeVersions.Where(version => !version.StartsWith("!")).Any(version => IsApplicableUpgrade(upgradingFrom.Version, version))
                    &&
                    onUpgradeVersions.Where(version => version.StartsWith("!")).All(version => IsApplicableUpgrade(upgradingFrom.Version, version));
                isApplicable = versionApplicable.Value;
            }

            if (onUpgrade == null)
                return true;

            if (isApplicable && option != null)
            {
                isApplicable = String.Equals(option.Trim(), "ignore", StringComparison.OrdinalIgnoreCase) ||
                    (!(upgradingFrom.AvailableOptions?.Contains(option) ?? false) || (upgradingFrom.SelectedOptions?.Contains(option) ?? false));
            }
            
            if (upgradingFrom.GetVersionNumber() == Playbook.GetVersionNumber() && (versionApplicable == null || !onUpgradeVersions.Any(x => x.Equals(Playbook.Version))))
                return !onUpgrade.Value && !isApplicable;

            return onUpgrade.Value ? isApplicable : !isApplicable;
        }
        
        [CanBeNull]
        public static List<ITaskAction> ParseActions(string configPath, [CanBeNull] string isoBuild, [CanBeNull] string isoUpdateBuild, global::System.Runtime.InteropServices.Architecture? isoArch, List<string> options, string file, [CanBeNull] Playbook upgradingFrom)
        {
            var returnExceptionMessage = string.Empty;
            try
            {
                if (!File.Exists(Path.Combine(configPath, file)))
                    return null;
                
                var configData = File.ReadAllText(Path.Combine(configPath, file));
                var task = PlaybookParser.Deserializer.Deserialize<UninstallTask>(configData);

                //if (task.ISO == ISOSetting.Only && task.OOBE == OOBESetting.Only)
                //    throw new SerializationException($"Cannot have both ISO and OOBE set to only on task.");
                
                if ((!IsApplicable(upgradingFrom, task.OnUpgrade, task.OnUpgradeVersions, task.PreviousOption ?? task.Option) || 
                        !IsApplicableOption(task.Option, Playbook.Options) || !IsApplicableArch(task.Arch, ISO ? isoArch?.ToString() : null)) ||
                    (task.Builds != null && (
                        !task.Builds.Where(build => !build.StartsWith("!")).Any(build => IsApplicableWindowsVersion(build, ISO, isoBuild, isoUpdateBuild))
                        ||
                        task.Builds.Where(build => build.StartsWith("!")).Any(build => !IsApplicableWindowsVersion(build, ISO, isoBuild, isoUpdateBuild)))) ||
                    (task.Options != null && (
                        !task.Options.Where(option => !option.StartsWith("!")).Any(option => IsApplicableOption(option, Playbook.Options))
                        ||
                        task.Options.Where(option => option.StartsWith("!")).Any(option => !IsApplicableOption(option, Playbook.Options)))) ||
                    ((!LiveISO && task.OOBE == OOBESetting.Only && (!ISO || task.ISO != ISOSetting.Only)) || (LiveISO && (task.OOBE == OOBESetting.False || (task.OOBE == null && task.ISO == ISOSetting.True)))) ||
                    ((!ISO && task.ISO == ISOSetting.Only && (!LiveISO || task.OOBE != OOBESetting.Only)) || (ISO && task.ISO == ISOSetting.False)))
                {
                    return null;
                }
                
                var list = new List<ITaskAction>();

                // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
                foreach (Tasks.TaskAction taskAction in task.Actions)
                {
                    var isoCompatibilityError = taskAction.ISO != ISOSetting.False ? taskAction.IsISOCompatible() : null;
                    if (isoCompatibilityError != null)
                        throw new SerializationException(isoCompatibilityError);

                    //if (taskAction.ISO == ISOSetting.Only && taskAction.OOBE == OOBESetting.Only)
                    //    throw new SerializationException($"Cannot have both ISO and OOBE set to only on {taskAction.GetType().Name}.");
                    
                    if ((!IsApplicable(upgradingFrom, taskAction.OnUpgrade, taskAction.OnUpgradeVersions, taskAction.PreviousOption ?? taskAction.Option) || 
                            !IsApplicableOption(taskAction.Option, options) || !IsApplicableArch(taskAction.Arch, ISO ? isoArch?.ToString() : null)) ||
                        (taskAction.Builds != null && (
                            !taskAction.Builds.Where(build => !build.StartsWith("!")).Any(build => IsApplicableWindowsVersion(build, ISO, isoBuild, isoUpdateBuild))
                            ||
                            taskAction.Builds.Where(build => build.StartsWith("!")).Any(build => !IsApplicableWindowsVersion(build, ISO, isoBuild, isoUpdateBuild)))) ||
                        (taskAction.Options != null && (
                            !taskAction.Options.Where(option => !option.StartsWith("!")).Any(option => IsApplicableOption(option, Playbook.Options))
                            ||
                            taskAction.Options.Where(option => option.StartsWith("!")).Any(option => !IsApplicableOption(option, Playbook.Options)))) ||
                        ((!LiveISO && taskAction.OOBE == OOBESetting.Only && (!ISO || taskAction.ISO != ISOSetting.Only)) || (LiveISO && (taskAction.OOBE == OOBESetting.False || (taskAction.OOBE == null && taskAction.ISO == ISOSetting.True)))) ||
                        ((!ISO && taskAction.ISO == ISOSetting.Only && (!LiveISO || taskAction.OOBE != OOBESetting.Only)) || (ISO && taskAction.ISO == ISOSetting.False)))
                    {
                        continue;
                    }
                    
                    if (taskAction is Actions.TaskAction taskTaskAction)
                    {
                        if (!File.Exists(Path.Combine(configPath, taskTaskAction.Path)))
                            throw new FileNotFoundException("Could not find YAML file: " + taskTaskAction.Path);
                        try
                        {
                            list.AddRange(ParseActions(configPath, isoBuild, isoUpdateBuild, isoArch, options, taskTaskAction.Path, upgradingFrom) ?? new List<ITaskAction>());
                        }
                        catch (Exception e)
                        {
                            if (e is SerializationException exception)
                                returnExceptionMessage += exception.Message + Environment.NewLine + Environment.NewLine;
                            else
                                throw;
                        }
                    }
                    else
                    {
                        list.Add((ITaskAction)taskAction);
                    }
                }

                foreach (var childTask in task.Tasks)
                {
                    if (!File.Exists(Path.Combine(configPath, childTask)))
                        throw new FileNotFoundException("Could not find YAML file: " + childTask);
                    try
                    {
                        list.AddRange(ParseActions(configPath, isoBuild, isoUpdateBuild, isoArch, options, childTask, upgradingFrom) ?? new List<ITaskAction>());
                    }
                    catch (Exception e)
                    {
                        if (e is SerializationException exception)
                            returnExceptionMessage += exception.Message + Environment.NewLine + Environment.NewLine;
                        else
                            throw;
                    }
                }

                if (!string.IsNullOrEmpty(returnExceptionMessage))
                    throw new SerializationException(returnExceptionMessage.TrimEnd('\n', '\r'));

                return list;
            }
            catch (YamlException e)
            {
                var faultyText = Wrap.ExecuteSafe(() => GetFaultyYamlText(Path.Combine(configPath, file), e), true);
                if (faultyText.Failed || string.IsNullOrWhiteSpace(faultyText.Value))
                {
                    Log.EnqueueExceptionSafe(e);
                    throw new SerializationException(e.Message.TrimEnd('.') + $" in {Path.GetFileName(file)}.");
                }
                else
                {
                    Log.EnqueueExceptionSafe(e, ("YAML", faultyText.Value));
                    throw new SerializationException(FilterYAMLMessage(e).TrimEnd('.') + $" in {Path.GetFileName(file)}:{Environment.NewLine}{faultyText.Value}");
                }
            }
        }
        
        public static string GetFaultyYamlText(string yamlFilePath, YamlException yamlEx)
        {
            using (var reader = new StreamReader(yamlFilePath))
            {
                int currentLine = 0;
                StringBuilder sb = new StringBuilder();

                while (!reader.EndOfStream)
                {
                    currentLine++;
                    string line = reader.ReadLine();
                    if (line == null)
                        throw new IndexOutOfRangeException();

                    var prefix = $"Line {currentLine}: ";
                    if (currentLine == yamlEx.Start.Line)
                    {
                        if (yamlEx.Start.Line == yamlEx.End.Line)
                        {
                            int endIndexInLine = yamlEx.End.Column - Math.Max(0, yamlEx.Start.Column - 1);
                            var text = line.Substring(Math.Max(0, yamlEx.Start.Column - 1), endIndexInLine);
                            if (text.Length <= 1 || string.IsNullOrWhiteSpace(text))
                                text = line;

                            text = string.Join(Environment.NewLine + prefix.Length, text.SplitByLength(25).Select(x => x.Trim()));

                            sb.Append(prefix + text);
                            break;
                        }
                        else
                        {
                            var text = line.Substring(Math.Max(0, yamlEx.Start.Column - 1));
                            text = string.Join(Environment.NewLine + prefix.Length, text.SplitByLength(25).Select(x => x.Trim()));
                            sb.Append(prefix + text);
                        }
                    }
                    else if (currentLine > yamlEx.Start.Line && currentLine < yamlEx.End.Line)
                    {
                        var text = string.Join(Environment.NewLine + prefix.Length, line.SplitByLength(25).Select(x => x.Trim()));
                        sb.Append(Environment.NewLine).Append(prefix + text);
                    } else if (currentLine == yamlEx.End.Line)
                    {
                        var text = string.Join(Environment.NewLine + prefix.Length, line.Substring(0, yamlEx.End.Column).SplitByLength(25).Select(x => x.Trim()));
                        sb.Append(Environment.NewLine).Append(prefix + text);
                        break;
                    }
                }

                var faultyText = sb.ToString();
                return faultyText;
            }
        }
        private static string FilterYAMLMessage(YamlException exception)
        {
            int count = 0;
            int i = 0;

            for (; i < exception.Message.Length; i++)
            {
                if (exception.Message[i] == '(')
                    ++count;
                else if (exception.Message[i] == ')')
                    --count;

                if (exception.Message.Length >= i + 1 + 3 && exception.Message.Substring(i + 1, 3) == " - ")
                {
                    i += 3;
                    continue;
                }
                if (count == 0)
                    return exception.Message.Substring(i + 1).Trim().TrimStart(':', ' ');
            }
            throw new UnexpectedException();
        }

        public static async Task<bool> DoActions(List<ITaskAction> actions, string logFolder, Action<int> progressReport)
        {
            bool errorOccurred = false;
            foreach (ITaskAction action in actions)
            {
                var actionName = action.GetType().ToString().Split('.').Last();
                using var writer = new Output.OutputWriter(actionName.Replace("Action", ""), Path.Combine(logFolder, "Output.txt"), Path.Combine(logFolder, "Log.yml"));
                writer.LogOptions.SourceOverride = actionName;

                ErrorAction errorAction = ((Tasks.TaskAction)action).ErrorAction ?? action.GetDefaultErrorAction();
                var errorString = action.ErrorString();
                var retryAllowed = ((Tasks.TaskAction)action).AllowRetries ?? action.GetRetryAllowed();
                
                int i = 0;
                try
                {
                    if (!string.IsNullOrWhiteSpace(((Tasks.TaskAction)action).Status) && action.GetType() != typeof(WriteStatusAction))
                        await new WriteStatusAction() { Status = ((Tasks.TaskAction)action).Status }.RunTask(writer);
                    do
                    {
                        if (i > 0)
                            writer.WriteLineSafe("Warning", "Action detected as unsuccessful. Retrying...");
                        try
                        {
                            var actionTask = action.RunTask(writer);
                            if (actionTask == null)
                                action.RunTaskOnMainThread(writer);
                            else await actionTask;
                            action.ResetProgress();
                        }
                        catch (Exception e)
                        {
                            action.ResetProgress();

                            if (e is ErrorHandlingException errorHandlingException)
                            {
                                if (errorHandlingException.Action == TaskAction.ExitCodeAction.Retry || errorHandlingException.Action == TaskAction.ExitCodeAction.RetryError)
                                {
                                    errorString = errorHandlingException.Message;
                                    Thread.Sleep(50);
                                    i += 2;
                                    if (i == 10)
                                    {
                                        if (errorHandlingException.Action == TaskAction.ExitCodeAction.Retry)
                                        {
                                            i = 0;
                                            break;
                                        }
                                        if (errorHandlingException.Action == TaskAction.ExitCodeAction.RetryError)
                                        {
                                            i = 0;
                                            throw errorHandlingException;
                                        }
                                    }
                                    continue;
                                }
                                errorAction = errorHandlingException.Action switch
                                {
                                    TaskAction.ExitCodeAction.Log => ErrorAction.Log,
                                    TaskAction.ExitCodeAction.Error => ErrorAction.Notify,
                                    TaskAction.ExitCodeAction.Halt => ErrorAction.Halt,
                                    _ => ErrorAction.Log
                                };

                                errorString = errorHandlingException.Message;
                                ((Tasks.TaskAction)action).IgnoreErrors = false;
                                i = 10;
                                break;
                            }

                            Log.WriteExceptionSafe(e, null, new Log.LogOptions(writer));
                            
                            List<string> ExceptionBreakList = new List<string>() { "System.ArgumentException", "System.SecurityException", "System.UnauthorizedAccessException", "System.TimeoutException" };
                            if (ExceptionBreakList.Any(x => x.Equals(e.GetType().ToString())) || !retryAllowed)
                            {
                                i = 10;
                                break;
                            }
                            Thread.Sleep(300);
                        }

                        if (i > 0) Thread.Sleep(50);
                        i++;
                        
                        if (action.GetStatus(writer) == UninstallTaskStatus.Completed)
                            break;
                    } while (i < 10);
                }
                catch (Exception e)
                {
                    if (!((Tasks.TaskAction)action).IgnoreErrors)
                    {
                        if (errorAction == ErrorAction.Log)
                            Log.WriteExceptionSafe(LogType.Info, e, "An ignored error occurred while running an action.", new Log.LogOptions(writer));
                        if (errorAction == ErrorAction.Notify)
                        {
                            Log.WriteExceptionSafe(e, "An error occurred while running an action.", new Log.LogOptions(writer));
                            errorOccurred = true;
                        }
                        if (errorAction == ErrorAction.Halt)
                        {
                            Log.WriteExceptionSafe(LogType.Critical, e, "Playbook halted due to a failed critical action.", new Log.LogOptions(writer));
                            throw e;
                        }
                    }
                }
                
                progressReport(action.GetProgressWeight());
                if (i == 10)
                {
                    if (!((Tasks.TaskAction)action).IgnoreErrors)
                    {
                        if (errorAction == ErrorAction.Log)
                            Log.WriteSafe(LogType.Info, errorString, null, new Log.LogOptions(writer));
                        if (errorAction == ErrorAction.Notify)
                        {
                            Log.WriteSafe(LogType.Error, errorString, null, new Log.LogOptions(writer));
                            errorOccurred = true;
                        }
                        if (errorAction == ErrorAction.Halt)
                        {
                            Log.WriteSafe(LogType.Critical, "Playbook halted due to a critical error: " + errorString, null, new Log.LogOptions(writer));
                            throw new Exception("Critical error: " + errorString);
                        }
                    }
                }
            }

            ProcessPrivilege.ResetTokens();
            return errorOccurred;
        }

        public static Playbook DeserializePlaybook(string dir)
        {
            Playbook pb;
            
            XmlSerializer serializer = new XmlSerializer(typeof(Playbook));
            /*serializer.UnknownElement += delegate(object sender, XmlElementEventArgs args)
            {
                MessageBox.Show(args.Element.Name);
            };
            serializer.UnknownAttribute += delegate(object sender, XmlAttributeEventArgs args)
            {
                MessageBox.Show(args.Attr.Name);
            };*/
            try
            {
                using (XmlReader reader = XmlReader.Create($"{dir}\\playbook.conf"))
                {
                    pb = (Playbook)serializer.Deserialize(reader);
                }
            }
            catch (InvalidOperationException e)
            {
                if (e.InnerException == null)
                    throw;

                throw new XmlException(e.Message.TrimEnd('.') + ": " + e.InnerException.Message);
            }

            pb.Path = dir;
            return pb;
        }

        [Serializable]
        public class PlaybookMetadata : Log.ILogMetadata
        {
            public PlaybookMetadata(string[] options, string playbookName, string playbookVersion) => (Options, Playbook, Version) = (options, playbookName, playbookVersion);
         
            public DateTime CreationTime { get; set; }
            public string ClientVersion { get; set; }
            public string WindowsVersion { get; set; }
            public string SystemLanguage { get; set; }
            public string UserLanguage { get; set; }
            public global::System.Runtime.InteropServices.Architecture Architecture { get; set; }
            public string SystemMemory { get; set; }
            public int SystemThreads { get; set; }
            public string FreeSpace { get; set; }
            // ReSharper disable once MemberHidesStaticFromOuterClass
            public string Playbook { get; set; }
            public string Version { get; set; }
            public string[] Options { get; set; }
            public virtual void Construct()
            {
                ClientVersion = Globals.CurrentVersion;
                WindowsVersion = $"Windows {Win32.SystemInfoEx.WindowsVersion.MajorVersion} {Win32.SystemInfoEx.WindowsVersion.Edition} {Win32.SystemInfoEx.WindowsVersion.BuildNumber}.{Win32.SystemInfoEx.WindowsVersion.UpdateNumber}";
                SystemLanguage = Win32.SystemInfoEx.GetSystemLanguage();
                UserLanguage = Win32.SystemInfoEx.GetUserLanguage();
                SystemMemory = StringUtils.HumanReadableBytes(Win32.SystemInfoEx.GetSystemMemoryInBytes());
                SystemThreads = Environment.ProcessorCount;
                FreeSpace = StringUtils.HumanReadableBytes(Win32.SystemInfoEx.GetFreeDiskSpaceInBytes());
                Architecture = Win32.SystemInfoEx.SystemArchitecture;
                CreationTime = DateTime.UtcNow;
            }

            public string Serialize(ISerializer serializer) => serializer.Serialize(this);
        }


        private static void CreateWindowsSkeleton(string folder)
        {
            foreach (var directory in new []
                     {
                         @"Windows\System32\OOBE", @"Windows\TEMP", @"ProgramData\Microsoft\Windows", @"Program Files", @"Program Files (x86)",
                         @"Users\Public\Documents", @"Users\Public\Downloads", @"Users\Public\Desktop", @"Users\Public\Libraries", @"Users\Public\Music", @"Users\Public\Videos", @"Users\Public\Pictures",
                         @"Users\Default\Documents", @"Users\Default\Downloads", @"Users\Default\Desktop", @"Users\Default\Libraries", @"Users\Default\Music", @"Users\Default\Videos", @"Users\Default\Pictures",
                         @"Users\Default\AppData\Roaming\Microsoft\Windows\Start Menu\Programs", @"Users\Default\AppData\Local\Microsoft", @"Users\Default\Desktop", @"Users\Default\Libraries", @"Users\Default\Music", @"Users\Default\Videos", @"Users\Default\Pictures"
                     })
            {
                Directory.CreateDirectory(Path.Combine(folder, directory));
                var realFolder = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMDRIVE")!, folder);
                if (!Directory.Exists(realFolder))
                    continue;

                Wrap.ExecuteSafe(() =>
                {
                    DirectoryInfo aclFolder = new DirectoryInfo(realFolder);
                    do
                    {
                        var target = new DirectoryInfo(aclFolder.FullName.Replace(Environment.GetEnvironmentVariable("SYSTEMDRIVE")!, folder));
                        target.SetAccessControl(aclFolder.GetAccessControl());
                    } while ((aclFolder = aclFolder.Parent) != null);
                }, true);
            }
        }
        
        [InterprocessMethod(Level.TrustedInstaller)]
        public static async Task<bool> RunPlaybook(string playbookPath, bool verified, bool autoLogon, [CanBeNull] string username, [CanBeNull] string password, [CanBeNull] string adminPassword,
            string playbookName, string playbookVersion, string[] options, string[] allOptions, string logFolder, InterLink.InterProgress progress, [CanBeNull] InterLink.InterMessageReporter statusReporter,
            bool useKernelDriver) => await RunPlaybook(playbookPath, false, false, false, verified, autoLogon, username, password, adminPassword, false, null, null, null, null, null, playbookName, playbookVersion, options, allOptions, logFolder, progress, statusReporter, useKernelDriver);

        
        [InterprocessMethod(Level.TrustedInstaller)]
        public static async Task<bool> RunPlaybook(string playbookPath, bool networkDrivers, bool graphicsDrivers, bool systemDrivers, bool verified, bool autoLogon, [CanBeNull] string username, [CanBeNull] string password, [CanBeNull] string adminPassword, bool esd, string isoDest, [CanBeNull] string isoPath, [CanBeNull] string isoBuild, [CanBeNull] string isoUpdateBuild, global::System.Runtime.InteropServices.Architecture? isoArch, string playbookName, string playbookVersion, string[] options, string[] allOptions, string logFolder, InterLink.InterProgress progress, [CanBeNull] InterLink.InterMessageReporter statusReporter, bool useKernelDriver)
        {
            Log.LogFileOverride = Path.Combine(logFolder, "Log.yml");
            Log.MetadataSource = new PlaybookMetadata(options, playbookName, playbookVersion);
            
            ISO = isoPath != null;
            
            WriteStatusAction.StatusReporter = statusReporter;
            
            if (ISO)
                ThrowIfNotEnoughFreeSpace(isoPath, Path.GetTempPath());

            AmeliorationUtil.UseKernelDriver = useKernelDriver;

            AmeliorationUtil.Playbook = AmeliorationUtil.DeserializePlaybook(playbookPath);
            AmeliorationUtil.Playbook.Options = options?.ToList();

            var extractedIso = Path.Combine(Path.GetTempPath(), "KTWirzade-ISO-" + Guid.NewGuid());
            var mountGuidString = Guid.NewGuid().ToString().Replace("-", "").Replace("{", "").Replace("}", "");
            var winMount = Path.Combine(Path.GetPathRoot(Path.GetTempPath()), "ISO-" + mountGuidString);
            var wimMount = Path.Combine(Path.GetPathRoot(Path.GetTempPath()), "WIM-" + mountGuidString);
            var wimStaging = Path.Combine(Path.GetPathRoot(Path.GetTempPath()), "TMP-" + mountGuidString);
            
            if (ISO) {
                await new WriteStatusAction() { Status = "Extracting Image" }.RunTask(Output.OutputWriter.Null);

                var extractProgress = new Progress<decimal>(value =>
                {
                    if (value <= 100)
                        progress.Report(value / 20);
                });
                if (isoPath != null)
                    await iso_mode.ISO.WriteISO(isoPath, extractedIso, extractProgress);
            }
            bool unhooked = false;
            try
            {
                if (ISO)
                {
                    await new WriteStatusAction() { Status = "Extracting WIM" }.RunTask(Output.OutputWriter.Null);

                    try
                    {
                        Wim.GlobalInit("libwim-15.dll", InitFlags.None);
                    }
                    catch (InvalidOperationException e)
                    {
                        // Already initialized
                    }

                    if (Playbook.ISO?.DisableHardwareRequirements == true && File.Exists(Path.Combine(extractedIso, @"sources\boot.wim")))
                    {
                        using var bootWim = Wim.OpenWim(Path.Combine(extractedIso, @"sources\boot.wim"), OpenFlags.None);
                        int image = bootWim.GetWimInfo().BootIndex == 0 ? 1 : (int)bootWim.GetWimInfo().BootIndex;
                        var systemHivePath = Path.Combine(Path.GetTempPath(), "KTWirzade-BOOTWIM-" + ISOGuid, "SYSTEM");
                        bootWim.ExtractPath(image, Path.GetDirectoryName(systemHivePath), @"Windows\System32\config\SYSTEM", ExtractFlags.NoPreserveDirStructure);
                        if (Wrap.ExecuteSafe(() => WinUtil.RegistryManager.HookHive("BOOT-" + ISOGuid, systemHivePath), true) == null)
                        {
                            using (var labKey = Registry.Users.CreateSubKey("BOOT-" + ISOGuid + @"\Setup\LabConfig"))
                            {
                                labKey.SetValue("BypassRAMCheck", 1, RegistryValueKind.DWord);
                                labKey.SetValue("BypassSecureBootCheck", 1, RegistryValueKind.DWord);
                                labKey.SetValue("BypassCPUCheck", 1, RegistryValueKind.DWord);
                                labKey.SetValue("BypassTPMCheck", 1, RegistryValueKind.DWord);
                            }
                            if (Wrap.ExecuteSafe(() => WinUtil.RegistryManager.UnhookHive("BOOT-" + ISOGuid), true) == null)
                            {
                                bootWim.UpdateImage(
                                    image,
                                    UpdateCommand.SetAdd(systemHivePath, @"Windows\System32\config\SYSTEM", null, AddFlags.None),
                                    UpdateFlags.None);
                                bootWim.Overwrite(WriteFlags.None, Wim.DefaultThreads);
                            }
                        }
                    }
                    if (Playbook.ISO?.DisableBitLocker == true)
                    {
                        Directory.CreateDirectory(Path.Combine(extractedIso, @"sources\$OEM$\$$\Panther"));
                        File.WriteAllText(Path.Combine(extractedIso, @"sources\$OEM$\$$\Panther\unattend.xml"), ISOWIM.GenerateUnattendXml(isoArch switch
                        {
                            global::System.Runtime.InteropServices.Architecture.X86 => "x86",
                            global::System.Runtime.InteropServices.Architecture.X64 => "amd64",
                            global::System.Runtime.InteropServices.Architecture.Arm => "arm",
                            global::System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
                        }, true, true));
                    }

                    var wimPath = Path.Combine(extractedIso, @"sources\install.wim");
                    bool wimExists = File.Exists(wimPath);

                    if (!wimExists)
                    {
                        using (var esdWim = WimWrapper.OpenWim(Path.Combine(extractedIso, @"sources\install.esd"))) {
                            esdWim.WriteToWIM(Path.Combine(extractedIso, @"sources\install.wim"), wimStaging);
                        }
                        File.Delete(Path.Combine(extractedIso, @"sources\install.esd"));
                    }

                    WimInstance = WimWrapper.OpenWim(Path.Combine(extractedIso, @"sources\install.wim"));
                    
                    WimInstance.RemoveSuperfluousImages();
                    WimInstance.WriteChanges();
                    
                    Directory.CreateDirectory(winMount);
                    CreateWindowsSkeleton(winMount);
                    //wim.ExtractImage(wim.GetWimInfo().BootIndex == 0 ? 1 : (int)wim.GetWimInfo().BootIndex, mountedWim, ExtractFlags.None);
                    progress.Report(8);

                    WimPath = winMount;
                    ISOGuid = mountGuidString;
                    
                    if (Playbook.Requirements.Contains(Requirements.Requirement.DefenderDisabled))
                    {
                        var nameList = new List<string>();
                        for (int i = 1; i <= WimInstance.ImageCount; i++)
                        {
                            nameList.Add(WimInstance.GetImageName(i));
                        }

                        for (int i = 1; i <= WimInstance.ImageCount; i++)
                        {
                            await new WriteStatusAction() { Status = "Mounting " + nameList[i - 1] }.RunTask(Output.OutputWriter.Null);
                            
                            Directory.CreateDirectory(wimMount);
                            WimInstance.Mount(i, wimMount, wimStaging);

                            try
                            {
                                await new WriteStatusAction() { Status = "Applying Defender Package" }.RunTask(Output.OutputWriter.Null);
                                var cabPath = ExtractCab(isoArch ?? global::System.Runtime.InteropServices.Architecture.X64);

                                using var writer = new Output.OutputWriter("Run", Path.Combine(logFolder, "Output.txt"), Path.Combine(logFolder, "Log.yml"));
                                writer.LogOptions.SourceOverride = "Run";
                                var action = new RunAction()
                                {
                                    Exe = "DISM",
                                    Arguments = $"/Image:\"{wimMount}\" /Add-Package /PackagePath:\"{cabPath}\" /NoRestart /IgnoreCheck",
                                };
                                action.RunTaskOnMainThread(writer);

                                Wrap.ExecuteSafe(() => File.Delete(cabPath), true);

                                if (action.ExitCode != 0 && action.ExitCode != 3010)
                                    throw new Exception("Failed to apply Defender package.");
                            }
                            finally
                            {
                                WimInstance.Unmount();
                            }
                        }
                    }
                    
                    //await new WriteStatusAction() { Status = "Mounting Amogus" }.RunTask(Output.OutputWriter.Null);
                    
                    //Thread.Sleep(TimeSpan.FromSeconds(100));
                    
                    WimInstance.MountHives(ISOGuid);
                    
                    await new Actions.RegistryValueAction()
                    {
                        KeyName = @"HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection",
                        Value = "DisableRealtimeMonitoring",
                        Data = 1,
                        Type = RegistryValueType.REG_DWORD
                    }.RunTask(Output.OutputWriter.Null);
                    await new Actions.RegistryValueAction()
                    {
                        KeyName = @"HKLM\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection",
                        Value = "DisableRealtimeMonitoring",
                        Data = 1,
                        Type = RegistryValueType.REG_DWORD
                    }.RunTask(Output.OutputWriter.Null);
                    
                    if (Playbook.Requirements.Contains(Requirements.Requirement.DefenderToggled))
                    {
                        await new Actions.RegistryValueAction()
                        {
                            KeyName = @"HKLM\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection",
                            Value = "DisableAsyncScanOnOpen",
                            Data = 1,
                            Type = RegistryValueType.REG_DWORD
                        }.RunTask(Output.OutputWriter.Null);
                        await new Actions.RegistryValueAction()
                        {
                            KeyName = @"HKLM\SOFTWARE\Microsoft\Windows Defender\SpyNet",
                            Value = "SpyNetReporting",
                            Data = 0,
                            Type = RegistryValueType.REG_DWORD
                        }.RunTask(Output.OutputWriter.Null);
                        await new Actions.RegistryValueAction()
                        {
                            KeyName = @"HKLM\SOFTWARE\Microsoft\Windows Defender\SpyNet",
                            Value = "SubmitSamplesConsent",
                            Data = 0,
                            Type = RegistryValueType.REG_DWORD
                        }.RunTask(Output.OutputWriter.Null);
                        await new Actions.RegistryValueAction()
                        {
                            KeyName = @"HKLM\SOFTWARE\Microsoft\Windows Defender\Features",
                            Value = "TamperProtection",
                            Data = 4,
                            Type = RegistryValueType.REG_DWORD
                        }.RunTask(Output.OutputWriter.Null);
                    }
                    
                    /*
                    using (var wimDumpKey = Registry.Users.CreateSubKey("HKLM-SOFTWARE-" + ISOGuid + @"\Microsoft\Windows\Windows Error Reporting\LocalDumps")) {
                        wimDumpKey.SetValue("DumpType", 2, RegistryValueKind.DWord);
                        wimDumpKey.SetValue("DumpFolder", @"C:\CrashDumps", RegistryValueKind.ExpandString);
                    }
                    */

                    progress.Report(systemDrivers || graphicsDrivers || networkDrivers ? 10 : 20);
                    
                    CopyDirectory(Path.Combine(playbookPath), Path.Combine(WimPath, @"ProgramData\KTWirzade\OOBE\Playbook"));
                    
                    if (File.Exists(Path.Combine(WimPath, @"ProgramData\KTWirzade\OOBE\playbook.apbx")))
                        File.Delete(Path.Combine(WimPath, @"ProgramData\KTWirzade\OOBE\playbook.apbx"));
                    File.Move(Path.Combine(Directory.GetCurrentDirectory(), "oobe_playbook.apbx"), Path.Combine(WimPath, @"ProgramData\KTWirzade\OOBE\playbook.apbx"));

                    XmlSerializer serializerIso = new XmlSerializer(typeof(ISO));
                    using (XmlWriter writer = XmlWriter.Create(Path.Combine(extractedIso, @"iso.conf"), new XmlWriterSettings() {Indent = true}))
                    {
                        serializerIso.Serialize(writer, new ISO()
                        {
                            Name = Playbook.Name,
                            Creator = Playbook.Username,
                            UniqueId = Playbook.UniqueId,
                            Version = Playbook.Version,
                            WindowsVersion = isoBuild,
                            WindowsUpdateVersion = isoUpdateBuild,
                            Options = options ?? Array.Empty<string>(),
                            BitLockerDisabled = Playbook.ISO?.DisableBitLocker ?? false,
                            HardwareRequirementsDisabled = Playbook.ISO?.DisableHardwareRequirements ?? false,
                            InternetRequired = Playbook.OOBE?.Internet == OOBE.InternetRequirementLevel.Force,
                        });
                    }
                }

                Playbook upgradingFrom = null;
                if (!ISO)
                {
                    Playbook[] appliedPlaybooks = Playbook.GetAppliedPlaybooks();
                    upgradingFrom = Playbook.LastAppliedMatch(appliedPlaybooks);
                    if (upgradingFrom != null && (!Playbook.IsUpgradeApplicable(upgradingFrom.Version) && !(upgradingFrom.GetVersionNumber() <= Playbook.GetVersionNumber())))
                        upgradingFrom = null;
                }

                List<ITaskAction> actions = ParseActions($"{Playbook.Path}\\Configuration", isoBuild, isoUpdateBuild, isoArch, Playbook.Options,
                    File.Exists($"{Playbook.Path}\\Configuration\\main.yml") ? "main.yml" : "custom.yml",
                    upgradingFrom);
                if (actions == null)
                    throw new SerializationException("No applicable tasks were found in the Playbook.");

                bool errorOccurred = false;
                if (ISO)
                {
                    if (Playbook.Software == null)
                        Playbook.Software = Array.Empty<Playbook.Package>();
                    
                    Directory.CreateDirectory(Path.Combine(WimPath, @"ProgramData\KTWirzade\OOBE"));
                    XmlSerializer serializerOobe = new XmlSerializer(typeof(OOBE));
                    using (XmlWriter writer = XmlWriter.Create(Path.Combine(WimPath, @"ProgramData\KTWirzade\OOBE\oobe.conf"), new XmlWriterSettings() {Indent = true}))
                    {
                        serializerOobe.Serialize(writer, new OOBE()
                        {
                            Username = username,
                            Password = password,
                            AdminPassword = adminPassword,
                            AdminUserEnabled = options.Contains("security-enhanced"),
                            BulletPoints = Playbook.OOBE.BulletPoints.Select(x => new BulletPoint() {Icon = x.Icon, Title = x.Title, Description = x.Description}).ToList(),
                            AutoLogon = autoLogon,
                            Verified = verified,
                            Options = options ?? Array.Empty<string>(),
                            InternetRequirement = Playbook.OOBE.Internet,
                            Software = Playbook.Software.Where(x => string.IsNullOrEmpty(x.Option) || IsApplicableOption(x.Option, Playbook.Options)).Select(x => new OOBESoftware()
                            {
                                Name = x.Name,
                                Title = x.Title,
                                Description = x.Description,
                                IconPath = File.Exists(Path.Combine(Playbook.Path, "Images", x.Icon)) ? x.Icon : Directory.Exists(Path.Combine(Playbook.Path, "Images")) ? Directory.GetFiles(Path.Combine(Playbook.Path, "Images"), x.Icon).FirstOrDefault() ?? string.Empty : string.Empty,
                                IsDefaultWebBrowser = x.DefaultWebBrowser,
                                Local = x.Local,
                            }).ToList()
                        });
                    }
                    try
                    {

                        if (systemDrivers || networkDrivers || graphicsDrivers)
                        {
                            await DriverManager.HandleDrivers(driversProgress => progress.Report((decimal)(driversProgress / 10) + 10), statusReporter, graphicsDrivers, networkDrivers, systemDrivers,
                                "https://download.ameliorated.io/drivers.json",
                                Environment.ExpandEnvironmentVariables(@"%PROGRAMDATA%\KTWirzade\DriverCache"), Path.Combine(WimPath, @"ProgramData\KTWirzade\OOBE\Drivers"));
                        }
                    }
                    catch (Exception e)
                    {
                        Log.EnqueueExceptionSafe(e);
                        errorOccurred = true;
                    }
                }

                if (UseKernelDriver)
                {
                    //Check if KPH is installed.
                    ServiceController service = ServiceController.GetDevices()
                        .FirstOrDefault(s => s.DisplayName == "KProcessHacker2");
                    if (service == null)
                    {
                        //Installs KPH
                        await WinUtil.RemoveProtectionAsync();
                    }
                }

                var totalProgress = Math.Max(GetProgressMaximum(actions), 1);
                var progressLeft = totalProgress;
                Action<int> progressReport = addition =>
                {
                    progressLeft -= addition;
                    var progressValue = 1 - ((decimal)progressLeft / totalProgress);
                    if (isoPath != null)
                        progressValue = progressValue * 0.75M;
                    progress.Report(isoPath == null ? progressValue * 100 : 10 + (progressValue * 100));
                };

                WriteStatusAction.StatusReporter = statusReporter;

                try
                {
                    Environment.SetEnvironmentVariable("OOBE", LiveISO ? "true" : "false", EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable("ISO", ISO ? "true" : "false", EnvironmentVariableTarget.Process);
                    
                    try
                    {
                        errorOccurred = await DoActions(actions, logFolder, progressReport);
                    }
                    finally
                    {
                        WinUtil.RegistryManager.UnhookUserHives();
                    }

                    //Check if the kernel driver is installed.
                    //service = ServiceController.GetDevices()
                    //.FirstOrDefault(s => s.DisplayName == "KProcessHacker2");
                    if (UseKernelDriver)
                    {
                        //Remove Process Hacker's kernel driver.
                        await WinUtil.UninstallDriver();

                        CoreActions.SafeRun(new Core.Actions.RegistryKeyAction()
                        {
                            KeyName = @"HKLM\SYSTEM\CurrentControlSet\Services\KProcessHacker2",
                        });
                    }

                    if (ISO)
                    {
                        progress.Report(85);
                        await new WriteStatusAction() { Status = "Saving Image" }.RunTask(Output.OutputWriter.Null);
                        
                        await InjectOOBE();

                        WimInstance.UnmountHives(ISOGuid, true);
                        
                        //await new WriteStatusAction() { Status = "Mounting AmogusXXX" }.RunTask(Output.OutputWriter.Null);
                    
                        //Thread.Sleep(TimeSpan.FromSeconds(60));
                        unhooked = true;
                        
                        foreach (string directory in Directory.GetDirectories(winMount))
                        {
                            // We can't use AddTree on root directory because it messes up wim image name and other properties :(
                            WimInstance.AddTree(directory, @"\" + Path.GetFileName(directory));
                        }
                        
                        if (!esd)
                            WimInstance.WriteToWIM(Path.Combine(extractedIso, @"sources\install.wim"), wimStaging);
                        else
                        {
                            WimInstance.WriteToESD(Path.Combine(extractedIso, @"sources\install.esd"));
                            WimInstance.Dispose();
                            File.Delete(Path.Combine(extractedIso, @"sources\install.wim"));
                        }
                        progress.Report(96);
                        await new WriteStatusAction() { Status = "Cleaning WIM" }.RunTask(Output.OutputWriter.Null);
   
                        Wrap.ExecuteSafe(() => Directory.Delete(WimPath, true), true);
                        progress.Report(97);

                        await new WriteStatusAction() { Status = "Creating ISO" }.RunTask(Output.OutputWriter.Null);
                        
                        var runAction = new RunAction()
                        {
                            HandleExitCodes = new Dictionary<string, TaskAction.ExitCodeAction>() {{"!0", TaskAction.ExitCodeAction.Error}},
                            BaseDir = true,
                            Exe = "mkisofs.exe",
                            Arguments =
                                $@"-iso-level 4 -l -R -UDF -D -volid ""ISO"" -b boot/etfsboot.com -no-emul-boot -boot-load-size 8 -hide boot.catalog -eltorito-alt-boot -eltorito-platform efi -no-emul-boot -b efi/microsoft/boot/efisys.bin -o ""{isoDest}"" ""{extractedIso}"""
                        };
                        using var writer = new Output.OutputWriter("ISO Creator", Path.Combine(logFolder, "Output.txt"), Path.Combine(logFolder, "Log.yml"));
                        writer.LogOptions.SourceOverride = "ISO Creator";
                        
                        runAction.RunTaskOnMainThread(writer);
                        
                        progress.Report(98);
                        
                        await new WriteStatusAction() { Status = "Cleaning Up" }.RunTask(Output.OutputWriter.Null);
                    }

                    return errorOccurred;
                }
                catch (Exception e)
                {
                    if (!ISO)
                    {
                        Wrap.ExecuteSafe(() =>
                        {
                            WriteAppliedPlaybook(playbookPath, ISO ? Registry.Users.OpenSubKey("HKLM-" + mountGuidString) : null, ISO ? WimPath : null, Playbook.UniqueId, Playbook.Name, Playbook.Username,
                                Playbook.Overhaul, Playbook.Version, options ?? Array.Empty<string>(), allOptions, true, true, verified);   
                        }, true);
                    }
                    throw;
                }
                
            }
            finally
            {
                if (ISO)
                {
                    if (!unhooked)
                        Wrap.ExecuteSafe(() => WimInstance.UnmountHives(ISOGuid), true);
                    
                    WimInstance?.Dispose();

                    if (Directory.Exists(WimPath))
                    {
                        Wrap.ExecuteSafe(() => Directory.Delete(WimPath));
                        
                        Wrap.ExecuteSafe(() =>
                        {
                            DirectoryInfo dir = new DirectoryInfo(WimPath);
                            foreach (FileInfo file in dir.GetFiles("*", SearchOption.AllDirectories))
                                Wrap.ExecuteSafe(() => file.Attributes = FileAttributes.Normal);
                            foreach (DirectoryInfo subDir in dir.GetDirectories("*", SearchOption.AllDirectories))
                                Wrap.ExecuteSafe(() => subDir.Attributes = FileAttributes.Normal);
                            Wrap.ExecuteSafe(() => dir.Attributes = FileAttributes.Normal);
                        });
                        
                        Wrap.ExecuteSafe(() => Directory.Delete(WimPath, true));
                    }
                    if (Directory.Exists(wimMount))
                    {
                        Wrap.ExecuteSafe(() => Directory.Delete(wimMount));
                        
                        Wrap.ExecuteSafe(() =>
                        {
                            DirectoryInfo dir = new DirectoryInfo(wimMount);
                            foreach (FileInfo file in dir.GetFiles("*", SearchOption.AllDirectories))
                                Wrap.ExecuteSafe(() => file.Attributes = FileAttributes.Normal);
                            foreach (DirectoryInfo subDir in dir.GetDirectories("*", SearchOption.AllDirectories))
                                Wrap.ExecuteSafe(() => subDir.Attributes = FileAttributes.Normal);
                            Wrap.ExecuteSafe(() => dir.Attributes = FileAttributes.Normal);
                        });
                        
                        Wrap.ExecuteSafe(() => Directory.Delete(wimMount, true));
                    }
                    if (Directory.Exists(wimStaging))
                    {
                        Wrap.ExecuteSafe(() => Directory.Delete(wimStaging));
                        
                        Wrap.ExecuteSafe(() =>
                        {
                            DirectoryInfo dir = new DirectoryInfo(wimStaging);
                            foreach (FileInfo file in dir.GetFiles("*", SearchOption.AllDirectories))
                                Wrap.ExecuteSafe(() => file.Attributes = FileAttributes.Normal);
                            foreach (DirectoryInfo subDir in dir.GetDirectories("*", SearchOption.AllDirectories))
                                Wrap.ExecuteSafe(() => subDir.Attributes = FileAttributes.Normal);
                            Wrap.ExecuteSafe(() => dir.Attributes = FileAttributes.Normal);
                        });
                        
                        Wrap.ExecuteSafe(() => Directory.Delete(wimStaging, true));
                    }
                    Wrap.ExecuteSafe(() => Directory.Delete(extractedIso, true), true);
                }
            }
        }

        private static async Task InjectOOBE()
        {
            string destination = Path.Combine(WimPath, @"ProgramData\KTWirzade\OOBE\OOBE.exe");
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (UnmanagedMemoryStream stream = (UnmanagedMemoryStream)assembly!.GetManifestResourceStream($"KTWirzade.Shared.Properties.AME.Client.exe"))
            {
                byte[] buffer = new byte[stream!.Length];
                stream.Read(buffer, 0, buffer.Length);
                File.WriteAllBytes(destination, buffer);
            }
            destination = Path.Combine(WimPath, @"Windows\system32\OOBE\msoobe.exe");
            
            WimInstance.MoveFileOrFolder(@"Windows\system32\OOBE\msoobe.exe", @"Windows\system32\OOBE\msoobeext.exe");
            
            using (UnmanagedMemoryStream stream = (UnmanagedMemoryStream)assembly!.GetManifestResourceStream($"KTWirzade.Shared.Properties.oobe_shim.exe"))
            {
                byte[] buffer = new byte[stream!.Length];
                stream.Read(buffer, 0, buffer.Length);
                File.WriteAllBytes(destination, buffer);
            }
            
            await new Actions.RegistryValueAction()
            {
                KeyName = @"HKLM\SYSTEM\CurrentControlSet\Services\ameoobe",
                Value = "DisplayName",
                Data = "KT WIRZADE OOBE",
                Type = RegistryValueType.REG_SZ
            }.RunTask(Output.OutputWriter.Null);
            await new Actions.RegistryValueAction()
            {
                KeyName = @"HKLM\SYSTEM\CurrentControlSet\Services\ameoobe",
                Value = "ObjectName",
                Data = "LocalSystem",
                Type = RegistryValueType.REG_SZ
            }.RunTask(Output.OutputWriter.Null);
            await new Actions.RegistryValueAction()
            {
                KeyName = @"HKLM\SYSTEM\CurrentControlSet\Services\ameoobe",
                Value = "ImagePath",
                Data = "\"%ProgramData%\\KTWirzade\\OOBE\\OOBE.exe\" --service",
                Type = RegistryValueType.REG_EXPAND_SZ
            }.RunTask(Output.OutputWriter.Null);
            await new Actions.RegistryValueAction()
            {
                KeyName = @"HKLM\SYSTEM\CurrentControlSet\Services\ameoobe",
                Value = "ErrorControl",
                Data = 0,
                Type = RegistryValueType.REG_DWORD
            }.RunTask(Output.OutputWriter.Null);         
            await new Actions.RegistryValueAction()
            {
                KeyName = @"HKLM\SYSTEM\CurrentControlSet\Services\ameoobe",
                Value = "Start",
                Data = 2,
                Type = RegistryValueType.REG_DWORD
            }.RunTask(Output.OutputWriter.Null);
            await new Actions.RegistryValueAction()
            {
                KeyName = @"HKLM\SYSTEM\CurrentControlSet\Services\ameoobe",
                Value = "Type",
                Data = 16,
                Type = RegistryValueType.REG_DWORD
            }.RunTask(Output.OutputWriter.Null);
        }
        
        private static string ExtractCab(global::System.Runtime.InteropServices.Architecture arch)
        {
            var cabArch = arch == global::System.Runtime.InteropServices.Architecture.Arm || arch == global::System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "amd64";
            
            var fileDir = Environment.ExpandEnvironmentVariables("%ProgramData%\\KTWirzade");
            if (!Directory.Exists(fileDir)) Directory.CreateDirectory(fileDir);

            var destination = Path.Combine(fileDir, $"Z-KTWirzade-NoDefender-Package31bf3856ad364e35{cabArch}1.0.0.0.cab");

            if (File.Exists(destination))
            {
                return destination;
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            using (UnmanagedMemoryStream stream = (UnmanagedMemoryStream)assembly!.GetManifestResourceStream($"KTWirzade.Shared.Properties.Z-AME-NoDefender-Package31bf3856ad364e35{cabArch}1.0.0.0.cab"))
            {
                byte[] buffer = new byte[stream!.Length];
                stream.Read(buffer, 0, buffer.Length);
                File.WriteAllBytes(destination, buffer);
            }
            return destination;
        }
        
        private static int RunPSCommand(string command, [CanBeNull] DataReceivedEventHandler outputHandler, [CanBeNull] DataReceivedEventHandler errorHandler) =>
            RunCommand("powershell.exe", $"-NoP -C \"{command}\"", outputHandler, errorHandler);
        private static int RunCommand(string exe, string arguments, [CanBeNull] DataReceivedEventHandler outputHandler, [CanBeNull] DataReceivedEventHandler errorHandler)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = exe,
                    Arguments = arguments,

                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = outputHandler != null,
                    RedirectStandardError = errorHandler != null
                }
            };

            if (outputHandler != null)
                process.OutputDataReceived += outputHandler;
            if (errorHandler != null)
                process.ErrorDataReceived += errorHandler;

            if (!process.Start())
            {
                Log.WriteSafe(LogType.Error, $"Failed to start process: {process.StartInfo.FileName} {process.StartInfo.Arguments}", new SerializableTrace());
                return -1;
            }

            if (outputHandler != null)
                process.BeginOutputReadLine();
            if (errorHandler != null)
                process.BeginErrorReadLine();

            process.WaitForExit();
            return process.ExitCode;
        }
        
        static void ThrowIfNotEnoughFreeSpace(string isoPath, string destination)
        {
            if (!File.Exists(isoPath))
                throw new FileNotFoundException($"The file '{isoPath}' does not exist.");

            long fileSize = new FileInfo(isoPath).Length * 4;

            DriveInfo tempDrive = new DriveInfo(Path.GetPathRoot(destination));
            long freeSpace = tempDrive.AvailableFreeSpace;

            if (freeSpace < fileSize)
                throw new IOException($"Not enough free space on the drive. Required: {fileSize} bytes, Available: {freeSpace} bytes.");
        }
        
        private static void DeleteAppliedPlaybook(string folderName)
        {
            var appliedDir = Environment.ExpandEnvironmentVariables(@"%ProgramData%\KTWirzade\AppliedPlaybooks");
            if (Directory.Exists(Path.Combine(appliedDir, folderName)))
                Directory.Delete(Path.Combine(appliedDir, folderName), true);
        }
        
        private static void WriteAppliedPlaybook(string playbookPath, [CanBeNull] RegistryKey rootKey, [CanBeNull] string ameRoot, Guid? uniqueId, string name, string username, bool overhaul, string version, [NotNull] string[] selectedOptions, [NotNull] string[] allOptions, bool hadErrors, bool fatalError, bool isVerified)
        {
            try
            {
                if (uniqueId != null)
                {
                    using var key = (rootKey ?? Registry.LocalMachine).CreateSubKey(@$"SOFTWARE\KTWirzade\Playbooks\Applied\{{{uniqueId.Value.ToString().ToUpper()}}}", true);
                    key.SetValue("Name", name, RegistryValueKind.String);
                    key.SetValue("Username", username, RegistryValueKind.String);
                    key.SetValue("Overhaul", overhaul ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("Version", version, RegistryValueKind.String);
                    key.SetValue("ErrorLevel", hadErrors ? fatalError ? 2 : 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("AvailableOptions", allOptions, RegistryValueKind.MultiString);
                    key.SetValue("SelectedOptions", selectedOptions, RegistryValueKind.MultiString);
                    key.SetValue("AppliedTimeUTC", DateTime.UtcNow.ToBinary(), RegistryValueKind.QWord);
                    if (File.Exists(Path.Combine(playbookPath, "playbook.png")))
                        key.SetValue("Image", File.ReadAllBytes(Path.Combine(playbookPath, "playbook.png")), RegistryValueKind.Binary);
                    else if (File.Exists(Path.Combine(playbookPath, "Images\\playbook.png")))
                        key.SetValue("Image", File.ReadAllBytes(Path.Combine(playbookPath, "Images\\playbook.png")), RegistryValueKind.Binary);
                }
                else
                {
                    var parent = Directory.CreateDirectory(ameRoot != null ? Path.Combine(ameRoot, "AppliedPlaybooks") : Environment.ExpandEnvironmentVariables(@"%ProgramData%\KTWirzade\AppliedPlaybooks"));
                    var indexes = parent.GetDirectories().Where(v => int.TryParse(v.Name, out _)).Select(v => int.Parse(v.Name)).ToList();

                    var currentIndex = indexes.Count > 0 ? indexes.Max() : 0;
                    if (currentIndex >= 10)
                        Wrap.ExecuteSafe(() => parent.GetDirectories().First().Delete(true), true);

                    var target = parent.CreateSubdirectory((currentIndex + 1).ToString());

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
            }
            catch (Exception e)
            {
                Log.EnqueueExceptionSafe(LogType.Warning, e);
            }
        }
        
        static void CopyDirectory(string sourceDir, string destDir)
        {
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string newDestSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, newDestSubDir);
            }
        }
        
        public static async Task DownloadLanguagesAsync(IEnumerable<string> langsSelected)
        {

            foreach (var lang in langsSelected)
            {

                var lowerLang = lang.ToLower();

                var arch = RuntimeInformation.OSArchitecture;
                var winVersion = Environment.OSVersion.Version.Build;

                var convertedArch = "";
                switch (arch)
                {
                    case global::System.Runtime.InteropServices.Architecture.X64:
                        convertedArch = "amd64";
                        break;
                    case global::System.Runtime.InteropServices.Architecture.Arm64:
                        convertedArch = "arm64";
                        break;
                    case global::System.Runtime.InteropServices.Architecture.X86:
                        convertedArch = "x86";
                        break;
                }

                var uuidOfWindowsVersion = "";
                var uuidResponse =
                    await Client.GetAsync(
                        $"https://api.uupdump.net/listid.php?search={winVersion}%20{convertedArch}&sortByDate=1");
                switch (uuidResponse.StatusCode)
                {
                    //200 Status code
                    case HttpStatusCode.OK:
                        {
                            var result = uuidResponse.Content.ReadAsStringAsync().Result;
                            //Gets the UUID of the first build object in the response, we take the first since it's the newest.
                            uuidOfWindowsVersion = (string)(JToken.Parse(result)["response"]?["builds"]?.Children().First()
                                .Children().First().Last());
                            break;
                        }
                    //400 Status code
                    case HttpStatusCode.BadRequest:
                        {
                            var result = uuidResponse.Content.ReadAsStringAsync().Result;
                            dynamic data = JObject.Parse(result);
                            Console.WriteLine($"Bad request.\r\nError:{data["response"]["error"]}");
                            break;
                        }
                    //429 Status code
                    case (HttpStatusCode)429:
                        {
                            var result = uuidResponse.Content.ReadAsStringAsync().Result;
                            dynamic data = JObject.Parse(result);
                            Console.WriteLine($"Too many requests, try again later.\r\nError:{data["response"]["error"]}");
                            break;
                        }
                    //500 Status code
                    case HttpStatusCode.InternalServerError:
                        {
                            var result = uuidResponse.Content.ReadAsStringAsync().Result;
                            dynamic data = JObject.Parse(result);
                            Console.WriteLine($"Internal Server Error.\r\nError:{data["response"]["error"]}");
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var responseString =
                    await Client.GetAsync(
                        $"https://api.uupdump.net/get.php?id={uuidOfWindowsVersion}&lang={lowerLang}");
                switch (responseString.StatusCode)
                {
                    //200 Status code
                    case HttpStatusCode.OK:
                        {

                            var result = responseString.Content.ReadAsStringAsync().Result;
                            dynamic data = JObject.Parse(result);
                            //Add different urls to different packages to a list
                            var urls = new Dictionary<string, string>
                        {
                            {
                                "basic", (string) data["response"]["files"][
                                    $"microsoft-windows-languagefeatures-basic-{lowerLang}-package-{convertedArch}.cab"]
                                [
                                    "url"]
                            },
                            {
                                "hw", (string) data["response"]["files"][
                                    $"microsoft-windows-languagefeatures-handwriting-{lowerLang}-package-{convertedArch}.cab"]
                                [
                                    "url"]
                            },
                            {
                                "ocr", (string) data["response"]["files"][
                                    $"microsoft-windows-languagefeatures-ocr-{lowerLang}-package-{convertedArch}.cab"][
                                    "url"]
                            },
                            {
                                "speech", (string) data["response"]["files"][
                                    $"microsoft-windows-languagefeatures-speech-{lowerLang}-package-{convertedArch}.cab"]
                                [
                                    "url"]
                            },
                            {
                                "tts", (string) data["response"]["files"][
                                    $"microsoft-windows-languagefeatures-texttospeech-{lowerLang}-package-{convertedArch}.cab"]
                                [
                                    "url"]
                            }
                        };


                            var amePath = Path.Combine(Path.GetTempPath(), "KTWirzade\\");
                            //Create the directory if it doesn't exist.
                            var file = new FileInfo(amePath);
                            file.Directory?.Create(); //Does nothing if the directory already exists

                            //Final result being "temp\KTWirzade\Languages\file.cab"
                            var downloadPath = Path.Combine(amePath, "Languages\\");
                            file = new FileInfo(downloadPath);
                            file.Directory?.Create();
                            using (var webClient = new WebClient())
                            {
                                Console.WriteLine($"Downloading {lowerLang}.cab file, please wait..");
                                foreach (var url in urls)
                                {
                                    //Check if the file exists, if it does exist, skip it.
                                    if (File.Exists(Path.Combine(downloadPath, $"{url.Key}_{lowerLang}.cab")))
                                    {
                                        Console.WriteLine($"{url.Key}_{lowerLang} already exists, skipping.");
                                        continue;
                                    }
                                    //Output file format: featureName_languageCode.cab: speech_de-de.cab
                                    webClient.DownloadFile(url.Value, $@"{downloadPath}\{url.Key}_{lowerLang}.cab");
                                }
                            }

                            break;
                        }
                    //400 Status code
                    case HttpStatusCode.BadRequest:
                        {
                            var result = responseString.Content.ReadAsStringAsync().Result;
                            dynamic data = JObject.Parse(result);
                            Console.WriteLine($"Bad request.\r\nError:{data["response"]["error"]}");
                            break;
                        }
                    //429 Status code
                    case (HttpStatusCode)429:
                        {
                            var result = responseString.Content.ReadAsStringAsync().Result;
                            dynamic data = JObject.Parse(result);
                            Console.WriteLine($"Too many requests, try again later.\r\nError:{data["response"]["error"]}");
                            break;
                        }
                    //500 Status code
                    case HttpStatusCode.InternalServerError:
                        {
                            var result = responseString.Content.ReadAsStringAsync().Result;
                            dynamic data = JObject.Parse(result);
                            Console.WriteLine($"Internal Server Error.\r\nError:{data["response"]["error"]}");
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static bool IsApplicableUpgrade(string oldVersion, string allowedVersion)
        {
            var oldVersionNumber = VersionNumber.GetVersionNumber(oldVersion);
            var version = allowedVersion;
            bool negative = false;
            if (version.StartsWith("!"))
            {
                version = version.TrimStart('!');
                negative = true;
            }
            bool result = false;

            if (version.StartsWith(">="))
            {
                var parsed = VersionNumber.GetVersionNumber(version.Substring(2));
                if (oldVersionNumber >= parsed)
                    result = true;
            }
            else if (version.StartsWith("<="))
            {
                var parsed = VersionNumber.GetVersionNumber(version.Substring(2));
                if (oldVersionNumber <= parsed)
                    result = true;
            }
            else if (version.StartsWith(">"))
            {
                var parsed = VersionNumber.GetVersionNumber(version.Substring(1));
                if (oldVersionNumber > parsed)
                    result = true;
            }
            else if (version.StartsWith("<"))
            {
                var parsed = VersionNumber.GetVersionNumber(version.Substring(1));
                if (oldVersionNumber < parsed)
                    result = true;
            }
            else
            {
                var parsed = VersionNumber.GetVersionNumber(version);
                if (oldVersionNumber == parsed)
                    result = true;
            }

            return negative ? !result : result;
        }
        
        public static bool IsApplicableWindowsVersion(string version, bool ISO, [CanBeNull] string targetISOVersion = null, [CanBeNull] string targetISOUpdateVersion = null)
        {
            bool negative = false;
            if (version.StartsWith("!"))
            {
                version = version.TrimStart('!');
                negative = true;
            }
            bool result = false;

            bool compareUpdateBuild = version.Contains(".");
            if ((ISO && targetISOUpdateVersion == null) && compareUpdateBuild)
                version = version.Split('.').First();
            decimal currentBuild;
            if (ISO)
            {
                if (targetISOVersion != null)
                    currentBuild = decimal.Parse(targetISOUpdateVersion == null ? targetISOVersion : targetISOVersion + "." + targetISOUpdateVersion, CultureInfo.InvariantCulture);
                else
                    return false;
            } else
                currentBuild = decimal.Parse(compareUpdateBuild ? Win32.SystemInfoEx.WindowsVersion.BuildNumber + "." + Win32.SystemInfoEx.WindowsVersion.UpdateNumber : Win32.SystemInfoEx.WindowsVersion.BuildNumber.ToString(), CultureInfo.InvariantCulture);

            if (version.StartsWith(">="))
            {
                var parsed = decimal.Parse(version.Substring(2), CultureInfo.InvariantCulture);
                if (currentBuild >= parsed)
                    result = true;
            }
            else if (version.StartsWith("<="))
            {
                var parsed = decimal.Parse(version.Substring(2), CultureInfo.InvariantCulture);
                if (currentBuild <= parsed)
                    result = true;
            }
            else if (version.StartsWith(">"))
            {
                var parsed = decimal.Parse(version.Substring(1), CultureInfo.InvariantCulture);
                if (currentBuild > parsed)
                    result = true;
            }
            else if (version.StartsWith("<"))
            {
                var parsed = decimal.Parse(version.Substring(1), CultureInfo.InvariantCulture);
                if (currentBuild < parsed)
                    result = true;
            }
            else
            {
                var parsed = decimal.Parse(version, CultureInfo.InvariantCulture);
                if (currentBuild == parsed)
                    result = true;
            }

            return negative ? !result : result;
        }
        
        private static bool IsApplicableOption(string option, List<string> options)
        {
            if (String.IsNullOrEmpty(option))
                return true;

            if (option.Contains("&"))
            {
                if (option.Contains("!"))
                    throw new ArgumentException("YAML options item must not contain both & and !", "options");

                return option.Split('&').All(splitOption => IsApplicableOption(splitOption, options));
            }
            
            bool negative = false;
            if (option.StartsWith("!"))
            {
                option = option.TrimStart('!');
                negative = true;
            }
            
            if (options == null)
                return negative ? true : false;

            var result = options.Contains(option, StringComparer.OrdinalIgnoreCase);

            return negative ? !result : result;
        }
        
        private static bool IsApplicableArch(string arch, [CanBeNull] string isoArch)
        {
            if (String.IsNullOrEmpty(arch))
                return true;

            bool negative = false;
            if (arch.StartsWith("!"))
            {
                arch = arch.TrimStart('!');
                negative = true;
            }

            var result = String.Equals(arch, isoArch ?? Win32.SystemInfoEx.SystemArchitecture.ToString(), StringComparison.OrdinalIgnoreCase);
            

            return negative ? !result : result;
        }
    }
}
