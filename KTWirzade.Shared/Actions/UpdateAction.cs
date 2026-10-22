using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using KTWirzade.Shared.Tasks;
using YamlDotNet.Serialization;

namespace KTWirzade.Shared.Actions
{
    public class UpdateAction : Tasks.TaskAction, ITaskAction
    {
        public void RunTaskOnMainThread(Output.OutputWriter output) { throw new NotImplementedException(); }
        [YamlMember(typeof(string), Alias = "name")]
        public string PackageName { get; set; }
        
        [YamlMember(typeof(int), Alias = "weight")]
        public int ProgressWeight { get; set; } = 1;
        public int GetProgressWeight() => ProgressWeight;
        public ErrorAction GetDefaultErrorAction() => Tasks.ErrorAction.Notify;
        public bool GetRetryAllowed() => true;
        
        private bool InProgress { get; set; }
        private int? ExitCode { get; set; }
        public void ResetProgress() => InProgress = false;
        
        public string ErrorString() => $"UpdateAction failed to remove update package {PackageName}.";
        
        public UninstallTaskStatus GetStatus(Output.OutputWriter output)
        {
            if (InProgress)
            {
                return UninstallTaskStatus.InProgress;
            }

            return ExitCode == 0 ? UninstallTaskStatus.Completed : UninstallTaskStatus.ToDo;
        }

        public async Task<bool> RunTask(Output.OutputWriter output)
        {
            if (InProgress)
            {
                output.WriteLineSafe("Info", "An update action is already in progress...");
                return false;
            }
            InProgress = true;
            try
            {
                output.WriteLineSafe("Info", $"Removing update package '{PackageName}'...");

                CmdAction removeUpdate = new CmdAction()
                {
                    Command = @$"DISM.exe /Online /Remove-Package /PackageName:{PackageName} /quiet /norestart",
                    Wait = true,
                    // "!0" matches any non-zero exit code; CmdAction then throws
                    // ErrorHandlingException, the engine's failure signal for exit codes.
                    HandleExitCodes = new Dictionary<string, Tasks.TaskAction.ExitCodeAction>
                    {
                        { "!0", Tasks.TaskAction.ExitCodeAction.Error }
                    }
                };

                removeUpdate.RunTaskOnMainThread(output);

                ExitCode = 0;
                return true;
            }
            finally
            {
                InProgress = false;
            }
        }
    }
}
