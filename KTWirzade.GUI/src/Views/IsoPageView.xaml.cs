using Core;
using Interprocess;
using iso_mode;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.Models;
using KTWirzade.GUI.Pages.IsoPage;
using KTWirzade.GUI.Utils;
using KTWirzade.GUI.ViewModels;
using KTWirzade.GUI.Windows;
using KTWirzade.Shared;
using static Core.Wrap;
using static iso_mode.USB;
using static iso_mode.USB.NotificationContext;
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
    public partial class IsoPageView : System.Windows.Controls.UserControl
    {
        public struct RECT
        {
            public int Left;

            public int Top;

            public int Right;

            public int Bottom;
        }

        private static NotificationContext _usbNotifier = null;

        public static RoutedCommand RefreshCommand = new RoutedCommand
        {
            InputGestures = { (InputGesture)new KeyGesture(Key.R, ModifierKeys.Control | ModifierKeys.Shift) }
        };

        private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        private List<UsbDisk> _selectedDisks = new List<UsbDisk>();


        public IsoPageView()
        {
            InitializeComponent();
            base.DataContextChanged += async delegate (object o, DependencyPropertyChangedEventArgs e)
            {
                if (e.OldValue is IsoPageViewModel oldViewModel)
                {
                    NotificationContext usbNotifier = oldViewModel.UsbNotifier;
                    if (usbNotifier != null)
                    {
                        usbNotifier.Unregister();
                    }
                    oldViewModel.SelectedUSBDisks = null;
                }
                if (base.DataContext is IsoPageViewModel viewModel)
                {
                    viewModel.MainNextButtonCommand = new GlobalsGUI.CommandHandler(Next, () => true);
                    if (GlobalsGUI.Current.ISO != null)
                    {
                        GlobalsGUI.Current.ISO.SelectedPlaybook = null;
                    }
                    if (viewModel.Downloading)
                    {
                        viewModel.MainNextButtonActive = false;
                        viewModel.MainUpdatesButtonActive = false;
                        viewModel.MainStatusButtonActive = true;
                        MainGrid.IsHitTestVisible = false;
                        MainGrid.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.5, new Duration(TimeSpan.FromMilliseconds(250.0))));
                    }
                    else
                    {
                        MainGrid.IsHitTestVisible = true;
                        MainGrid.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(250.0))));
                        if (GlobalsGUI.Current.ISO != null && GlobalsGUI.Current.ISO.IsWindows11 && GlobalsGUI.Current.ISO.Username == "Microsoft")
                        {
                            PlaybookBox.Visibility = Visibility.Visible;
                            CompatibleBox.Visibility = Visibility.Collapsed;
                            PlaybookSelectedBox.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            RevealBox.Visibility = Visibility.Visible;
                            PlaybookBox.Visibility = Visibility.Collapsed;
                            CompatibleBox.Visibility = Visibility.Collapsed;
                            PlaybookSelectedBox.Visibility = Visibility.Collapsed;
                        }
                        SlideModuleUp();
                        viewModel.MainNextButtonActive = false;
                        viewModel.MainUpdatesButtonActive = true;
                        viewModel.MainStatusButtonActive = false;
                    }
                }
            };
            SelectPopup.Opened += delegate
            {
                IntPtr handle = ((HwndSource)PresentationSource.FromVisual(SelectPopup.Child)).Handle;
                if (GetWindowRect(handle, out var lpRect))
                {
                    SetWindowPos(handle, -2, lpRect.Left, lpRect.Top, (int)base.Width, (int)base.Height, 0);
                }
                SelectSelectButton.IsEnabled = false;
                DragBoxContainer.Visibility = Visibility.Visible;
                DragBoxContainer.Margin = new Thickness(0.0, 0.0, 0.0, 0.0);
                SelectPlaybookStack.Children.RemoveRange(0, SelectPlaybookStack.Children.Count - 1);
                int num = 0;
                foreach (PlaybookGUI current in GlobalsGUI.Current.Playbooks.Where((PlaybookGUI x) => ((Playbook)x).SupportsISO))
                {
                    SelectPlaybookStack.Children.Insert(num, RadioPlaybookOption(current, num == 0));
                    SelectSelectButton.IsEnabled = true;
                    DragBoxContainer.Margin = new Thickness(7.0, 0.0, 0.0, 0.0);
                    if (num >= 2)
                    {
                        DragBoxContainer.Visibility = Visibility.Collapsed;
                        break;
                    }
                    num++;
                }
            };
            UsbPopup.Opened += async delegate
            {
                IntPtr hwnd = ((HwndSource)PresentationSource.FromVisual(UsbPopup.Child)).Handle;
                if (GetWindowRect(hwnd, out var rect))
                {
                    SetWindowPos(hwnd, -2, rect.Left, rect.Top, (int)base.Width, (int)base.Height, 0);
                }
                _usbNotifier = new NotificationContext();
                _usbNotifier.Register(new CM_NOTIFY_CALLBACK(NotificationReceived));
                UsbScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                UsbHideBorder.Visibility = Visibility.Collapsed;
                UsbTextStack.Visibility = Visibility.Visible;
                UsbHoldShiftText.Visibility = Visibility.Collapsed;
                UsbMainContainerGrid.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation(new Thickness(0.0), new Duration(TimeSpan.Zero)));
                long size = (GlobalsGUI.Current.ISO.Size ?? 6442450944L) + 10485760;
                string text = ((size < 4294967296L) ? "4 GB" : ((size < 8589934592L) ? "8 GB" : ((size >= 17179869184L) ? USB.HumanReadableDiskSize(size) : "16 GB")));
                string sizeString = text;
                TopUsbLineBlock.Text = "The drive needs to be " + sizeString + " or larger in size.";
                await RefreshUsbPage(log: false);
            };
            UsbPopup.Closed += delegate
            {
                NotificationContext usbNotifier = _usbNotifier;
                if (usbNotifier != null)
                {
                    usbNotifier.Unregister();
                }
            };
        }

        private int NotificationReceived(IntPtr hNotify, IntPtr Context, CM_NOTIFY_ACTION Action, IntPtr EventDataPtr, int EventDataSize)
        {
            Marshal.PtrToStructure<CM_NOTIFY_EVENT_DATA>(EventDataPtr);
            _ = EventDataPtr + Marshal.SizeOf<CM_NOTIFY_EVENT_DATA>();
            System.Windows.Application.Current.Dispatcher.Invoke((Func<Task>)async delegate
            {
                _ = 1;
                try
                {
                    await Task.Delay(1000);
                    await RefreshUsbPage(log: false);
                }
                catch (Exception value)
                {
                    Console.WriteLine(value);
                }
            });
            return 0;
        }

        private async void RefreshCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            await RefreshUsbPage(log: true);
        }

        private async Task RefreshUsbPage(bool log)
        {
            if (!_refreshLock.Wait(0))
            {
                return;
            }
            UsbSelectSelectButton.IsEnabled = false;
            UsbSelectCancelButton.IsEnabled = false;
            UsbStack.Visibility = Visibility.Collapsed;
            UsbStack.Children.Clear();
            UsbLoadContainer.Visibility = Visibility.Visible;
            Spinner spinner = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            UsbLoadContainer.Children.Add(spinner);
            SafeResult<List<UsbDisk>> usbDevices = await InterLink.ExecuteSafeAsync<List<UsbDisk>>((Expression<Func<List<UsbDisk>>>)(() => USB.GetDevices(false, log)), false, -1);
            UsbLoadContainer.Children.Remove(spinner);
            UsbLoadContainer.Visibility = Visibility.Collapsed;
            UsbStack.Visibility = Visibility.Visible;
            if (usbDevices.Failed)
            {
                Log.EnqueueExceptionSafe(usbDevices.Exception, Array.Empty<(string, object)>());
                MessageBox.Show(typeof(MainWindow), "Error loading USB devices: " + usbDevices.Exception);
                UsbPopup.IsOpen = false;
            }
            UsbSelectCancelButton.IsEnabled = true;
            int i = 0;
            foreach (UsbDisk item in usbDevices.Value.Where((UsbDisk x) => x.Size > (GlobalsGUI.Current.ISO.Size ?? 6442450944L) + 10485760))
            {
                UsbProgressItem button = new UsbProgressItem(item)
                {
                    IsChecked = (i == 0),
                    Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
                };
                button.Checked += delegate (object sender, RoutedEventArgs args)
                {
                    IEnumerable<UsbProgressItem> enumerable = UsbStack.Children.OfType<UsbProgressItem>();
                    if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                    {
                        foreach (UsbProgressItem current in enumerable)
                        {
                            if (current != sender)
                            {
                                current.IsChecked = false;
                            }
                        }
                    }
                };
                button.PreviewMouseLeftButtonDown += delegate (object sender, MouseButtonEventArgs args)
                {
                    if (UsbStack.Children.OfType<UsbProgressItem>().Count((UsbProgressItem x) => x.IsChecked == true) >= 5 && (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                    {
                        args.Handled = true;
                    }
                };
                UsbStack.Children.Insert(i, button);
                UsbSelectSelectButton.IsEnabled = true;
                i++;
            }
            if (usbDevices.Value.Count > 3 && UsbScrollViewer.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled)
            {
                UsbHideBorder.Visibility = Visibility.Visible;
            }
            if (usbDevices.Value.Count == 0)
            {
                UsbStack.Children.Add(new TextBlock
                {
                    Text = "Plug in a USB device",
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush")
                });
            }
            _refreshLock.Release();
        }

        private void Next()
        {
            Continue(this, new RoutedEventArgs());
        }

        private void UsbShowAllButton_OnClick(object sender, RoutedEventArgs e)
        {
            UsbScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            UsbHideBorder.Visibility = Visibility.Collapsed;
            UsbTextStack.Visibility = Visibility.Collapsed;
            UsbHoldShiftText.Visibility = Visibility.Visible;
        }

        private void RevealButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", "/select,\"" + GlobalsGUI.Current.ISO.FilePath + "\"");
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, Array.Empty<(string, object)>());
            }
        }

        private void PlaybookButton_OnClick(object sender, RoutedEventArgs e)
        {
            SelectPopup.IsOpen = true;
        }

        private async void CompatibleCancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            await SlideModuleDown();
            _selectedDisks = new List<UsbDisk>();
            CompatibleBox.Visibility = Visibility.Collapsed;
            if (GlobalsGUI.Current.ISO.IsWindows11 && GlobalsGUI.Current.ISO.Username == "Microsoft")
            {
                PlaybookBox.Visibility = Visibility.Visible;
            }
            else
            {
                RevealBox.Visibility = Visibility.Visible;
            }
            ClickSelectText.Visibility = Visibility.Visible;
            ClickNextText.Visibility = Visibility.Collapsed;
            if (base.DataContext is ViewModelBase viewModel)
            {
                viewModel.MainNextButtonActive = false;
                SlideModuleUp();
            }
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

        private void Continue(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            if (base.DataContext is IsoPageViewModel viewModel && viewModel != null)
            {
                NotificationContext usbNotifier = viewModel.UsbNotifier;
                if (usbNotifier != null)
                {
                    usbNotifier.Unregister();
                }
            }
            if (_selectedDisks.Any())
            {
                MainWindow.CurrentDispatcher.Invoke(delegate
                {
                    MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                    IsoOptionsPageViewModel isoOptionsPageViewModel = new IsoOptionsPageViewModel(_selectedDisks);
                    GlobalsGUI.Current.ISO.CurrentPage = isoOptionsPageViewModel;
                    ((MainWindowViewModel)mainWindow.DataContext).CurrentViewModel = isoOptionsPageViewModel;
                });
            }
            else
            {
                MainWindow.CurrentDispatcher.Invoke(delegate
                {
                    MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                    IsoRequirementsPageViewModel isoRequirementsPageViewModel = new IsoRequirementsPageViewModel(new IsoRequirementsPage());
                    GlobalsGUI.Current.ISO.CurrentPage = isoRequirementsPageViewModel;
                    ((MainWindowViewModel)mainWindow.DataContext).CurrentViewModel = isoRequirementsPageViewModel;
                });
            }
        }

        private RadioPlaybookButton RadioPlaybookOption(PlaybookGUI pb, bool isSelected)
        {
            RadioPlaybookButton button = new RadioPlaybookButton
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness((SelectPlaybookStack.Children.Count - 1 != 0) ? 7 : 0, 0.0, 0.0, 0.0),
                Playbook = pb,
                Selected = isSelected
            };
            if (!isSelected)
            {
                SolidColorBrush newBrush = (SolidColorBrush)button.OverlayBorder.BorderBrush.Clone();
                newBrush.BeginAnimation(System.Windows.Media.Brush.OpacityProperty, new DoubleAnimation(0.0, new Duration(TimeSpan.FromMilliseconds(0.0))));
                button.OverlayBorder.BorderBrush = newBrush;
                button.InnerBorder.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation(new Thickness(-1.0), new Duration(TimeSpan.FromMilliseconds(0.0))));
                button.InnerBorder.BeginAnimation(Border.CornerRadiusProperty, new ObjectAnimationUsingKeyFrames
                {
                    KeyFrames = new ObjectKeyFrameCollection
                {
                    new DiscreteObjectKeyFrame(new CornerRadius(5.0), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0.0)))
                }
                });
                ColorAnimationUsingKeyFrames colorAnim = new ColorAnimationUsingKeyFrames
                {
                    Duration = new Duration(TimeSpan.FromMilliseconds(0.0)),
                    KeyFrames = new ColorKeyFrameCollection
                {
                    new EasingColorKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0.0)),
                        Value = (System.Windows.Media.Color)FindResource("ButtonTextPrimaryColor")
                    }
                }
                };
                button.ColorBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
                button.ColorBrush.BeginAnimation(System.Windows.Media.Brush.OpacityProperty, new DoubleAnimation(0.025, new Duration(TimeSpan.FromMilliseconds(0.0))));
            }
            button.Text.Text = ((Playbook)pb).Name;
            button.Image.Source = pb.Icon;
            button.MouseEnter += delegate
            {
                ColorAnimationUsingKeyFrames animation = new ColorAnimationUsingKeyFrames
                {
                    Duration = new Duration(TimeSpan.FromMilliseconds(150.0)),
                    KeyFrames = new ColorKeyFrameCollection
                {
                    new EasingColorKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)),
                        Value = (System.Windows.Media.Color)FindResource("ProgressBarColor")
                    }
                }
                };
                button.ColorBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
                button.ColorBrush.BeginAnimation(System.Windows.Media.Brush.OpacityProperty, new DoubleAnimation(0.1, new Duration(TimeSpan.FromMilliseconds(150.0))));
            };
            button.MouseLeave += delegate (object sender, System.Windows.Input.MouseEventArgs args)
            {
                if (!((RadioPlaybookButton)sender).Selected)
                {
                    ColorAnimationUsingKeyFrames animation = new ColorAnimationUsingKeyFrames
                    {
                        Duration = new Duration(TimeSpan.FromMilliseconds(150.0)),
                        KeyFrames = new ColorKeyFrameCollection
                    {
                        new EasingColorKeyFrame
                        {
                            EasingFunction = new SineEase
                            {
                                EasingMode = EasingMode.EaseInOut
                            },
                            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)),
                            Value = (System.Windows.Media.Color)FindResource("ButtonTextPrimaryColor")
                        }
                    }
                    };
                    button.ColorBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
                    button.ColorBrush.BeginAnimation(System.Windows.Media.Brush.OpacityProperty, new DoubleAnimation(0.025, new Duration(TimeSpan.FromMilliseconds(150.0))));
                }
            };
            button.Checked += SwitchRadioPlaybook;
            button.Unchecked += OptionDeselected;
            button.Checked += OptionSelected;
            return button;
        }

        private void OptionDeselected(object sender, RoutedEventArgs e)
        {
            SelectSelectButton.IsEnabled = false;
        }

        private void OptionSelected(object sender, RoutedEventArgs e)
        {
            SelectSelectButton.IsEnabled = true;
        }

        public void SwitchRadioPlaybook(object sender, RoutedEventArgs e)
        {
            RadioPlaybookButton newButton = (RadioPlaybookButton)sender;
            List<RadioPlaybookButton> children = ((StackPanel)newButton.Parent).Children.OfType<RadioPlaybookButton>().ToList();
            int activeIndex = children.FindIndex((RadioPlaybookButton x) => x.Selected);
            if (children.FindIndex((RadioPlaybookButton x) => x.Playbook.FileNameWithoutExtension == newButton.Playbook.FileNameWithoutExtension) != activeIndex)
            {
                RadioPlaybookButton obj = ((activeIndex == -1) ? new RadioPlaybookButton() : children[activeIndex]);
                obj.Selected = false;
                newButton.Selected = true;
                obj.InnerBorder.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation(new Thickness(-1.0), new Duration(TimeSpan.FromMilliseconds(0.0))));
                obj.InnerBorder.BeginAnimation(Border.CornerRadiusProperty, new ObjectAnimationUsingKeyFrames
                {
                    KeyFrames = new ObjectKeyFrameCollection
                {
                    new DiscreteObjectKeyFrame(new CornerRadius(5.0), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)))
                }
                });
                SolidColorBrush activeNewBrush = (SolidColorBrush)obj.OverlayBorder.BorderBrush.Clone();
                activeNewBrush.BeginAnimation(System.Windows.Media.Brush.OpacityProperty, new DoubleAnimation(0.0, new Duration(TimeSpan.FromMilliseconds(100.0))));
                obj.OverlayBorder.BorderBrush = activeNewBrush;
                newButton.InnerBorder.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation(new Thickness(0.0), new Duration(TimeSpan.FromMilliseconds(0.0))));
                newButton.InnerBorder.BeginAnimation(Border.CornerRadiusProperty, new ObjectAnimationUsingKeyFrames
                {
                    KeyFrames = new ObjectKeyFrameCollection
                {
                    new DiscreteObjectKeyFrame(new CornerRadius(3.0), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100.0)))
                }
                });
                SolidColorBrush newBrush = (SolidColorBrush)newButton.OverlayBorder.BorderBrush.Clone();
                newBrush.BeginAnimation(System.Windows.Media.Brush.OpacityProperty, new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(200.0))));
                newButton.OverlayBorder.BorderBrush = newBrush;
                ColorAnimationUsingKeyFrames colorAnim = new ColorAnimationUsingKeyFrames
                {
                    Duration = new Duration(TimeSpan.FromMilliseconds(150.0)),
                    KeyFrames = new ColorKeyFrameCollection
                {
                    new EasingColorKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)),
                        Value = (System.Windows.Media.Color)FindResource("ProgressBarColor")
                    }
                }
                };
                newButton.ColorBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
                newButton.ColorBrush.BeginAnimation(System.Windows.Media.Brush.OpacityProperty, new DoubleAnimation(0.1, new Duration(TimeSpan.FromMilliseconds(100.0))));
                ColorAnimationUsingKeyFrames colorAnim2 = new ColorAnimationUsingKeyFrames
                {
                    Duration = new Duration(TimeSpan.FromMilliseconds(150.0)),
                    KeyFrames = new ColorKeyFrameCollection
                {
                    new EasingColorKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150.0)),
                        Value = (System.Windows.Media.Color)FindResource("ButtonTextPrimaryColor")
                    }
                }
                };
                obj.ColorBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim2);
                obj.ColorBrush.BeginAnimation(System.Windows.Media.Brush.OpacityProperty, new DoubleAnimation(0.025, new Duration(TimeSpan.FromMilliseconds(100.0))));
            }
        }

        private async Task LoadPlaybook(string apbx)
        {
            SelectCancelButton.IsEnabled = false;
            SelectSelectButton.IsEnabled = false;
            DragBox.SetResourceReference(FrameworkElement.StyleProperty, "DragBoxSmallLoading");
            PBLoadContainer.Visibility = Visibility.Visible;
            Spinner spinner = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            PBLoadContainer.Children.Add(spinner);
            Storyboard board1 = new Storyboard();
            PlaybookGUI pb = null;
            try
            {
                pb = await Task.Run(() => APBX.ImportAPBX(apbx));
                if (pb == null)
                {
                    board1.Pause();
                    if (MessageBox.Show(typeof(MainWindow), "Selected Playbook already exists. Overwrite?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        board1.Resume();
                        pb = await Task.Run(() => APBX.ImportAPBX(apbx, overwrite: true));
                        PlaybookGUI pbGui = GlobalsGUI.Current.Playbooks.FirstOrDefault((PlaybookGUI x) => (((Playbook)x).UniqueId.HasValue && ((Playbook)pb).UniqueId.HasValue && ((Playbook)x).UniqueId == ((Playbook)pb).UniqueId) || ((!((Playbook)x).UniqueId.HasValue || !((Playbook)pb).UniqueId.HasValue) && ((Playbook)x).Name == ((Playbook)pb).Name && ((Playbook)x).Username == ((Playbook)pb).Username));
                        if (pbGui != null)
                        {
                            ((Playbook)pbGui).Path = null;
                        }
                        GlobalsGUI.Current.Items.Remove(pbGui);
                        if (string.IsNullOrEmpty(((Playbook)pb).Path))
                        {
                            pb = null;
                            throw new Exception("Could not remove existing Playbook files.");
                        }
                        RadioPlaybookButton pbButton = SelectPlaybookStack.Children.OfType<RadioPlaybookButton>().FirstOrDefault((RadioPlaybookButton x) => (((Playbook)x.Playbook).UniqueId.HasValue && ((Playbook)pb).UniqueId.HasValue && ((Playbook)x.Playbook).UniqueId == ((Playbook)pb).UniqueId) || ((!((Playbook)x.Playbook).UniqueId.HasValue || !((Playbook)pb).UniqueId.HasValue) && x.Name == ((Playbook)pb).Name && ((Playbook)x.Playbook).Username == ((Playbook)pb).Username));
                        if (pbButton != null)
                        {
                            pbButton.Playbook = pb;
                        }
                        PBLoadContainer.Visibility = Visibility.Collapsed;
                        PBLoadContainer.Children.Remove(spinner);
                        DragBox.SetResourceReference(FrameworkElement.StyleProperty, "DragBoxSmall");
                        pb.Checked = true;
                        GlobalsGUI.Current.Items.Add(pb);
                        SelectCancelButton.IsEnabled = true;
                        if (((Playbook)pb).SupportsISO)
                        {
                            SelectSelectButton.IsEnabled = true;
                        }
                        return;
                    }
                    board1.Stop();
                }
                else
                {
                    pb.Checked = true;
                }
            }
            catch (Exception ex)
            {
                board1.Stop();
                MessageBox.Show(typeof(UpdatesDialog), "Error while attempting to load Playbook: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            PBLoadContainer.Visibility = Visibility.Collapsed;
            PBLoadContainer.Children.Remove(spinner);
            DragBox.SetResourceReference(FrameworkElement.StyleProperty, "DragBoxSmall");
            if (pb != null)
            {
                GlobalsGUI.Current.Items.Add(pb);
            }
            if (pb != null && ((Playbook)pb).SupportsISO)
            {
                RadioPlaybookButton element = RadioPlaybookOption(pb, isSelected: false);
                SelectPlaybookStack.Children.Insert(SelectPlaybookStack.Children.Count - 1, element);
                if (SelectPlaybookStack.Children.Count >= 4)
                {
                    DragBoxContainer.Visibility = Visibility.Collapsed;
                }
                SwitchRadioPlaybook(element, new RoutedEventArgs());
                DragBoxContainer.Margin = new Thickness(7.0, 0.0, 0.0, 0.0);
            }
            SelectCancelButton.IsEnabled = true;
            SelectSelectButton.IsEnabled = true;
            DragBox.Visibility = Visibility.Visible;
        }

        private async void DragBox_OnClick(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new();
            dialog.DefaultExt = ".apbx";
            dialog.Filter = "KT WIRZADE Playbooks|*.apbx|All Files|*";
            dialog.Multiselect = false;
            if (dialog.ShowDialog() == true)
            {
                await LoadPlaybook(dialog.FileName);
            }
        }

        private async void DragBox_OnDrop(object sender, System.Windows.DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop, autoConvert: true))
                {
                    string[] files = e.Data.GetData(System.Windows.DataFormats.FileDrop, autoConvert: true) as string[];
                    string[] array = files;
                    foreach (string file in array)
                    {
                        await LoadPlaybook(file);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(typeof(UpdatesDialog), "Ensure you've updated KT WIRZADE, and contact your Playbook creator for further assistance.", "Error loading Playbook", MessageBoxButton.OK, MessageBoxImage.Warning, ex.ToString());
            }
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
            PlaybookBox.Visibility = Visibility.Hidden;
            RevealBox.Visibility = Visibility.Hidden;
            PlaybookSelectedBox.Visibility = Visibility.Hidden;
            CompatibleBox.Visibility = Visibility.Hidden;
            UsbDisconnectedBox.Visibility = Visibility.Hidden;
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

        private void WriteButton_OnClick(object sender, RoutedEventArgs e)
        {
            UsbPopup.IsOpen = true;
        }

        private async void SelectSelectButton_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalsGUI.Current.ISO.SelectedPlaybook = SelectPlaybookStack.Children.OfType<RadioPlaybookButton>().First((RadioPlaybookButton x) => x.Selected).Playbook;
            SelectPopup.IsOpen = false;
            if (base.DataContext is ViewModelBase viewModel)
            {
                viewModel.MainNextButtonActive = true;
                WriteStack.Opacity = 0.4;
                WriteButton.IsEnabled = false;
                PlaybookSelectedText.Text = ((Playbook)GlobalsGUI.Current.ISO.SelectedPlaybook).Name + " Playbook selected";
                await SlideModuleDown();
                PlaybookSelectedBox.Visibility = Visibility.Visible;
                SlideModuleUp();
            }
        }

        private void SelectCancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            SelectPopup.IsOpen = false;
            UsbPopup.IsOpen = false;
        }

        private async void PlaybookSelectedCancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (base.DataContext is ViewModelBase viewModel)
            {
                viewModel.MainNextButtonActive = false;
                WriteStack.Opacity = 1.0;
                WriteButton.IsEnabled = true;
                GlobalsGUI.Current.ISO.SelectedPlaybook = null;
                await SlideModuleDown();
                PlaybookBox.Visibility = Visibility.Visible;
                SlideModuleUp();
            }
        }

        private async void UsbSelectSelectButton_OnClick(object sender, RoutedEventArgs e)
        {
            _selectedDisks = (from x in UsbStack.Children.OfType<UsbProgressItem>()
                              where x.IsChecked == true
                              select x.USB).ToList();
            UsbPopup.IsOpen = false;
            if (base.DataContext is IsoPageViewModel viewModel)
            {
                viewModel.MainNextButtonActive = true;
                viewModel.SelectedUSBDisks = _selectedDisks;
                viewModel.UsbNotifier = new NotificationContext();
                viewModel.UsbNotifier.Register(new CM_NOTIFY_CALLBACK(ViewModelUsbNotifierNotification));
                ClickSelectText.Visibility = Visibility.Collapsed;
                ClickNextText.Visibility = Visibility.Visible;
                await SlideModuleDown();
                CompatibleText.Text = ((_selectedDisks.Count > 1) ? "Multiple flash drives selected" : "Compatible flash drive selected");
                CompatibleBox.Visibility = Visibility.Visible;
                SlideModuleUp();
            }
        }

        private int ViewModelUsbNotifierNotification(IntPtr hNotify, IntPtr Context, CM_NOTIFY_ACTION Action, IntPtr EventDataPtr, int EventDataSize)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)async delegate
            {
                try
                {
                    if (base.DataContext is IsoPageViewModel viewModel && GlobalsGUI.Current.ISO.CurrentPage == viewModel && ((int)Action == 1 || (int)Action == 0))
                    {
                        Marshal.PtrToStructure<CM_NOTIFY_EVENT_DATA>(EventDataPtr);
                        IntPtr offsetOfMoreInfo = EventDataPtr + Marshal.SizeOf<CM_NOTIFY_EVENT_DATA>();
                        string name = Marshal.PtrToStringAuto(offsetOfMoreInfo);
                        if (name != null && (int)Action == 1 && viewModel.SelectedUSBDisks.Any((UsbDisk x) => x.UsbDeviceID != null && name.StartsWith(x.UsbDeviceID.Replace('\\', '#'), StringComparison.OrdinalIgnoreCase)) && base.IsLoaded)
                        {
                            _selectedDisks = null;
                            viewModel.MainNextButtonActive = false;
                            WriteButton.IsEnabled = false;
                            WriteStack.Opacity = 0.4;
                            await SlideModuleDown();
                            UsbDisconnectedBox.Visibility = Visibility.Visible;
                            SlideModuleUp();
                        }
                    }
                }
                catch (Exception value)
                {
                    Console.WriteLine(value);
                }
            });
            return 0;
        }

        private async void DismissUsbDisconnectedButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (base.DataContext is ViewModelBase viewModel)
            {
                viewModel.MainNextButtonActive = false;
                WriteStack.Opacity = 1.0;
                WriteButton.IsEnabled = true;
                GlobalsGUI.Current.ISO.SelectedPlaybook = null;
                await SlideModuleDown();
            }
        }
    }
}
