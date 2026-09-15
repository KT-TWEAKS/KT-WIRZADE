using Core;
using Interprocess;
using iso_mode;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.UsbWriteDialog;
using KTWirzade.GUI.Utils;
using static Core.Log;
using static Core.Win32;
using static Core.Wrap;
using static Interprocess.InterLink;
using static iso_mode.USB;
using static iso_mode.USB.NotificationContext;

namespace KTWirzade.GUI.Windows
{
    public partial class UsbWriteDialog : AcrylicWindow
    {
        private List<UsbDisk> _usbDisks = new List<UsbDisk>();

        private bool? _tpm;

        private bool? _cpuRam;

        private bool? _internet;

        private bool? _bitlocker;

        private int _cachedTaskbarProgress;

        private static NotificationContext _usbNotifier;

        public UsbWriteDialog(List<UsbDisk> list, bool? tpm, bool? cpuRam, bool? internet, bool? bitlocker)
        {
            InitializeComponent();
            MainText.Text = ((list.Count <= 1) ? "Creating a bootable flash drive from the specified ISO file." : "Creating bootable flash drives from the specified ISO file.");
            _usbDisks = list;
            _tpm = tpm;
            _cpuRam = cpuRam;
            _internet = internet;
            _bitlocker = bitlocker;
            base.Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (GlobalsGUI.Current.ISO.Name.Contains("Ubuntu"))
            {
                UsbImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "Ubuntu_SVG");
            }
            else if (GlobalsGUI.Current.ISO.Name.Contains("Windows"))
            {
                UsbImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "Windows11_SVG");
            }
            else if (GlobalsGUI.Current.ISO.Name.Contains("Steam"))
            {
                UsbImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "Steam_SVG");
            }
            else if (GlobalsGUI.Current.ISO.Username == "KTWirzade")
            {
                UsbImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "AME_SVG");
            }
            if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
            {
                RootWindow.SetResourceReference(BackgroundProperty, "FakeBackgroundBrush");
                PageContainer.SetResourceReference(BackgroundProperty, "FakePageBackgroundBrush");
            }
            TaskBar.TaskbarNotifier taskbarProgress = Wrap.ExecuteSafe<TaskBar.TaskbarNotifier>((Func<TaskBar.TaskbarNotifier>)(() => new TaskBar.TaskbarNotifier()), true, (LogOptions)null).Value;
            Wrap.ExecuteSafe((Action)delegate
            {
                taskbarProgress?.SetProgressValue(this, 0);
            }, true, (LogOptions)null);
            foreach (UsbDisk usbDisk in _usbDisks)
            {
                UsbProgressItem usbItem = new UsbProgressItem();
                if (usbDisk != _usbDisks.First())
                {
                    usbItem.Margin = new Thickness(0.0, 30.0, 0.0, 0.0);
                }
                usbItem.UsbDisk = usbDisk;
                usbItem.Loaded += delegate
                {
                    //IL_0065: Unknown result type (might be due to invalid IL or missing references)
                    //IL_006f: Expected O, but got Unknown
                    usbItem.Active = true;
                    string brand = usbDisk.FriendlyName.Replace("USB", "").Trim().Split(' ')
                        .First();
                    usbItem.Progress = new InterProgress((Action<decimal>)delegate (decimal progress)
                    {
                        usbItem.ProgressBar.Value = (double)progress;
                        usbItem.StatusText.Text = "Writing to " + brand + "...";
                        Wrap.ExecuteSafe((Action)delegate
                        {
                            double num = 0.0;
                            foreach (UsbProgressItem current in UsbWriteStack.Children.OfType<UsbProgressItem>())
                            {
                                num += current.ProgressBar.Value;
                            }
                            num /= (double)UsbWriteStack.Children.Count;
                            int num2 = (int)Math.Round(num);
                            if (num2 != _cachedTaskbarProgress)
                            {
                                _cachedTaskbarProgress = num2;
                                taskbarProgress?.SetProgressValue(this, num2);
                            }
                        }, true, (LogOptions)null);
                    });
                    string text = default(string);
                    switch (GlobalsGUI.Current.ISO.Architecture ?? ImageParsers.ImageArchitecture.x64)
                    {
                        case ImageParsers.ImageArchitecture.x86:
                            text = "x86";
                            break;
                        case ImageParsers.ImageArchitecture.x64:
                            text = "amd64";
                            break;
                        case ImageParsers.ImageArchitecture.Arm32:
                            text = "arm";
                            break;
                        case ImageParsers.ImageArchitecture.Arm64:
                            text = "arm64";
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                    string archString = text;
                    string label = GlobalsGUI.Current.ISO.Name + ((!string.IsNullOrWhiteSpace(GlobalsGUI.Current.ISO.Version)) ? (" " + (GlobalsGUI.Current.ISO.Version.Contains('.') ? ("v" + GlobalsGUI.Current.ISO.Version.TrimStart('v')) : GlobalsGUI.Current.ISO.Version)) : "");
                    Log.WriteSafe((LogType)0, "UsbWriteDialog OnLoaded Begin WriteISO for " + usbDisk.FriendlyName, (SerializableTrace)null, Array.Empty<(string, object)>());
                    usbItem.WriteTask = InterLink.ExecuteSafeAsync<Task>((Expression<Func<Task>>)(() => InterMethods.WriteISO(archString, GlobalsGUI.Current.ISO.IsWindows, usbDisk.UsbDeviceID, usbDisk.Index, GlobalsGUI.Current.ISO.FilePath, label, _tpm, _cpuRam, _internet, _bitlocker, usbItem.Progress)), true, -1).ContinueWith(delegate (Task<SafeResult<Task>> task)
                    {
                        Log.WriteSafe((LogType)0, "UsbWriteDialog OnLoaded Finished WriteISO for " + usbDisk.FriendlyName, (SerializableTrace)null, Array.Empty<(string, object)>());
                        System.Windows.Application.Current.Dispatcher.Invoke(delegate
                        {
                            if (task.Result.Failed)
                            {
                                Log.WriteExceptionSafe(task.Result.Exception, Array.Empty<(string, object)>());
                                usbItem.Failed = true;
                                usbItem.StatusText.Text = brand + " failed to complete";
                                usbItem.FinishText.Visibility = Visibility.Visible;
                                usbItem.FinishText.Text = "This flash drive may be defective";
                                usbItem.ProgressBar.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                usbItem.Completed = true;
                                usbItem.StatusText.Text = brand + " written successfully";
                                usbItem.FinishText.Visibility = Visibility.Visible;
                                usbItem.FinishText.Text = "You can now remove this device";
                                usbItem.ProgressBar.Visibility = Visibility.Collapsed;
                            }
                            if (UsbWriteStack.Children.OfType<UsbProgressItem>().All((UsbProgressItem x) => x.Completed || x.Failed))
                            {
                                Wrap.ExecuteSafe((Action)delegate
                                {
                                    if (!base.IsActive)
                                    {
                                        if ((int?)Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PushNotifications")?.GetValue("ToastEnabled") == 0)
                                        {
                                            Activate();
                                        }
                                        else if (Wrap.ExecuteSafe((Action)delegate
                                        {
                                            TaskBar.ShowNotification(UsbWriteStack.Children.OfType<UsbProgressItem>().Any((UsbProgressItem x) => x.Failed) ? "ISO writing has completed with errors" : "ISO writing has completed");
                                        }, false, (LogOptions)null) != null)
                                        {
                                            Activate();
                                        }
                                    }
                                    taskbarProgress?.SetProgressNone(this);
                                    taskbarProgress?.Dispose();
                                }, true, (LogOptions)null);
                            }
                        });
                        usbItem.Progress.Dispose();
                    });
                };
                UsbWriteStack.Children.Add(usbItem);
            }
            _usbNotifier = new NotificationContext();
            _usbNotifier.Register(new CM_NOTIFY_CALLBACK(NotificationReceived));
            // Poll until the window is closed - a plain while(true) kept spinning forever
            // after the dialog was dismissed and dereferenced WriteTask before the
            // children were added (NRE in async void would crash the app).
            while (IsLoaded)
            {
                await Task.Delay(500);
                var items = UsbWriteStack.Children.OfType<UsbProgressItem>().ToList();
                if (items.All((UsbProgressItem x) => x.WriteTask?.IsCompleted != false))
                {
                    CloseButton.IsEnabled = true;
                }
            }
        }

        public void ShowDialog(Window owner)
        {
            base.Owner = owner;
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
            _usbNotifier.Unregister();
            CloseWindow(preparescale);
        }

        private int NotificationReceived(IntPtr hNotify, IntPtr Context, CM_NOTIFY_ACTION Action, IntPtr EventDataPtr, int EventDataSize)
        {
            try
            {
                if ((int)Action != 1 && (int)Action != 0)
                {
                    return 0;
                }
                Marshal.PtrToStructure<CM_NOTIFY_EVENT_DATA>(EventDataPtr);
                IntPtr offsetOfMoreInfo = EventDataPtr + Marshal.SizeOf<CM_NOTIFY_EVENT_DATA>();
                string name = Marshal.PtrToStringAuto(offsetOfMoreInfo);
                if (name == null)
                {
                    return 0;
                }
                if (_usbDisks.Any((UsbDisk x) => x.UsbDeviceID == null))
                {
                    SafeResult<List<UsbDisk>> usbDevices = InterLink.ExecuteSafe<List<UsbDisk>>((Expression<Func<List<UsbDisk>>>)(() => USB.GetDevices(true, false)), false, -1);
                    if (!usbDevices.Failed)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(delegate
                        {
                            foreach (UsbProgressItem usbItem in UsbWriteStack.Children.OfType<UsbProgressItem>())
                            {
                                if (usbItem.UsbDisk.UsbDeviceID == null && !usbDevices.Value.Any((UsbDisk x) => x.Index == usbItem.UsbDisk.Index && x.FriendlyName == usbItem.UsbDisk.FriendlyName))
                                {
                                    bool flag = true;
                                    usbItem.Active = !flag;
                                    if (!usbItem.Failed)
                                    {
                                        usbItem.FinishText.Text = (flag ? "Flash drive removed" : "You can now remove this device");
                                    }
                                }
                            }
                        });
                    }
                }
                bool removed = (int)Action == 1;
                System.Windows.Application.Current.Dispatcher.Invoke(delegate
                {
                    foreach (UsbProgressItem current in UsbWriteStack.Children.OfType<UsbProgressItem>())
                    {
                        if (current.UsbDisk.UsbDeviceID != null && name.StartsWith(current.UsbDisk.UsbDeviceID.Replace('\\', '#'), StringComparison.OrdinalIgnoreCase))
                        {
                            current.Active = !removed;
                            if (!current.Failed)
                            {
                                current.FinishText.Text = (removed ? "Flash drive removed" : "You can now remove this device");
                            }
                        }
                    }
                });
            }
            catch (Exception value)
            {
                Console.WriteLine(value);
            }
            return 0;
        }
    }
}

