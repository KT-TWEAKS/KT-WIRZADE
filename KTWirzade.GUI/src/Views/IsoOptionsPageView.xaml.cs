using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KTWirzade.GUI.Pages.IsoModePage;
using KTWirzade.GUI.ViewModels;
using static KTWirzade.Shared.Playbook;
using static KTWirzade.Shared.Playbook.CheckboxPage;
using static KTWirzade.Shared.Playbook.FeaturePage;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace KTWirzade.GUI.Views
{
    public partial class IsoOptionsPageView : System.Windows.Controls.UserControl
    {
        public struct RECT
        {
            public int Left;

            public int Top;

            public int Right;

            public int Bottom;
        }

        private List<string> defaultOptions;


        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32")]
        private static extern int SetWindowPos(IntPtr hWnd, int hwndInsertAfter, int x, int y, int cx, int cy, int wFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public IsoOptionsPageView()
        {
            InitializeComponent();
            OptionsPane.FinishAction = async delegate (List<string> list)
            {
                OptionsPopup.IsOpen = false;
                bool noDifference = defaultOptions.SequenceEqual(list);
                GlobalsGUI.Current.ISO.WriteOptions = list;
                if (DefaultBox.Visibility == Visibility.Visible && !noDifference)
                {
                    await SlideModuleDown();
                }
                if (!defaultOptions.SequenceEqual(GlobalsGUI.Current.ISO.WriteOptions))
                {
                    DefaultBox.Visibility = Visibility.Visible;
                    ResetButton.Visibility = Visibility.Visible;
                    if (!GlobalsGUI.Current.ISO.WriteOptions.Contains("TPMCheck") && !GlobalsGUI.Current.ISO.WriteOptions.Contains("InternetCheck"))
                    {
                        DefaultText.Text = "TPM and Internet requirements removed";
                    }
                    else if (!GlobalsGUI.Current.ISO.WriteOptions.Contains("RAMCPUCheck") && !GlobalsGUI.Current.ISO.WriteOptions.Contains("InternetCheck"))
                    {
                        DefaultText.Text = "CPU and Internet requirements removed";
                    }
                    else if (!GlobalsGUI.Current.ISO.WriteOptions.Contains("InternetCheck"))
                    {
                        DefaultText.Text = "Internet requirement removed";
                    }
                    else if (!GlobalsGUI.Current.ISO.WriteOptions.Contains("RAMCPUCheck"))
                    {
                        DefaultText.Text = "Hardware requirements removed";
                    }
                    else if (!GlobalsGUI.Current.ISO.WriteOptions.Contains("BitLocker"))
                    {
                        DefaultText.Text = "BitLocker drive encryption disabled";
                    }
                }
                if (!noDifference)
                {
                    SlideModuleUp();
                }
            };
            OptionsPane.CancelAction = delegate
            {
                OptionsPopup.IsOpen = false;
            };
            defaultOptions = new List<string>();
            if (GlobalsGUI.Current.ISO.Configuration == null || GlobalsGUI.Current.ISO.Configuration.InternetRequired)
            {
                defaultOptions.Add("InternetCheck");
            }
            defaultOptions.Add("BitLocker");
            defaultOptions.Add("TPMCheck");
            defaultOptions.Add("RAMCPUCheck");
            base.DataContextChanged += delegate
            {
                if (!(base.DataContext is ViewModelBase viewModelBase))
                {
                    return;
                }
                DefaultBox.Visibility = Visibility.Hidden;
                CustomFeaturesHeader.Text = "Select Options";
                CustomFeaturesDescriptor.Text = "Modify installation parameters";
                FeaturesButtonText.Text = "Manage image";
                viewModelBase.MainNextButtonCommand = new GlobalsGUI.CommandHandler(Next, () => true);
                FeaturesBox.IsHitTestVisible = GlobalsGUI.Current.ISO.IsWindows11 || GlobalsGUI.Current.ISO.Configuration != null;
                FeaturesBox.Opacity = ((GlobalsGUI.Current.ISO.IsWindows11 || GlobalsGUI.Current.ISO.Configuration != null) ? 1.0 : 0.5);
                if (GlobalsGUI.Current.ISO.WriteOptions == null)
                {
                    goto IL_0173;
                }
                if (GlobalsGUI.Current.ISO.Configuration != null)
                {
                    List<string> writeOptions = GlobalsGUI.Current.ISO.WriteOptions;
                    List<string> second = (GlobalsGUI.Current.ISO.WriteOptions = defaultOptions.Where(delegate (string x)
                    {
                        Shared.ISO configuration3 = GlobalsGUI.Current.ISO.Configuration;
                        if (configuration3 == null || !configuration3.HardwareRequirementsDisabled || x != "BitLocker")
                        {
                            Shared.ISO configuration4 = GlobalsGUI.Current.ISO.Configuration;
                            if (configuration4 == null || !configuration4.HardwareRequirementsDisabled || (x != "TPMCheck" && x != "RAMCPUCheck"))
                            {
                                Shared.ISO configuration5 = GlobalsGUI.Current.ISO.Configuration;
                                if (configuration5 == null || !configuration5.InternetRequired)
                                {
                                    return x != "InternetCheck";
                                }
                                return true;
                            }
                        }
                        return false;
                    }).ToList());
                    if (writeOptions.SequenceEqual(second))
                    {
                        goto IL_0173;
                    }
                }
                goto IL_023d;
            IL_0173:
                GlobalsGUI.Current.ISO.WriteOptions = defaultOptions.Where(delegate (string x)
                {
                    Shared.ISO configuration3 = GlobalsGUI.Current.ISO.Configuration;
                    if (configuration3 == null || !configuration3.HardwareRequirementsDisabled || x != "BitLocker")
                    {
                        Shared.ISO configuration4 = GlobalsGUI.Current.ISO.Configuration;
                        if (configuration4 == null || !configuration4.HardwareRequirementsDisabled || (x != "TPMCheck" && x != "RAMCPUCheck"))
                        {
                            Shared.ISO configuration5 = GlobalsGUI.Current.ISO.Configuration;
                            if (configuration5 == null || !configuration5.InternetRequired)
                            {
                                return x != "InternetCheck";
                            }
                            return true;
                        }
                    }
                    return false;
                }).ToList();
                Shared.ISO configuration = GlobalsGUI.Current.ISO.Configuration;
                if (configuration == null || !configuration.HardwareRequirementsDisabled)
                {
                    Shared.ISO configuration2 = GlobalsGUI.Current.ISO.Configuration;
                    if (configuration2 != null && configuration2.InternetRequired)
                    {
                        goto IL_023d;
                    }
                }
                FeaturesBox.IsHitTestVisible = false;
                FeaturesBox.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.5, new Duration(TimeSpan.FromMilliseconds(550.0)))
                {
                    EasingFunction = new QuadraticEase
                    {
                        EasingMode = EasingMode.EaseIn
                    }
                });
                goto IL_023d;
            IL_023d:
                if (GlobalsGUI.Current.ISO.WriteOptions != null && !defaultOptions.SequenceEqual(GlobalsGUI.Current.ISO.WriteOptions) && (GlobalsGUI.Current.ISO.IsWindows11 || GlobalsGUI.Current.ISO.Configuration != null))
                {
                    DefaultBox.Visibility = Visibility.Visible;
                    ResetButton.Visibility = Visibility.Visible;
                    if (!GlobalsGUI.Current.ISO.WriteOptions.Contains("TPMCheck") && !GlobalsGUI.Current.ISO.WriteOptions.Contains("InternetCheck"))
                    {
                        DefaultText.Text = "TPM and Internet requirements removed";
                    }
                    else if (!GlobalsGUI.Current.ISO.WriteOptions.Contains("RAMCPUCheck") && !GlobalsGUI.Current.ISO.WriteOptions.Contains("InternetCheck"))
                    {
                        DefaultText.Text = "CPU and Internet requirements removed";
                    }
                    else if (!GlobalsGUI.Current.ISO.WriteOptions.Contains("InternetCheck"))
                    {
                        DefaultText.Text = "Internet requirement removed";
                    }
                    else if (!GlobalsGUI.Current.ISO.WriteOptions.Contains("RAMCPUCheck"))
                    {
                        DefaultText.Text = "Hardware requirements removed";
                    }
                    else if (!GlobalsGUI.Current.ISO.WriteOptions.Contains("BitLocker"))
                    {
                        DefaultText.Text = "BitLocker drive encryption disabled";
                    }
                    SlideModuleUp();
                }
            };
            ConfirmPopup.Opened += delegate
            {
                IntPtr handle = ((HwndSource)PresentationSource.FromVisual(ConfirmPopup.Child)).Handle;
                if (GetWindowRect(handle, out var lpRect))
                {
                    SetWindowPos(handle, -2, lpRect.Left, lpRect.Top, (int)base.Width, (int)base.Height, 0);
                }
                ConfirmCheckbox.IsChecked = false;
                ConfirmSelectSelectButton.IsEnabled = false;
            };
            OptionsPopup.Opened += delegate
            {
                IntPtr handle = ((HwndSource)PresentationSource.FromVisual(OptionsPopup.Child)).Handle;
                if (GetWindowRect(handle, out var lpRect))
                {
                    SetWindowPos(handle, -2, lpRect.Left, lpRect.Top, (int)base.Width, (int)base.Height, 0);
                }
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
                IsoOptionsPane optionsPane = OptionsPane;
                CheckboxPage[] array = new CheckboxPage[2];
                CheckboxPage val = new CheckboxPage();
                ((FeaturePage)val).Description = "Enable or disable security settings embedded in imaged USB media.";
                CheckboxPage obj2 = val;
                Line val2 = new Line();
                Shared.ISO configuration = GlobalsGUI.Current.ISO.Configuration;
                val2.Text = ((configuration != null && configuration.HardwareRequirementsDisabled) ? "Enable options to prevent usage on old hardware." : "Disable options to install on old hardware.");
                ((FeaturePage)obj2).TopLine = val2;
                CheckboxPage obj3 = val;
                CheckboxOption[] array2 = new CheckboxOption[3];
                CheckboxOption val3 = new CheckboxOption
                {
                    IsChecked = ((GlobalsGUI.Current.ISO.WriteOptions == null) ? defaultOptions.Contains("TPMCheck") : GlobalsGUI.Current.ISO.WriteOptions.Contains("TPMCheck")),
                    Name = "TPMCheck"
                };
                Shared.ISO configuration2 = GlobalsGUI.Current.ISO.Configuration;
                ((Option)val3).Text = ((configuration2 == null || !configuration2.HardwareRequirementsDisabled) ? "TPM requirements (recommended)" : "TPM requirements");
                array2[0] = val3;
                array2[1] = new CheckboxOption
                {
                    IsChecked = ((GlobalsGUI.Current.ISO.WriteOptions == null) ? defaultOptions.Contains("RAMCPUCheck") : GlobalsGUI.Current.ISO.WriteOptions.Contains("RAMCPUCheck")),
                    Name = "RAMCPUCheck",
                    Text = "Minimum RAM and CPU requirements"
                };
                array2[2] = new CheckboxOption
                {
                    IsChecked = ((GlobalsGUI.Current.ISO.WriteOptions == null) ? defaultOptions.Contains("InternetCheck") : GlobalsGUI.Current.ISO.WriteOptions.Contains("InternetCheck")),
                    Name = "InternetCheck",
                    Text = "Internet requirements",
                    IsEnabled = (GlobalsGUI.Current.ISO.Configuration == null)
                };
                Option[] options = (Option[])(object)array2;
                ((FeaturePage)obj3).Options = options;
                array[0] = val;
                val = new CheckboxPage();
                ((FeaturePage)val).Description = ((GlobalsGUI.Current.ISO.Configuration == null) ? "Enable or disable automatic BitLocker drive encryption, which is turned on by default for new systems." : ("Enable or disable automatic BitLocker drive encryption, which is " + (defaultOptions.Contains("BitLocker") ? "enabled" : "disabled") + " by default in this ISO."));
                ((FeaturePage)val).TopLine = new Line
                {
                    Text = "Allows drive access without encryption."
                };
                CheckboxPage obj4 = val;
                options = (Option[])(object)new CheckboxOption[1]
                {
                new CheckboxOption
                {
                    IsChecked = ((GlobalsGUI.Current.ISO.WriteOptions == null) ? defaultOptions.Contains("BitLocker") : GlobalsGUI.Current.ISO.WriteOptions.Contains("BitLocker")),
                    Name = "BitLocker",
                    Text = "Automatic BitLocker drive encryption"
                }
                };
                ((FeaturePage)obj4).Options = options;
                array[1] = val;
                FeaturePage[] pages = (FeaturePage[])(object)array;
                optionsPane.LoadPages(pages);
            };
        }

        private void Next()
        {
            ConfirmPopup.IsOpen = true;
        }

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
            DefaultBox.Visibility = Visibility.Hidden;
            RequiredCompletedBox.Visibility = Visibility.Hidden;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
            {
                yield break;
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T)
                {
                    yield return (T)child;
                }
                foreach (T item in FindVisualChildren<T>(child))
                {
                    yield return item;
                }
            }
        }

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

        private void FeaturesButton_OnClick(object sender, RoutedEventArgs e)
        {
            OptionsPopup.IsOpen = true;
        }

        private async void ResetButton_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalsGUI.Current.ISO.WriteOptions = null;
            FeaturesBox.IsHitTestVisible = GlobalsGUI.Current.ISO.IsWindows11 || GlobalsGUI.Current.ISO.Configuration != null;
            FeaturesBox.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(150.0))));
            await SlideModuleDown();
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            ConfirmSelectSelectButton.IsEnabled = true;
        }

        private void ToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            ConfirmSelectSelectButton.IsEnabled = false;
        }

        private async void ConfirmSelectSelectButton_OnClick(object sender, RoutedEventArgs e)
        {
            ConfirmPopup.IsOpen = false;
            IsoOptionsPageViewModel viewModel = (IsoOptionsPageViewModel)base.DataContext;
            if (GlobalsGUI.Current.ISO.WriteOptions == null)
            {
                GlobalsGUI.Current.ISO.WriteOptions = defaultOptions;
            }
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                bool? obj;
                if (GlobalsGUI.Current.ISO.WriteOptions.Contains("TPMCheck"))
                {
                    Shared.ISO configuration = GlobalsGUI.Current.ISO.Configuration;
                    obj = ((configuration == null || !configuration.HardwareRequirementsDisabled) ? ((bool?)null) : new bool?(false));
                }
                else
                {
                    Shared.ISO configuration2 = GlobalsGUI.Current.ISO.Configuration;
                    obj = ((configuration2 != null && configuration2.HardwareRequirementsDisabled) ? ((bool?)null) : new bool?(true));
                }
                bool? tpm = obj;
                bool? internet = (GlobalsGUI.Current.ISO.WriteOptions.Contains("InternetCheck") ? (defaultOptions.Contains("InternetCheck") ? ((bool?)null) : new bool?(false)) : ((!defaultOptions.Contains("InternetCheck")) ? ((bool?)null) : new bool?(true)));
                bool? obj2;
                if (GlobalsGUI.Current.ISO.WriteOptions.Contains("CPURAMCheck"))
                {
                    Shared.ISO configuration3 = GlobalsGUI.Current.ISO.Configuration;
                    obj2 = ((configuration3 == null || !configuration3.HardwareRequirementsDisabled) ? ((bool?)null) : new bool?(false));
                }
                else
                {
                    Shared.ISO configuration4 = GlobalsGUI.Current.ISO.Configuration;
                    obj2 = ((configuration4 != null && configuration4.HardwareRequirementsDisabled) ? ((bool?)null) : new bool?(true));
                }
                bool? cpuRam = obj2;
                bool? obj3;
                if (GlobalsGUI.Current.ISO.WriteOptions.Contains("BitLocker"))
                {
                    Shared.ISO configuration5 = GlobalsGUI.Current.ISO.Configuration;
                    obj3 = ((configuration5 == null || !configuration5.BitLockerDisabled) ? ((bool?)null) : new bool?(false));
                }
                else
                {
                    Shared.ISO configuration6 = GlobalsGUI.Current.ISO.Configuration;
                    obj3 = ((configuration6 != null && configuration6.BitLockerDisabled) ? ((bool?)null) : new bool?(true));
                }
                bool? bitlocker = obj3;
                new Windows.UsbWriteDialog(viewModel.SelectedUSBDisks, tpm, cpuRam, internet, bitlocker).ShowDialog(mainWindow);
                IsoPageViewModel isoPageViewModel = new IsoPageViewModel();
                GlobalsGUI.Current.ISO.CurrentPage = isoPageViewModel;
                ((MainWindowViewModel)mainWindow.DataContext).CurrentViewModel = isoPageViewModel;
            });
        }

        private void SelectCancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            ConfirmPopup.IsOpen = false;
            OptionsPopup.IsOpen = false;
        }
    }
}
