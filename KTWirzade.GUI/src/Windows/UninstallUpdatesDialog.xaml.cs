using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.UninstallUpdateDialog;
using KTWirzade.GUI.Utils;
using static Core.Win32;

namespace KTWirzade.GUI.Windows
{
    public partial class UninstallUpdatesDialog : AcrylicWindow
    {
        private struct ImageParams
        {
            internal int scaleY;

            internal int scaleX;

            internal BitmapImage raw;
        }

        public static List<uint> BypassedUpdates = new List<uint>();

        public static List<KeyValuePair<uint, string>> ExcludedUpdates = new List<KeyValuePair<uint, string>>();

        public static List<KeyValuePair<uint, string>> PresentUninstallUpdates = new List<KeyValuePair<uint, string>>();

        private int _pageIndex;

        public UninstallUpdatesDialog()
        {
            InitializeComponent();
            int i = -1;
            foreach (KeyValuePair<uint, string> update in PresentUninstallUpdates)
            {
                i++;
                UninstallUpdateStack.Children.Add(new Default
                {
                    UpdateID = update.Key,
                    UpdateDescription = update.Value
                });
                ((IUninstallUpdateModule)UninstallUpdateStack.Children[i]).Completed += OnUninstallUpdateCompleted;
                if (i == PresentUninstallUpdates.Count - 1)
                {
                    UninstallUpdateStack.Children.Cast<IUninstallUpdateModule>().Last().SetLast();
                }
                ExcludeGeometries.Children.Add(GenerateGeometry(i, PresentUninstallUpdates.Count - 1));
            }
            base.Loaded += OnLoaded;
        }

