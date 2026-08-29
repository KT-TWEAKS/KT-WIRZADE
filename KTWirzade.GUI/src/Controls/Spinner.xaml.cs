using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
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
    public partial class Spinner : System.Windows.Controls.UserControl
    {

        private const double duration = 1000.0;

        public Spinner()
        {
            InitializeComponent();
        }

        private void OnLoad(object sender, RoutedEventArgs e)
        {
            double centerX = canvas.ActualWidth / 2.0;
            double centerY = canvas.ActualHeight / 2.0;
            double r1 = Math.Min(centerX, centerY);
            Ellipse dott = new Ellipse();
            double width = (dott.Height = 20.0);
            dott.Width = width;
            dott.Fill = Foreground;
            dott.StrokeThickness = 2.0;
            dott.Visibility = Visibility.Hidden;
            canvas.Children.Add(dott);
            Canvas.SetTop(dott, centerY);
            Canvas.SetLeft(dott, centerX);
            double d2 = Math.PI * r1 / 10.0 * 0.75;
            double r2 = d2 / 2.0;
            double wait = 200.0;
            LoopAnimation(d2, r2, centerX, centerY, wait);
        }

        private void LoopAnimation(double d2, double r2, double centerX, double centerY, double wait)
        {
            for (int i = 1; i <= 5; i++)
            {
                Ellipse dot = new Ellipse();
                Ellipse ellipse = dot;
                double width = (dot.Height = d2);
                ellipse.Width = width;
                dot.Fill = Foreground;
                dot.StrokeThickness = 0.0;
                dot.Visibility = Visibility.Hidden;
                canvas.Children.Add(dot);
                Canvas.SetTop(dot, canvas.Height - r2 * 2.0);
                Canvas.SetLeft(dot, centerX - r2);
                dot.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                RotateTransform rotateTransform = new RotateTransform
                {
                    CenterY = r2 - centerY
                };
                dot.RenderTransform = rotateTransform;
                bool secLoop = false;
                DoubleAnimation dotanim = new DoubleAnimation(0.0, 360.0, new Duration(TimeSpan.FromMilliseconds(wait * i)));
                dotanim.Completed += delegate
                {
                    dot.Visibility = Visibility.Visible;
                    DoubleAnimation Anim1 = new DoubleAnimation(0.0, 90.0, new Duration(TimeSpan.FromMilliseconds(wait)));
                    Anim1.Completed += delegate
                    {
                        DoubleAnimation doubleAnimation = new DoubleAnimation(90.0, 270.0, new Duration(TimeSpan.FromMilliseconds(7.0 * wait)));
                        doubleAnimation.Completed += delegate
                        {
                            DoubleAnimation doubleAnimation2 = new DoubleAnimation(270.0, 360.0, new Duration(TimeSpan.FromMilliseconds(wait)));
                            doubleAnimation2.Completed += delegate
                            {
                                if (secLoop)
                                {
                                    dot.Visibility = Visibility.Hidden;
                                    secLoop = false;
                                    DoubleAnimation doubleAnimation3 = new DoubleAnimation(0.0, 360.0, new Duration(TimeSpan.FromMilliseconds(1500.0)));
                                    doubleAnimation3.Completed += delegate
                                    {
                                        canvas.Children.Remove(dot);
                                        if (canvas.Children.Count == 1)
                                        {
                                            LoopAnimation(d2, r2, centerX, centerY, wait);
                                        }
                                    };
                                    rotateTransform.BeginAnimation(RotateTransform.AngleProperty, doubleAnimation3);
                                }
                                else
                                {
                                    secLoop = true;
                                    rotateTransform.BeginAnimation(RotateTransform.AngleProperty, Anim1);
                                }
                            };
                            rotateTransform.BeginAnimation(RotateTransform.AngleProperty, doubleAnimation2);
                        };
                        rotateTransform.BeginAnimation(RotateTransform.AngleProperty, doubleAnimation);
                    };
                    rotateTransform.BeginAnimation(RotateTransform.AngleProperty, Anim1);
                };
                rotateTransform.BeginAnimation(RotateTransform.AngleProperty, dotanim);
            }
        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            canvas.SizeChanged -= Canvas_SizeChanged;
            Canvas obj = canvas;
            double height = (canvas.Width = Math.Min(panel.ActualHeight, panel.ActualWidth));
            obj.Height = height;
            canvas.SizeChanged += Canvas_SizeChanged;
        }
    }
}
