using Core;
using Core.Actions;
using Interprocess;
using System.ComponentModel;
using System.Diagnostics;
using static Core.Output;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.Utils
{
    internal static class WUAStopper
    {
        internal static bool IsRunning()
        {
            return Worker != null && Worker.IsBusy;
        }

        [InterprocessMethod(Level.Administrator)]
        internal static void Cancel()
        {
            if (!IsRunning())
            {
                return;
            }
            if (!Worker.CancellationPending)
            {
                Worker.CancelAsync();
            }
        }

        [InterprocessMethod(Level.Administrator)]
        internal static void Initialize()
        {
            if (IsRunning())
            {
                return;
            }
            Worker = new BackgroundWorker();
            Worker.WorkerSupportsCancellation = true;
            Worker.DoWork += DoWork;
            Worker.RunWorkerAsync();
        }

        private static void DoWork(object sender, DoWorkEventArgs e)
        {
            Wrap.ExecuteSafe(delegate ()
            {
                ServiceAction WaasMedicSvc = new ServiceAction
                {
                    ServiceName = "WaaSMedicSvc",
                    Operation = 0
                };
                ServiceAction wuauserv = new ServiceAction
                {
                    ServiceName = "wuauserv",
                    Operation = 0
                };
                ServiceAction UsoSvc = new ServiceAction
                {
                    ServiceName = "UsoSvc",
                    Operation = 0
                };
                ServiceAction BITS = new ServiceAction
                {
                    ServiceName = "BITS",
                    Operation = 0
                };
                bool firstLoop = true;
                while (!((BackgroundWorker)sender).CancellationPending)
                {
                    CoreActions.SafeRun(WaasMedicSvc, false);
                    CoreActions.SafeRun(wuauserv, false);
                    CoreActions.SafeRun(UsoSvc, false);
                    CoreActions.SafeRun(BITS, false);
                    Wrap.ExecuteSafe(delegate ()
                    {
                        Process process = Process.GetProcessesByName("MoNotificationUx").FirstOrDefault();
                        if (process == null)
                        {
                            return;
                        }
                        process.Kill();
                    }, false, null);
                    if (firstLoop)
                    {
                        try
                        {
                            new Shared.Actions.FileAction
                            {
                                RawPath = Environment.ExpandEnvironmentVariables("%WINDIR%\\SoftwareDistribution\\Download")
                            }.RunTask(OutputWriter.Null).Wait();
                        }
                        catch
                        {
                        }
                    }
                    Thread.Sleep(5000);
                    firstLoop = false;
                }
                e.Cancel = true;
            }, true, null);
        }

        private static BackgroundWorker Worker;
    }
}
