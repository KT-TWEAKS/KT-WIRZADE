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
using KTWirzade.GUI.TweakDialog;
using KTWirzade.GUI.Utils;
using static Core.Win32;

namespace KTWirzade.GUI.Windows
{
    public partial class TweaksDialog : AcrylicWindow
    {
        public enum Tweak
        {
            Shutup10,
            Start11,
            StartAllBack,
            Rectify11,
            CCleaner
        }

        private struct ImageParams
        {
            internal int scaleY;

            internal int scaleX;

            internal BitmapImage raw;
        }

        public static List<Tweak> Tweaks = new List<Tweak>();

        private int _pageIndex;

        public TweaksDialog()
        {
            InitializeComponent();
            //string exe = InterLink.Execute((Expression<Func<string>>)(() => Shutup10.GetExePath()), false, -1);
            //Shutup10.FileLocation = ((exe == "null") ? null : exe);
            //Shutup10.IsPresent();
            //int i = -1;
            //foreach (Tweak tweak in Tweaks)
            //{
            //    i++;
            //    switch (tweak)
            //    {
            //        case Tweak.Shutup10:
            //            TweakStack.Children.Add(new Shutup10());
            //            break;
            //        case Tweak.StartAllBack:
            //            TweakStack.Children.Add(new StartAllBack());
            //            break;
            //        case Tweak.Start11:
            //            TweakStack.Children.Add(new Start11());
            //            break;
            //        case Tweak.Rectify11:
            //            TweakStack.Children.Add(new Rectify11());
            //            break;
            //        case Tweak.CCleaner:
            //            TweakStack.Children.Add(new CCleaner());
            //            break;
            //    }
            //    ((ITweakModule)TweakStack.Children[i]).Completed += OnTweakCompleted;
            //    ExcludeGeometries.Children.Add(GenerateGeometry(i, Tweaks.Count - 1));
            //}
            Loaded += OnLoaded;
        }

        private void OnTweakCompleted(object sender, EventArgs e)
        {
            if (!TweakStack.Children.Cast<ITweakModule>().Skip(_pageIndex + 1).Any((ITweakModule x) => x.IsUninstallable()))
            {
                try
                {
                    System.Diagnostics.Process.GetProcessesByName("SystemSettings").FirstOrDefault()?.Kill();
                }
                catch (Exception)
                {
                }
            }
            if (_pageIndex != Tweaks.Count - 1)
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
                    new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 0))),
                    new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, 100)))
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
            return new RectangleGeometry(new Rect(395.5, 40.5 + (double)(511 * index), 431.3, (index != lastIndex) ? 484 : 500), 8.0, 8.0);
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
            TweakStack.Children.Cast<ITweakModule>().Last().SetLast();
            if (MaterialManager.IsVMwareVM && SystemInfoEx.WindowsVersion.BuildNumber >= 22523)
            {
                RootWindow.SetResourceReference(BackgroundProperty, "FakeBackgroundBrush");
            }
            double ppuX = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
            double ppuY = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M22;
            TweakStack.Children.Cast<ITweakModule>().First().StartOperations();
            List<ImageParams> images = new List<ImageParams>();
            List<System.Windows.Controls.Image> raw = (from image in FindVisualChildren<System.Windows.Controls.Image>(TweakStack)
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

        public void ShowDialog(Window owner, string playbookName)
        {
            Owner = owner;
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
                To = new Thickness(0.0, TweakStack.Margin.Top - 511.0, 0.0, 0.0),
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                },
                Duration = new Duration(TimeSpan.FromMilliseconds(400.0))
            };
            Storyboard.SetTarget(pageAnim, TweakStack);
            Storyboard.SetTargetProperty(pageAnim, new PropertyPath(FrameworkElement.MarginProperty));
            storyboard.Children.Add(pageAnim);
            ThicknessAnimation backdropAnim = new ThicknessAnimation
            {
                To = new Thickness(0.0, FillRect.Margin.Top - 511.0, 0.0, 0.0),
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                },
                Duration = new Duration(TimeSpan.FromMilliseconds(400.0))
            };
            Storyboard.SetTarget(backdropAnim, FillRect);
            Storyboard.SetTargetProperty(backdropAnim, new PropertyPath(FrameworkElement.MarginProperty));
            storyboard.Children.Add(backdropAnim);
            UIElement activePage = TweakStack.Children[_pageIndex];
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
            ((ITweakModule)TweakStack.Children[_pageIndex]).StartOperations();
        }
    }
}