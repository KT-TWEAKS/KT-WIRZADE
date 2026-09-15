using Core;
using Interprocess;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using KTWirzade.GUI.Utils;
using KTWirzade.GUI.Windows;
using WmiLight;
using static Core.Win32;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.UninstallUpdateDialog
{
    public partial class Default : Grid
    {

        public uint UpdateID;

        public string UpdateDescription;

        private bool _isLast;

        public event EventHandler Completed;

        private void OnCompleted()
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }

        public Default()
        {
            InitializeComponent();
            if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
            {
                PageBackgroundBorder.SetResourceReference(BackgroundProperty, "FakePageBackgroundBrush");
            }
            Loaded += delegate
            {
                ToStartText.Text = "To start using this Playbook. " + (UpdateDescription ?? "This update causes issues with the system and should be uninstalled.");
                TitleText.Text = $"Uninstall KB{UpdateID}";
            };
        }

        public static bool IsPresent()
        {
            throw new NotImplementedException();
        }

        private static void GetKeys()
        {
        }

        public new int Height()
        {
            if (!_isLast)
            {
                return 485;
            }
            return 501;
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
            ThicknessAnimationUsingKeyFrames cursorAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(2000.0)),
                BeginTime = TimeSpan.FromMilliseconds(2000.0),
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(248.0, 187.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650.0)),
                    EasingFunction = new ExponentialEase
                    {
                        Exponent = 1.5,
                        EasingMode = EasingMode.EaseOut
                    }
                },
                new EasingThicknessKeyFrame
                {
                    Value = new Thickness(248.0, 187.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromPercent(1.0),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                },
                new LinearThicknessKeyFrame(MouseCursor.Margin, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(cursorAnim, MouseCursor);
            Storyboard.SetTargetProperty(cursorAnim, new PropertyPath("Margin"));
            board.Children.Add(cursorAnim);
            ThicknessAnimationUsingKeyFrames searchingAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(4000.0)),
                BeginTime = TimeSpan.FromMilliseconds(0.0),
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new LinearThicknessKeyFrame
                {
                    Value = new Thickness(198.0, 100.75, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(4000.0))
                },
                new LinearThicknessKeyFrame(WusaSearchingIndicator.Margin, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(searchingAnim, WusaSearchingIndicator);
            Storyboard.SetTargetProperty(searchingAnim, new PropertyPath("Margin"));
            board.Children.Add(searchingAnim);
            DoubleAnimationUsingKeyFrames uninstallOpenAnimX = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(2750.0)),
                BeginTime = TimeSpan.FromMilliseconds(1250.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2000.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2250.0)),
                    Value = 0.9
                },
                new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(WusaUninstallPromptScaleTransform.ScaleX, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallOpenAnimX, WusaUninstallPrompt);
            Storyboard.SetTargetProperty(uninstallOpenAnimX, new PropertyPath("RenderTransform.ScaleX"));
            board.Children.Add(uninstallOpenAnimX);
            DoubleAnimationUsingKeyFrames uninstallOpenAnimY = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(2750.0)),
                BeginTime = TimeSpan.FromMilliseconds(1250.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2000.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2250.0)),
                    Value = 0.9
                },
                new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(WusaUninstallPromptScaleTransform.ScaleY, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallOpenAnimY, WusaUninstallPrompt);
            Storyboard.SetTargetProperty(uninstallOpenAnimY, new PropertyPath("RenderTransform.ScaleY"));
            board.Children.Add(uninstallOpenAnimY);
            DoubleAnimationUsingKeyFrames uninstallOpenAnimOpacity = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(2750.0)),
                BeginTime = TimeSpan.FromMilliseconds(1250.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2000.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2250.0)),
                    Value = 0.0
                },
                new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(WusaUninstallPrompt.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallOpenAnimOpacity, WusaUninstallPrompt);
            Storyboard.SetTargetProperty(uninstallOpenAnimOpacity, new PropertyPath("Opacity"));
            board.Children.Add(uninstallOpenAnimOpacity);
            DoubleAnimationUsingKeyFrames uninstallHoverAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(1550.0)),
                BeginTime = TimeSpan.FromMilliseconds(2450.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(800.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900.0)),
                    Value = 0.0
                },
                new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(YesButtonHoverEffect.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallHoverAnim, YesButtonHoverEffect);
            Storyboard.SetTargetProperty(uninstallHoverAnim, new PropertyPath("Opacity"));
            board.Children.Add(uninstallHoverAnim);
            ObjectAnimationUsingKeyFrames searchingVisibilityAnim = new ObjectAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(750.0)),
                BeginTime = TimeSpan.FromMilliseconds(3250.0),
                KeyFrames = new ObjectKeyFrameCollection
            {
                new DiscreteObjectKeyFrame(Visibility.Hidden, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0.0))),
                new DiscreteObjectKeyFrame(Visibility.Visible, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(searchingVisibilityAnim, WusaSearching);
            Storyboard.SetTargetProperty(searchingVisibilityAnim, new PropertyPath("Visibility"));
            board.Children.Add(searchingVisibilityAnim);
            ObjectAnimationUsingKeyFrames uninstallingVisibilityAnim = new ObjectAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(750.0)),
                BeginTime = TimeSpan.FromMilliseconds(3250.0),
                KeyFrames = new ObjectKeyFrameCollection
            {
                new DiscreteObjectKeyFrame(Visibility.Visible, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0.0))),
                new DiscreteObjectKeyFrame(Visibility.Hidden, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(uninstallingVisibilityAnim, WusaUninstalling);
            Storyboard.SetTargetProperty(uninstallingVisibilityAnim, new PropertyPath("Visibility"));
            board.Children.Add(uninstallingVisibilityAnim);
            board.RepeatBehavior = RepeatBehavior.Forever;
            board.Begin();
            List<uint> ids = new List<uint>();
            while (IsLoaded)
            {
                if (await Task.Run(delegate
                {
                    Thread.Sleep(4000);
                    WmiConnection val = new WmiConnection();
                    try
                    {
                        ids.Clear();
                        foreach (WmiObject current in WmiConnectionExtensions.CreateQuery(val, "SELECT * FROM Win32_QuickFixEngineering"))
                        {
                            try
                            {
                                string text = current.GetPropertyValue<string>("HotFixID").Replace("KB", "");
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    uint item = uint.Parse(text);
                                    ids.Add(item);
                                }
                            }
                            finally
                            {
                                current.Dispose();
                            }
                        }
                    }
                    finally
                    {
                        ((IDisposable)val)?.Dispose();
                    }
                    return !ids.Contains(UpdateID) || UninstallUpdatesDialog.BypassedUpdates.Contains(UpdateID);
                }))
                {
                    foreach (uint id in ids)
                    {
                        if (UninstallUpdatesDialog.ExcludedUpdates.Any((KeyValuePair<uint, string> x) => x.Key == id) && !UninstallUpdatesDialog.PresentUninstallUpdates.Any((KeyValuePair<uint, string> x) => x.Key == id))
                        {
                            UninstallUpdatesDialog.PresentUninstallUpdates.Add(UninstallUpdatesDialog.ExcludedUpdates.First((KeyValuePair<uint, string> x) => x.Key == id));
                        }
                    }
                    bool isLast = UninstallUpdatesDialog.PresentUninstallUpdates.FindIndex((KeyValuePair<uint, string> x) => x.Key == UpdateID) == UninstallUpdatesDialog.PresentUninstallUpdates.Count - 1;
                    await ProgressBar.WaitForAnimation();
                    StatusText.Text = "Update has been uninstalled";
                    FinishText.Text = (isLast ? "This window can now be closed" : "Click the button below to continue");
                    UninstallButton.IsEnabled = false;
                    BypassButton.IsHitTestVisible = false;
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
                await Task.Delay(500);
            }
        }

        public static Task WaitForExitAsync(System.Diagnostics.Process process, CancellationToken cancellationToken = default)
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

        private async void UninstallClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                await InterLink.ExecuteAsync((Expression<Action>)(() => RunWusaAdmin(UpdateID)), false, -1);
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe((LogType)1, ex, Array.Empty<(string, object)>());
                MessageBox.Show(typeof(UninstallUpdatesDialog), "Could not uninstall update. Please do it manually or select Bypass.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [InterprocessMethod(Level.Administrator)]
        public static void RunWusaAdmin(uint updateID)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo("wusa.exe", $"/uninstall /kb:{updateID} /norestart")
            {
                UseShellExecute = true
            });
        }

        private async void BypassButton_OnClick(object sender, RoutedEventArgs e)
        {
            UninstallUpdatesDialog.BypassedUpdates.Add(UpdateID);
            BypassButton.IsEnabled = false;
            UninstallButton.IsEnabled = false;
        }
    }
}