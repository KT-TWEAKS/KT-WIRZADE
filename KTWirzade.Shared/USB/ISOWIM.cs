using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Core;
using ManagedWimLib;
using Microsoft.Win32;
using KTWirzade.Shared;
using KTWirzade.Shared.Actions;

namespace iso_mode
{
    public static class ISOWIM
    {
        public static void SetBootWimRequirements(string isoPath, bool? tpm, bool? cpuRam)
        {
            var guid = Guid.NewGuid().ToString().Replace("-", "").Replace("{", "").Replace("}", "");
            
            try
            {
                Wim.GlobalInit("libwim-15.dll", InitFlags.None);
            }
            catch (InvalidOperationException e)
            {
                // Already initialized
            }
            if (File.Exists(Path.Combine(isoPath, @"sources\boot.wim")))
            {
                using var bootWim = Wim.OpenWim(Path.Combine(isoPath, @"sources\boot.wim"), OpenFlags.None);
                int image = bootWim.GetWimInfo().BootIndex == 0 ? 1 : (int)bootWim.GetWimInfo().BootIndex;
                var systemHivePath = Path.Combine(Path.GetTempPath(), "AME-BOOTWIM-" + guid, "SYSTEM");
                bootWim.ExtractPath(image, Path.GetDirectoryName(systemHivePath), @"Windows\System32\config\SYSTEM", ExtractFlags.NoPreserveDirStructure);
                if (Wrap.ExecuteSafe(() => WinUtil.RegistryManager.HookHive("BOOT-" + guid, systemHivePath), true) == null)
                {
                    using (var labKey = Registry.Users.CreateSubKey("BOOT-" + guid + @"\Setup\LabConfig")!)
                    {
                        if (cpuRam.HasValue)
                        {
                            labKey.SetValue("BypassRAMCheck", cpuRam == true ? 1 : 0, RegistryValueKind.DWord);
                            labKey.SetValue("BypassSecureBootCheck", cpuRam == true ? 1 : 0, RegistryValueKind.DWord);
                            labKey.SetValue("BypassCPUCheck", cpuRam == true ? 1 : 0, RegistryValueKind.DWord);
                        }
                        if (tpm.HasValue)
                            labKey.SetValue("BypassTPMCheck", tpm == true ? 1 : 0, RegistryValueKind.DWord);
                    }
                    if (Wrap.ExecuteSafe(() => WinUtil.RegistryManager.UnhookHive("BOOT-" + guid), true) == null)
                    {
                        bootWim.UpdateImage(
                            image,
                            UpdateCommand.SetAdd(systemHivePath, @"Windows\System32\config\SYSTEM", null, AddFlags.None),
                            UpdateFlags.None);
                        bootWim.Overwrite(WriteFlags.None, Wim.DefaultThreads);
                    }
                }
                Wrap.ExecuteSafe(() => Directory.Delete(Path.GetDirectoryName(systemHivePath)!, true), true);
            }
        }
        public static async Task SetInstallWimRequirements(string isoPath, string isoArch, bool? internet, bool? bitlocker)
        {
            Directory.CreateDirectory(Path.Combine(isoPath, @"sources\$OEM$\$$\Panther"));
            var unattendPath = Path.Combine(isoPath, @"sources\$OEM$\$$\Panther\unattend.xml");
            bool bitlockerSet = false;
            bool internetSet = false;
            if (File.Exists(unattendPath))
            {
                Wrap.ExecuteSafe(() =>
                {
                    var text = File.ReadAllText(unattendPath);
                    if (text.Contains("<PreventDeviceEncryption>true</PreventDeviceEncryption>"))
                        bitlockerSet = true;
                    if (text.Contains("BypassNRO /t REG_DWORD /d 1"))
                        internetSet = true;
                }, true);
            }
            if ((internet == false || (!internet.HasValue && !internetSet)) && (bitlocker == false || (!bitlocker.HasValue && !bitlockerSet)))
            {
                if (File.Exists(unattendPath))
                    File.Delete(unattendPath);
            }
            else
                File.WriteAllText(unattendPath, GenerateUnattendXml(isoArch, internet ?? internetSet, bitlocker ?? bitlockerSet));
            
            return;
            
            var guid = Guid.NewGuid().ToString().Replace("-", "").Replace("{", "").Replace("}", "");
            
            using var wrapper = WimWrapper.OpenWim(Path.Combine(Path.Combine(isoPath, @"sources\install.wim")));
            try
            {
                wrapper.MountHives(guid);

                try
                {
                    AmeliorationUtil.ISO = true;
                    AmeliorationUtil.ISOGuid = guid;

                    await new RegistryValueAction()
                    {
                        KeyName = @"HKLM\SYSTEM\CurrentControlSet\Control\BitLocker",
                        Value = "PreventDeviceEncryption",
                        Data = 1,
                        Type = RegistryValueType.REG_DWORD,
                    }.RunTask(Output.OutputWriter.Null);
                    await new RegistryValueAction()
                    {
                        KeyName = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE",
                        Value = "BypassNRO",
                        Type = RegistryValueType.REG_DWORD,
                    }.RunTask(Output.OutputWriter.Null);
                }
                finally
                {
                    AmeliorationUtil.ISO = false;
                    AmeliorationUtil.ISOGuid = null;
                }
            }
            finally
            {
                wrapper.UnmountHives(guid);
            }
        }
        public static void ConvertWIMToESD(string isoPath)
        {
            if (File.Exists(Path.Combine(isoPath, @"sources\install.esd")))
                return;
            if (!File.Exists(Path.Combine(isoPath, @"sources\install.wim")))
            {
                Log.EnqueueSafe(LogType.Warning, "ISO install.wim not found.", new SerializableTrace());
                return;
            }
            using (var wrapper = WimWrapper.OpenWim(Path.Combine(Path.Combine(isoPath, @"sources\install.wim"))))
            {
                wrapper.WriteToESD(Path.Combine(isoPath, @"sources\install.esd"));
            }
            File.Delete(Path.Combine(isoPath, @"sources\install.wim"));
        }

