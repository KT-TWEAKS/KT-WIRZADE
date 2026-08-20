using Core;
using Core.Actions;
using Interprocess;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KTWirzade.GUI.TweakDialog.TweakModules;

using KTWirzade.GUI.Utils;
using KTWirzade.GUI.ViewModels;
using KTWirzade.GUI.Windows;
using KTWirzade.Shared;
using WmiLight;
using static Core.Win32;
using static KTWirzade.Shared.Playbook;
using static KTWirzade.Shared.Requirements;

namespace KTWirzade.GUI.Views
{
    public partial class RequirementsPageView : System.Windows.Controls.UserControl
    {
        private enum IconType
        {
            Default,
            Checkmark,
            Warning
        }

        public class UninstallKey
        {
            public string DisplayName;

            public string SecurityName;

            public string InstallLocation;

            public string UninstallString;

            public string RootKey;

            public bool Remnant;

            public bool HKCU;
        }

        private static readonly SolidColorBrush SuccessBrush = new SolidColorBrush(new System.Windows.Media.Color
        {
            A = byte.MaxValue,
            R = 22,
            G = 124,
            B = 50
        });

        private static readonly SolidColorBrush ErrorBrush = new SolidColorBrush(new System.Windows.Media.Color
        {
            A = byte.MaxValue,
            R = 217,
            G = 43,
            B = 54
        });

        private static readonly SolidColorBrush WarningBrushDark = new SolidColorBrush(new System.Windows.Media.Color
        {
            A = byte.MaxValue,
            R = 109,
            G = 170,
            B = 240
        });

        private static readonly SolidColorBrush WarningBrushLight = new SolidColorBrush(new System.Windows.Media.Color
        {
            A = byte.MaxValue,
            R = 0,
            G = 91,
            B = 198
        });

        private bool KernelDriverMet = true;

        private bool KernelDriverOnly;

        private static bool? _activationRequirementMet = null;

        private static bool? _buildRequirementMet = null;

        private string WindowsProductName = "Windows";

        private int TickCount;

        private bool activationChecked;

        private bool pastFirstRequirementsTick;

        private static bool _pendingPendingUpdatesHelpMessage = false;

        private bool _uninstallUpdatesIsMet;

        private static List<WindowsUpdate> _badWindowsUpdates = null;

        private static int _pendingUpdatesCheckCount;

        private RequirementsPageViewModel ViewModel => (RequirementsPageViewModel)base.DataContext;

        private IconType CurrentActivationIcon
        {
            set
            {
                switch (value)
                {
                    case IconType.Default:
                        ViewModel.ActivationIcon = null;
                        break;
                    case IconType.Checkmark:
                        ViewModel.ActivationIcon = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/checkmark_green_gradient_64.png"));
                        break;
                    case IconType.Warning:
                        ViewModel.ActivationIcon = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/warning_circle_yellow_gradient_64.png"));
                        break;
                }
            }
        }

        private IconType CurrentSystemCheckIcon
        {
            set
            {
                switch (value)
                {
                    case IconType.Default:
                        ViewModel.SystemCheckIcon = null;
                        break;
                    case IconType.Checkmark:
                        ViewModel.SystemCheckIcon = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/checkmark_green_gradient_64.png"));
                        break;
                    case IconType.Warning:
                        ViewModel.SystemCheckIcon = new BitmapImage(new Uri("pack://application:,,,/KTWirzade.GUI;component/Icons/warning_circle_yellow_gradient_64.png"));
                        break;
                }
            }
        }

        private bool AreRequirementsMet()
        {
            if (ViewModel.MetRequirements == null || !ViewModel.IsBuildSupported.HasValue)
            {
                throw new Exception("AreRequirementsMet was called before requirements were checked.");
            }
            if (!ViewModel.IsBuildSupported.Value)
            {
                return false;
            }
            if (!KernelDriverMet)
            {
                return false;
            }
            if (_activationRequirementMet == false)
            {
                return false;
            }
            if (!_uninstallUpdatesIsMet)
            {
                return false;
            }
            Requirement[] requirements = ((Playbook)GlobalsGUI.Current.Playbook).Requirements;
            if (requirements == null)
            {
                return true;
            }
            return !requirements.Except(ViewModel.MetRequirements).Any();
        }

