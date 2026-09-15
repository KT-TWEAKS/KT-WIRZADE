using System;
using System.IO;
using System.Threading.Tasks;
using Core;
using KTWirzade.Shared.Tasks;
using YamlDotNet.Serialization;
using File = System.IO.File;

namespace KTWirzade.Shared.Actions
{
    class ShortcutAction : Tasks.TaskAction, ITaskAction
    {
        public void RunTaskOnMainThread(Output.OutputWriter output) { throw new NotImplementedException(); }
        [YamlMember(typeof(string), Alias = "path")]
        public string RawPath { get; set; }

        [YamlMember(typeof(string), Alias = "name")]
        public string Name { get; set; }

        [YamlMember(typeof(string), Alias = "destination")]
        public string Destination { get; set; }

        [YamlMember(typeof(string), Alias = "description")]
        public string Description { get; set; }
        
        [YamlMember(typeof(int), Alias = "weight")]
        public int ProgressWeight { get; set; } = 1;
        public int GetProgressWeight() => ProgressWeight;
        public ErrorAction GetDefaultErrorAction() => Tasks.ErrorAction.Log;
        public bool GetRetryAllowed() => true;
        
        private bool InProgress { get; set; }
        public void ResetProgress() => InProgress = false;
        
        public string ErrorString() => $"ShortcutAction failed to create shortcut to '{Destination}' from '{RawPath}' with name {Name}.";
        
        public UninstallTaskStatus GetStatus(Output.OutputWriter output)
        {
            //If the shortcut already exists return Completed
            return File.Exists(Path.Combine(this.Destination, this.Name + ".lnk")) ? 
                UninstallTaskStatus.Completed : UninstallTaskStatus.ToDo;
        }

        public async Task<bool> RunTask(Output.OutputWriter output)
        {
            RawPath = Environment.ExpandEnvironmentVariables(RawPath);
            Destination = Environment.ExpandEnvironmentVariables(Destination);
            output.WriteLineSafe("Info", $"Creating shortcut from '{Destination}' to '{RawPath}'...");
            
            if (!File.Exists(this.RawPath))
            {
                throw new FileNotFoundException($"File '{RawPath}' not found.");
            }

            if (!Directory.Exists(this.Destination))
                Directory.CreateDirectory(this.Destination);

            var lnkPath = Path.Combine(this.Destination, this.Name + ".lnk");

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                throw new InvalidOperationException("WScript.Shell COM object is not available on this system.");
            }

            dynamic shell = Activator.CreateInstance(shellType);
            try
            {
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                shortcut.TargetPath = this.RawPath;
                if (!string.IsNullOrEmpty(this.Description))
                    shortcut.Description = this.Description;
                shortcut.WorkingDirectory = Path.GetDirectoryName(this.RawPath);
                shortcut.Save();
            }
            finally
            {
                if (shell != null)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            }

            output.WriteLineSafe("Info", $"Shortcut created at '{lnkPath}'.");
            return true;
        }
    }
}
