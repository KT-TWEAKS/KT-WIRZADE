using Core.Actions;
using Interprocess;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Xml.Serialization;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.Utils;
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
    public partial class Shutup10 : Grid
    {
        public class Settings
        {
            [XmlType("Setting")]
            public class Setting
            {
                [XmlAttribute]
                public string Name { get; set; }
            }

            public Setting[] InitialState;
        }

        private bool _isLast;

        private bool forceReset;

        private Task resetTask = Task.CompletedTask;

        private bool animating;

        public static Dictionary<string, RegistryValueAction> FactoryDefaultValues;


        public static string FileLocation { get; set; } = null;

        public event EventHandler Completed;

        public bool IsUninstallable()
        {
            return false;
        }

        private void OnCompleted()
        {
            this.Completed?.Invoke(this, EventArgs.Empty);
        }

        public Shutup10()
        {
            InitializeComponent();
            if (string.IsNullOrEmpty(FileLocation))
            {
                RunAppButton.IsEnabled = false;
            }
            else
            {
                RunAppButton.IsEnabled = true;
            }
            if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
            {
                PageBackgroundBorder.SetResourceReference(BackgroundProperty, "FakePageBackgroundBrush");
            }
            ToStartText.Text = "To start using the " + ((Playbook)GlobalsGUI.Current.Playbook).Name + " Playbook. These tweaks cause conflicts with various modifications, along with some duplicated functionality.";
        }

        public static bool IsPresent()
        {
            InterLink.Execute((Expression<Action>)(() => ShutupController.ShutupRefresh()), false, -1);
            return InterLink.Execute<bool>((Expression<Func<bool>>)(() => ShutupController.ShutupCheckIfDefaultSettings()), false, -1);
        }

        private static void GetKeys()
        {
        }

        public void SetLast()
        {
            PageColumn.Height = 501.0;
            _isLast = true;
        }

        private void ModuleLocateButton_OnClick(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new();
            dialog.DefaultExt = ".exe";
            dialog.Filter = "Executables|*.exe";
            dialog.Multiselect = false;
            if (dialog.ShowDialog() == true)
            {
                FileLocation = dialog.FileName;
                SlideModule();
            }
        }

        private async Task ResetShutup()
        {
            Task task = InterLink.ExecuteAsync((Expression<Action>)(() => ShutupController.ShutupReset()), false, -1);
            await Task.Delay(3000);
            await task;
            forceReset = true;
        }

        private async void ModuleResetButton_OnClick(object sender, RoutedEventArgs e)
        {
            ModuleLocateButton.IsEnabled = false;
            ResetButton.IsEnabled = false;
            ModuleResetButton.IsEnabled = false;
            ModuleLoadContainer.Visibility = Visibility.Visible;
            Spinner spinner = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            ModuleLoadContainer.Children.Add(spinner);
            await ResetShutup();
            ModuleLoadContainer.Visibility = Visibility.Collapsed;
            ModuleLoadContainer.Children.Remove(spinner);
            SlideModule();
            Storyboard storyboard = new Storyboard();
            DoubleAnimation opacityAnimSteps = new DoubleAnimation
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200)),
                To = 0.4
            };
            Storyboard.SetTarget(opacityAnimSteps, Steps);
            Storyboard.SetTargetProperty(opacityAnimSteps, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityAnimSteps);
            storyboard.Begin();
        }

        private void RunAppButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                try
                {
                    System.Diagnostics.Process.Start(new ProcessStartInfo(FileLocation)
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
                KTWirzade.GUI.MessageBox.Show(typeof(TweaksDialog), "Error starting Shutup10++. Please do it manually.", "Warning");
            }
        }

        private async void ResetButton_OnClick(object sender, RoutedEventArgs e)
        {
            ((System.Windows.Controls.Button)sender).IsEnabled = false;
            RunAppButton.IsHitTestVisible = false;
            LoadContainer.Visibility = Visibility.Visible;
            Spinner spinner = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            LoadContainer.Children.Add(spinner);
            resetTask = ResetShutup();
            await resetTask;
            LoadContainer.Visibility = Visibility.Collapsed;
            LoadContainer.Children.Remove(spinner);
        }

        public async void StartOperations()
        {
            ProgressBar.Start();
            Storyboard board = new Storyboard();
            DoubleAnimationUsingKeyFrames cursorAnimX = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(6000.0)),
                BeginTime = TimeSpan.FromMilliseconds(500.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new LinearDoubleKeyFrame
                {
                    Value = -69.0,
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000.0))
                },
                new LinearDoubleKeyFrame
                {
                    Value = -69.0,
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1700.0))
                },
                new EasingDoubleKeyFrame
                {
                    Value = -65.0,
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2500.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                },
                new EasingDoubleKeyFrame
                {
                    Value = -65.0,
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3000.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                },
                new EasingDoubleKeyFrame
                {
                    Value = 90.0,
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(4300.0))
                },
                new LinearDoubleKeyFrame(90.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTargetName(cursorAnimX, "CursorTransform");
            Storyboard.SetTargetProperty(cursorAnimX, new PropertyPath(TranslateTransform.XProperty));
            board.Children.Add(cursorAnimX);
            DoubleAnimationUsingKeyFrames cursorAnimY = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(6000.0)),
                BeginTime = TimeSpan.FromMilliseconds(500.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    Value = -48.0,
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    }
                },
                new EasingDoubleKeyFrame
                {
                    Value = -48.0,
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1700.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    }
                },
                new EasingDoubleKeyFrame
                {
                    Value = 15.0,
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2500.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    }
                },
                new EasingDoubleKeyFrame
                {
                    Value = 15.0,
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3000.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                },
                new EasingDoubleKeyFrame
                {
                    Value = 139.0,
                    EasingFunction = new QuadraticEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(4300.0))
                },
                new LinearDoubleKeyFrame(139.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTargetName(cursorAnimY, "CursorTransform");
            Storyboard.SetTargetProperty(cursorAnimY, new PropertyPath(TranslateTransform.YProperty));
            board.Children.Add(cursorAnimY);
            DoubleAnimationUsingKeyFrames actionHoverAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(5020.0)),
                BeginTime = TimeSpan.FromMilliseconds(1480.0),
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
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1670.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1720.0)),
                    Value = 0.0
                },
                new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(ActionsHoverEffect.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(actionHoverAnim, ActionsHoverEffect);
            Storyboard.SetTargetProperty(actionHoverAnim, new PropertyPath("Opacity"));
            board.Children.Add(actionHoverAnim);
            DoubleAnimationUsingKeyFrames dropdownFadeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(4600.0)),
                BeginTime = TimeSpan.FromMilliseconds(1900.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1250.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1350.0)),
                    Value = 0.0
                },
                new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(Shutup10Dropdown.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(dropdownFadeAnim, Shutup10DropdownGrid);
            Storyboard.SetTargetProperty(dropdownFadeAnim, new PropertyPath("Opacity"));
            board.Children.Add(dropdownFadeAnim);
            DoubleAnimationUsingKeyFrames revertHoverAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(3700.0)),
                BeginTime = TimeSpan.FromMilliseconds(2800.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(20.0)),
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = 1.0
                },
                new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(UndoHoverEffect.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(revertHoverAnim, UndoHoverEffect);
            Storyboard.SetTargetProperty(revertHoverAnim, new PropertyPath("Opacity"));
            board.Children.Add(revertHoverAnim);
            DoubleAnimationUsingKeyFrames modelFadeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(3300.0)),
                BeginTime = TimeSpan.FromMilliseconds(3200.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)),
                    Value = 1.0
                },
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
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
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2100.0)),
                    Value = 0.0
                },
                new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(Shutup10Dropdown.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(modelFadeAnim, Shutup10ModelGrid);
            Storyboard.SetTargetProperty(modelFadeAnim, new PropertyPath("Opacity"));
            board.Children.Add(modelFadeAnim);
            DoubleAnimationUsingKeyFrames yesNoFadeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(3300.0)),
                BeginTime = TimeSpan.FromMilliseconds(3200.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)),
                    Value = 1.0
                },
                new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(Shutup10Dropdown.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(yesNoFadeAnim, Shutup10YesNo);
            Storyboard.SetTargetProperty(yesNoFadeAnim, new PropertyPath("Opacity"));
            board.Children.Add(yesNoFadeAnim);
            DoubleAnimationUsingKeyFrames yesNoHoverFadeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(1880.0)),
                BeginTime = TimeSpan.FromMilliseconds(4620.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0.0)),
                    Value = 1.0
                },
                new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(Shutup10Dropdown.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(yesNoHoverFadeAnim, Shutup10YesNoHover);
            Storyboard.SetTargetProperty(yesNoHoverFadeAnim, new PropertyPath("Opacity"));
            board.Children.Add(yesNoHoverFadeAnim);
            DoubleAnimationUsingKeyFrames togglesAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(1250.0)),
                BeginTime = TimeSpan.FromMilliseconds(5250.0),
                KeyFrames = new DoubleKeyFrameCollection
            {
                new EasingDoubleKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseIn
                    },
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0.0)),
                    Value = 1.0
                },
                new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)),
                new LinearDoubleKeyFrame(Shutup10Dropdown.Opacity, KeyTime.FromPercent(1.0))
            }
            };
            Storyboard.SetTarget(togglesAnim, Shutup10Toggled);
            Storyboard.SetTargetProperty(togglesAnim, new PropertyPath("Opacity"));
            board.Children.Add(togglesAnim);
            board.RepeatBehavior = RepeatBehavior.Forever;
            board.Begin(this, isControllable: true);
            bool moduleOpen = false;
            while (base.IsLoaded)
            {
                bool num = await InterLink.ExecuteAsync<bool>((Expression<Func<bool>>)(() => ShutupController.ShutupCheckIfDefaultSettings()), false, -1);
                if (string.IsNullOrEmpty(FileLocation))
                {
                    RunAppButton.IsEnabled = false;
                }
                else
                {
                    RunAppButton.IsEnabled = true;
                }
                if (!num || forceReset)
                {
                    await InterLink.ExecuteAsync((Expression<Action>)(() => ShutupController.ShutupKill()), false, -1);
                    await ProgressBar.WaitForAnimation();
                    if (NotFoundGrid.Visibility == Visibility.Visible)
                    {
                        SlideModule();
                    }
                    RunAppButton.IsHitTestVisible = false;
                    ResetButton.IsHitTestVisible = false;
                    StatusText.Text = (forceReset ? "Shutup10 has been reset" : "Shutup10 has been reset");
                    FinishText.Text = (_isLast ? "This window can now be closed" : "Click the button below to continue");
                    ProgressBar.Visibility = Visibility.Collapsed;
                    FinishText.Visibility = Visibility.Visible;
                    CheckmarkIcon.Visibility = Visibility.Visible;
                    board.Stop(this);
                    DoubleAnimation opacityAnimSteps = new DoubleAnimation();
                    opacityAnimSteps.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
                    opacityAnimSteps.To = 0.4;
                    Steps.BeginAnimation(UIElement.OpacityProperty, opacityAnimSteps);
                    OnCompleted();
                    break;
                }
                if (FileLocation == null && !moduleOpen)
                {
                    moduleOpen = true;
                    await Task.Delay(1500);
                    Storyboard storyboard = new Storyboard();
                    DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames
                    {
                        Duration = new Duration(new TimeSpan(0, 0, 0, 0, 240)),
                        KeyFrames =
                    {
                        (DoubleKeyFrame)new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 100))),
                        (DoubleKeyFrame)new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240)))
                    }
                    };
                    DoubleAnimation opacityAnimSteps2 = new DoubleAnimation
                    {
                        Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200)),
                        To = 0.4
                    };
                    ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
                    {
                        Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
                    };
                    ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(0.0, -106.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                    };
                    ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                    };
                    ThicknessKeyFrame transitionKey3 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                    };
                    ThicknessKeyFrame transitionKey4 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(0.0, 0.0, 0.0, 0.0),
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
                        Value = new Thickness(0.0, -106.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 440))
                    };
                    ThicknessKeyFrame transitionKeyStep3 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(0.0, -106.0, 0.0, 0.0),
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
                    Storyboard.SetTarget(opacityAnimSteps2, Steps);
                    Storyboard.SetTargetProperty(opacityAnimSteps2, new PropertyPath("Opacity"));
                    Storyboard.SetTarget(transitionAnim, ModuleGrid);
                    Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
                    storyboard.Children.Add(opacityAnim);
                    storyboard.Children.Add(opacityAnimSteps2);
                    storyboard.Children.Add(transitionAnim);
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
                    Storyboard.SetTarget(scale_x, notfoundtransform);
                    Storyboard.SetTargetProperty(scale_x, new PropertyPath(ScaleTransform.ScaleXProperty));
                    storyboard.Children.Add(scale_x);
                    Storyboard.SetTarget(scale_y, notfoundtransform);
                    Storyboard.SetTargetProperty(scale_y, new PropertyPath(ScaleTransform.ScaleXProperty));
                    storyboard.Children.Add(scale_y);
                    NotFoundGrid.Visibility = Visibility.Visible;
                    ResetButton.IsEnabled = false;
                    RunAppButton.IsEnabled = false;
                    storyboard.Begin();
                }
            }
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
                    (DoubleKeyFrame)new LinearDoubleKeyFrame(0.4, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))),
                    (DoubleKeyFrame)new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 440)))
                }
                };
                ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                };
                ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
                {
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Value = new Thickness(380.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                };
                ThicknessKeyFrame transitionKey3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(380.0, 0.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                };
                ThicknessKeyFrame transitionKey4 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, -106.0, 0.0, 0.0),
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
                    Value = new Thickness(0.0, -106.0, 0.0, 0.0),
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 440))
                };
                ThicknessKeyFrame transitionKeyStep3 = new LinearThicknessKeyFrame
                {
                    Value = new Thickness(0.0, -106.0, 0.0, 0.0),
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
                Storyboard.SetTarget(transitionAnimStep, StepsProgressContainer);
                Storyboard.SetTargetProperty(transitionAnimStep, new PropertyPath("Margin"));
                storyboard.Children.Add(opacityAnim);
                storyboard.Children.Add(opacityAnimSteps);
                storyboard.Children.Add(transitionAnim);
                storyboard.Children.Add(transitionAnimStep);
                storyboard.Begin();
                await Task.Delay(540);
                NotFoundGrid.Visibility = Visibility.Hidden;
                ResetButton.IsEnabled = true;
                RunAppButton.IsEnabled = true;
                animating = false;
            }
        }

        [InterprocessMethod(Level.Administrator)]
        public static string GetExePath()
        {
            try
            {
                RegistryKey settingsKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\bam\\State\\UserSettings");
                string[] subKeyNames = settingsKey.GetSubKeyNames();
                foreach (string settingKey in subKeyNames)
                {
                    string realPath = DevicePathMapper.FromDevicePath(settingsKey.OpenSubKey(settingKey)?.GetValueNames().FirstOrDefault((string x) => x.Split('\\').LastOrDefault() != null && x.Split('\\').Last().StartsWith("OOSU") && x.Split('\\').Last().EndsWith(".exe")));
                    if (File.Exists(realPath))
                    {
                        settingsKey.Close();
                        return realPath;
                    }
                }
                settingsKey.Close();
            }
            catch
            {
            }
            try
            {
                RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Compatibility Assistant\\Store");
                string path = registryKey?.GetValueNames().FirstOrDefault((string x) => x.Split('\\').LastOrDefault() != null && x.Split('\\').Last().StartsWith("OOSU") && x.Split('\\').Last().EndsWith(".exe"));
                registryKey?.Close();
                if (path != null && File.Exists(path))
                {
                    return path;
                }
            }
            catch
            {
            }
            string[] ignoreUsers = new string[5] { "Default User", "Public", "All Users", "Public", "Default" };
            try
            {
                foreach (string user in from x in Directory.GetDirectories(Environment.ExpandEnvironmentVariables("%SYSTEMDRIVE%\\Users"))
                                        where !ignoreUsers.Contains(x)
                                        select x)
                {
                    string[] subKeyNames = new string[3] { "Downloads", "Documents", "Desktop" };
                    foreach (string dir in subKeyNames)
                    {
                        string path2 = Directory.GetFiles(user + "\\" + dir, "OOSU*.exe").FirstOrDefault();
                        if (path2 != null)
                        {
                            return path2;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }
    }
}
