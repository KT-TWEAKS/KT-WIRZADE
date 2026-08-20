using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.Utils;
using KTWirzade.GUI.ViewModels;
using KTWirzade.Shared;
using static Core.Win32;

namespace KTWirzade.GUI.Windows
{
    public partial class UpdatesDialog : AcrylicWindow
    {
        public struct RECT
        {
            public int Left;

            public int Top;

            public int Right;

            public int Bottom;
        }

        private static ObservableCollection<PlaybookGUI> ItemList = new ObservableCollection<PlaybookGUI>();

        private int ActiveItemIndex;

        private bool _playbooksLocked;


        public void ShowDialog(Window owner)
        {
            Owner = owner;
            ShowDialog();
        }

        public UpdatesDialog()
        {
            ItemList = new ObservableCollection<PlaybookGUI>();
            ItemList.Add(GlobalsGUI.Current.WizardPlaybook);
            foreach (PlaybookGUI pb in GlobalsGUI.Current.Playbooks)
            {
                ItemList.Add(pb);
            }
            while (ItemList.Count < 3)
            {
                ItemList.Add(new PlaybookGUI(new Playbook
                {
                    Name = "None",
                    Username = "Ameliorated",
                    Version = "1"
                })
                {
                    VerificationStatus = PlaybookGUI.VerificationLevel.Verified
                });
            }
            DataContext = new UpdatesDialogViewModel
            {
                SelectedPlaybook = ItemList.First()
            };
            InitializeComponent();
            if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
            {
                RootWindow.SetResourceReference(BackgroundProperty, "FakeBackgroundBrush");
            }
            PlaybookSidebarItems.ItemsSource = ItemList;
            scrollViewer.ScrollChanged += ScrollViewerOnScrollChanged;
            Loaded += delegate
            {
                foreach (TextBlock item in from x in FindVisualChildren<TextBlock>(PlaybookSidebarItems)
                                           where x.Text == "None"
                                           select x)
                {
                    item.Opacity = 0.7;
                }
                FindVisualChildren<System.Windows.Shapes.Rectangle>(PlaybookSidebarItems).First().Width = 18.0;
                FindVisualChildren<Border>(PlaybookSidebarItems).Last((Border x) => x.Name.ToString() == "PlaybookContainer").Margin = default(Thickness);
            };
            Popup.Opened += async delegate (object sender, EventArgs args)
            {
                IntPtr hwnd = ((HwndSource)PresentationSource.FromVisual(Popup.Child)).Handle;
                if (GetWindowRect(hwnd, out var rect))
                {
                    SetWindowPos(hwnd, -2, rect.Left, rect.Top, (int)Width, (int)Height, 0);
                }
                try
                {
                    SetForegroundWindow(new WindowInteropHelper(this).Handle);
                }
                catch
                {
                }
                PatreonControl.OnOpened(this, args);
            };
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32")]
        private static extern int SetWindowPos(IntPtr hWnd, int hwndInsertAfter, int x, int y, int cx, int cy, int wFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private void ScrollViewerOnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalOffset + e.ViewportWidth < e.ExtentWidth)
            {
                MoveRightButton.Visibility = Visibility.Visible;
            }
            else
            {
                MoveRightButton.Visibility = Visibility.Hidden;
            }
            if (e.HorizontalOffset > 0.0)
            {
                MoveLeftButton.Visibility = Visibility.Visible;
            }
            else
            {
                MoveLeftButton.Visibility = Visibility.Hidden;
            }
        }

