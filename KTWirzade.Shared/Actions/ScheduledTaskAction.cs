using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using KTWirzade.Shared.Exceptions;
using KTWirzade.Shared.Tasks;
using YamlDotNet.Serialization;

namespace KTWirzade.Shared.Actions
{
    internal enum ScheduledTaskOperation
    {
        Delete = 0,
        Enable = 1,
        Disable = 2,
        DeleteFolder = 3
    }

    internal class ScheduledTaskAction : Tasks.TaskAction, ITaskAction
    {
        public void RunTaskOnMainThread(Output.OutputWriter output) { throw new NotImplementedException(); }
        [YamlMember(typeof(ScheduledTaskOperation), Alias = "operation")]
        public ScheduledTaskOperation Operation { get; set; } = ScheduledTaskOperation.Delete;
        [YamlMember(Alias = "data")]
        public string? RawTask { get; set; } = null;
        [YamlMember(Alias = "path")]
        public string Path { get; set; }

        [YamlMember(typeof(string), Alias = "weight")]
        public int ProgressWeight { get; set; } = 1;
        public int GetProgressWeight() => ProgressWeight;
        public ErrorAction GetDefaultErrorAction() => Tasks.ErrorAction.Log;
        public bool GetRetryAllowed() => true;

        private bool InProgress { get; set; } = false;
        public void ResetProgress() => InProgress = false;

        public string ErrorString() => $"ScheduledTaskAction failed to change task {Path} to state {Operation.ToString()}";

