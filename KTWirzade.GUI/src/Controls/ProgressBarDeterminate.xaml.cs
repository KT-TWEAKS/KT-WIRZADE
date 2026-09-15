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
    public partial class ProgressBarDeterminate : System.Windows.Controls.UserControl
    {

        public TimeSpan BoardTime = new TimeSpan(0, 0, 0, 0, 2750);

        private double _maximum = 1.0;

        private double _progressOffset;

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(ProgressBarDeterminate), new PropertyMetadata(0.0, OnValueChanged));

        public int BarHeight { get; set; } = 4;

        public System.Windows.Media.Brush ProgressBackground { get; set; }

        public System.Windows.Media.Brush RectBorderBrush { get; set; }

        public System.Windows.Media.Brush RectFill { get; set; }

        public Thickness RectBorderThickness { get; set; } = new Thickness(1.0);

        public CornerRadius CornerRadius { get; set; } = new CornerRadius(1.0);

        /// <summary>
        /// Eased width transition applied on value changes (milliseconds).
        /// </summary>
        public int AnimationDurationMs { get; set; } = 260;

        public double Maximum
        {
            get
            {
                return _maximum;
            }
            set
            {
                _maximum = value;
                if (IsLoaded)
                {
                    _maximum = value;
                    double toBeWidth = Value / Math.Max(_maximum, 1.0) * Container.ActualWidth;
                    if (toBeWidth > Container.ActualWidth)
                    {
                        SetWidthDirect(Container.ActualWidth);
                    }
                    else if (toBeWidth < 0.0)
                    {
                        SetWidthDirect(0.0);
                    }
                    else
                    {
                        SetWidthDirect(toBeWidth);
                    }
                }
            }
        }

        public double ProgressOffset
        {
            get
            {
                return _progressOffset;
            }
            set
            {
                _progressOffset = value;
                if (IsLoaded)
                {
                    if (value > Container.ActualWidth)
                    {
                        SetWidthDirect(Container.ActualWidth);
                    }
                    else if (value < 0.0)
                    {
                        SetWidthDirect(0.0);
                    }
                    else
                    {
                        SetWidthDirect(value);
                    }
                }
            }
        }

        public double Value
        {
            get
            {
                return (double)GetValue(ValueProperty);
            }
            set
            {
                SetValue(ValueProperty, value);
            }
        }

        public ProgressBarDeterminate()
        {
            InitializeComponent();
            if (ProgressBackground == null)
            {
                Container.SetResourceReference(Border.BackgroundProperty, "ProgressBarBackground");
            }
            if (RectBorderBrush == null)
            {
                Rect.SetResourceReference(Border.BorderBrushProperty, "ProgressBarBrush");
            }
            if (RectFill == null)
            {
                Rect.SetResourceReference(Border.BackgroundProperty, "ProgressBarBrush");
            }
            DataContext = this;
            Loaded += delegate
            {
                Container.SizeChanged += delegate { RefreshWidth(); };
                RefreshWidth();
            };
        }

        private void RefreshWidth()
        {
            double num = Value / Math.Max(_maximum, 1.0) * (Container.ActualWidth - _progressOffset) + _progressOffset;
            if (num > Container.ActualWidth)
            {
                SetWidthDirect(Container.ActualWidth);
            }
            else if (num < 0.0)
            {
                SetWidthDirect(0.0);
            }
            else
            {
                SetWidthDirect(num);
            }
        }

        private void SetWidthDirect(double width)
        {
            // Clear any running animation so the direct value wins.
            Rect.BeginAnimation(Border.WidthProperty, null);
            Rect.Width = width;
        }

        private void AnimateToWidth(double targetWidth)
        {
            if (!IsLoaded || Container.ActualWidth <= 0 || AnimationDurationMs <= 0)
            {
                SetWidthDirect(targetWidth);
                return;
            }

            DoubleAnimation animation = new DoubleAnimation
            {
                From = Rect.ActualWidth,
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(AnimationDurationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Rect.BeginAnimation(Border.WidthProperty, animation);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ProgressBarDeterminate control = (ProgressBarDeterminate)d;
            if (control.IsLoaded)
            {
                double num = (double)e.NewValue;
                double maximum = control._maximum;
                double containerWidth = control.Container.ActualWidth;
                double progressOffset = control.ProgressOffset;
                double toBeWidth = num / Math.Max(maximum, 1.0) * (containerWidth - progressOffset) + progressOffset;
                if (toBeWidth > containerWidth)
                {
                    control.AnimateToWidth(containerWidth);
                }
                else if (toBeWidth < 0.0)
                {
                    control.AnimateToWidth(0.0);
                }
                else
                {
                    control.AnimateToWidth(toBeWidth);
                }
            }
        }
    }
}