        public static string GenerateUnattendXml(
            string arch,
            bool noOnlineAccount,
            bool disableBitlocker)
        {
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\">");

        string componentAttributes = $"processorArchitecture=\"{arch}\" language=\"neutral\" " +
            "xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" " +
            "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" " +
            "publicKeyToken=\"31bf3856ad364e35\" versionScope=\"nonSxS\"";

        if (noOnlineAccount)
        {
            xml.AppendLine("  <settings pass=\"specialize\">");
            xml.AppendLine($"    <component name=\"Microsoft-Windows-Deployment\" {componentAttributes}>");
            xml.AppendLine("      <RunSynchronous>");
            xml.AppendLine("        <RunSynchronousCommand wcm:action=\"add\">");
            xml.AppendLine("          <Order>1</Order>");
            xml.AppendLine("          <Path>reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE /v BypassNRO /t REG_DWORD /d 1 /f</Path>");
            xml.AppendLine("        </RunSynchronousCommand>");
            xml.AppendLine("      </RunSynchronous>");
            xml.AppendLine("    </component>");
            xml.AppendLine("  </settings>");
        }

        if (disableBitlocker)
        {
            xml.AppendLine("  <settings pass=\"oobeSystem\">");
            xml.AppendLine($"    <component name=\"Microsoft-Windows-SecureStartup-FilterDriver\" {componentAttributes}>");
            xml.AppendLine("      <PreventDeviceEncryption>true</PreventDeviceEncryption>");
            xml.AppendLine("    </component>");
            xml.AppendLine($"    <component name=\"Microsoft-Windows-EnhancedStorage-Adm\" {componentAttributes}>");
            xml.AppendLine("      <TCGSecurityActivationDisabled>1</TCGSecurityActivationDisabled>");
            xml.AppendLine("    </component>");
            xml.AppendLine("  </settings>");
        }

        xml.AppendLine("</unattend>");
        return xml.ToString();
    }
    }
}