        public UninstallTaskStatus GetStatus(Output.OutputWriter output)
        {
            if (InProgress)
            {
                return UninstallTaskStatus.InProgress;
            }

            if (AmeliorationUtil.ISO && Operation != ScheduledTaskOperation.Delete && Operation != ScheduledTaskOperation.DeleteFolder)
            {
                output.WriteLineSafe("Warning", "Enabling and disabling scheduled tasks is not supported on ISOs, skipping...");
                return UninstallTaskStatus.Completed;
            }
            else
            {
                using var schedule = (AmeliorationUtil.ISO ? Registry.Users.OpenSubKey($@"HKLM-SOFTWARE-{AmeliorationUtil.ISOGuid}\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache") :
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache"))!;
                using var taskKey = schedule.OpenSubKey(@"Tree\" + Path.TrimStart('\\').Replace('/', '\\'), true);
                return (taskKey == null || (!AmeliorationUtil.ISO && Wrap.ExecuteSafe(() =>
                {
                    using TaskService ts = new TaskService();

                    if (Operation != ScheduledTaskOperation.DeleteFolder)
                    {
                        var task = ts.GetTask(Path);
                        if (task is null)
                        {
                            return Operation == ScheduledTaskOperation.Delete;
                        }

                        if (task.Enabled)
                        {
                            return Operation == ScheduledTaskOperation.Enable;
                        }

                        return Operation == ScheduledTaskOperation.Disable;
                    }
                    else
                    {
                        var folder = ts.GetFolder(Path);
                        if (folder == null)
                            return true;

                        return !folder.GetTasks().Any();
                    }
                }, false, true).Value)) ? UninstallTaskStatus.Completed : UninstallTaskStatus.ToDo;
            }

        }

        public async Task<bool> RunTask(Output.OutputWriter output)
        {
            if (GetStatus(output) == UninstallTaskStatus.Completed)
            {
                return true;
            }

            if (InProgress) throw new TaskInProgressException("Another ScheduledTask action was called while one was in progress.");

            output.WriteLineSafe("Info", $"{Operation.ToString().TrimEnd('e')}ing scheduled task '{Path}'...");

            InProgress = true;

            if (AmeliorationUtil.ISO)
            {
                if (Operation != ScheduledTaskOperation.Delete && Operation != ScheduledTaskOperation.DeleteFolder)
                    output.WriteLineSafe("Warning", "Enabling and disabling scheduled tasks is not supported on ISOs, skipping...");
                else
                    DeleteUsingRegistry(Path.TrimStart('\\').Replace('/', '\\'));

                InProgress = false;
                return true;
            }
            
            using TaskService ts = new TaskService();
            
            if (Operation != ScheduledTaskOperation.DeleteFolder)
            {
                var task = ts.GetTask(Path);
                if (task is null)
                {
                    if (Operation == ScheduledTaskOperation.Delete)
                    {
                        return true;
                    }

                    if (RawTask is null || RawTask.Length == 0)
                    {
                        return false;
                    }
                }

                switch (Operation)
                {
                    case ScheduledTaskOperation.Delete:
                        // TODO: This will probably not work if we actually use sub-folders
                        try
                        {
                            ts.RootFolder.DeleteTask(Path, false);
                            DeleteUsingRegistry(Path.TrimStart('\\').Replace('/', '\\'));
                        }
                        catch (Exception e)
                        {
                            Log.EnqueueExceptionSafe(LogType.Warning, e);
                            DeleteUsingRegistry(Path.TrimStart('\\').Replace('/', '\\'));
                        }
                        break;
                    case ScheduledTaskOperation.Enable:
                    case ScheduledTaskOperation.Disable:
                        {
                            
                            if (AmeliorationUtil.ISO)
                            {
                                output.WriteLineSafe("Warning", "Enabling and disabling scheduled tasks is not supported on ISOs, skipping...");
                                return true;
                            }
                            
                            if (task is null && !(RawTask is null))
                            {
                                task = ts.RootFolder.RegisterTask(Path, RawTask);
                            }

                            if (!(task is null))
                            {
                                task.Enabled = Operation == ScheduledTaskOperation.Enable;
                            }
                            else
                            {
                                throw new ArgumentException($"Task provided is null.");
                            }

                            break;
                        }
                    default:
                        throw new ArgumentException($"Argument out of range.");
                }

                InProgress = false;
                return true;
            }
            else
            {
                if (AmeliorationUtil.ISO)
                    DeleteUsingRegistry(Path);
                else
                {
                    var folder = ts.GetFolder(Path);

                    if (folder is null) return true;

                    folder.GetTasks().ToList().ForEach(x =>
                    {
                        try
                        {
                            folder.DeleteTask(x.Name, false);
                            DeleteUsingRegistry(System.IO.Path.Combine(Path.TrimStart('\\').Replace('/', '\\'), x.Name));
                        }
                        catch (Exception e)
                        {
                            Log.EnqueueExceptionSafe(LogType.Warning, e);
                            DeleteUsingRegistry(System.IO.Path.Combine(Path.TrimStart('\\').Replace('/', '\\'), x.Name));
                        }
                    });

                    try
                    {
                        folder.Parent.DeleteFolder(folder.Name);
                        DeleteUsingRegistry(Path.TrimStart('\\').Replace('/', '\\'));
                    }
                    catch (Exception e)
                    {
                        Log.EnqueueExceptionSafe(LogType.Warning, e);
                        DeleteUsingRegistry(Path.TrimStart('\\').Replace('/', '\\'));
                    }
                }

                InProgress = false;
                return true;
            }
        }
        private static void DeleteUsingRegistry(string path, bool throwOnTaskNotFound = false)
        {
            using var schedule = (AmeliorationUtil.ISO ? Registry.Users.OpenSubKey($@"HKLM-SOFTWARE-{AmeliorationUtil.ISOGuid}\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache") : Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache"))!;
            using var taskKey = schedule.OpenSubKey(@"Tree\" + path, true);
            if (taskKey == null)
            {
                if (throwOnTaskNotFound)
                    throw new Exception($"Task '{path}' not found in tree.");
                else
                    return;
            }

            foreach (string subTask in taskKey.GetSubKeyNames())
            {
                if (taskKey.OpenSubKey(subTask)?.GetSubKeyNames().Any() == true)
                {
                    DeleteUsingRegistry(path + "\\" + subTask);
                }
            }
            foreach (string subTask in taskKey.GetSubKeyNames())
            {
                using (var subTaskKey = taskKey.OpenSubKey(subTask)!)
                {
                    var id = (string)subTaskKey.GetValue("Id");
                    if (id != null)
                    {
                        DeleteTaskIdRelations(schedule, id);
                    }
                }
                taskKey.DeleteSubKeyTree(subTask);
            }

            using (var treeKey = schedule.OpenSubKey(@"Tree", true))
            {
                treeKey!.DeleteSubKeyTree(path);
            }
        }
        
        private static void DeleteTaskIdRelations(RegistryKey schedule, string id)
        {
            foreach (var relation in new [] { "Boot", "Logon", "Maintenance", "Plain", "Tasks" })
            {
                using var relationKey = schedule.OpenSubKey(relation, true);
                if (relationKey == null)
                    continue;
                var match = relationKey.GetSubKeyNames().FirstOrDefault(x => x.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    continue;
                
                relationKey.DeleteSubKeyTree(match);
            }
            using var flagsKey = schedule.OpenSubKey("TaskStateFlags", true);
            if (flagsKey == null)
                return;
            var flagMatch = flagsKey.GetSubKeyNames().FirstOrDefault(x => x.Equals("/" + id.Replace("\\", "/"), StringComparison.OrdinalIgnoreCase));
            if (flagMatch == null)
                return;
                
            flagsKey.DeleteSubKeyTree(flagMatch);
        }
    }
}
