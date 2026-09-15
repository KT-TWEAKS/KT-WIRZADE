using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Interprocess;
using ManagedWimLib;

namespace iso_mode
{
    public class InterMethods
    {
        [InterprocessMethod(Level.Administrator)]
        public static async Task WriteISO(string isoArch, bool windows, string deviceId, uint diskIndex, string isoPath, string label, bool? tpm, bool? cpuRam, bool? internet, bool? bitlocker, InterLink.InterProgress progress)
        {
            progress.Report(1);
            
            var usb = Wrap.Retry().Execute(() =>
            {
                // TODO: Change log value
                var usb = Wrap.RetryExponential.Execute(() => USB.GetDevices(true, true).FirstOrDefault(x => x.UsbDeviceID == deviceId && x.Index == diskIndex));
                if (usb == null)
                {
                    throw new Exception("Device not found");
                }
                
                return usb;
            });
            progress.Report(2);
            
            if (windows)
            {
                var size = new FileInfo(isoPath).Length;

                Wrap.RetryExponential.Execute(() =>
                {
                    if (Drive.CreatePartitionGPT(usb, size, false) != 1)
                    {
                        throw new Exception("Failed to create GPT partition table.");
                    }
                });

                progress.Report(3);
                
                var mount = Wrap.RetryExponential.Execute(() => Drive.GetVolumeMount(usb, usb.ISOPartitionOffset, true, true));

                progress.Report(5);
                
                Wrap.RetryExponential.Execute(() => Format.FormatDrive(mount.VolumeName.Substring(mount.VolumeName.IndexOf('{') + 1, mount.VolumeName.IndexOf('}') - (mount.VolumeName.IndexOf('{') + 1)), label, mount.Letter!.Value!));
                Wrap.RetryExponential.ExecuteSafe(Drive.UnmountUEFINTFS, true);
                progress.Report(10);
                
                await ISO.WriteISO(isoPath, mount.Letter!.Value + @":\", progress);
                
                Wrap.ExecuteSafe(() =>
                {
                    using (var handle = Wrap.RetryExponential.Execute(() => Drive.GetHandle(@$"\\.\{mount.Letter.Value}:")))
                    {
                        if (!Win32.FlushFileBuffers(handle))
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }, true);
                
                Drive.TryFlushDrive(mount.Letter.Value);
                Drive.RemountSafe(mount.Letter.Value);
                if (File.Exists(mount.Letter.Value + @":\sources\boot.wim"))
                {
                    if (tpm.HasValue || cpuRam.HasValue)
                        Wrap.ExecuteSafe(() => ISOWIM.SetBootWimRequirements(mount.Letter.Value + @":\", tpm, cpuRam), true);
                    if (internet.HasValue || bitlocker.HasValue)
                        Wrap.ExecuteSafe(() => ISOWIM.SetInstallWimRequirements(mount.Letter.Value + @":\", isoArch, internet, bitlocker), true);
                    
                    Drive.RemountSafe(mount.Letter.Value);
                }
                Wrap.ExecuteSafe(() => Drive.Chkdsk(mount.Letter.Value), true);
                Wrap.RetryExponential.ExecuteSafe(Drive.UnmountUEFINTFS, true);

                if (usb.UsbDeviceID != null)
                {
                    using (var handle =  Wrap.RetryExponential.Execute(() => Drive.GetHandle(@$"\\.\{mount.Letter.Value}:")))
                    {
                        if (!Win32.DeviceIoControl(handle, Win32.IoCtl.FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
                        {
                            if (Wrap.ExecuteSafe(() => USB.Eject(usb.UsbDeviceID), false) == null)
                                Log.WriteSafe(LogType.Info, "Successfully ejected USB.", null);
                        }
                        else
                            Win32.DeviceIoControl(handle, Win32.IoCtl.FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
                    }
                }
            }
            else
            {
                Wrap.Retry().Execute(() => ISO.DD_ISO(usb, isoPath, progress));
            }
            
            Drive.TryFlushDrive(usb.Index);
        }
    }
}
