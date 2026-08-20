using Core;
using Core.Actions;
using Core.Exceptions;
using Interprocess;
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.Utils;
using static Core.Win32;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace KTWirzade.GUI.Windows
{
    public partial class SecurityDialog : AcrylicWindow
    {
        private static List<bool> origList = new List<bool>();

        private bool animating;


        private bool PoliciesPresent()
        {
            try
            {
                RegistryKey policiesKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Policies\\Microsoft\\Windows Defender");
                if (policiesKey == null)
                {
                    return false;
                }
                try
                {
                    RegistryKey realtimeKey = policiesKey.OpenSubKey("Real-Time Protection");
                    if (realtimeKey != null && realtimeKey.GetValueNames().Contains("DisableRealtimeMonitoring") && (int)realtimeKey.GetValue("DisableRealtimeMonitoring") != 1)
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                }
                try
                {
                    RegistryKey spyNetKey = policiesKey.OpenSubKey("SpyNet");
                    if (spyNetKey != null && spyNetKey.GetValueNames().Contains("SpyNetReporting") && (int)spyNetKey.GetValue("SpyNetReporting") != 0)
                    {
                        return true;
                    }
                    if (spyNetKey != null && spyNetKey.GetValueNames().Contains("SubmitSamplesConsent") && (int)spyNetKey.GetValue("SubmitSamplesConsent") != 0 && (int)spyNetKey.GetValue("SubmitSamplesConsent") != 4 && (int)spyNetKey.GetValue("SubmitSamplesConsent") != 2)
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                }
            }
            catch
            {
            }
            return false;
        }

        public SecurityDialog()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public static BitmapSource Convert(Bitmap bitmap)
        {
            using MemoryStream memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0L;
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage)
        {
            using MemoryStream outStream = new MemoryStream();
            PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
            pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapImage));
            pngBitmapEncoder.Save(outStream);
            return new Bitmap(new Bitmap(outStream));
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
            {
                RootWindow.SetResourceReference(BackgroundProperty, "FakeBackgroundBrush");
                PageContainer.SetResourceReference(BackgroundProperty, "FakePageBackgroundBrush");
            }
            ProgressBar.Start();
            double ppuX = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
            double ppuY = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M22;
            double toggleY = ppuY * RealtimeImg.ActualHeight;
            double toggleX = ppuX * RealtimeImg.ActualWidth;
            double resY = ppuY * SecurityImage.ActualHeight;
            double resX = ppuX * SecurityImage.ActualWidth;
            BitmapSource toggleOnSource = null;
            BitmapSource toggleOffSource = null;
            BitmapSource securitySource = null;
            BitmapImage bmiOn = (BitmapImage)FindResource("SecurityToggleOnBitmap");
            BitmapImage bmiOff = (BitmapImage)FindResource("SecurityToggleOffBitmap");
            BitmapImage bmiSec = (BitmapImage)FindResource("WindowsSecurityBitmap");
            await Task.Run(delegate
            {
                toggleOnSource = Convert(ImageUtilities.ResizeImage(BitmapImage2Bitmap(bmiOn), (int)toggleX, (int)toggleY));
                toggleOffSource = Convert(ImageUtilities.ResizeImage(BitmapImage2Bitmap(bmiOff), (int)toggleX, (int)toggleY));
                securitySource = Convert(ImageUtilities.ResizeImage(BitmapImage2Bitmap(bmiSec), (int)resX, (int)resY));
            });
            SecurityImage.Source = securitySource;
            Storyboard policyBoard = null;
            if (PoliciesPresent())
            {
                policyBoard = new Storyboard();
                DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames();
                opacityAnim.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 240));
                opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 100))));
                opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))));
                DoubleAnimation opacityAnimSteps = new DoubleAnimation();
                opacityAnimSteps.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
                opacityAnimSteps.To = 0.4;
                ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames();
                transitionAnim.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540));
                ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, -96.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                };
                ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    Value = new Thickness(0.0, 9.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                ThicknessKeyFrame transitionKey3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 9.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKey4 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 9.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                transitionAnim.KeyFrames.Add(transitionKey1);
                transitionAnim.KeyFrames.Add(transitionKey2);
                transitionAnim.KeyFrames.Add(transitionKey3);
                transitionAnim.KeyFrames.Add(transitionKey4);
                ThicknessAnimationUsingKeyFrames obj = new ThicknessAnimationUsingKeyFrames
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKeyStep1 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                ThicknessKeyFrame transitionKeyStep2 = new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    Value = new Thickness(0.0, -105.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 440))
                };
                ThicknessKeyFrame transitionKeyStep3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, -105.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKeyStep4 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                obj.KeyFrames.Add(transitionKeyStep1);
                obj.KeyFrames.Add(transitionKeyStep2);
                obj.KeyFrames.Add(transitionKeyStep3);
                obj.KeyFrames.Add(transitionKeyStep4);
                Storyboard.SetTarget(opacityAnim, ModuleGrid);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
                Storyboard.SetTarget(opacityAnimSteps, Steps);
                Storyboard.SetTargetProperty(opacityAnimSteps, new PropertyPath("Opacity"));
                Storyboard.SetTarget(transitionAnim, ModuleGrid);
                Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
                policyBoard.Children.Add(opacityAnim);
                policyBoard.Children.Add(opacityAnimSteps);
                policyBoard.Children.Add(transitionAnim);
                DoubleAnimationUsingKeyFrames scale_x = new DoubleAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromMilliseconds(200.0),
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new LinearDoubleKeyFrame
                    {
                        Value = 0.9,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                    },
                    new EasingDoubleKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        Value = 1.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                    }
                }
                };
                DoubleAnimationUsingKeyFrames scale_y = new DoubleAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromMilliseconds(200.0),
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new LinearDoubleKeyFrame
                    {
                        Value = 0.9,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                    },
                    new EasingDoubleKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        Value = 1.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                    }
                }
                };
                Storyboard.SetTarget(scale_x, policiestransform);
                Storyboard.SetTargetProperty(scale_x, new PropertyPath(ScaleTransform.ScaleXProperty));
                policyBoard.Children.Add(scale_x);
                Storyboard.SetTarget(scale_y, policiestransform);
                Storyboard.SetTargetProperty(scale_y, new PropertyPath(ScaleTransform.ScaleXProperty));
                policyBoard.Children.Add(scale_y);
                PolicyGrid.Visibility = Visibility.Visible;
            }
            List<bool> list = await GUIUtil.GetDefenderToggles();
            origList = new List<bool>();
            origList.AddRange(list);
            List<bool> everToggledList = new List<bool>();
            everToggledList.AddRange(list);
            bool helpMsgShown = false;
            bool msgSlided = false;
            Stopwatch timer = new Stopwatch();
            while (IsLoaded)
            {
                if (list[0])
                {
                    RealtimeImg.Source = toggleOnSource;
                }
                else
                {
                    RealtimeImg.Source = toggleOffSource;
                }
                if (list[1])
                {
                    CloudImg.Source = toggleOnSource;
                }
                else
                {
                    CloudImg.Source = toggleOffSource;
                }
                if (list[2])
                {
                    SampleImg.Source = toggleOnSource;
                }
                else
                {
                    SampleImg.Source = toggleOffSource;
                }
                if (list[3])
                {
                    TamperImg.Source = toggleOnSource;
                }
                else
                {
                    TamperImg.Source = toggleOffSource;
                }
                if (policyBoard != null)
                {
                    await Task.Delay(1500);
                    policyBoard.Begin();
                    policyBoard = null;
                }
                bool waiting = false;
                bool disabled = false;
                if (list.All((bool x) => !x))
                {
                    await ProgressBar.WaitForAnimation();
                    List<bool> refresh = await GUIUtil.GetDefenderToggles();
                    if (refresh.Any((bool x) => x))
                    {
                        OpenSecurityButton.IsEnabled = true;
                        FinishText.Visibility = Visibility.Collapsed;
                        ProgressBar.Visibility = Visibility.Visible;
                        StatusText.Text = "Checking Windows Security...";
                        CheckMark.Visibility = Visibility.Hidden;
                        ProgressBar.Start();
                        list = refresh;
                    }
                    else
                    {
                        if (HelpGrid.Visibility == Visibility.Visible)
                        {
                            SlideModule();
                            helpMsgShown = true;
                        }
                        timer.Reset();
                        bool num = StatusText.Text == "Windows Security is disabled";
                        OpenSecurityButton.IsEnabled = false;
                        ProgressBar.Visibility = Visibility.Collapsed;
                        FinishText.Visibility = Visibility.Visible;
                        StatusText.Text = "Windows Security is disabled";
                        CheckMark.Visibility = Visibility.Visible;
                        if (!num)
                        {
                            try
                            {
                                System.Diagnostics.Process.GetProcessesByName("SecHealthUI").FirstOrDefault()?.Kill();
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                }
                else
                {
                    List<bool> refresh2 = await GUIUtil.GetDefenderToggles();
                    if (everToggledList[0] && !refresh2[0])
                    {
                        if (HelpGrid.Visibility == Visibility.Visible && !disabled)
                        {
                            SlideModule();
                            helpMsgShown = false;
                            msgSlided = true;
                        }
                        timer.Restart();
                        everToggledList[0] = false;
                    }
                    if (everToggledList[1] && !refresh2[1])
                    {
                        if (HelpGrid.Visibility == Visibility.Visible && !disabled)
                        {
                            SlideModule();
                            helpMsgShown = false;
                            msgSlided = true;
                        }
                        timer.Restart();
                        everToggledList[1] = false;
                    }
                    if (everToggledList[2] && !refresh2[2])
                    {
                        if (HelpGrid.Visibility == Visibility.Visible && !disabled)
                        {
                            SlideModule();
                            helpMsgShown = false;
                            msgSlided = true;
                        }
                        timer.Restart();
                        everToggledList[2] = false;
                    }
                    if (everToggledList[3] && !refresh2[3])
                    {
                        if (HelpGrid.Visibility == Visibility.Visible && !disabled)
                        {
                            SlideModule();
                            helpMsgShown = false;
                            msgSlided = true;
                        }
                        timer.Restart();
                        everToggledList[3] = false;
                    }
                    if (!helpMsgShown && PolicyGrid.Visibility != Visibility.Visible)
                    {
                        if (!refresh2.SequenceEqual(origList))
                        {
                            waiting = true;
                        }
                        if (waiting && list.Any((bool x) => !x))
                        {
                            if (!timer.IsRunning)
                            {
                                timer.Start();
                            }
                            if ((timer.ElapsedMilliseconds > 5000 && !msgSlided) || (msgSlided && timer.ElapsedMilliseconds > 7500))
                            {
                                timer.Reset();
                                msgSlided = false;
                                helpMsgShown = true;
                                Storyboard storyboard = new Storyboard();
                                DoubleAnimationUsingKeyFrames opacityAnim2 = new DoubleAnimationUsingKeyFrames
                                {
                                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 240)),
                                    KeyFrames =
                                {
                                    new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 100))),
                                    new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240)))
                                }
                                };
                                DoubleAnimation opacityAnimSteps2 = new DoubleAnimation
                                {
                                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200)),
                                    To = 0.4
                                };
                                ThicknessAnimationUsingKeyFrames transitionAnim2 = new ThicknessAnimationUsingKeyFrames
                                {
                                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
                                };
                                ThicknessKeyFrame transitionKey5 = new LinearThicknessKeyFrame
                                {
                                    Value = new Thickness(0.0, -96.0, 0.0, 0.0),
                                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                                };
                                ThicknessKeyFrame transitionKey6 = new EasingThicknessKeyFrame
                                {
                                    EasingFunction = new SineEase
                                    {
                                        EasingMode = EasingMode.EaseInOut
                                    },
                                    Value = new Thickness(0.0, 9.0, 0.0, 0.0),
                                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                                };
                                ThicknessKeyFrame transitionKey7 = new LinearThicknessKeyFrame
                                {
                                    Value = new Thickness(0.0, 9.0, 0.0, 0.0),
                                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                                };
                                ThicknessKeyFrame transitionKey8 = new LinearThicknessKeyFrame
                                {
                                    Value = new Thickness(0.0, 9.0, 0.0, 0.0),
                                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                                };
                                transitionAnim2.KeyFrames.Add(transitionKey5);
                                transitionAnim2.KeyFrames.Add(transitionKey6);
                                transitionAnim2.KeyFrames.Add(transitionKey7);
                                transitionAnim2.KeyFrames.Add(transitionKey8);
                                ThicknessAnimationUsingKeyFrames obj2 = new ThicknessAnimationUsingKeyFrames
                                {
                                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
                                };
                                ThicknessKeyFrame transitionKeyStep5 = new LinearThicknessKeyFrame
                                {
                                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                                };
                                ThicknessKeyFrame transitionKeyStep6 = new EasingThicknessKeyFrame
                                {
                                    EasingFunction = new SineEase
                                    {
                                        EasingMode = EasingMode.EaseInOut
                                    },
                                    Value = new Thickness(0.0, -105.0, 0.0, 0.0),
                                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 440))
                                };
                                ThicknessKeyFrame transitionKeyStep7 = new LinearThicknessKeyFrame
                                {
                                    Value = new Thickness(0.0, -105.0, 0.0, 0.0),
                                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                                };
                                ThicknessKeyFrame transitionKeyStep8 = new LinearThicknessKeyFrame
                                {
                                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                                };
                                obj2.KeyFrames.Add(transitionKeyStep5);
                                obj2.KeyFrames.Add(transitionKeyStep6);
                                obj2.KeyFrames.Add(transitionKeyStep7);
                                obj2.KeyFrames.Add(transitionKeyStep8);
                                Storyboard.SetTarget(opacityAnim2, ModuleGrid);
                                Storyboard.SetTargetProperty(opacityAnim2, new PropertyPath("Opacity"));
                                Storyboard.SetTarget(opacityAnimSteps2, Steps);
                                Storyboard.SetTargetProperty(opacityAnimSteps2, new PropertyPath("Opacity"));
                                Storyboard.SetTarget(transitionAnim2, ModuleGrid);
                                Storyboard.SetTargetProperty(transitionAnim2, new PropertyPath("Margin"));
                                storyboard.Children.Add(opacityAnim2);
                                storyboard.Children.Add(opacityAnimSteps2);
                                storyboard.Children.Add(transitionAnim2);
                                DoubleAnimationUsingKeyFrames scale_x2 = new DoubleAnimationUsingKeyFrames
                                {
                                    Duration = TimeSpan.FromMilliseconds(200.0),
                                    KeyFrames = new DoubleKeyFrameCollection
                                {
                                    new LinearDoubleKeyFrame
                                    {
                                        Value = 0.9,
                                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                                    },
                                    new EasingDoubleKeyFrame
                                    {
                                        EasingFunction = new SineEase
                                        {
                                            EasingMode = EasingMode.EaseInOut
                                        },
                                        Value = 1.0,
                                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                                    }
                                }
                                };
                                DoubleAnimationUsingKeyFrames scale_y2 = new DoubleAnimationUsingKeyFrames
                                {
                                    Duration = TimeSpan.FromMilliseconds(200.0),
                                    KeyFrames = new DoubleKeyFrameCollection
                                {
                                    new LinearDoubleKeyFrame
                                    {
                                        Value = 0.9,
                                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                                    },
                                    new EasingDoubleKeyFrame
                                    {
                                        EasingFunction = new SineEase
                                        {
                                            EasingMode = EasingMode.EaseInOut
                                        },
                                        Value = 1.0,
                                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                                    }
                                }
                                };
                                helptransform.BeginAnimation(ScaleTransform.ScaleXProperty, scale_x2);
                                helptransform.BeginAnimation(ScaleTransform.ScaleYProperty, scale_y2);
                                storyboard.Begin();
                                HelpGrid.Visibility = Visibility.Visible;
                            }
                        }
                    }
                    list = refresh2;
                    OpenSecurityButton.IsEnabled = true;
                    FinishText.Visibility = Visibility.Collapsed;
                    ProgressBar.Visibility = Visibility.Visible;
                    StatusText.Text = "Checking Windows Security...";
                    CheckMark.Visibility = Visibility.Hidden;
                    ProgressBar.Start();
                }
                await Task.Delay(200);
            }
        }

        public void ShowDialog(Window owner, string playbookName)
        {
            Owner = owner;
            ToStartText.Text = "To start using the " + playbookName + " Playbook. Windows Security will otherwise interfere with the process.";
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
            CloseWindow(securityscale);
        }

        private void OpenSecurity(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("WINDOWSDEFENDER://threatsettings/");
            }
            catch (Exception)
            {
                MessageBox.Show(typeof(SecurityDialog), "Could not open Windows Defender settings. Please do it manually.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void ErasePolicies_OnClick(object sender, RoutedEventArgs e)
        {
            ((System.Windows.Controls.Button)sender).IsEnabled = false;
            LoadContainer.Visibility = Visibility.Visible;
            Spinner spinner = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            LoadContainer.Children.Add(spinner);
            await Task.Delay(new Random().Next(700, 1300));
            Exception result = await InterLink.ExecuteSafeAsync((Expression<Action>)(() => EraseDefenderPolicies()), false, -1);
            if (result != null)
            {
                MessageBox.Show(typeof(SecurityDialog), "Could not remove policy settings: " + result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ((System.Windows.Controls.Button)sender).IsEnabled = true;
                LoadContainer.Visibility = Visibility.Collapsed;
                LoadContainer.Children.Remove(spinner);
                return;
            }
            try
            {
                if ((await GUIUtil.GetDefenderToggles()).Any((bool x) => x))
                {
                    System.Diagnostics.Process kill = System.Diagnostics.Process.GetProcessesByName("SecHealthUI").FirstOrDefault();
                    if (kill != null)
                    {
                        kill.Kill();
                        await Task.Delay(900);
                        System.Diagnostics.Process.Start("WINDOWSDEFENDER://threatsettings/");
                    }
                }
            }
            catch (Exception)
            {
            }
            ((System.Windows.Controls.Button)sender).IsEnabled = true;
            LoadContainer.Visibility = Visibility.Collapsed;
            LoadContainer.Children.Remove(spinner);
            origList = await GUIUtil.GetDefenderToggles();
            SlideModule();
        }

        [InterprocessMethod(Level.Administrator)]
        private static void EraseDefenderPolicies()
        {
            int attempts = 0;
            while (attempts <= 10)
            {
                try
                {
                    Registry.LocalMachine.DeleteSubKeyTree("SOFTWARE\\Policies\\Microsoft\\Windows Defender");
                    return;
                }
                catch (Exception)
                {
                    if (attempts == 10)
                    {
                        throw;
                    }
                }
                attempts++;
                Thread.Sleep(200);
            }
            throw new UnexpectedException();
        }

        private async void HelpButton_OnClick(object sender, RoutedEventArgs e)
        {
            await Wrap.ExecuteSafeAsync(async delegate
            {
                RegistryKey choice = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\Shell\\Associations\\UrlAssociations\\https\\UserChoice");
                if (choice == null || (choice.GetValueNames().Contains("ProgId", StringComparer.InvariantCultureIgnoreCase) && ((string)choice.GetValue("ProgId")).Contains("Edge")))
                {
                    await InterLink.ExecuteSafeAsync((Expression<Action>)(() => ConfigureEdge()), false, -1);
                }
            }, default, false, null);
            try
            {
                System.Diagnostics.Process.Start("https://docs.ameliorated.io/guides/security-toggles.html");
            }
            catch (Exception)
            {
                MessageBox.Show(typeof(SecurityDialog), "Error opening link.", "Warning");
            }
            ImGoodText.Text = "Dismiss";
        }

        [InterprocessMethod(Level.Administrator)]
        private static void ConfigureEdge()
        {
            CoreActions.SafeRun(new RegistryValueAction
            {
                KeyName = "HKLM\\SOFTWARE\\Policies\\Microsoft\\Edge",
                Value = "HideFirstRunExperience",
                Data = 1,
                Type = (RegistryValueType)4
            }, false);
            CoreActions.SafeRun(new RegistryValueAction
            {
                KeyName = "HKLM\\SOFTWARE\\Policies\\Microsoft\\Edge",
                Value = "SyncDisabled",
                Data = 1,
                Type = (RegistryValueType)4
            }, false);
        }

        private void ImGoodButton_OnClick(object sender, RoutedEventArgs e)
        {
            SlideModule();
        }

        private async void SlideModule()
        {
            if (!animating)
            {
                animating = true;
                Storyboard storyboard = new Storyboard();
                DoubleAnimation opacityAnim = new DoubleAnimation
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 400)),
                    To = 0.0
                };
                DoubleAnimationUsingKeyFrames opacityAnimSteps = new DoubleAnimationUsingKeyFrames
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 440)),
                    KeyFrames =
                {
                    new LinearDoubleKeyFrame(0.4, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))),
                    new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 440)))
                }
                };
                ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 9.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                };
                ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = new Thickness(380.0, 9.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                ThicknessKeyFrame transitionKey3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(380.0, 9.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKey4 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, -96.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                transitionAnim.KeyFrames.Add(transitionKey1);
                transitionAnim.KeyFrames.Add(transitionKey2);
                transitionAnim.KeyFrames.Add(transitionKey3);
                transitionAnim.KeyFrames.Add(transitionKey4);
                ThicknessAnimationUsingKeyFrames transitionAnimStep = new ThicknessAnimationUsingKeyFrames
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKeyStep1 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                ThicknessKeyFrame transitionKeyStep2 = new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    Value = new Thickness(0.0, -105.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 440))
                };
                ThicknessKeyFrame transitionKeyStep3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, -105.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKeyStep4 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                transitionAnimStep.KeyFrames.Add(transitionKeyStep1);
                transitionAnimStep.KeyFrames.Add(transitionKeyStep2);
                transitionAnimStep.KeyFrames.Add(transitionKeyStep3);
                transitionAnimStep.KeyFrames.Add(transitionKeyStep4);
                Storyboard.SetTarget(opacityAnim, ModuleGrid);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
                Storyboard.SetTarget(opacityAnimSteps, Steps);
                Storyboard.SetTargetProperty(opacityAnimSteps, new PropertyPath("Opacity"));
                Storyboard.SetTarget(transitionAnim, ModuleGrid);
                Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
                Storyboard.SetTarget(transitionAnimStep, Steps);
                Storyboard.SetTargetProperty(transitionAnimStep, new PropertyPath("Margin"));
                storyboard.Children.Add(opacityAnim);
                storyboard.Children.Add(opacityAnimSteps);
                storyboard.Children.Add(transitionAnim);
                storyboard.Children.Add(transitionAnimStep);
                storyboard.Begin();
                await Task.Delay(540);
                PolicyGrid.Visibility = Visibility.Hidden;
                HelpGrid.Visibility = Visibility.Hidden;
                animating = false;
            }
        }
    }
}