using System;
using System.Threading.Tasks;
using Core;
using KTWirzade.Shared.Tasks;
using YamlDotNet.Serialization;

namespace KTWirzade.Shared.Actions
{
    public class TaskAction : Tasks.TaskAction, ITaskAction
    {
        [YamlMember(typeof(string), Alias = "path")]
        public string Path { get; set; }
        
        
        [YamlMember(typeof(ISOSetting), Alias = "iso")]
        public override ISOSetting ISO { get; set; } = ISOSetting.True;
        [YamlMember(typeof(OOBESetting?), Alias = "oobe")]
        public override OOBESetting? OOBE { get; set; } = OOBESetting.True;
        
        public void RunTaskOnMainThread(Output.OutputWriter output) => throw new NotImplementedException();
        private bool InProgress { get; set; }
        public void ResetProgress() => InProgress = false;
        public string ErrorString() => "";
        public int GetProgressWeight() => 0;
        public ErrorAction GetDefaultErrorAction() => Tasks.ErrorAction.Notify;
        public bool GetRetryAllowed() => false;
        public UninstallTaskStatus GetStatus(Output.OutputWriter output) => throw new NotImplementedException();
        public Task<bool> RunTask(Output.OutputWriter output) => throw new NotImplementedException();

    }
}