        private void scrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + (double)e.Delta);
        }

        private void MoveRight_OnClick(object sender, RoutedEventArgs e)
        {
            scrollViewer.ScrollToHorizontalOffset(Math.Min(scrollViewer.HorizontalOffset + 55.0, scrollViewer.ExtentWidth));
        }

        private void MoveLeft_OnClick(object sender, RoutedEventArgs e)
        {
            scrollViewer.ScrollToHorizontalOffset(Math.Max(scrollViewer.HorizontalOffset - 55.0, 0.0));
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
            CloseWindow(updatesscale);
        }

        private async void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is UpdatesDialogViewModel { SelectedPlaybook: var pb }) || pb.VerificationStatus != PlaybookGUI.VerificationLevel.Verified)
            {
                return;
            }
            try
            {
                List<string> obj = await (pb).GetPlaybookVersions();
                string matchingTag = null;
                foreach (string tag in obj)
                {
                    VersionNumber number;
                    try
                    {
                        number = VersionNumber.GetVersionNumber(tag);
                    }
                    catch
                    {
                        continue;
                    }
                    if (number == (pb).GetVersionNumber())
                    {
                        matchingTag = tag;
                        break;
                    }
                }
                if (matchingTag == null)
                {
                    MessageBox.Show(typeof(UpdatesDialog), "Unable to find release notes for this version.", "Warning");
                    return;
                }
                System.Diagnostics.Process.Start("https://" + (pb).GetPlaybookGitPlatform() + "/" + (pb).GetRepository() + "/releases/tag/" + matchingTag);
            }
            catch (Exception)
            {
                MessageBox.Show(typeof(UpdatesDialog), "Unable to open release notes for this version.", "Warning");
            }
        }

        private async void SelectItem(object sender, RoutedEventArgs e)
        {
            if (_playbooksLocked)
            {
                return;
            }
            PlaybookGUI selectedPB = ItemList.FirstOrDefault((PlaybookGUI x) => x == ((FrameworkElement)sender).DataContext);
            if (selectedPB == null)
            {
                return;
            }
            int newIndex = ItemList.IndexOf(selectedPB);
            if (newIndex == ActiveItemIndex)
            {
                return;
            }
            object dataContext = DataContext;
            if (dataContext is UpdatesDialogViewModel viewModel)
            {
                _playbooksLocked = true;
                viewModel.TransitionSelectedPlaybook = ItemList[newIndex];
                System.Windows.Shapes.Rectangle ActivePB = new();
                System.Windows.Shapes.Rectangle SelectedPB = FindVisualChildren<System.Windows.Shapes.Rectangle>(PlaybookSidebarItems).ElementAt(newIndex);
                if (ActiveItemIndex != -1)
                {
                    ActivePB = FindVisualChildren<System.Windows.Shapes.Rectangle>(PlaybookSidebarItems).ElementAt(ActiveItemIndex);
                }
                Storyboard board = new Storyboard();
                Thickness origMargin = new Thickness(46.0, 31.0, 0.0, 0.0);
                int origWidth = 18;
                if (newIndex > ActiveItemIndex)
                {
                    Grid.SetColumn(TransitionGrid, 2);
                    DoubleAnimationUsingKeyFrames mainAnim1 = new DoubleAnimationUsingKeyFrames();
                    mainAnim1.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 240));
                    DoubleKeyFrame anim1Key1 = new LinearDoubleKeyFrame
                    {
                        Value = 64.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                    };
                    DoubleKeyFrame anim1Key2 = new LinearDoubleKeyFrame
                    {
                        Value = 64.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 220))
                    };
                    DoubleKeyFrame anim1Key3 = new LinearDoubleKeyFrame
                    {
                        Value = 0.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                    };
                    mainAnim1.KeyFrames.Add(anim1Key1);
                    mainAnim1.KeyFrames.Add(anim1Key2);
                    mainAnim1.KeyFrames.Add(anim1Key3);
                    ThicknessAnimationUsingKeyFrames marginAnim1 = new ThicknessAnimationUsingKeyFrames();
                    marginAnim1.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 40));
                    marginAnim1.BeginTime = new TimeSpan(0, 0, 0, 0, 240);
                    ThicknessKeyFrame marginAnim1Key1 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(110.0, 31.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 220))
                    };
                    ThicknessKeyFrame marginAnim1KeyDelay = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(110.0, 31.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                    };
                    ThicknessKeyFrame marginAnim1Key2 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(46.0, 31.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                    };
                    marginAnim1.KeyFrames.Add(marginAnim1Key1);
                    marginAnim1.KeyFrames.Add(marginAnim1KeyDelay);
                    marginAnim1.KeyFrames.Add(marginAnim1Key2);
                    DoubleAnimationUsingKeyFrames mainAnim2 = new DoubleAnimationUsingKeyFrames();
                    mainAnim2.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
                    mainAnim2.BeginTime = new TimeSpan(0, 0, 0, 0, 200);
                    DoubleKeyFrame anim2Key1 = new LinearDoubleKeyFrame
                    {
                        Value = 64.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                    };
                    DoubleKeyFrame anim2Key2 = new LinearDoubleKeyFrame
                    {
                        Value = origWidth,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                    };
                    mainAnim2.KeyFrames.Add(anim2Key1);
                    mainAnim2.KeyFrames.Add(anim2Key2);
                    ThicknessAnimation marginAnim2 = new ThicknessAnimation();
                    marginAnim2.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
                    marginAnim2.BeginTime = new TimeSpan(0, 0, 0, 0, 200);
                    marginAnim2.From = new Thickness(0.0, 31.0, 0.0, 0.0);
                    marginAnim2.To = origMargin;
                    Storyboard.SetTarget(mainAnim1, ActivePB);
                    Storyboard.SetTargetProperty(mainAnim1, new PropertyPath("Width"));
                    Storyboard.SetTarget(marginAnim1, ActivePB);
                    Storyboard.SetTargetProperty(marginAnim1, new PropertyPath("Margin"));
                    Storyboard.SetTarget(mainAnim2, SelectedPB);
                    Storyboard.SetTargetProperty(mainAnim2, new PropertyPath("Width"));
                    Storyboard.SetTarget(marginAnim2, SelectedPB);
                    Storyboard.SetTargetProperty(marginAnim2, new PropertyPath("Margin"));
                    board.Children.Add(mainAnim1);
                    board.Children.Add(marginAnim1);
                    board.Children.Add(mainAnim2);
                    board.Children.Add(marginAnim2);
                    ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames();
                    transitionAnim.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540));
                    ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                    };
                    ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        Value = new Thickness(-844.0, 0.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                    };
                    ThicknessKeyFrame transitionKey3 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(-844.0, 0.0, 0.0, 0.0),
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
                    Storyboard.SetTarget(transitionAnim, MainContainerGrid);
                    Storyboard.SetTargetProperty(transitionAnim, new PropertyPath("Margin"));
                    board.Children.Add(transitionAnim);
                    board.Begin();
                }
                else
                {
                    Grid.SetColumn(TransitionGrid, 0);
                    DoubleAnimationUsingKeyFrames mainAnim3 = new DoubleAnimationUsingKeyFrames();
                    mainAnim3.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 240));
                    DoubleKeyFrame anim1Key4 = new LinearDoubleKeyFrame
                    {
                        Value = 64.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                    };
                    DoubleKeyFrame anim1Key5 = new LinearDoubleKeyFrame
                    {
                        Value = 64.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 220))
                    };
                    DoubleKeyFrame anim1Key6 = new LinearDoubleKeyFrame
                    {
                        Value = 0.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                    };
                    mainAnim3.KeyFrames.Add(anim1Key4);
                    mainAnim3.KeyFrames.Add(anim1Key5);
                    mainAnim3.KeyFrames.Add(anim1Key6);
                    ThicknessAnimationUsingKeyFrames marginAnim3 = new ThicknessAnimationUsingKeyFrames();
                    marginAnim3.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 240));
                    ThicknessKeyFrame marginAnim1Key3 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(0.0, 31.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                    };
                    ThicknessKeyFrame marginAnim1KeyDelay2 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(0.0, 31.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                    };
                    ThicknessKeyFrame marginAnim1Key4 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(46.0, 31.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                    };
                    marginAnim3.KeyFrames.Add(marginAnim1Key3);
                    marginAnim3.KeyFrames.Add(marginAnim1KeyDelay2);
                    marginAnim3.KeyFrames.Add(marginAnim1Key4);
                    DoubleAnimationUsingKeyFrames mainAnim4 = new DoubleAnimationUsingKeyFrames();
                    mainAnim4.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
                    mainAnim4.BeginTime = new TimeSpan(0, 0, 0, 0, 200);
                    DoubleKeyFrame anim2Key3 = new LinearDoubleKeyFrame
                    {
                        Value = 64.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                    };
                    DoubleKeyFrame anim2Key4 = new LinearDoubleKeyFrame
                    {
                        Value = origWidth,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 200))
                    };
                    mainAnim4.KeyFrames.Add(anim2Key3);
                    mainAnim4.KeyFrames.Add(anim2Key4);
                    ThicknessAnimation setMargin2 = new ThicknessAnimation();
                    setMargin2.Duration = new Duration(new TimeSpan(0L));
                    setMargin2.To = new Thickness(46.0, 31.0, 0.0, 0.0);
                    Storyboard.SetTarget(mainAnim3, ActivePB);
                    Storyboard.SetTargetProperty(mainAnim3, new PropertyPath("Width"));
                    Storyboard.SetTarget(marginAnim3, ActivePB);
                    Storyboard.SetTargetProperty(marginAnim3, new PropertyPath("Margin"));
                    Storyboard.SetTarget(mainAnim4, SelectedPB);
                    Storyboard.SetTargetProperty(mainAnim4, new PropertyPath("Width"));
                    Storyboard.SetTarget(setMargin2, SelectedPB);
                    Storyboard.SetTargetProperty(setMargin2, new PropertyPath("Margin"));
                    board.Children.Add(mainAnim3);
                    board.Children.Add(marginAnim3);
                    board.Children.Add(mainAnim4);
                    board.Children.Add(setMargin2);
                    board.Begin();
                    ThicknessAnimationUsingKeyFrames transitionAnim2 = new ThicknessAnimationUsingKeyFrames();
                    transitionAnim2.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540));
                    ThicknessKeyFrame transitionKey5 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                    };
                    ThicknessKeyFrame transitionKey6 = new EasingThicknessKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        Value = new Thickness(0.0, 0.0, -844.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 240))
                    };
                    ThicknessKeyFrame transitionKey7 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(0.0, 0.0, -844.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                    };
                    ThicknessKeyFrame transitionKey8 = new LinearThicknessKeyFrame
                    {
                        Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 540))
                    };
                    transitionAnim2.KeyFrames.Add(transitionKey5);
                    transitionAnim2.KeyFrames.Add(transitionKey6);
                    transitionAnim2.KeyFrames.Add(transitionKey7);
                    transitionAnim2.KeyFrames.Add(transitionKey8);
                    Storyboard.SetTarget(transitionAnim2, MainContainerGrid);
                    Storyboard.SetTargetProperty(transitionAnim2, new PropertyPath("Margin"));
                    board.Children.Add(transitionAnim2);
                    board.Begin();
                    ActivePB.Margin = new Thickness(46.0, 31.0, 0.0, 0.0);
                }
                await Task.Delay(240);
                viewModel.SelectedPlaybook = ItemList[newIndex];
                ActiveItemIndex = newIndex;
                _playbooksLocked = false;
            }
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

        private void SlideModuleUp()
        {
            Storyboard storyboard = new Storyboard();
            DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 260)),
                KeyFrames =
            {
                new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))),
                new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 260)))
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
                Value = new Thickness(0.0, -48.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 160))
            };
            ThicknessKeyFrame transitionKey3 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, -48.0, 0.0, 0.0),
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
        }

        private void SlideModuleDown()
        {
            Storyboard storyboard = new Storyboard();
            DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 260)),
                KeyFrames =
            {
                new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))),
                new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 160)))
            }
            };
            ThicknessAnimationUsingKeyFrames transitionAnim = new ThicknessAnimationUsingKeyFrames
            {
                Duration = new Duration(new TimeSpan(0, 0, 0, 0, 540))
            };
            ThicknessKeyFrame transitionKey1 = new LinearThicknessKeyFrame
            {
                Value = new Thickness(0.0, -48.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
            };
            ThicknessKeyFrame transitionKey2 = new EasingThicknessKeyFrame
            {
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseOut
                },
                Value = new Thickness(0.0, 0.0, 0.0, 0.0),
                KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 160))
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

        private async void CheckForUpdates_OnClick(object sender, RoutedEventArgs e) // fuck updates
        {
            //_playbooksLocked = true;
            //object dataContext = DataContext;
            //if (!(dataContext is UpdatesDialogViewModel viewModel))
            //{
            //    return;
            //}
            //viewModel.UpdateButtonsActive = false;
            //PlaybookGUI pb = viewModel.SelectedPlaybook;
            //Updater updater = new Updater();
            //LoadContainer.Visibility = Visibility.Visible;
            //Spinner spinner = new Spinner
            //{
            //    Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            //};
            //LoadContainer.Children.Add(spinner);
            //if (!(await Task.Run(async () => await new Internet().IsMet())))
            //{
            //    MessageBox.Show(typeof(UpdatesDialog), "You must have an active internet connection to check for updates.", "Information");
            //    viewModel.UpdateButtonsActive = true;
            //    LoadContainer.Visibility = Visibility.Collapsed;
            //    LoadContainer.Children.Remove(spinner);
            //    _playbooksLocked = false;
            //    return;
            //}
            //SlideModuleUp();
            //try
            //{
            //    Task task = Task.Run(async delegate
            //    {
            //        await updater.CheckForUpdates(pb);
            //        if ((pb).Name != "KT WIRZADE Beta")
            //        {
            //            try
            //            {
            //                await pb.WriteEncryptedStatus();
            //            }
            //            catch
            //            {
            //            }
            //        }
            //    });
            //    await Task.Delay(3000);
            //    await task;
            //    pb.LastChecked = DateTime.Now;
            //    pb.UpdatesChecked = true;
            //    if (pb.VerificationStatus == PlaybookGUI.VerificationLevel.Verified && (pb).Name == "KT WIRZADE Beta")
            //    {
            //        WizardConfig.Current.LastChecked.Set(pb.LastChecked);
            //    }
            //    if (pb.PendingUpdate != null)
            //    {
            //        if (pb.VerificationStatus == PlaybookGUI.VerificationLevel.Verified && (pb).Name == "KT WIRZADE Beta")
            //        {
            //            WizardConfig.Current.PendingUpdate.Set(pb.PendingUpdate);
            //        }
            //        viewModel.CheckVisibility = Visibility.Collapsed;
            //        viewModel.InstallVisibility = Visibility.Visible;
            //        viewModel.UpToDateVisibility = Visibility.Collapsed;
            //        viewModel.UpdateReadyVisibility = Visibility.Visible;
            //    }
            //    else
            //    {
            //        viewModel.UpToDateVisibility = Visibility.Visible;
            //    }
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(typeof(UpdatesDialog), "Error while checking for updates: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            //}
            //SlideModuleDown();
            //viewModel.UpdateButtonsActive = true;
            //LoadContainer.Visibility = Visibility.Collapsed;
            //LoadContainer.Children.Remove(spinner);
            //_playbooksLocked = false;
        }

        private async void InstallButton_OnClick(object sender, RoutedEventArgs e)
        {
            //_playbooksLocked = true;
            //object dataContext = DataContext;
            //if (!(dataContext is UpdatesDialogViewModel viewModel))
            //{
            //    return;
            //}
            //viewModel.CloseButtonActive = false;
            //viewModel.UpdateButtonsActive = false;
            //PlaybookGUI pb = viewModel.SelectedPlaybook;
            //BackgroundWorker bg = new BackgroundWorker();
            //bg.ProgressChanged += delegate (object o, ProgressChangedEventArgs args)
            //{
            //    Dispatcher.Invoke(delegate
            //    {
            //        PercentText.Text = args.ProgressPercentage + "%";
            //    });
            //};
            ////Updater updater = new Updater
            ////{
            ////    BackgroundWorker = bg
            ////};
            //LoadContainer.Visibility = Visibility.Visible;
            //Spinner spinner = new Spinner
            //{
            //    Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            //};
            //LoadContainer.Children.Add(spinner);
            //PercentText.Text = "0%";
            //if (!(await Task.Run(async () => await new Internet().IsMet())))
            //{
            //    MessageBox.Show(typeof(UpdatesDialog), "You must have an active internet connection to install an update.", "Information");
            //    viewModel.UpdateButtonsActive = true;
            //    LoadContainer.Visibility = Visibility.Collapsed;
            //    LoadContainer.Children.Remove(spinner);
            //    PercentText.Text = "";
            //    _playbooksLocked = false;
            //    return;
            //}
            //SlideModuleUp();
            //try
            //{
            //    if (pb.VerificationStatus == PlaybookGUI.VerificationLevel.Verified && (pb).Name == "KT WIRZADE Beta")
            //    {
            //        await Task.Run(async delegate
            //        {
            //            await updater.InstallWizardUpdate();
            //        });
            //    }
            //    else
            //    {
            //        PlaybookGUI updatedPb = pb;
            //        string previousName = pb.FileNameWithoutExtension;
            //        bool previousSel = pb.Selected;
            //        int previousSidebar = pb.SidebarInitialHeight;
            //        DateTime previousChecked = pb.LastChecked;
            //        await Task.Run(async delegate
            //        {
            //            updatedPb = await updater.InstallPlaybookUpdate(pb);
            //        });
            //        updatedPb.LastChecked = previousChecked;
            //        updatedPb.Selected = previousSel;
            //        updatedPb.SidebarInitialHeight = previousSidebar;
            //        updatedPb.CurrentPage = new IntroPageViewModel();
            //        viewModel.SelectedPlaybook = updatedPb;
            //        int replace = GlobalsGUI.Current.Items.FindPlaybookIndex((PlaybookGUI x) => x.FileNameWithoutExtension == previousName);
            //        _ = -1;
            //        GlobalsGUI.Current.Items[replace] = updatedPb;
            //        GlobalsGUI.Current.Playbook = (PlaybookGUI)GlobalsGUI.Current.Items[replace];
            //        int replace2 = ItemList.ToList().FindIndex((PlaybookGUI x) => x.FileNameWithoutExtension == previousName);
            //        _ = -1;
            //        ItemList[replace2] = updatedPb;
            //    }
            //    if (pb.VerificationStatus == PlaybookGUI.VerificationLevel.Verified && (pb).Name == "KT WIRZADE Beta")
            //    {
            //        WizardConfig.Current.PendingUpdate.Set(null);
            //    }
            //    viewModel.InstallVisibility = Visibility.Collapsed;
            //    viewModel.CheckVisibility = Visibility.Visible;
            //    viewModel.UpdateReadyVisibility = Visibility.Collapsed;
            //    viewModel.UpToDateVisibility = Visibility.Visible;
            //}
            //catch (Exception ex)
            //{
            //    pb.PendingUpdate = null;
            //    if (pb.VerificationStatus == PlaybookGUI.VerificationLevel.Verified && (pb).Name == "KT WIRZADE Beta")
            //    {
            //        WizardConfig.Current.PendingUpdate.Set(null);
            //    }
            //    viewModel.TransitionInstallVisibility = Visibility.Collapsed;
            //    viewModel.TransitionCheckVisibility = Visibility.Visible;
            //    viewModel.TransitionUpdateReadyVisibility = Visibility.Collapsed;
            //    viewModel.TransitionUpToDateVisibility = Visibility.Collapsed;
            //    viewModel.InstallVisibility = Visibility.Collapsed;
            //    viewModel.CheckVisibility = Visibility.Visible;
            //    viewModel.UpdateReadyVisibility = Visibility.Collapsed;
            //    viewModel.UpToDateVisibility = Visibility.Collapsed;
            //    try
            //    {
            //        if (Assembly.GetExecutingAssembly().Location.EndsWith(".bak"))
            //        {
            //            File.Move(Assembly.GetExecutingAssembly().Location, Assembly.GetExecutingAssembly().Location.Substring(0, Assembly.GetExecutingAssembly().Location.Length - 3) + "exe");
            //        }
            //        if (File.Exists(Assembly.GetExecutingAssembly().Location.Replace(".exe", ".bak")))
            //        {
            //            File.Delete(Assembly.GetExecutingAssembly().Location.Replace(".exe", ".bak"));
            //        }
            //    }
            //    catch (Exception)
            //    {
            //    }
            //    MessageBox.Show(typeof(UpdatesDialog), "Error while installing update: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            //}
            //PercentText.Text = "";
            //SlideModuleDown();
            //viewModel.UpdateButtonsActive = true;
            //viewModel.CloseButtonActive = true;
            //LoadContainer.Visibility = Visibility.Collapsed;
            //LoadContainer.Children.Remove(spinner);
            //_playbooksLocked = false;
        }

        private void SourceButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is UpdatesDialogViewModel { SelectedPlaybook: var pb }))
            {
                return;
            }
            try
            {
                if ((pb).Username == "Ameliorated" && pb.VerificationStatus == PlaybookGUI.VerificationLevel.Verified)
                {
                    System.Diagnostics.Process.Start((pb).Website);
                }
                else
                {
                    System.Diagnostics.Process.Start((pb).Git);
                }
            }
            catch (Exception)
            {
                MessageBox.Show(typeof(UpdatesDialog), "Link is invalid.", "Warning");
            }
        }

        private void DonateButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is UpdatesDialogViewModel viewModel))
            {
                return;
            }
            if (viewModel.SelectedPlaybook.VerificationStatus == PlaybookGUI.VerificationLevel.Verified && (viewModel.SelectedPlaybook).Name == "KT WIRZADE Beta" && (viewModel.SelectedPlaybook).Username == "Ameliorated")
            {
                Popup.IsOpen = true;
                return;
            }
            PlaybookGUI pb = viewModel.SelectedPlaybook;
            try
            {
                System.Diagnostics.Process.Start((pb).DonateLink);
            }
            catch (Exception)
            {
                MessageBox.Show(typeof(UpdatesDialog), "Link is invalid.", "Warning");
            }
        }

        private async Task LoadPlaybook(string apbx)
        {
            _playbooksLocked = true;
            DragBox.SetResourceReference(StyleProperty, "DragBoxLoading");
            PBLoadContainer.Visibility = Visibility.Visible;
            Spinner spinner = new Spinner
            {
                Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
            };
            PBLoadContainer.Children.Add(spinner);
            Storyboard board1 = new Storyboard();
            object dataContext = DataContext;
            if (!(dataContext is UpdatesDialogViewModel viewModel))
            {
                return;
            }
            PlaybookGUI pb = null;
            try
            {
                pb = await Task.Run(() => APBX.ImportAPBX(apbx));
                if (pb == null)
                {
                    board1.Pause();
                    if (MessageBox.Show(typeof(UpdatesDialog), "Selected Playbook already exists. Overwrite?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        board1.Resume();
                        pb = await Task.Run(() => APBX.ImportAPBX(apbx, overwrite: true));
                        PlaybookGUI pbGui = GlobalsGUI.Current.Playbooks.FirstOrDefault((PlaybookGUI x) => ((x).UniqueId.HasValue && (pb).UniqueId.HasValue && (x).UniqueId == (pb).UniqueId) || ((!(x).UniqueId.HasValue || !(pb).UniqueId.HasValue) && (x).Name == (pb).Name && (x).Username == (pb).Username));
                        if (pbGui != null)
                        {
                            (pbGui).Path = "Ignore";
                        }
                        GlobalsGUI.Current.Items.Remove(pbGui);
                        if (string.IsNullOrEmpty((pb).Path))
                        {
                            pb = null;
                            ActiveItemIndex = -1;
                            if (GlobalsGUI.Current.Playbook != null)
                            {
                                GlobalsGUI.Current.Playbook.Selected = false;
                                GlobalsGUI.Current.Playbook.SidebarInitialHeight = 0;
                                GlobalsGUI.Current.Playbook = null;
                            }
                            throw new Exception("Could not remove existing Playbook files.");
                        }
                        int pbIndex = ItemList.ToList().FindIndex((PlaybookGUI x) => ((x).UniqueId.HasValue && (pb).UniqueId.HasValue && (x).UniqueId == (pb).UniqueId) || ((!(x).UniqueId.HasValue || !(pb).UniqueId.HasValue) && (x).Name == (pb).Name && (x).Username == (pb).Username));
                        ItemList[pbIndex] = pb;
                        PBLoadContainer.Visibility = Visibility.Collapsed;
                        PBLoadContainer.Children.Remove(spinner);
                        DragBox.SetResourceReference(StyleProperty, "DragBox");
                        pb.Checked = true;
                        GlobalsGUI.Current.Items.Add(pb);
                        _playbooksLocked = false;
                        SelectItem(new FrameworkElement
                        {
                            DataContext = pb
                        }, new RoutedEventArgs());
                        return;
                    }
                    board1.Stop();
                }
            }
            catch (Exception ex)
            {
                board1.Stop();
                MessageBox.Show(typeof(UpdatesDialog), "Error while attempting to load Playbook: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            PBLoadContainer.Visibility = Visibility.Collapsed;
            PBLoadContainer.Children.Remove(spinner);
            DragBox.SetResourceReference(StyleProperty, "DragBox");
            if (pb != null)
            {
                pb.Checked = true;
                GlobalsGUI.Current.Items.Add(pb);
                int newIndex = ItemList.ToList().FindIndex((PlaybookGUI x) => (x).Name == "None");
                ItemList[newIndex] = pb;
                foreach (TextBlock item in from x in FindVisualChildren<TextBlock>(PlaybookSidebarItems)
                                           where x.Text == (pb).Name
                                           select x)
                {
                    item.Opacity = 1.0;
                }
                _playbooksLocked = false;
                if (newIndex == ActiveItemIndex)
                {
                    viewModel.SelectedPlaybook = pb;
                }
                else
                {
                    SelectItem(new FrameworkElement
                    {
                        DataContext = pb
                    }, new RoutedEventArgs());
                }
            }
            _playbooksLocked = false;
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

        private async void LearnButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://docs.ameliorated.io/developers.html");
            }
            catch (Exception ex)
            {
                MessageBox.Show(typeof(UpdatesDialog), "Error opening link: " + ex.Message, "Warning");
            }
        }

        private void Hyperlink_OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (DataContext is UpdatesDialogViewModel viewModel && viewModel.SelectedPlaybook.VerificationStatus == PlaybookGUI.VerificationLevel.Verified)
            {
                ((Hyperlink)sender).TextDecorations = TextDecorations.Underline;
            }
        }

        private void Hyperlink_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ((Hyperlink)sender).TextDecorations = null;
        }
    }
}