        private void OnUninstallUpdateCompleted(object sender, EventArgs e)
        {
            for (int i = UninstallUpdateStack.Children.Count; i < PresentUninstallUpdates.Count; i++)
            {
                Default element = new Default
                {
                    UpdateID = PresentUninstallUpdates[i].Key,
                    UpdateDescription = PresentUninstallUpdates[i].Value,
                    Opacity = 0.0,
                    Margin = new Thickness(0.0, 15.0, 0.0, 0.0)
                };
                element.Loaded += delegate
                {
                    Storyboard storyboard2 = new Storyboard();
                    ThicknessAnimation thicknessAnimation = new ThicknessAnimation
                    {
                        To = new Thickness(0.0, 0.0, 0.0, 0.0),
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseOut
                        },
                        Duration = new Duration(TimeSpan.FromMilliseconds(500.0))
                    };
                    Storyboard.SetTarget(thicknessAnimation, element);
                    Storyboard.SetTargetProperty(thicknessAnimation, new PropertyPath(FrameworkElement.MarginProperty));
                    storyboard2.Children.Add(thicknessAnimation);
                    DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames
                    {
                        Duration = new Duration(new TimeSpan(0, 0, 0, 0, 400)),
                        KeyFrames = { (DoubleKeyFrame)new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 400))) }
                    };
                    Storyboard.SetTarget(doubleAnimationUsingKeyFrames, element);
                    Storyboard.SetTargetProperty(doubleAnimationUsingKeyFrames, new PropertyPath("Opacity"));
                    storyboard2.Children.Add(doubleAnimationUsingKeyFrames);
                    storyboard2.Begin();
                    storyboard2.Completed += delegate
                    {
                        ExcludeGeometries.Children.Add(GenerateGeometry(i, PresentUninstallUpdates.Count - 1));
                    };
                };
                UninstallUpdateStack.Children.Add(element);
                ((IUninstallUpdateModule)UninstallUpdateStack.Children[i]).Completed += OnUninstallUpdateCompleted;
                if (i == PresentUninstallUpdates.Count - 1)
                {
                    UninstallUpdateStack.Children.Cast<IUninstallUpdateModule>().Last().SetLast();
                }
            }
            if (_pageIndex != PresentUninstallUpdates.Count - 1)
            {
                Storyboard storyboard = new Storyboard();
                ThicknessAnimation buttonAnim = new ThicknessAnimation(new Thickness(0.0, 392.0, 185.0, 0.0), new Duration(TimeSpan.FromMilliseconds(0.0)));
                Storyboard.SetTarget(buttonAnim, SwitchPageButton);
                Storyboard.SetTargetProperty(buttonAnim, new PropertyPath("Margin"));
                storyboard.Children.Add(buttonAnim);
                DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames
                {
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 100)),
                    KeyFrames =
                {
                    (DoubleKeyFrame)new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))),
                    (DoubleKeyFrame)new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 100)))
                }
                };
                Storyboard.SetTarget(opacityAnim, SwitchPageButton);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
                storyboard.Children.Add(opacityAnim);
                DoubleAnimationUsingKeyFrames scale_x = new DoubleAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromMilliseconds(350.0),
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new LinearDoubleKeyFrame
                    {
                        Value = 0.75,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                    },
                    new EasingDoubleKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseOut
                        },
                        Value = 1.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 350))
                    }
                }
                };
                DoubleAnimationUsingKeyFrames scale_y = new DoubleAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromMilliseconds(350.0),
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new LinearDoubleKeyFrame
                    {
                        Value = 0.75,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))
                    },
                    new EasingDoubleKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseOut
                        },
                        Value = 1.0,
                        KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 350))
                    }
                }
                };
                Storyboard.SetTargetName(scale_x, "ContinueScaleTransform");
                Storyboard.SetTargetProperty(scale_x, new PropertyPath(ScaleTransform.ScaleXProperty));
                storyboard.Children.Add(scale_x);
                Storyboard.SetTargetName(scale_y, "ContinueScaleTransform");
                Storyboard.SetTargetProperty(scale_y, new PropertyPath(ScaleTransform.ScaleXProperty));
                storyboard.Children.Add(scale_y);
                DoubleAnimation moveAnim = new DoubleAnimation
                {
                    To = -24.0,
                    EasingFunction = new SineEase
                    {
                        EasingMode = EasingMode.EaseOut
                    },
                    Duration = new Duration(TimeSpan.FromMilliseconds(350.0))
                };
                Storyboard.SetTargetName(moveAnim, "ContinueTranslateTransform");
                Storyboard.SetTargetProperty(moveAnim, new PropertyPath(TranslateTransform.YProperty));
                storyboard.Children.Add(moveAnim);
                storyboard.Begin(this);
                SwitchPageButton.IsHitTestVisible = true;
            }
        }

        private RectangleGeometry GenerateGeometry(int index, int lastIndex)
        {
            return new RectangleGeometry(new Rect(395.5, 40.5 + (double)((from IUninstallUpdateModule x in UninstallUpdateStack.Children
                                                                          where x != UninstallUpdateStack.Children[index]
                                                                          select x).Sum((IUninstallUpdateModule x) => x.Height() + 26) * index), 431.3, (index != lastIndex) ? 484 : 500), 8.0, 8.0);
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

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
            {
                RootWindow.SetResourceReference(BackgroundProperty, "FakeBackgroundBrush");
            }
            double ppuX = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
            double ppuY = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M22;
            UninstallUpdateStack.Children.Cast<IUninstallUpdateModule>().First().StartOperations();
            List<ImageParams> images = new List<ImageParams>();
            List<System.Windows.Controls.Image> raw = (from image in FindVisualChildren<System.Windows.Controls.Image>(UninstallUpdateStack)
                                                       where image.Name != null && !image.Name.Contains("Icon")
                                                       select image).ToList();
            raw.ForEach(delegate (System.Windows.Controls.Image x)
            {
                images.Add(new ImageParams
                {
                    raw = (BitmapImage)FindResource(x.Name + "Bitmap"),
                    scaleY = (int)(ppuY * x.ActualHeight),
                    scaleX = (int)(ppuX * x.ActualWidth)
                });
            });
            List<BitmapSource> sources = new List<BitmapSource>();
            await Task.Run(delegate
            {
                foreach (ImageParams current in images)
                {
                    sources.Add(Convert(ImageUtilities.ResizeImage(BitmapImage2Bitmap(current.raw), current.scaleX, current.scaleY)));
                }
            });
            int i = 0;
            foreach (BitmapSource source in sources)
            {
                raw[i].Source = source;
                i++;
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
            CloseWindow(securityscale);
        }

        private async void SwitchPageButton_OnClick(object sender, RoutedEventArgs e)
        {
            Storyboard storyboard = new Storyboard();
            SwitchPageButton.IsEnabled = false;
            ThicknessAnimation buttonAnim = new ThicknessAnimation(new Thickness(0.0, SwitchPageButton.Margin.Top - 1300.0, 185.0, 0.0), new Duration(TimeSpan.FromMilliseconds(400.0)))
            {
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };
            Storyboard.SetTarget(buttonAnim, SwitchPageButton);
            Storyboard.SetTargetProperty(buttonAnim, new PropertyPath("Margin"));
            storyboard.Children.Add(buttonAnim);
            DoubleAnimation moveAnim = new DoubleAnimation
            {
                BeginTime = TimeSpan.FromMilliseconds(60.0),
                To = 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(0.0))
            };
            Storyboard.SetTargetName(moveAnim, "ContinueTranslateTransform");
            Storyboard.SetTargetProperty(moveAnim, new PropertyPath(TranslateTransform.YProperty));
            storyboard.Children.Add(moveAnim);
            ThicknessAnimation pageAnim = new ThicknessAnimation
            {
                To = new Thickness(0.0, UninstallUpdateStack.Margin.Top - (double)(UninstallUpdateStack.Children.Cast<IUninstallUpdateModule>().ToList()[_pageIndex].Height() + 26), 0.0, 0.0),
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                },
                Duration = new Duration(TimeSpan.FromMilliseconds(400.0))
            };
            Storyboard.SetTarget(pageAnim, UninstallUpdateStack);
            Storyboard.SetTargetProperty(pageAnim, new PropertyPath(FrameworkElement.MarginProperty));
            storyboard.Children.Add(pageAnim);
            ThicknessAnimation backdropAnim = new ThicknessAnimation
            {
                To = new Thickness(0.0, FillRect.Margin.Top - (double)(UninstallUpdateStack.Children.Cast<IUninstallUpdateModule>().ToList()[_pageIndex].Height() + 26), 0.0, 0.0),
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                },
                Duration = new Duration(TimeSpan.FromMilliseconds(400.0))
            };
            Storyboard.SetTarget(backdropAnim, FillRect);
            Storyboard.SetTargetProperty(backdropAnim, new PropertyPath(FrameworkElement.MarginProperty));
            storyboard.Children.Add(backdropAnim);
            UIElement activePage = UninstallUpdateStack.Children[_pageIndex];
            RectangleGeometry activeRect = (RectangleGeometry)ExcludeGeometries.Children[_pageIndex];
            RegisterName("Rect" + _pageIndex, activeRect);
            activePage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            activePage.RenderTransform = new TranslateTransform();
            RegisterName("TransformPage" + _pageIndex, activePage.RenderTransform);
            DoubleAnimation pageAnimActive = new DoubleAnimation
            {
                BeginTime = TimeSpan.FromMilliseconds(170.0),
                To = -150.0,
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseIn
                },
                Duration = new Duration(TimeSpan.FromMilliseconds(230.0))
            };
            Storyboard.SetTargetName(pageAnimActive, "TransformPage" + _pageIndex);
            Storyboard.SetTargetProperty(pageAnimActive, new PropertyPath(TranslateTransform.YProperty));
            storyboard.Children.Add(pageAnimActive);
            RectAnimation pageAnimRectActive = new RectAnimation
            {
                BeginTime = TimeSpan.FromMilliseconds(150.0),
                To = new Rect(activeRect.Rect.Left, activeRect.Rect.Top, activeRect.Rect.Width, activeRect.Rect.Height - 150.0),
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseIn
                },
                Duration = new Duration(TimeSpan.FromMilliseconds(230.0))
            };
            Storyboard.SetTargetName(pageAnimRectActive, "Rect" + _pageIndex);
            Storyboard.SetTargetProperty(pageAnimRectActive, new PropertyPath(RectangleGeometry.RectProperty));
            storyboard.Children.Add(pageAnimRectActive);
            storyboard.Begin(this);
            _pageIndex++;
            await Task.Delay(350);
            SwitchPageButton.IsEnabled = true;
            ((IUninstallUpdateModule)UninstallUpdateStack.Children[_pageIndex]).StartOperations();
        }

    }
}