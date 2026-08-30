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

            return ExitCode == null ? UninstallTaskStatus.ToDo : UninstallTaskStatus.Completed;
        }

        public async Task<bool> RunTask(Output.OutputWriter output)
        {
            if (InProgress)
            {
                output.WriteLineSafe("Info", "An update action is already in progress...");
                return false;
            }
            InProgress = true;
            
            output.WriteLineSafe("Info", $"Removing update package '{PackageName}'...");
            
            CmdAction removeUpdate = new CmdAction()
            {
                Command = @$"DISM.exe /Online /Remove-Package /PackageName:{PackageName} /quiet /norestart"
            };

            removeUpdate.RunTaskOnMainThread(output);

            InProgress = false;
            return true;
            
        }
    }
}