        public RequirementsPageView()
        {
            InitializeComponent();
            base.DataContextChanged += delegate
            {
                if (base.DataContext != null)
                {
                    ViewModel.MainNextButtonActive = false;
                    bool flag = ((Playbook)GlobalsGUI.Current.Playbook).Requirements != null && ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)5);
                    ActivationContainer.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
                    SystemCheckContainer.Opacity = (flag ? 0.6 : 1.0);
                    PendingUpdatesActionText.Text = "Run action";
                    _pendingUpdatesCheckCount = 0;
                    if (ViewModel.MetRequirements != null && ViewModel.IsBuildSupported.HasValue)
                    {
                        SystemCheckContainer.Opacity = 1.0;
                        UpdateActivationDisplay().Wait();
                        UpdateSystemCheckDisplay().Wait();
                        ActivationBar.Visibility = Visibility.Collapsed;
                        SystemBar.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ActivationBar.Visibility = Visibility.Visible;
                        SystemBar.Visibility = Visibility.Visible;
                        base.Loaded += OnLoaded;
                        if (flag)
                        {
                            SystemBar.Visibility = Visibility.Hidden;
                        }
                        else
                        {
                            SystemBar.Visibility = Visibility.Visible;
                        }
                        ViewModel.ProgressBarVisibility = Visibility.Visible;
                        DispatcherTimer dispatcherTimer = new DispatcherTimer();
                        dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, new Random().Next(1500, 2500));
                        dispatcherTimer.Tick += async delegate (object sender, EventArgs args)
                        {
                            await dispatcherTimer_Tick(sender, args);
                        };
                        dispatcherTimer.Start();
                        base.Loaded += CheckRequirements;
                    }
                }
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (((Playbook)GlobalsGUI.Current.Playbook).Requirements != null && ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)5))
            {
                ActivationBar.Start();
            }
            else
            {
                SystemBar.Start();
            }
            base.Loaded -= OnLoaded;
        }

        private async void CheckRequirements(object sender, RoutedEventArgs e)
        {
            Requirement[] requirements = ((Playbook)GlobalsGUI.Current.Playbook).Requirements;
            string[] builds = ((Playbook)GlobalsGUI.Current.Playbook).SupportedBuilds;
            try
            {
                ViewModel.IsBuildSupported = builds?.Contains(SystemInfoEx.WindowsVersion.BuildNumber.ToString()) ?? true;
                _buildRequirementMet = ViewModel.IsBuildSupported;
                RequirementsPageViewModel viewModel = ViewModel;
                Requirement[] metRequirements = ((requirements == null) ? Enum.GetValues(typeof(Requirement)).Cast<Requirement>().ToArray() : (await Task.Run(delegate
                {
                    WindowsProductName = $"Windows {SystemInfoEx.WindowsVersion.MajorVersion} {SystemInfoEx.WindowsVersion.Edition}";
                    if (requirements.Contains((Requirement)5))
                    {
                        Task<bool> task = new Activation().IsMet();
                        task.Wait();
                        _activationRequirementMet = task.Result;
                    }
                    else
                    {
                        _activationRequirementMet = true;
                    }
                    List<Requirement> list = new List<Requirement>(requirements);
                    if (list.Contains((Requirement)10))
                    {
                        TweaksDialog.Tweaks = new List<TweaksDialog.Tweak>();
                        try
                        {
                            List<UninstallKey> uninstallKeys = GetUninstallKeys();
                            UninstallKey uninstallKey = null;
                            UninstallKey uninstallKey2 = null;
                            UninstallKey uninstallKey3 = null;
                            UninstallKey uninstallKey4 = null;
                            try
                            {
                                uninstallKey = uninstallKeys.FirstOrDefault((UninstallKey x) => x.RootKey.ToLower().Contains("\\start11") || (x.DisplayName != null && x.DisplayName.ToLower().Contains("start11")));
                                uninstallKey2 = uninstallKeys.FirstOrDefault((UninstallKey x) => x.RootKey.ToLower().Contains("\\startallback") || (x.DisplayName != null && x.DisplayName.ToLower().Contains("startallback")));
                                uninstallKey3 = uninstallKeys.FirstOrDefault((UninstallKey x) => x.RootKey.ToLower().Contains("\\rectify11") || (x.DisplayName != null && x.DisplayName.ToLower().Contains("rectify11")));
                                uninstallKey4 = uninstallKeys.FirstOrDefault((UninstallKey x) => x.RootKey.ToLower().Contains("\\ccleaner") || (x.DisplayName != null && x.DisplayName.ToLower().Contains("ccleaner")));
                            }
                            catch (Exception)
                            {
                            }
                            if (uninstallKey != null)
                            {
                                Start11.UninstallKey = uninstallKey;
                                TweaksDialog.Tweaks.Add(TweaksDialog.Tweak.Start11);
                            }
                            if (uninstallKey2 != null)
                            {
                                StartAllBack.UninstallKey = uninstallKey2;
                                TweaksDialog.Tweaks.Add(TweaksDialog.Tweak.StartAllBack);
                            }
                            if (uninstallKey3 != null)
                            {
                                Rectify11.UninstallKey = uninstallKey3;
                                TweaksDialog.Tweaks.Add(TweaksDialog.Tweak.Rectify11);
                            }
                            if (uninstallKey4 != null)
                            {
                                CCleaner.UninstallKey = uninstallKey4;
                                TweaksDialog.Tweaks.Add(TweaksDialog.Tweak.CCleaner);
                            }
                            if (Shutup10.IsPresent())
                            {
                                string text = InterLink.Execute<string>((Expression<Func<string>>)(() => Shutup10.GetExePath()), false, -1);
                                Shutup10.FileLocation = ((text == "null") ? null : text);
                                TweaksDialog.Tweaks.Add(TweaksDialog.Tweak.Shutup10);
                            }
                            if (!TweaksDialog.Tweaks.Any())
                            {
                                list.Remove((Requirement)10);
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                    if (((Playbook)GlobalsGUI.Current.Playbook).UseKernelDriver.HasValue && ((Playbook)GlobalsGUI.Current.Playbook).UseKernelDriver.Value)
                    {
                        KernelDriverMet = (int)new RegistryValueAction
                        {
                            KeyName = "HKLM\\SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity",
                            Value = "Enabled",
                            Data = 1
                        }.GetStatus() != 0 && (int)new RegistryValueAction
                        {
                            KeyName = "HKLM\\SYSTEM\\CurrentControlSet\\Control\\CI\\Config",
                            Value = "VulnerableDriverBlocklistEnable",
                            Data = 0
                        }.GetStatus() == 0;
                    }
                    if (GlobalsGUI.Current.Playbook.LastAppliedMatch(GlobalsGUI.Current.AppliedPlaybooks) != null)
                    {
                        list.Remove((Requirement)12);
                    }
                    _uninstallUpdatesIsMet = UninstallUpdatesIsMet();
                    return Requirements.MetRequirements(list.Where((Requirement x) => (int)x != 3).ToArray(), !GlobalsGUI.WUAStopperEngaged && !list.Contains((Requirement)10) && _activationRequirementMet.Value && _buildRequirementMet.Value);
                })));
                viewModel.MetRequirements = metRequirements;
            }
            catch (NullReferenceException)
            {
            }
            base.Loaded -= CheckRequirements;
        }

        private List<UninstallKey> GetUninstallKeys()
        {
            List<UninstallKey> UninstallKeys = new List<UninstallKey>();
            try
            {
                try
                {
                    RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
                    if (key != null)
                    {
                        string[] subKeyNames = key.GetSubKeyNames();
                        foreach (string subKey in subKeyNames)
                        {
                            UninstallKeys.Add(new UninstallKey
                            {
                                RootKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + subKey
                            });
                        }
                        key.Close();
                    }
                }
                catch (Exception)
                {
                }
                try
                {
                    RegistryKey key2 = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
                    if (key2 != null)
                    {
                        string[] subKeyNames = key2.GetSubKeyNames();
                        foreach (string subKey2 in subKeyNames)
                        {
                            UninstallKeys.Add(new UninstallKey
                            {
                                RootKey = "SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + subKey2
                            });
                        }
                        key2.Close();
                    }
                }
                catch (Exception)
                {
                }
                try
                {
                    RegistryKey key3 = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
                    if (key3 != null)
                    {
                        string[] subKeyNames = key3.GetSubKeyNames();
                        foreach (string subKey3 in subKeyNames)
                        {
                            UninstallKeys.Add(new UninstallKey
                            {
                                RootKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + subKey3,
                                HKCU = true
                            });
                        }
                        key3.Close();
                    }
                }
                catch (Exception)
                {
                }
                try
                {
                    RegistryKey key4 = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
                    if (key4 != null)
                    {
                        string[] subKeyNames = key4.GetSubKeyNames();
                        foreach (string subKey4 in subKeyNames)
                        {
                            UninstallKeys.Add(new UninstallKey
                            {
                                RootKey = "SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + subKey4,
                                HKCU = true
                            });
                        }
                        key4.Close();
                    }
                }
                catch (Exception)
                {
                }
                foreach (UninstallKey uninstallKey in UninstallKeys)
                {
                    try
                    {
                        RegistryKey obj = (uninstallKey.HKCU ? Registry.CurrentUser.OpenSubKey(uninstallKey.RootKey) : Registry.LocalMachine.OpenSubKey(uninstallKey.RootKey));
                        object val = obj.GetValue("DisplayName");
                        uninstallKey.DisplayName = ((val == null) ? null : ((string)val));
                        val = obj.GetValue("UninstallString");
                        uninstallKey.UninstallString = ((val == null) ? null : ((string)val));
                        val = obj.GetValue("InstallLocation");
                        uninstallKey.InstallLocation = ((val == null) ? null : ((string)val));
                        obj.Close();
                    }
                    catch (Exception)
                    {
                    }
                }
                return UninstallKeys;
            }
            catch (Exception ex6)
            {
                Log.WriteExceptionSafe((LogType)1, ex6, "Could not fetch all install keys.", Array.Empty<(string, object)>());
                return UninstallKeys;
            }
        }

        private async Task UpdateActivationDisplay()
        {
            if (base.DataContext is ViewModelBase)
            {
                _ = ((Playbook)GlobalsGUI.Current.Playbook).Requirements;
                IconType activationIcon = IconType.Checkmark;
                string activationStatusText = "Windows is activated";
                string activationResultText = WindowsProductName;
                if (!_activationRequirementMet.HasValue || !_activationRequirementMet.Value)
                {
                    activationStatusText = "Windows is not activated";
                    activationResultText = "Please activate " + WindowsProductName;
                    activationIcon = IconType.Warning;
                }
                CurrentActivationIcon = activationIcon;
                ViewModel.ActivationStatusText = activationStatusText;
                ViewModel.ActivationResultText = activationResultText;
                ViewModel.ActivationResultTextVisibility = Visibility.Visible;
            }
        }

        private async Task UpdateSystemCheckDisplay()
        {
            if (!(base.DataContext is ViewModelBase))
            {
                return;
            }
            _ = ((Playbook)GlobalsGUI.Current.Playbook).Requirements;
            IconType systemCheckIcon = IconType.Checkmark;
            string systemCheckStatusText = "Requirements met";
            string systemCheckResultText = "The system meets all requirements for this Playbook";
            bool systemCheckLocked = false;
            if (!ViewModel.IsBuildSupported.Value)
            {
                systemCheckIcon = IconType.Warning;
                systemCheckStatusText = "Requirements not met";
                systemCheckResultText = "This Windows build is not supported by this Playbook.";
                systemCheckLocked = true;
            }
            if (!ViewModel.MetRequirements.Contains((Requirement)12) && !systemCheckLocked)
            {
                FreshInstallBox.Visibility = Visibility.Visible;
                systemCheckStatusText = "Requirements not met";
                systemCheckResultText = "Your Windows installation is older than 1.5 days old";
                systemCheckIcon = IconType.Warning;
                systemCheckLocked = true;
            }
            else
            {
                FreshInstallBox.Visibility = Visibility.Collapsed;
            }
            if (!ViewModel.MetRequirements.Contains((Requirement)10) && !systemCheckLocked)
            {
                TweaksBox.Visibility = Visibility.Visible;
                systemCheckStatusText = "Requirements not met";
                systemCheckResultText = "Tweaks must be disabled";
                systemCheckIcon = IconType.Warning;
                systemCheckLocked = true;
            }
            else
            {
                TweaksBox.Visibility = Visibility.Collapsed;
            }
            if (!ViewModel.MetRequirements.Contains((Requirement)9) && !systemCheckLocked)
            {
                CheckBatteryBox.Visibility = Visibility.Visible;
                systemCheckStatusText = "Requirements not met";
                systemCheckResultText = "Device must be plugged in before continuing";
                systemCheckIcon = IconType.Warning;
                systemCheckLocked = true;
            }
            else
            {
                CheckBatteryBox.Visibility = Visibility.Collapsed;
            }
            if (!ViewModel.MetRequirements.Contains((Requirement)4) && !systemCheckLocked)
            {
                PendingUpdatesBox.Visibility = Visibility.Visible;
                systemCheckStatusText = "Requirements not met";
                systemCheckResultText = "Pending system updates must be installed before continuing";
                systemCheckIcon = IconType.Warning;
                systemCheckLocked = true;
            }
            else
            {
                if (!systemCheckLocked && !GlobalsGUI.WUAStopperEngaged && ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)4))
                {
                    await InterLink.ExecuteSafeAsync((Expression<Action>)(() => WUAStopper.Initialize()), true, -1);
                    GlobalsGUI.WUAStopperEngaged = true;
                }
                PendingUpdatesBox.Visibility = Visibility.Collapsed;
            }
            if (!_uninstallUpdatesIsMet && !systemCheckLocked)
            {
                UninstallUpdatesBox.Visibility = Visibility.Visible;
                systemCheckStatusText = "Requirements not met";
                systemCheckResultText = "Faulty Windows updates need to be removed before continuing";
                systemCheckIcon = IconType.Warning;
                systemCheckLocked = true;
            }
            else
            {
                UninstallUpdatesBox.Visibility = Visibility.Collapsed;
            }
            if ((!ViewModel.MetRequirements.Contains((Requirement)2) || !ViewModel.MetRequirements.Contains((Requirement)13) || !KernelDriverMet) && !systemCheckLocked)
            {
                if (!KernelDriverMet && ViewModel.MetRequirements.Contains((Requirement)2))
                {
                    KernelDriverOnly = true;
                }
                PrepareBox.Visibility = Visibility.Visible;
                systemCheckStatusText = "Requirements not met";
                systemCheckResultText = "The system needs to be prepared before continuing";
                systemCheckIcon = IconType.Warning;
                systemCheckLocked = true;
            }
            else
            {
                PrepareBox.Visibility = Visibility.Collapsed;
            }
            if (!ViewModel.MetRequirements.Contains((Requirement)1) && !systemCheckLocked)
            {
                CheckNoInternetBox.Visibility = Visibility.Visible;
                systemCheckStatusText = "Requirements not met";
                systemCheckResultText = "Internet must be disconnected before continuing";
                systemCheckIcon = IconType.Warning;
                systemCheckLocked = true;
            }
            else
            {
                CheckNoInternetBox.Visibility = Visibility.Collapsed;
            }
            if (!ViewModel.MetRequirements.Contains((Requirement)0) && !systemCheckLocked)
            {
                CheckInternetBox.Visibility = Visibility.Visible;
                systemCheckStatusText = "Requirements not met";
                systemCheckResultText = "Internet must be connected before continuing";
                systemCheckIcon = IconType.Warning;
            }
            else
            {
                CheckInternetBox.Visibility = Visibility.Collapsed;
            }
            CurrentSystemCheckIcon = systemCheckIcon;
            ViewModel.SystemCheckStatusText = systemCheckStatusText;
            ViewModel.SystemCheckResultText = systemCheckResultText;
            ViewModel.ResultTextVisibility = Visibility.Visible;
            ViewModel.ProgressBarVisibility = Visibility.Collapsed;
            if (AreRequirementsMet())
            {
                ViewModel.MainNextButtonActive = true;
            }
            ViewModel.MainPreviousButtonActive = true;
        }

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(((Hyperlink)sender).NavigateUri.ToString());
        }

        private async Task dispatcherTimer_Tick(object sender, EventArgs e)
        {
            _ = 7;
            try
            {
                if (!_activationRequirementMet.HasValue)
                {
                    TickCount++;
                    return;
                }
                if ((ViewModel.MetRequirements == null || !ViewModel.IsBuildSupported.HasValue) && TickCount < 2)
                {
                    TickCount++;
                    return;
                }
                bool requiresActivation = ((Playbook)GlobalsGUI.Current.Playbook).Requirements != null && ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)5);
                bool noPendingUpdates = ((Playbook)GlobalsGUI.Current.Playbook).Requirements != null && ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)4);
                DispatcherTimer dispatchTimer = (DispatcherTimer)sender;
                if (!activationChecked && requiresActivation && dispatchTimer != null)
                {
                    activationChecked = true;
                    dispatchTimer.Stop();
                    await ActivationBar.WaitForAnimation();
                    await Task.Delay(100);
                    ActivationBar.Visibility = Visibility.Collapsed;
                    await UpdateActivationDisplay();
                    Storyboard fadeBoard = new Storyboard();
                    DoubleAnimation fadeAnim1 = new DoubleAnimation
                    {
                        Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200)),
                        To = 1.0
                    };
                    Storyboard.SetTarget(fadeAnim1, SystemCheckContainer);
                    Storyboard.SetTargetProperty(fadeAnim1, new PropertyPath("Opacity"));
                    fadeBoard.Children.Add(fadeAnim1);
                    BeginStoryboard(fadeBoard);
                    SystemBar.Visibility = Visibility.Visible;
                    SystemBar.Start();
                    dispatchTimer.Interval = new TimeSpan(0, 0, 0, 0, new Random().Next(1500, 2500));
                    dispatchTimer.Start();
                    return;
                }
                if (ViewModel.MetRequirements == null || !ViewModel.IsBuildSupported.HasValue)
                {
                    if (pastFirstRequirementsTick && noPendingUpdates)
                    {
                        ViewModel.SystemCheckStatusText = "Checking System Updates...";
                    }
                    TickCount++;
                    pastFirstRequirementsTick = true;
                    return;
                }
                dispatchTimer.Stop();
                await SystemBar.WaitForAnimation();
                await Task.Delay(100);
                SystemBar.Visibility = Visibility.Hidden;
                await UpdateSystemCheckDisplay();
                try
                {
                    ((System.Windows.Controls.Button)((RoutedEventArgs)e).Source).IsEnabled = true;
                }
                catch (InvalidCastException)
                {
                }
                if (((_pendingUpdatesCheckCount == 0 || _pendingUpdatesCheckCount % 3 != 0) && !_pendingPendingUpdatesHelpMessage) || ViewModel.MetRequirements.Contains((Requirement)4))
                {
                    return;
                }
                int checkCountCache = _pendingUpdatesCheckCount;
                await Task.Delay(500);
                if (checkCountCache != _pendingUpdatesCheckCount)
                {
                    _pendingPendingUpdatesHelpMessage = true;
                    return;
                }
                _pendingUpdatesCheckCount = 0;
                if (KTWirzade.GUI.MessageBox.Show(typeof(MainWindow), "Make sure you have restarted to apply any pending updates, even if you paused them.\r\n\r\nIf you are unable install an update, or none are available, select Bypass.", "Having trouble?", KTWirzade.GUI.MessageBoxButton.ImFineBypass) != KTWirzade.GUI.MessageBoxResult.Bypass)
                {
                    return;
                }
                ((Playbook)GlobalsGUI.Current.Playbook).Requirements = ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Where((Requirement x) => (int)x != 4).ToArray();
                PendingUpdatesBox.Visibility = Visibility.Collapsed;
                CheckInternetButton_OnClick(new System.Windows.Controls.Button(), new RoutedEventArgs());
                if (!GlobalsGUI.WUAStopperEngaged)
                {
                    await InterLink.ExecuteSafeAsync((Expression<Action>)(() => WUAStopper.Initialize()), true, -1);
                    GlobalsGUI.WUAStopperEngaged = true;
                }
            }
            catch (NullReferenceException)
            {
            }
        }

        private void activationAccept_Checked(object sender, RoutedEventArgs e)
        {
            ToggleButton button = (ToggleButton)sender;
            if (base.DataContext is ViewModelBase ViewModel)
            {
                ViewModel.MainNextButtonActive = button.IsChecked == true;
            }
        }

        private async void CheckInternetButton_OnClick(object sender, RoutedEventArgs e)
        {
            ((System.Windows.Controls.Button)sender).IsEnabled = false;
            ViewModel.ResultTextVisibility = Visibility.Collapsed;
            ViewModel.SystemCheckStatusText = "Analyzing Installation...";
            ViewModel.SystemCheckIcon = null;
            SystemBar.Visibility = Visibility.Visible;
            SystemBar.Start();
            ViewModel.MetRequirements = null;
            TickCount = 0;
            pastFirstRequirementsTick = false;
            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, new Random().Next(1500, 2500));
            dispatcherTimer.Tick += async delegate (object send, EventArgs args)
            {
                args = new RoutedEventArgs(null, sender);
                await dispatcherTimer_Tick(send, args);
            };
            dispatcherTimer.Start();
            Requirement[] requirements = ((Playbook)GlobalsGUI.Current.Playbook).Requirements;
            RequirementsPageViewModel viewModel = ViewModel;
            Requirement[] metRequirements = ((requirements == null) ? Enum.GetValues(typeof(Requirement)).Cast<Requirement>().ToArray() : (await Task.Run(delegate
            {
                List<Requirement> list = new List<Requirement>(requirements);
                if (list.Contains((Requirement)10))
                {
                    TweaksDialog.Tweaks = new List<TweaksDialog.Tweak>();
                    try
                    {
                        List<UninstallKey> uninstallKeys = GetUninstallKeys();
                        UninstallKey uninstallKey = null;
                        UninstallKey uninstallKey2 = null;
                        UninstallKey uninstallKey3 = null;
                        UninstallKey uninstallKey4 = null;
                        try
                        {
                            uninstallKey = uninstallKeys.FirstOrDefault((UninstallKey x) => x.RootKey.ToLower().Contains("\\start11") || (x.DisplayName != null && x.DisplayName.ToLower().Contains("start11")));
                            uninstallKey2 = uninstallKeys.FirstOrDefault((UninstallKey x) => x.RootKey.ToLower().Contains("\\startallback") || (x.DisplayName != null && x.DisplayName.ToLower().Contains("startallback")));
                            uninstallKey3 = uninstallKeys.FirstOrDefault((UninstallKey x) => x.RootKey.ToLower().Contains("\\rectify11") || (x.DisplayName != null && x.DisplayName.ToLower().Contains("rectify11")));
                            uninstallKey4 = uninstallKeys.FirstOrDefault((UninstallKey x) => x.RootKey.ToLower().Contains("\\ccleaner") || (x.DisplayName != null && x.DisplayName.ToLower().Contains("ccleaner")));
                        }
                        catch (Exception)
                        {
                        }
                        if (uninstallKey != null)
                        {
                            Start11.UninstallKey = uninstallKey;
                            TweaksDialog.Tweaks.Add(TweaksDialog.Tweak.Start11);
                        }
                        if (uninstallKey2 != null)
                        {
                            StartAllBack.UninstallKey = uninstallKey2;
                            TweaksDialog.Tweaks.Add(TweaksDialog.Tweak.StartAllBack);
                        }
                        if (uninstallKey3 != null)
                        {
                            Rectify11.UninstallKey = uninstallKey3;
                            TweaksDialog.Tweaks.Add(TweaksDialog.Tweak.Rectify11);
                        }
                        if (uninstallKey4 != null)
                        {
                            CCleaner.UninstallKey = uninstallKey4;
                            TweaksDialog.Tweaks.Add(TweaksDialog.Tweak.CCleaner);
                        }
                        if (Shutup10.IsPresent())
                        {
                            string text = InterLink.Execute<string>((Expression<Func<string>>)(() => Shutup10.GetExePath()), false, -1);
                            Shutup10.FileLocation = ((text == "null") ? null : text);
                            TweaksDialog.Tweaks.Add(TweaksDialog.Tweak.Shutup10);
                        }
                        if (!TweaksDialog.Tweaks.Any())
                        {
                            list.Remove((Requirement)10);
                        }
                    }
                    catch (Exception ex2)
                    {
                        list.Remove((Requirement)10);
                        Log.WriteExceptionSafe(ex2, Array.Empty<(string, object)>());
                    }
                    if (((Playbook)GlobalsGUI.Current.Playbook).UseKernelDriver.HasValue && ((Playbook)GlobalsGUI.Current.Playbook).UseKernelDriver.Value)
                    {
                        KernelDriverMet = (int)new RegistryValueAction
                        {
                            KeyName = "HKLM\\SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity",
                            Value = "Enabled",
                            Data = 1
                        }.GetStatus() != 0 && (int)new RegistryValueAction
                        {
                            KeyName = "HKLM\\SYSTEM\\CurrentControlSet\\Control\\CI\\Config",
                            Value = "VulnerableDriverBlocklistEnable",
                            Data = 0
                        }.GetStatus() == 0;
                    }
                }
                if (GlobalsGUI.Current.Playbook.LastAppliedMatch(GlobalsGUI.Current.AppliedPlaybooks) != null)
                {
                    list.Remove((Requirement)12);
                }
                _uninstallUpdatesIsMet = UninstallUpdatesIsMet();
                return Requirements.MetRequirements(list.Where((Requirement x) => (int)x != 3).ToArray(), !GlobalsGUI.WUAStopperEngaged && !list.Contains((Requirement)10) && _activationRequirementMet.Value && _buildRequirementMet.Value);
            })));
            viewModel.MetRequirements = metRequirements;
        }

        private bool UninstallUpdatesIsMet()
        {
            if (!File.Exists(Path.Combine(Environment.SystemDirectory, "wusa.exe")))
            {
                return true;
            }
            try
            {
                List<WindowsUpdate> uninstallWindowsUpdates = new List<WindowsUpdate>();
                if (((Playbook)GlobalsGUI.Current.Playbook).ExcludeBadWindowsUpdates)
                {
                    if (_badWindowsUpdates == null)
                    {
                        _badWindowsUpdates = new List<WindowsUpdate>();
                        try
                        {
                            HttpClient httpClient = new HttpClient();
                            try
                            {
                                List<WindowsUpdate> list = JsonSerializer.Deserialize<List<WindowsUpdate>>(httpClient.GetStringAsync("http://download.amelabs.net/bad_windows_updates.json").ConfigureAwait(continueOnCapturedContext: false).GetAwaiter()
                                    .GetResult(), (JsonSerializerOptions)null);
                                _badWindowsUpdates.AddRange(list);
                            }
                            finally
                            {
                                ((IDisposable)httpClient)?.Dispose();
                            }
                        }
                        catch (Exception)
                        {
                            _badWindowsUpdates = new List<WindowsUpdate>
                        {
                            new WindowsUpdate
                            {
                                KB = 5063878u,
                                Description = "This update can cause SSD failure and should be uninstalled."
                            },
                            new WindowsUpdate
                            {
                                KB = 5064081u,
                                Description = "This update supersedes an update that can cause SSD failure and should be uninstalled."
                            }
                        };
                        }
                    }
                    uninstallWindowsUpdates.AddRange(_badWindowsUpdates);
                }
                if (((Playbook)GlobalsGUI.Current.Playbook).ExcludedWindowsUpdates != null)
                {
                    uninstallWindowsUpdates.AddRange(((Playbook)GlobalsGUI.Current.Playbook).ExcludedWindowsUpdates);
                }
                WmiConnection connection = new WmiConnection();
                try
                {
                    foreach (WindowsUpdate excludedUpdate in uninstallWindowsUpdates)
                    {
                        UninstallUpdatesDialog.ExcludedUpdates.Add(new KeyValuePair<uint, string>(excludedUpdate.KB, excludedUpdate.Description));
                    }
                    UninstallUpdatesDialog.PresentUninstallUpdates.Clear();
                    foreach (WmiObject product in WmiConnectionExtensions.CreateQuery(connection, "SELECT * FROM Win32_QuickFixEngineering"))
                    {
                        try
                        {
                            string hotFixId = product.GetPropertyValue<string>("HotFixID").Replace("KB", "");
                            if (!string.IsNullOrWhiteSpace(hotFixId))
                            {
                                uint id = uint.Parse(hotFixId);
                                WindowsUpdate matching = uninstallWindowsUpdates.FirstOrDefault((WindowsUpdate x) => x.KB == id);
                                if (matching != null && !UninstallUpdatesDialog.BypassedUpdates.Contains(id))
                                {
                                    UninstallUpdatesDialog.PresentUninstallUpdates.Add(new KeyValuePair<uint, string>(matching.KB, matching.Description));
                                }
                            }
                        }
                        finally
                        {
                            product.Dispose();
                        }
                    }
                }
                finally
                {
                    ((IDisposable)connection)?.Dispose();
                }
                return !UninstallUpdatesDialog.PresentUninstallUpdates.Any();
            }
            catch (Exception ex2)
            {
                Log.WriteExceptionSafe(ex2, Array.Empty<(string, object)>());
            }
            return true;
        }

        private void PrepareButton_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow owner = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                new PrepareDialog().ShowDialog(owner, ViewModel.MetRequirements, remnantsOnly: false, KernelDriverOnly);
            });
            System.Windows.Application.Current.Shutdown();
        }

        private void CheckBatteryButton_OnClick(object sender, RoutedEventArgs e)
        {
            CheckInternetButton_OnClick(sender, e);
        }

        private void TweaksButton_OnClick(object sender, RoutedEventArgs e)
        {
            new TweaksDialog().ShowDialog();
            CheckInternetButton_OnClick(sender, e);
        }

        private void CheckNoInternetButton_OnClick(object sender, RoutedEventArgs e)
        {
            CheckInternetButton_OnClick(sender, e);
        }

        private async void PendingUpdatesButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (PendingUpdatesActionText.Text == "Check again")
            {
                _pendingUpdatesCheckCount++;
                CheckInternetButton_OnClick(sender, e);
                return;
            }
            try
            {
                System.Diagnostics.Process.Start("ms-settings:windowsupdate");
            }
            catch (Exception ex)
            {
                _ = ex;
                if (KTWirzade.GUI.MessageBox.Show(typeof(MainWindow), "Could not open system updates, please update your system manually.", "Information", KTWirzade.GUI.MessageBoxButton.OKBypass) == KTWirzade.GUI.MessageBoxResult.Bypass)
                {
                    ((Playbook)GlobalsGUI.Current.Playbook).Requirements = ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Where((Requirement x) => (int)x != 4).ToArray();
                    PendingUpdatesBox.Visibility = Visibility.Collapsed;
                    CheckInternetButton_OnClick(sender, e);
                    if (!GlobalsGUI.WUAStopperEngaged)
                    {
                        await InterLink.ExecuteSafeAsync((Expression<Action>)(() => WUAStopper.Initialize()), true, -1);
                        GlobalsGUI.WUAStopperEngaged = true;
                    }
                }
            }
            PendingUpdatesActionText.Text = "Check again";
        }

        private void BypassButton_OnClick(object sender, RoutedEventArgs e)
        {
            ((Playbook)GlobalsGUI.Current.Playbook).Requirements = ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Where((Requirement x) => (int)x != 12).ToArray();
            FreshInstallBox.Visibility = Visibility.Collapsed;
            CheckInternetButton_OnClick(new System.Windows.Controls.Button(), new RoutedEventArgs());
        }

        private void ViewInstallGuideButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(((Playbook)GlobalsGUI.Current.Playbook).InstallGuide ?? "https://docs.ameliorated.io/using-wizard/install-windows.html");
            }
            catch (Exception)
            {
                KTWirzade.GUI.MessageBox.Show(typeof(MainWindow), "Install guide link is invalid.", "Warning");
            }
        }

        private void Hyperlink_OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ((Hyperlink)sender).TextDecorations = TextDecorations.Underline;
        }

        private void Hyperlink_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ((Hyperlink)sender).TextDecorations = null;
        }

        private void UninstallUpdatesButton_OnClick(object sender, RoutedEventArgs e)
        {
            new UninstallUpdatesDialog().ShowDialog();
            UninstallUpdatesBox.Visibility = System.Windows.Visibility.Collapsed;
            CheckInternetButton_OnClick(new System.Windows.Controls.Button(), new RoutedEventArgs());
        }
    }
}