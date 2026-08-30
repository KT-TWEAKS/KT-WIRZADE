using Core;
using Core.Actions;
using Microsoft.Win32.TaskScheduler;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using System.Windows.Input;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.Utils;
using KTWirzade.Shared;
using static Core.Win32;
using static Interprocess.InterLink;
using static KTWirzade.Shared.Requirements;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace KTWirzade.GUI.Windows
{
    public partial class PrepareDialog : AcrylicWindow
    {
        private bool DefenderRemnantsOnly;

        private bool KernelDriverOnly;

        private Requirement[] MetRequirements;

        public PrepareDialog()
        {
            InitializeComponent();
            if ((GlobalsGUI.Current.Playbook).Username == "KTWirzade" && GlobalsGUI.Current.Playbook.VerificationStatus == PlaybookGUI.VerificationLevel.Verified)
            {
                EnsureText.Text = "This ensures that your installation is in the proper condition to be ameliorated. It includes disabling certain services and components. This process may take a few minutes.";
            }
            else
            {
                EnsureText.Text = "This ensures that your installation meets the proper conditions to use this Playbook. It includes disabling certain services and components. This process may take a few minutes.";
            }
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
            {
                RootWindow.SetResourceReference(BackgroundProperty, "FakeBackgroundBrush");
                PageContainer.SetResourceReference(BackgroundProperty, "FakePageBackgroundBrush");
            }
            ProgressBar.Maximum = 100.0;
            TaskBar.TaskbarNotifier taskbarProgress = Wrap.ExecuteSafe((() => new TaskBar.TaskbarNotifier()), true, null).Value;
            try
            {
                Wrap.ExecuteSafe(delegate
                {
                    taskbarProgress?.SetProgressValue(this, 0);
                }, true, null);
                InterProgress progress = new InterProgress(delegate (decimal value)
                {
                    Wrap.ExecuteSafe(delegate
                    {
                        taskbarProgress?.SetProgressValue(this, (int)Math.Round(value) + 1);
                    }, false, null);
                    ProgressBar.Value = (double)value;
                });
                try
                {
                    InterMessageReporter messageReporter = new InterMessageReporter(delegate (string message)
                    {
                        StatusText.Text = message;
                    });
                    try
                    {
                        System.Threading.Tasks.Task workTask = (KernelDriverOnly ? ExecuteAsync((Expression<System.Action>)(() => Defender.DisableBlocklist(progress, messageReporter, !MetRequirements.Contains((Requirement)13))), false, -1) :
                            (MetRequirements.Contains((Requirement)2) ? ExecuteAsync((Expression<System.Action>)(() => Defender.DisableUCPD(progress)), false, -1) :
                            ExecuteAsync((Expression<Func<bool>>)(() => Defender.KillAndDisable(progress, messageReporter, WizardConfig.Current.LiveServicePackageApplied.Get(), false)), false, -1)));
                        if (KernelDriverOnly || MetRequirements.Contains((Requirement)2))
                        {
                            await workTask;
                        }
                        else
                        {
                            WizardConfig.ConfigObject<bool> liveServicePackageApplied = WizardConfig.Current.LiveServicePackageApplied;
                            liveServicePackageApplied.Set(await (Task<bool>)workTask);
                        }
                        await SafeTask.Run(delegate
                        {
                            TaskDefinition val = TaskService.Instance.NewTask();
                            val.Principal.LogonType = (TaskLogonType)3;
                            val.Triggers.Add(new LogonTrigger
                            {
                                UserId = WindowsIdentity.GetCurrent().Name
                            });
                            val.Actions.Add(new ExecAction(Assembly.GetExecutingAssembly().Location, null, null));
                            val.Actions.Add(new ExecAction("SCHTASKS", "/delete /tn \"KTWirzade\" /f", null));
                            val.Settings.DisallowStartIfOnBatteries = false;
                            val.Settings.StopIfGoingOnBatteries = false;
                            val.Settings.AllowHardTerminate = false;
                            val.Settings.ExecutionTimeLimit = TimeSpan.Zero;
                            TaskService.Instance.RootFolder.RegisterTaskDefinition("KTWirzade", val);
                        }, true, null);
                        CloseButton.IsEnabled = true;
                        Wrap.ExecuteSafe(delegate
                        {
                            taskbarProgress?.SetProgressNone(this);
                        }, true, null);
                        StatusText.Text = ((WizardConfig.Current.LiveServicePackageApplied.Get() || MetRequirements.Contains((Requirement)2)) ? "System preparation complete" : "Process will continue in Safe Mode");
                        ProgressBar.Visibility = Visibility.Collapsed;
                        FinishText.Visibility = Visibility.Visible;
                        CheckImage.Visibility = ((!WizardConfig.Current.LiveServicePackageApplied.Get() && !MetRequirements.Contains((Requirement)2)) ? Visibility.Hidden : Visibility.Visible);
                        RestartImage.Visibility = ((WizardConfig.Current.LiveServicePackageApplied.Get() || MetRequirements.Contains((Requirement)2)) ? Visibility.Hidden : Visibility.Visible);
                        if (DefenderRemnantsOnly)
                        {
                            FinishText.Text = "This window can now be closed";
                            return;
                        }
                        if (!CheckBox.IsChecked.Value)
                        {
                            RestartTextCheck.Opacity = 0.5;
                            CheckBox.Opacity = 0.5;
                            CheckBox.IsEnabled = false;
                            FinishText.Text = "Restart at the soonest possible time";
                            return;
                        }
                        int seconds = 0;
                        while (seconds <= 10)
                        {
                            FinishText.Text = "Windows will restart in " + (10 - seconds) + " seconds";
                            seconds++;
                            await System.Threading.Tasks.Task.Delay(1000);
                            if (!CheckBox.IsChecked.Value)
                            {
                                RestartTextCheck.Opacity = 0.5;
                                CheckBox.Opacity = 0.5;
                                CheckBox.IsEnabled = false;
                                FinishText.Text = "Restart at the soonest possible time";
                                return;
                            }
                        }
                        await System.Threading.Tasks.Task.Delay(100);
                        CoreActions.SafeRun(new CmdAction
                        {
                            Command = "timeout /t 1 & shutdown /r /t 0",
                            Wait = false
                        }, false);
                        CloseWindow(preparescale);
                    }
                    finally
                    {
                        if (messageReporter != null)
                        {
                            ((IDisposable)messageReporter).Dispose();
                        }
                    }
                }
                finally
                {
                    if (progress != null)
                    {
                        ((IDisposable)progress).Dispose();
                    }
                }
            }
            finally
            {
                if (taskbarProgress != null)
                {
                    ((IDisposable)taskbarProgress).Dispose();
                }
            }
        }

        public void ShowDialog(Window owner, Requirement[] metRequirements, bool remnantsOnly, bool driverOnly)
        {
            Owner = owner;
            DefenderRemnantsOnly = remnantsOnly;
            KernelDriverOnly = driverOnly;
            MetRequirements = metRequirements;
            if (DefenderRemnantsOnly)
            {
                RestartGrid.Visibility = Visibility.Hidden;
            }
            ShowDialog();
        }

        public void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWindow(preparescale);
        }
    }
}
