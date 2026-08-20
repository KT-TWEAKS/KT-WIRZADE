using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.Models;
using KTWirzade.GUI.Utils;
using KTWirzade.GUI.ViewModels;
using KTWirzade.GUI.Windows;
using KTWirzade.Shared;
using static KTWirzade.Shared.Requirements;

namespace KTWirzade.GUI.Views
{
    public partial class IntroPageView : System.Windows.Controls.UserControl
    {
        public struct RECT
        {
            public int Left;

            public int Top;

            public int Right;

            public int Bottom;
        }

        public IntroPageView()
        {
            InitializeComponent();
            base.DataContextChanged += async delegate
            {
                object dataContext = base.DataContext;
                if (!(dataContext is ViewModelBase viewModel))
                {
                    return;
                }
                try
                {
                    viewModel.MainNextButtonCommand = new GlobalsGUI.CommandHandler(Next, () => true);
                    RunAgainButton.IsEnabled = true;
                    RunAgainErrorButton.IsEnabled = true;
                    UpgradeButton.IsEnabled = true;
                    AntivirusBox.Visibility = Visibility.Hidden;
                    NotRequiredBox.Visibility = Visibility.Hidden;
                    AppliedBox.Visibility = Visibility.Hidden;
                    AppliedErrorBox.Visibility = Visibility.Hidden;
                    UpgradeBox.Visibility = Visibility.Hidden;
                    NextStepText.Visibility = Visibility.Collapsed;
                    SlideModuleUp();
                    bool nextButton = true;
                    viewModel.MainNextButtonActive = false;
                    DisableSecurityStack.Opacity = 0.4;
                    DisableSecurityButton.IsEnabled = false;
                    PlaybookGUI lastAppliedMatch = GlobalsGUI.Current.Playbook.LastAppliedMatch(GlobalsGUI.Current.AppliedPlaybooks);
                    if (lastAppliedMatch != null && ((Playbook)lastAppliedMatch).Version != ((Playbook)GlobalsGUI.Current.Playbook).Version && ((Playbook)GlobalsGUI.Current.Playbook).IsUpgradeApplicable(((Playbook)lastAppliedMatch).Version))
                    {
                        UpgradeBox.Visibility = Visibility.Visible;
                        nextButton = false;
                    }
                    else if (lastAppliedMatch != null && ((Playbook)lastAppliedMatch).Version == ((Playbook)GlobalsGUI.Current.Playbook).Version)
                    {
                        AppliedText.Text = "This Playbook is currently applied";
                        nextButton = false;
                        if ((int)((Playbook)lastAppliedMatch).ErrorLevel == 1)
                        {
                            AppliedErrorBoxText.Text = "This Playbook is applied with errors";
                            AppliedErrorBox.Visibility = Visibility.Visible;
                        }
                        else if ((int)((Playbook)lastAppliedMatch).ErrorLevel == 2)
                        {
                            AppliedErrorBoxText.Text = "This Playbook failed to apply";
                            AppliedErrorBox.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            AppliedBox.Visibility = Visibility.Visible;
                        }
                    }
                    else if (((Playbook)GlobalsGUI.Current.Playbook).Username == "Ameliorated" && GlobalsGUI.Current.Playbook.VerificationStatus == PlaybookGUI.VerificationLevel.Verified && HasAMEIntegriy())
                    {
                        nextButton = false;
                        AppliedText.Text = "This Playbook is currently applied";
                        AppliedBox.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        bool flag = ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)6);
                        if (flag)
                        {
                            flag = !(await Task.Run(delegate
                            {
                                try
                                {
                                    if (Convert.ToUInt32(Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\Winmgmt").GetValue("Start")) == 4)
                                    {
                                        GUIUtil.EnsureWMI().GetAwaiter().GetResult();
                                    }
                                }
                                catch
                                {
                                    GUIUtil.EnsureWMI().GetAwaiter().GetResult();
                                }
                                return (!WizardConfig.Current.IgnoreRemnants.Get()) ? (!WinUtil.GetEnabledAvList(false).Any()) : (!WinUtil.GetEnabledAvList(false).Any((ProviderStatus x) => x.FileExists));
                            }));
                        }
                        if (flag)
                        {
                            nextButton = false;
                            NextStepText.Visibility = Visibility.Hidden;
                            AntivirusBox.Visibility = Visibility.Visible;
                        }
                        else if (((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)2) || ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)3))
                        {
                            if ((await GUIUtil.GetDefenderToggles()).Any((bool x) => x) && Process.GetProcessesByName("MsMpEng").Any())
                            {
                                nextButton = false;
                                NotRequiredBox.Visibility = Visibility.Hidden;
                                NextStepText.Visibility = Visibility.Visible;
                                DisableSecurityStack.Opacity = 1.0;
                                DisableSecurityButton.IsEnabled = true;
                            }
                        }
                        else if (!((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)6))
                        {
                            NotRequiredBox.Visibility = Visibility.Visible;
                        }
                    }
                    if (viewModel.MainNextButtonActive)
                    {
                        viewModel.MainNextButtonActive = nextButton;
                    }
                    else
                    {
                        if (lastAppliedMatch != null && ((Playbook)lastAppliedMatch).Version != ((Playbook)GlobalsGUI.Current.Playbook).Version && !((Playbook)GlobalsGUI.Current.Playbook).IsUpgradeApplicable(((Playbook)lastAppliedMatch).Version))
                        {
                            viewModel.MainNextButtonCommand = new GlobalsGUI.CommandHandler(delegate
                            {
                                ConflictPopup.IsOpen = true;
                            }, () => true);
                            ConflictCurrentImage.Source = lastAppliedMatch.Icon;
                            ConflictCurrentTitle.Text = "v" + ((Playbook)lastAppliedMatch).Version;
                            ConflictImage.Source = GlobalsGUI.Current.Playbook.Icon;
                            ConflictTitle.Text = "v" + ((Playbook)GlobalsGUI.Current.Playbook).Version;
                            ConflictPopupText.Text = "This Playbook is already applied to your system and doesn't support this upgrade.";
                            ConflictTopLine.Text = "This can cause major conflicts and issues!";
                            ConflictCancelButton.Visibility = Visibility.Visible;
                            ConflictContinueButton.Visibility = Visibility.Visible;
                            ConflictOKButton.Visibility = Visibility.Collapsed;
                            if (((Playbook)GlobalsGUI.Current.Playbook).AllowUnsupportedUpgrades == false)
                            {
                                ConflictPopupText.Text = "An older version of this Playbook is already applied to your system.";
                                ConflictTopLine.Text = ((Playbook)GlobalsGUI.Current.Playbook).Name + " does not allow upgrades.";
                                ConflictCancelButton.Visibility = Visibility.Collapsed;
                                ConflictContinueButton.Visibility = Visibility.Collapsed;
                                ConflictOKButton.Visibility = Visibility.Visible;
                            }
                        }
                        if (lastAppliedMatch == null && GlobalsGUI.Current.AppliedPlaybooks.Any((Playbook x) => x.Overhaul) && ((Playbook)GlobalsGUI.Current.Playbook).Overhaul)
                        {
                            viewModel.MainNextButtonCommand = new GlobalsGUI.CommandHandler(delegate
                            {
                                ConflictPopup.IsOpen = true;
                            }, () => true);
                            Playbook conflict = GlobalsGUI.Current.AppliedPlaybooks.First((Playbook x) => x.Overhaul);
                            ConflictPopupText.Text = "You already have a total conversion Playbook applied to your system. Continue anyway?";
                            ConflictTopLine.Text = "This can cause major conflicts and issues!";
                            ConflictCancelButton.Visibility = Visibility.Visible;
                            ConflictContinueButton.Visibility = Visibility.Visible;
                            ConflictOKButton.Visibility = Visibility.Collapsed;
                            if (conflict.ImageBytes != null)
                            {
                                try
                                {
                                    using MemoryStream stream = new MemoryStream(conflict.ImageBytes);
                                    BitmapImage bitmapImage = new BitmapImage();
                                    bitmapImage.BeginInit();
                                    bitmapImage.StreamSource = stream;
                                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmapImage.EndInit();
                                    bitmapImage.Freeze();
                                    ConflictCurrentImage.Source = bitmapImage;
                                }
                                catch (Exception)
                                {
                                }
                            }
                            ConflictCurrentTitle.Text = conflict.Name;
                            ConflictImage.Source = GlobalsGUI.Current.Playbook.Icon;
                            ConflictTitle.Text = ((Playbook)GlobalsGUI.Current.Playbook).Name;
                        }
                        string cache = GlobalsGUI.Current.Playbook.FileNameWithoutExtension;
                        try
                        {
                            while (App.DeCrippleDefender ||
                                   (GlobalsGUI.Current?.Playbook?.FileNameWithoutExtension == cache &&
                                    GlobalsGUI.Current?.Playbook?.VerificationTask?.IsCompleted == false))
                            {
                                await Task.Delay(100);
                            }
                        }
                        catch (NullReferenceException)
                        {
                            return;
                        }
                        if (!(cache != GlobalsGUI.Current.Playbook.FileNameWithoutExtension))
                        {
                            if (nextButton)
                            {
                                viewModel.MainNextButtonActive = true;
                            }
                            viewModel.MainUpdatesButtonActive = true;
                            viewModel.MainStatusButtonActive = GlobalsGUI.Current.Playbook.VerificationStatus == PlaybookGUI.VerificationLevel.Malicious || GlobalsGUI.Current.Playbook.VerificationStatus == PlaybookGUI.VerificationLevel.Verified;
                        }
                    }
                }
                catch (NullReferenceException)
                {
                    if (GlobalsGUI.Current.Playbook == null)
                    {
                        return;
                    }
                    throw;
                }
            };
            ConflictPopup.Opened += delegate
            {
                IntPtr handle = ((HwndSource)PresentationSource.FromVisual(ConflictPopup.Child)).Handle;
                if (GetWindowRect(handle, out var lpRect))
                {
                    SetWindowPos(handle, -2, lpRect.Left, lpRect.Top, (int)base.Width, (int)base.Height, 0);
                }
            };
            Popup.Opened += delegate
            {
                IntPtr handle = ((HwndSource)PresentationSource.FromVisual(Popup.Child)).Handle;
                if (GetWindowRect(handle, out var lpRect))
                {
                    SetWindowPos(handle, -2, lpRect.Left, lpRect.Top, (int)base.Width, (int)base.Height, 0);
                }
            };
        }

        private void Next()
        {
            try
            {
                if (GlobalsGUI.Current.WizardPlaybook.PendingUpdate != null && GlobalsGUI.Current.Playbook.PendingUpdate != null)
                {
                    Popup.IsOpen = true;
                    return;
                }
                if (GlobalsGUI.Current.WizardPlaybook.PendingUpdate != null)
                {
                    UpdatesPopupText.Text = "There are updates available for KT WIRZADE.";
                    Popup.IsOpen = true;
                    return;
                }
                if (GlobalsGUI.Current.Playbook.PendingUpdate != null)
                {
                    UpdatesPopupText.Text = "There are updates available for this Playbook.";
                    Popup.IsOpen = true;
                    return;
                }
            }
            catch
            {
            }
            Continue(this, new RoutedEventArgs());
        }

        private bool HasAMEIntegriy()
        {
            if (Directory.Exists(Environment.ExpandEnvironmentVariables("%ProgramFiles%\\Windows Defender")))
            {
                return false;
            }
            if (Directory.Exists(Environment.ExpandEnvironmentVariables("%ProgramData%\\Microsoft\\Windows Defender")))
            {
                return false;
            }
            if (File.Exists(Environment.ExpandEnvironmentVariables("%WINDIR%\\System32\\wuaueng.dll")))
            {
                return false;
            }
            if (Directory.Exists(Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Microsoft\\Edge")))
            {
                return false;
            }
            if (File.Exists(Environment.ExpandEnvironmentVariables("%WINDIR%\\System32\\smartscreen.exe")))
            {
                return false;
            }
            if (File.Exists(Environment.ExpandEnvironmentVariables("%WINDIR%\\System32\\SIHClient.exe")))
            {
                return false;
            }
            if (File.Exists(Environment.ExpandEnvironmentVariables("%WINDIR%\\System32\\StorSvc.dll")))
            {
                return false;
            }
            if (!File.Exists(Environment.ExpandEnvironmentVariables("%WINDIR%\\System32\\ameck.exe")))
            {
                return false;
            }
            return true;
        }

        private void ConflictContinueButton_OnClick(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            ConflictPopup.IsOpen = false;
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                RequirementsPageViewModel requirementsPageViewModel = new RequirementsPageViewModel(new RequirementsPage());
                GlobalsGUI.Current.Playbook.CurrentPage = requirementsPageViewModel;
                ((MainWindowViewModel)mainWindow.DataContext).CurrentViewModel = requirementsPageViewModel;
            });
        }

        private void ConflictCancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            ConflictPopup.IsOpen = false;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32")]
        private static extern int SetWindowPos(IntPtr hWnd, int hwndInsertAfter, int x, int y, int cx, int cy, int wFlags);

        private void FocusWindow(object sender, EventArgs e)
        {
            try
            {
                MainWindow.CurrentDispatcher.Invoke(delegate
                {
                    SetForegroundWindow(new WindowInteropHelper(System.Windows.Application.Current.Windows.OfType<MainWindow>().First()).Handle);
                });
            }
            catch
            {
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private void SlideModuleUp()
        {
            Storyboard storyboard = new Storyboard();
            DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 260)),
                KeyFrames =
            {
                (DoubleKeyFrame)new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))),
                (DoubleKeyFrame)new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 260)))
            }
            };
            ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 460))
            };
            ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
            };
            ThicknessKeyFrame transitionKey2 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
            };
            ThicknessKeyFrame transitionKey3 = new EasingThicknessKeyFrame
            {
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseOut
                },
                Value = new Thickness(0.0, -53.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 460))
            };
            transitionAnim.KeyFrames.Add(transitionKey1);
            transitionAnim.KeyFrames.Add(transitionKey2);
            transitionAnim.KeyFrames.Add(transitionKey3);
            Storyboard.SetTarget(opacityAnim, ModuleGrid);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            Storyboard.SetTarget(transitionAnim, ModuleGrid);
            Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
            storyboard.Children.Add(opacityAnim);
            storyboard.Children.Add(transitionAnim);
            DoubleAnimationUsingKeyFrames scale_x = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(460.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 0.95,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                },
                new LinearDoubleKeyFrame
                {
                    Value = 0.95,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = 1.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 460))
                }
            }
            };
            DoubleAnimationUsingKeyFrames scale_y = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(160.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 0.95,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = 1.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 160))
                }
            }
            };
            moduletransform.BeginAnimation(ScaleTransform.ScaleXProperty, scale_x);
            moduletransform.BeginAnimation(ScaleTransform.ScaleYProperty, scale_y);
            storyboard.Begin();
        }

        private async void Run_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow owner = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                new SecurityDialog().ShowDialog(owner, ((Playbook)GlobalsGUI.Current.Playbook).Name);
            });
            if (((await GUIUtil.GetDefenderToggles()).All((bool x) => !x) || !Process.GetProcessesByName("MsMpEng").Any()) && base.DataContext is ViewModelBase viewModel)
            {
                viewModel.MainNextButtonActive = true;
                DisableSecurityStack.Opacity = 0.4;
                DisableSecurityButton.IsEnabled = false;
                NextStepText.Visibility = Visibility.Hidden;
            }
        }

        private async void AntivirusButton_OnClick(object sender, RoutedEventArgs e)
        {
            bool? removed = false;
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow owner = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                AntivirusDialog antivirusDialog = new AntivirusDialog();
                removed = antivirusDialog.ShowDialog(owner, ((Playbook)GlobalsGUI.Current.Playbook).Name);
            });
            if (!removed.HasValue)
            {
                if ((await GUIUtil.GetDefenderToggles()).Any((bool x) => x) && Process.GetProcessesByName("MsMpEng").Any())
                {
                    DisableSecurityStack.Opacity = 1.0;
                    DisableSecurityButton.IsEnabled = true;
                }
                else
                {
                    if (!(base.DataContext is ViewModelBase viewModel))
                    {
                        return;
                    }
                    viewModel.MainNextButtonActive = true;
                }
                await SlideModuleDown();
            }
            else if (removed.HasValue && removed.Value)
            {
                System.Windows.Application.Current.Shutdown(0);
            }
        }

        private async void RunAgainButton_OnClick(object sender, RoutedEventArgs e)
        {
            object dataContext = base.DataContext;
            if (!(dataContext is ViewModelBase viewModel))
            {
                return;
            }
            RunAgainButton.IsEnabled = false;
            RunAgainErrorButton.IsEnabled = false;
            UpgradeButton.IsEnabled = false;
            AppliedLoadContainer.Visibility = Visibility.Visible;
            Spinner spinner = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            AppliedLoadContainer.Children.Add(spinner);
            AppliedErrorLoadContainer.Visibility = Visibility.Visible;
            Spinner spinner2 = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            AppliedErrorLoadContainer.Children.Add(spinner2);
            UpgradeLoadContainer.Visibility = Visibility.Visible;
            Spinner spinner3 = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            UpgradeLoadContainer.Children.Add(spinner3);
            await Task.Delay(2000);
            AppliedLoadContainer.Visibility = Visibility.Collapsed;
            AppliedLoadContainer.Children.Remove(spinner);
            AppliedErrorLoadContainer.Visibility = Visibility.Collapsed;
            AppliedErrorLoadContainer.Children.Remove(spinner2);
            UpgradeLoadContainer.Visibility = Visibility.Collapsed;
            UpgradeLoadContainer.Children.Remove(spinner3);
            await SlideModuleDown();
            bool nextButton = true;
            if (GlobalsGUI.Current.Playbook != null)
            {
                bool flag = ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)6);
                if (flag)
                {
                    flag = !(await Task.Run(delegate
                    {
                        try
                        {
                            if (Convert.ToUInt32(Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\Winmgmt").GetValue("Start")) == 4)
                            {
                                GUIUtil.EnsureWMI().GetAwaiter().GetResult();
                            }
                        }
                        catch
                        {
                            GUIUtil.EnsureWMI().GetAwaiter().GetResult();
                        }
                        return (!WizardConfig.Current.IgnoreRemnants.Get()) ? (!WinUtil.GetEnabledAvList(false).Any()) : (!WinUtil.GetEnabledAvList(false).Any((ProviderStatus x) => x.FileExists));
                    }));
                }
                if (flag)
                {
                    nextButton = false;
                    NextStepText.Visibility = Visibility.Hidden;
                    AntivirusBox.Visibility = Visibility.Visible;
                }
                else if (((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)2) || ((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)3))
                {
                    if ((await GUIUtil.GetDefenderToggles()).Any((bool x) => x) && Process.GetProcessesByName("MsMpEng").Any())
                    {
                        nextButton = false;
                        NotRequiredBox.Visibility = Visibility.Hidden;
                        NextStepText.Visibility = Visibility.Visible;
                        DisableSecurityStack.Opacity = 1.0;
                        DisableSecurityButton.IsEnabled = true;
                    }
                }
                else if (!((Playbook)GlobalsGUI.Current.Playbook).Requirements.Contains((Requirement)6))
                {
                    NotRequiredBox.Visibility = Visibility.Visible;
                }
            }
            if (nextButton)
            {
                viewModel.MainNextButtonActive = true;
            }
            SlideModuleUp();
        }

        private async Task SlideModuleDown()
        {
            Storyboard storyboard = new Storyboard();
            DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 280)),
                KeyFrames =
            {
                (DoubleKeyFrame)new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))),
                (DoubleKeyFrame)new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 280)))
            }
            };
            ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 210))
            };
            ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, -53.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
            };
            ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
            {
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseOut
                },
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 210))
            };
            transitionAnim.KeyFrames.Add(transitionKey1);
            transitionAnim.KeyFrames.Add(transitionKey2);
            Storyboard.SetTarget(opacityAnim, ModuleGrid);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            Storyboard.SetTarget(transitionAnim, ModuleGrid);
            Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
            storyboard.Children.Add(opacityAnim);
            storyboard.Children.Add(transitionAnim);
            DoubleAnimationUsingKeyFrames scale_x = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(160.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 1.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = 0.95,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 160))
                }
            }
            };
            DoubleAnimationUsingKeyFrames scale_y = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(160.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 1.0,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = 0.95,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 160))
                }
            }
            };
            moduletransform.BeginAnimation(ScaleTransform.ScaleXProperty, scale_x);
            moduletransform.BeginAnimation(ScaleTransform.ScaleYProperty, scale_y);
            storyboard.Begin();
            await Task.Delay(300);
            AppliedBox.Visibility = Visibility.Hidden;
            AppliedErrorBox.Visibility = Visibility.Hidden;
            UpgradeBox.Visibility = Visibility.Hidden;
            AntivirusBox.Visibility = Visibility.Hidden;
        }

        private void OpenUpdates(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            Popup.IsOpen = false;
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow owner = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                new UpdatesDialog().ShowDialog(owner);
            });
        }

        private void Continue(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            Popup.IsOpen = false;
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                RequirementsPageViewModel requirementsPageViewModel = new RequirementsPageViewModel(new RequirementsPage());
                GlobalsGUI.Current.Playbook.CurrentPage = requirementsPageViewModel;
                ((MainWindowViewModel)mainWindow.DataContext).CurrentViewModel = requirementsPageViewModel;
            });
        }
    }
}
