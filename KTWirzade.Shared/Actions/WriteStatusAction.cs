using System;
using System.IO;
using System.Threading.Tasks;
using Core;
using KTWirzade.Shared.Tasks;
using YamlDotNet.Serialization;

namespace KTWirzade.Shared.Actions
{
    public class WriteStatusAction : Tasks.TaskAction, ITaskAction
    {
        public static IProgress<string> StatusReporter { get; set; } = null;
        
        public void RunTaskOnMainThread(Output.OutputWriter output) { throw new NotImplementedException(); }
        [YamlMember(typeof(string), Alias = "status")]
        public string Status { get; set; }
        private bool InProgress { get; set; }
        public void ResetProgress() => InProgress = false;
        public string ErrorString() => "";
        public int GetProgressWeight() => 0;
        public ErrorAction GetDefaultErrorAction() => Tasks.ErrorAction.Log;
        public bool GetRetryAllowed() => false;
        public UninstallTaskStatus GetStatus(Output.OutputWriter output)
        {
            if (InProgress)
            {
                return UninstallTaskStatus.InProgress;
            }

            return hasCompleted ? UninstallTaskStatus.Completed : UninstallTaskStatus.ToDo;
        }
        private bool hasCompleted;
        public async Task<bool> RunTask(Output.OutputWriter output)
        {
            hasCompleted = true;
            const string separator = "--------------------------------------------------";
            var statusText = String.IsNullOrWhiteSpace(Status) ? "Running actions" : Status;
             
            output.WriteLineRawSafe((File.Exists(output.OutputFile) ? Environment.NewLine + separator : separator) + Environment.NewLine + $"[Status] {statusText}" + Environment.NewLine + separator + Environment.NewLine);
             
            StatusReporter?.Report(statusText);
             
            return true;
        }
    }
}
