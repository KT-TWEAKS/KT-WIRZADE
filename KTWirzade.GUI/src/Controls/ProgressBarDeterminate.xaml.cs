using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
                        Rect.Width = Container.ActualWidth;
                    }
                    else if (toBeWidth < 0.0)
                    {
                        Rect.Width = 0.0;
                    }
                    else
                    {
                        Rect.Width = toBeWidth;
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
                        Rect.Width = Container.ActualWidth;
                    }
                    else if (value < 0.0)
                    {
                        Rect.Width = 0.0;
                    }
                    else
                    {
                        Rect.Width = value;
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
                double num = Value / Math.Max(_maximum, 1.0) * Container.ActualWidth;
                if (num > Container.ActualWidth)
                {
                    Rect.Width = Container.ActualWidth;
                }
                else if (num < 0.0)
                {
                    Rect.Width = 0.0;
                }
                else
                {
                    Rect.Width = num;
                }
            };
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
                    control.Rect.Width = containerWidth;
                }
                else if (toBeWidth < 0.0)
                {
                    control.Rect.Width = 0.0;
                }
                else
                {
                    control.Rect.Width = toBeWidth;
                }
            }
        }
    }
}
