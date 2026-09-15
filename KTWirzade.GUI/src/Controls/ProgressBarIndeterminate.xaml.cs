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
    public partial class ProgressBarIndeterminate : System.Windows.Controls.UserControl
    {

        private bool pendingStop;

        private Storyboard board;

        public TimeSpan BoardTime { get; set; } = new TimeSpan(0, 0, 0, 0, 2500);

        public int BarHeight { get; set; } = 3;

        public int RectWidth { get; set; } = 160;

        public int BetweenDelay { get; set; } = 700;

        public System.Windows.Media.Brush RectBorderBrush { get; set; }

        public System.Windows.Media.Brush RectFill { get; set; }

        public Thickness RectBorderThickness { get; set; } = new Thickness(1.0);

        public CornerRadius RectCornerRadius { get; set; } = new CornerRadius(2.0);

        public ProgressBarIndeterminate()
        {
            InitializeComponent();
            if (RectBorderBrush == null)
            {
                Rect.SetResourceReference(Border.BorderBrushProperty, "ProgressBarBrush");
            }
            if (RectFill == null)
            {
                Rect.SetResourceReference(Border.BackgroundProperty, "ProgressBarBrush");
            }
            DataContext = this;
            Rect.Margin = new Thickness(-RectWidth, 0.0, 0.0, 0.0);
            Loaded += OnLoaded;
        }

        public async Task WaitForAnimation()
        {
            if (board != null)
            {
                pendingStop = true;
                while (pendingStop)
                {
                    await Task.Delay(20);
                }
            }
        }

        public async Task WaitForAnimationFast(double multiplier = 2.0)
        {
            if (board != null)
            {
                board.SetSpeedRatio(multiplier);
                pendingStop = true;
                while (pendingStop)
                {
                    await Task.Delay(20);
                }
            }
        }

        public async void Start()
        {
            if (board == null)
            {
                Container.Clip = new RectangleGeometry(new Rect(0.0, 0.0, ActualWidth, BarHeight));
                RepeatBoard(this, EventArgs.Empty);
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Container.Clip = new RectangleGeometry(new Rect(0.0, 0.0, ActualWidth, BarHeight));
        }

        private async void RepeatBoard(object sender, EventArgs e)
        {
            if (pendingStop)
            {
                board = null;
                pendingStop = false;
                return;
            }
            if (board != null)
            {
                await Task.Delay(BetweenDelay);
            }
            board = new Storyboard();
            ThicknessAnimationUsingKeyFrames anim = new ThicknessAnimationUsingKeyFrames
            {
                KeyFrames = new ThicknessKeyFrameCollection
            {
                new LinearThicknessKeyFrame
                {
                    Value = new Thickness(ActualWidth + 2.0, 0.0, 0.0, 0.0),
                    KeyTime = BoardTime
                },
                new LinearThicknessKeyFrame
                {
                    Value = new Thickness(-(RectWidth + 1), 0.0, 0.0, 0.0),
                    KeyTime = BoardTime
                }
            },
                Duration = BoardTime
            };
            Storyboard.SetTarget(anim, Rect);
            Storyboard.SetTargetProperty(anim, new PropertyPath(FrameworkElement.MarginProperty));
            board.Children.Add(anim);
            board.Begin();
            await Task.Delay((int)BoardTime.TotalMilliseconds);
            RepeatBoard(this, EventArgs.Empty);
        }
    }
}
