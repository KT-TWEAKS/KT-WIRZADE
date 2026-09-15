using FluentIcons.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace KTWirzade.GUI.Controls
{
    public partial class TextDropDownBox : System.Windows.Controls.UserControl
    {

        private double BoxHeight = -1.0;

        public StackPanel Contents { get; set; }

        public int ThumbHeight { get; set; } = 19;

        public bool IsOpen { get; private set; }

        public string Title { get; set; }

        public event RoutedEventHandler Click;

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

        public async Task Open()
        {
            if (!IsOpen)
            {
                IsOpen = true;
                DoubleAnimation chevanimation = new DoubleAnimation
                {
                    To = -180.0,
                    Duration = TimeSpan.FromSeconds(0.18)
                };
                RotateTransform rotate = new RotateTransform(0.0)
                {
                    CenterX = 0.0,
                    CenterY = 0.0
                };
                SymbolIcon obj = FindVisualChildren<SymbolIcon>((DependencyObject)Button).Last();
                ((UIElement)(object)obj).RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                ((UIElement)(object)obj).RenderTransform = rotate;
                ((UIElement)(object)obj).RenderTransform.BeginAnimation(RotateTransform.AngleProperty, chevanimation);
                Border BoxContainer = FindVisualChildren<Border>(Button).First();
                if (BoxHeight == -1.0)
                {
                    BoxHeight = BoxContainer.Height;
                }
                DoubleAnimationUsingKeyFrames adjust = new DoubleAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromMilliseconds(30.0),
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new EasingDoubleKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseOut
                        },
                        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(30.0)),
                        Value = BoxHeight + 4.0
                    }
                }
                };
                BoxContainer.BeginAnimation(HeightProperty, adjust);
                BoxContainer.CornerRadius = new CornerRadius(5.0, 5.0, 0.0, 0.0);
                DoubleAnimationUsingKeyFrames animation = new DoubleAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromSeconds(0.18),
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new EasingDoubleKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180.0)),
                        Value = 148.0
                    }
                }
                };
                ScrollBorder.BeginAnimation(HeightProperty, animation);
            }
        }

        public async Task Close()
        {
            if (IsOpen)
            {
                IsOpen = false;
                Storyboard storyboard = new Storyboard();
                DoubleAnimation chevanimation = new DoubleAnimation
                {
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(180.0)
                };
                RotateTransform rotate = new RotateTransform(-180.0)
                {
                    CenterX = 0.0,
                    CenterY = 0.0
                };
                SymbolIcon obj = FindVisualChildren<SymbolIcon>(Button).Last();
                ((UIElement)(object)obj).RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                ((UIElement)(object)obj).RenderTransform = rotate;
                ((UIElement)(object)obj).RenderTransform.BeginAnimation(RotateTransform.AngleProperty, chevanimation);
                DoubleAnimationUsingKeyFrames animation = new DoubleAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromMilliseconds(180.0),
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new EasingDoubleKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        },
                        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180.0)),
                        Value = 0.0
                    }
                }
                };
                Storyboard.SetTarget(animation, ScrollBorder);
                Storyboard.SetTargetProperty(animation, new PropertyPath(FrameworkElement.HeightProperty));
                storyboard.Children.Add(animation);
                Border BoxContainer = FindVisualChildren<Border>(Button).First();
                if (BoxHeight == -1.0)
                {
                    BoxHeight = BoxContainer.Height;
                }
                DoubleAnimationUsingKeyFrames adjust = new DoubleAnimationUsingKeyFrames
                {
                    BeginTime = TimeSpan.FromMilliseconds(135.0),
                    Duration = TimeSpan.FromMilliseconds(60.0),
                    KeyFrames = new DoubleKeyFrameCollection
                {
                    new EasingDoubleKeyFrame
                    {
                        EasingFunction = new SineEase
                        {
                            EasingMode = EasingMode.EaseOut
                        },
                        KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60.0)),
                        Value = BoxHeight
                    }
                }
                };
                Storyboard.SetTarget(adjust, BoxContainer);
                Storyboard.SetTargetProperty(adjust, new PropertyPath(FrameworkElement.HeightProperty));
                storyboard.Children.Add(adjust);
                storyboard.Begin();
                await Task.Delay(135);
                BoxContainer.CornerRadius = new CornerRadius(5.0, 5.0, 5.0, 5.0);
            }
        }

        public TextDropDownBox()
        {
            InitializeComponent();
            DataContext = this;
            Scroller.ScrollChanged += delegate (object sender, ScrollChangedEventArgs args)
            {
                if (IsLoaded)
                {
                    int num = 127 - ThumbHeight;
                    double verticalOffset = args.VerticalOffset;
                    double scrollableHeight = Scroller.ScrollableHeight;
                    double num2 = verticalOffset / scrollableHeight;
                    double top = (double)num * num2 + 5.0;
                    Thumb.Margin = new Thickness(0.0, top, -12.0, 0.0);
                }
            };
        }

        private async void DropButton_OnClick(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(sender, e);
            if (IsOpen)
            {
                await Close();
            }
            else
            {
                await Open();
            }
        }
    }
}
