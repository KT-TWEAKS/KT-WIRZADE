
using System;
using KTWirzade.Shared.Actions;
using KTWirzade.Shared.Tasks;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using TaskAction = KTWirzade.Shared.Actions.TaskAction;

namespace KTWirzade.Shared.Parser
{
    internal class TaskActionResolver : INodeTypeResolver
    {
        public bool Resolve(NodeEvent? nodeEvent, ref Type currentType)
        {
            if (!currentType.IsInterface || currentType != typeof(ITaskAction))
            {
                return false;
            }

            switch (nodeEvent?.Tag.Value)
            {
                case "!file:":
                    currentType = typeof(FileAction);
                    return true;
                case "!service:":
                    currentType = typeof(ServiceAction);
                    return true;
                case "!user:":
                    currentType = typeof(UserAction);
                    return true;
                case "!run:":
                    currentType = typeof(RunAction);
                    return true;
                case "!powerShell:":
                    currentType = typeof(PowerShellAction);
                    return true;
                case "!shortcut:":
                    currentType = typeof(ShortcutAction);
                    return true;
                case "!cmd:":
                    currentType = typeof(CmdAction);
                    return true;
                case "!scheduledTask:":
                    currentType = typeof(ScheduledTaskAction);
                    return true;
                case "!lineInFile:":
                    currentType = typeof(LineInFileAction);
                    return true;
                case "!regexFile:":
                    currentType = typeof(RegexFileAction);
                    return true;
                case "!registryKey:":
                    currentType = typeof(RegistryKeyAction);
                    return true;
                case "!registryValue:":
                    currentType = typeof(RegistryValueAction);
                    return true;
                case "!appx:":
                    currentType = typeof(AppxAction);
                    return true;
                case "!systemPackage:":
                    currentType = typeof(SystemPackageAction);
                    return true;
                case "!taskKill:":
                    currentType = typeof(TaskKillAction);
                    return true;
                case "!software:":
                    currentType = typeof(SoftwareAction);
                    return true;
                case "!download:":
                    currentType = typeof(DownloadAction);
                    return true;
                case "!update:":
                    currentType = typeof(UpdateAction);
                    return true;
                case "!writeStatus:":
                    currentType = typeof(WriteStatusAction);
                    return true;
                case "!status:":
                    currentType = typeof(WriteStatusAction);
                    return true;
                case "!task:":
                    currentType = typeof(TaskAction);
                    return true;
                case "!language:":
                    currentType = typeof(LanguageAction);
                    return true;
                default:
                    return false;
            }
        }
    }
}