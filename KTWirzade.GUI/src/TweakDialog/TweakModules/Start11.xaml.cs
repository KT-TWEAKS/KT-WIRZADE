using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using KTWirzade.GUI.Utils;
using KTWirzade.GUI.Views;
using KTWirzade.GUI.Windows;
using KTWirzade.Shared;
using static Core.Win32;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.TweakDialog.TweakModules
{
    public partial class Start11 : Grid
    {
        private bool _isLast;

        private System.Diagnostics.Process UninstallProcess;

        public static RequirementsPageView.UninstallKey UninstallKey { get; set; }

        public event EventHandler Completed;

        public bool IsUninstallable()
        {
            return true;
        }

        private void OnCompleted()
        {
            this.Completed?.Invoke(this, EventArgs.Empty);
        }

        public Start11()
        {
            InitializeComponent();
            if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
            {
                PageBackgroundBorder.SetResourceReference(BackgroundProperty, "FakePageBackgroundBrush");
            }
            ToStartText.Text = "To start using the " + ((Playbook)GlobalsGUI.Current.Playbook).Name + " Playbook. These tweaks cause conflicts with various modifications, along with some duplicated functionality.";
        }

        public static bool IsPresent()
        {
            throw new NotImplementedException();
        }

        private static void GetKeys()
        {
        }

        public void SetLast()
        {
            PageColumn.Height = 501.0;
            _isLast = true;
        }

        public async void StartOperations()
        {
            ProgressBar.Start();
            Storyboard board = new Storyboard();
            ThicknessAnimationUsingKeyFrames pageScrollAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(8300.0)),
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new LinearThicknessKeyFrame(Scroller.Margin, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700.0))),
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1050.0)),
                    Value = new Thickness(0.0, -50.0, 0.0, 0.0)
                },
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1550.0)),
                    Value = new Thickness(0.0, -50.0, 43.0, 0.0)
                },
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1900.0)),
                    Value = new Thickness(0.0, -100.0, 43.0, 0.0)
                },
                new LinearThicknessKeyFrame(new Thickness(0.0, -100.0, 43.0, 0.0), KeyTime.FromPercent(1.0)),
                new LinearThicknessKeyFrame(Scroller.Margin, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(pageScrollAnim, Scroller);
            Storyboard.SetTargetProperty(pageScrollAnim, new PropertyPath("Margin"));
            board.Children.Add(pageScrollAnim);
            ThicknessAnimationUsingKeyFrames scrollBarAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(8300.0)),
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new LinearThicknessKeyFrame(AppsScrollBar.Margin, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700.0))),
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1050.0)),
                    Value = new Thickness(0.0, 61.0, 43.0, 0.0)
                },
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1550.0)),
                    Value = new Thickness(0.0, 61.0, 43.0, 0.0)
                },
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1900.0)),
                    Value = new Thickness(0.0, 71.0, 43.0, 0.0)
                },
                new LinearThicknessKeyFrame(new Thickness(0.0, 71.0, 43.0, 0.0), KeyTime.FromPercent(1.0)),
                new LinearThicknessKeyFrame(AppsScrollBar.Margin, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(scrollBarAnim, AppsScrollBar);
            Storyboard.SetTargetProperty(scrollBarAnim, new PropertyPath("Margin"));
            board.Children.Add(scrollBarAnim);
            ThicknessAnimationUsingKeyFrames cursorAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(5900.0)),
                BeginTime = TimeSpan.FromMilliseconds(2400.0),
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(297.0, 258.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500.0)),
                    EasingFunction = new ExponentialEase
                    {
                        Exponent = 1.5,
                        EasingMode = EasingMode.EaseIn
                    }
                },
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(328.0, 257.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                },
                new LinearThicknessKeyFrame
                {
                    Value = new Thickness(328.0, 257.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2000.0))
                },
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(322.0, 277.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2400.0)),
                    EasingFunction = new ExponentialEase
                    {
                        Exponent = 1.5,
                        EasingMode = EasingMode.EaseIn
                    }
                },
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(310.0, 286.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2800.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                },
                new LinearThicknessKeyFrame
                {
                    Value = new Thickness(310.0, 286.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(4200.0))
                },
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(310.0, 293.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(4400.0)),
                    EasingFunction = new ExponentialEase
                    {
                        Exponent = 1.5,
                        EasingMode = EasingMode.EaseIn
                    }
                },
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(310.0, 300.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(4600.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                },
                new LinearThicknessKeyFrame(new Thickness(310.0, 300.0, 0.0, 0.0), KeyTime.FromPercent(1.0)),
                new LinearThicknessKeyFrame(MouseCursor.Margin, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(cursorAnim, MouseCursor);
            Storyboard.SetTargetProperty(cursorAnim, new PropertyPath("Margin"));
            board.Children.Add(cursorAnim);
            DoubleAnimationUsingKeyFrames dotsHoverAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(5100.0)),
                BeginTime = TimeSpan.FromMilliseconds(3200.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400.0)),
                    Value = 0.5
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500.0)),
                    Value = 1.0
                },
                new LinearDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(800.0)),
                    Value = 0.0
                },
                new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(DotsHoverEffect.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(dotsHoverAnim, DotsHoverEffect);
            Storyboard.SetTargetProperty(dotsHoverAnim, new PropertyPath("Opacity"));
            board.Children.Add(dotsHoverAnim);
            ThicknessAnimationUsingKeyFrames modifyDropdownAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(4700.0)),
                BeginTime = TimeSpan.FromMilliseconds(3600.0),
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250.0)),
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0)
                },
                new LinearThicknessKeyFrame(new Thickness(0.0, 0.0, 0.0, 0.0), KeyTime.FromPercent(1.0)),
                new LinearThicknessKeyFrame(SettingsAppsModifyBox.Margin, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(modifyDropdownAnim, SettingsAppsModifyBox);
            Storyboard.SetTargetProperty(modifyDropdownAnim, new PropertyPath("Margin"));
            board.Children.Add(modifyDropdownAnim);
            DoubleAnimationUsingKeyFrames modifyFadeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(4700.0)),
                BeginTime = TimeSpan.FromMilliseconds(3600.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)),
                    Value = 1.0
                },
                new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(SettingsAppsModifyBox.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(modifyFadeAnim, SettingsAppsModifyBox);
            Storyboard.SetTargetProperty(modifyFadeAnim, new PropertyPath("Opacity"));
            board.Children.Add(modifyFadeAnim);
            DoubleAnimationUsingKeyFrames modifyShadowAnim = new DoubleAnimationUsingKeyFrames
            {
                BeginTime = TimeSpan.FromMilliseconds(4150.0),
                Duration = new Duration(TimeSpan.FromMilliseconds(4150.0)),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 0.16,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 180))
                },
                new LinearDoubleKeyFrame(0.16, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(((DropShadowEffect)AppsModifyShadowBorder.Effect).Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(modifyShadowAnim, AppsModifyShadowBorder);
            Storyboard.SetTargetProperty(modifyShadowAnim, new PropertyPath("(Effect).Opacity"));
            board.Children.Add(modifyShadowAnim);
            DoubleAnimationUsingKeyFrames uninstallHoverAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(3400.0)),
                BeginTime = TimeSpan.FromMilliseconds(4900.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)),
                    Value = 1.0
                },
                new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(UninstallHoverEffect.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallHoverAnim, UninstallHoverEffect);
            Storyboard.SetTargetProperty(uninstallHoverAnim, new PropertyPath("Opacity"));
            board.Children.Add(uninstallHoverAnim);
            DoubleAnimationUsingKeyFrames modifyFadeOutAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(2700.0)),
                BeginTime = TimeSpan.FromMilliseconds(5700.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)),
                    Value = 0.0
                }
            }
            };
            Storyboard.SetTarget(modifyFadeOutAnim, AppsModifyShadowBorder);
            Storyboard.SetTargetProperty(modifyFadeOutAnim, new PropertyPath("Opacity"));
            board.Children.Add(modifyFadeOutAnim);
            ThicknessAnimationUsingKeyFrames uninstallDropdownAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(2350.0)),
                BeginTime = TimeSpan.FromMilliseconds(5950.0),
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300.0)),
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0)
                },
                new LinearThicknessKeyFrame(new Thickness(0.0, 0.0, 0.0, 0.0), KeyTime.FromPercent(1.0)),
                new LinearThicknessKeyFrame(SettingsAppsUninstallBox.Margin, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallDropdownAnim, SettingsAppsUninstallBox);
            Storyboard.SetTargetProperty(uninstallDropdownAnim, new PropertyPath("Margin"));
            board.Children.Add(uninstallDropdownAnim);
            DoubleAnimationUsingKeyFrames uninstallFadeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(2350.0)),
                BeginTime = TimeSpan.FromMilliseconds(5950.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)),
                    Value = 1.0
                },
                new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(SettingsAppsUninstallBox.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallFadeAnim, SettingsAppsUninstallBox);
            Storyboard.SetTargetProperty(uninstallFadeAnim, new PropertyPath("Opacity"));
            board.Children.Add(uninstallFadeAnim);
            DoubleAnimationUsingKeyFrames uninstallShadowAnim = new DoubleAnimationUsingKeyFrames
            {
                BeginTime = TimeSpan.FromMilliseconds(5500.0),
                Duration = new Duration(TimeSpan.FromMilliseconds(2800.0)),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = 0.16,
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 180))
                },
                new LinearDoubleKeyFrame(0.16, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(((DropShadowEffect)AppsUninstallShadowBorder.Effect).Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallShadowAnim, AppsUninstallShadowBorder);
            Storyboard.SetTargetProperty(uninstallShadowAnim, new PropertyPath("(Effect).Opacity"));
            board.Children.Add(uninstallShadowAnim);
            DoubleAnimationUsingKeyFrames uninstallButtonHoverAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(1900.0)),
                BeginTime = TimeSpan.FromMilliseconds(6800.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180.0)),
                    Value = 0.6
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650.0)),
                    Value = 0.6
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(750.0)),
                    Value = 0.1
                },
                new LinearDoubleKeyFrame(0.2, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(UninstallButtonHoverEffect.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallButtonHoverAnim, UninstallButtonHoverEffect);
            Storyboard.SetTargetProperty(uninstallButtonHoverAnim, new PropertyPath("Opacity"));
            board.Children.Add(uninstallButtonHoverAnim);
            DoubleAnimationUsingKeyFrames uninstallFadeOutAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(800.0)),
                BeginTime = TimeSpan.FromMilliseconds(7500.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)),
                    Value = 0.0
                }
            }
            };
            Storyboard.SetTarget(uninstallFadeOutAnim, SettingsAppsUninstallBox);
            Storyboard.SetTargetProperty(uninstallFadeOutAnim, new PropertyPath("Opacity"));
            board.Children.Add(uninstallFadeOutAnim);
            board.RepeatBehavior = RepeatBehavior.Forever;
            board.Begin();
            while (base.IsLoaded)
            {
                if (!(await Task.Run(() => (!UninstallKey.HKCU) ? (Registry.LocalMachine.OpenSubKey(UninstallKey.RootKey) == null) : (Registry.CurrentUser.OpenSubKey(UninstallKey.RootKey) == null))))
                {
                    continue;
                }
                try
                {
                    if (UninstallProcess != null && !UninstallProcess.HasExited)
                    {
                        await WaitForExitAsync(UninstallProcess, new CancellationTokenSource(500000).Token);
                    }
                }
                catch (Exception)
                {
                }
                await ProgressBar.WaitForAnimation();
                StatusText.Text = "Start11 has been uninstalled";
                FinishText.Text = (_isLast ? "This window can now be closed" : "Click the button below to continue");
                UninstallerButton.IsEnabled = false;
                OpenAppsButton.IsHitTestVisible = false;
                ProgressBar.Visibility = Visibility.Collapsed;
                FinishText.Visibility = Visibility.Visible;
                CheckmarkIcon.Visibility = Visibility.Visible;
                board.Stop();
                DoubleAnimation opacityAnimSteps = new DoubleAnimation();
                opacityAnimSteps.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
                opacityAnimSteps.To = 0.4;
                Steps.BeginAnimation(UIElement.OpacityProperty, opacityAnimSteps);
                OnCompleted();
                break;
            }
        }

        public static Task WaitForExitAsync(System.Diagnostics.Process process, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (process.HasExited)
            {
                return Task.CompletedTask;
            }
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += delegate
            {
                tcs.TrySetResult(null);
            };
            if (cancellationToken != default(CancellationToken))
            {
                cancellationToken.Register(delegate
                {
                    tcs.SetCanceled();
                });
            }
            if (!process.HasExited)
            {
                return tcs.Task;
            }
            return Task.CompletedTask;
        }

        private void OpenApps(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("ms-settings:appsfeatures");
            }
            catch (Exception)
            {
                MessageBox.Show(typeof(TweaksDialog), "Could not open installed apps. Please do it manually.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void UninstallerButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string exe = UninstallKey.UninstallString;
                if (!exe.StartsWith("\"") && !File.Exists(exe.Split(' ').FirstOrDefault()))
                {
                    exe = exe.Substring(0, UninstallKey.UninstallString.IndexOf('\\', 0));
                    do
                    {
                        int length1 = UninstallKey.UninstallString.IndexOf('\\', exe.Length + 1);
                        int length2 = UninstallKey.UninstallString.IndexOf(' ', exe.Length + 1);
                        int length3 = Math.Min(length1, length2);
                        if (length1 == -1)
                        {
                            length3 = length2;
                        }
                        if (length2 == -1)
                        {
                            length3 = length1;
                        }
                        exe = UninstallKey.UninstallString.Substring(0, length3);
                    }
                    while (!File.Exists(exe));
                }
                else if (exe.StartsWith("\""))
                {
                    exe = exe.Substring(1, exe.IndexOf('"', 1) - 1);
                }
                else
                {
                    exe = exe.Split(' ').FirstOrDefault();
                    if (exe == null)
                    {
                        exe = UninstallKey.UninstallString;
                    }
                }
                try
                {
                    UninstallProcess = System.Diagnostics.Process.Start(new ProcessStartInfo(exe, UninstallKey.UninstallString.Substring(Math.Min(UninstallKey.UninstallString.StartsWith("\"") ? (exe.Length + 2) : exe.Length, UninstallKey.UninstallString.Length)).TrimStart(' '))
                    {
                        Verb = "RunAs",
                        UseShellExecute = true
                    });
                }
                catch (Win32Exception)
                {
                }
            }
            catch (Exception)
            {
                MessageBox.Show(typeof(TweaksDialog), "Error starting uninstaller. Please do it manually.", "Warning");
            }
        }
    }
}