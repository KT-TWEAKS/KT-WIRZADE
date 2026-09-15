using Interprocess;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq.Expressions;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using KTWirzade.Shared;
using static Core.Win32;


namespace KTWirzade.GUI.Utils
{
    public static class GUIUtil
    {
        public static BitmapImage GetIconResource(string dll, int resourceId, int width = 128, int height = 128)
        {
            IntPtr hLibrary = Resource.LoadLibrary(dll);
            if (hLibrary != IntPtr.Zero)
            {
                IntPtr hIcon = Resource.LoadImage(hLibrary, "#" + Math.Abs(resourceId), 1u, width, height, 32832u);
                if (hIcon != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource source = null;
                        using (Icon myIcon = Icon.FromHandle(hIcon))
                        {
                            source = Imaging.CreateBitmapSourceFromHIcon(myIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        }
                        BitmapImage image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        PngBitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(source));
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            encoder.Save(memoryStream);
                            image.StreamSource = new MemoryStream(memoryStream.ToArray());
                        }
                        image.EndInit();
                        return image;
                    }
                    finally
                    {
                        Resource.DestroyIcon(hIcon);
                        Resource.FreeLibrary(hLibrary);
                    }
                }
                Resource.FreeLibrary(hLibrary);
            }
            return null;
        }

        public static Task EnsureWMI()
        {
            return InterLink.ExecuteAsync((Expression<Action>)(() => EnsureWMIAdmin()), false, -1);
        }

        [InterprocessMethod(Level.Administrator)]
        private static void EnsureWMIAdmin()
        {
            WinUtil.ChangeStartMode(new ServiceController("Winmgmt"), ServiceStartMode.Automatic);
        }

        public static async Task<List<bool>> GetDefenderToggles()
        {
            List<bool> result = new List<bool>();

            await Task.Run(delegate
            {
                RegistryKey defenderKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows Defender");
                RegistryKey policyKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Policies\\Microsoft\\Windows Defender");

                // --- [0] Real-Time Protection ---
                try
                {
                    RegistryKey rtpKey = null;

                    if (policyKey != null)
                        rtpKey = policyKey.OpenSubKey("Real-Time Protection");

                    if (rtpKey == null && defenderKey != null)
                        rtpKey = defenderKey.OpenSubKey("Real-Time Protection");

                    if (rtpKey != null)
                    {
                        object val = rtpKey.GetValue("DisableRealtimeMonitoring");
                        result.Add(val == null || (int)val != 1);
                    }
                    else
                    {
                        result.Add(false);
                    }
                }
                catch
                {
                    result.Add(false);
                }

                // --- [1] SpyNet Reporting  [2] Sample Submission ---
                try
                {
                    RegistryKey spynetKey = null;

                    if (policyKey != null)
                        spynetKey = policyKey.OpenSubKey("SpyNet");

                    if (spynetKey == null && defenderKey != null)
                        spynetKey = defenderKey.OpenSubKey("SpyNet");

                    int spyNetReporting = 0;
                    int submitSamplesConsent = 0;

                    if (spynetKey != null)
                    {
                        object reportingVal = spynetKey.GetValue("SpyNetReporting");
                        if (reportingVal != null)
                            spyNetReporting = (int)reportingVal;

                        object samplesVal = spynetKey.GetValue("SubmitSamplesConsent");
                        if (samplesVal != null)
                            submitSamplesConsent = (int)samplesVal;
                    }

                    result.Add(spyNetReporting != 0);
                    result.Add(submitSamplesConsent != 0 && submitSamplesConsent != 2 && submitSamplesConsent != 4);
                }
                catch
                {
                    result.Add(false);
                    result.Add(false);
                }

                // --- [3] Tamper Protection ---
                try
                {
                    RegistryKey featuresKey = defenderKey?.OpenSubKey("Features");
                    if (featuresKey != null)
                    {
                        object val = featuresKey.GetValue("TamperProtection");
                        if (val != null)
                        {
                            int tamper = (int)val;
                            result.Add(tamper != 4 && tamper != 0);
                        }
                        else
                        {
                            result.Add(false);
                        }
                    }
                    else
                    {
                        result.Add(false);
                    }
                }
                catch
                {
                    result.Add(false);
                }
            });

            return result;
        }
    }
}
