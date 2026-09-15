using System.ComponentModel;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using KTWirzade.GUI.Controls;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace KTWirzade.GUI.Windows
{
    public partial class UACBox : Window
    {
        public const string VERIFIEDPUBLISHER = "Verified publisher";

        public const string PROGRAMLOCATION = "Program location";

        public static readonly System.Windows.Media.Brush VerifiedColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#76b9ed"));

        public static readonly System.Windows.Media.Brush WarningColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ffd45c"));

        public static readonly System.Windows.Media.Brush BlockedColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#e81123"));

        private bool _shown;

        private bool CanClose;


        public string BoxTitle
        {
            set
            {
                base.Title = value;
                TitleText.Text = value;
            }
        }

        public string BoxCaption
        {
            set
            {
                TBCaption.Text = value;
            }
        }

        public string BoxProgramName
        {
            set
            {
                TBProgramName.Text = value;
            }
        }

        public string BoxDescription1
        {
            set
            {
                TBDescription1.Text = value;
            }
        }

        public string BoxDescription2
        {
            set
            {
                TBDescription2.Text = value;
            }
        }

        public ImageSource BoxIcon
        {
            set
            {
                IconProgram.Source = value;
            }
        }

        public System.Windows.Media.Brush TitleColor
        {
            set
            {
                TitleBarHead.Background = value;
                TBCaption.Background = value;
            }
        }

        public event EventHandler UserAceepted;

        private async void OpenAnimation()
        {
            DoubleAnimation animation = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200.0));
            BeginAnimation(OpacityProperty, animation);
            DoubleAnimation scale_x = new DoubleAnimation
            {
                From = 0.9,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(200.0)
            };
            DoubleAnimation scale_y = new DoubleAnimation
            {
                From = 0.9,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(200.0)
            };
            UACScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale_x);
            UACScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale_y);
            await Task.Delay(200);
        }

        public UACBox()
        {
            InitializeComponent();
            OpenAnimation();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (!_shown)
            {
                _shown = true;
                StartSound();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ResultButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (((System.Windows.Controls.Button)sender).Equals(btnYes))
                {
                    OnUserAccepted();
                    return;
                }
                DialogResult = ((System.Windows.Controls.Button)sender).Equals(btnYes);
            }
            catch
            {
            }
            Close();
        }
        protected virtual async void OnUserAccepted()
        {
            btnYes.Visibility = Visibility.Hidden;
            btnNo.Visibility = Visibility.Hidden;
            CloseButton.Visibility = Visibility.Hidden;
            TBDescription2.Visibility = Visibility.Hidden;

            Spinner spinner = new Spinner
            {
                Height = 20.0,
                Width = 20.0
            };
            MainGrid.Children.Add(spinner);
            Grid.SetRow(spinner, 4);
            Grid.SetRowSpan(spinner, 3);
            Grid.SetColumnSpan(spinner, 3);

            if (UserAceepted != null)
            {
                Delegate[] eventListeners = UserAceepted.GetInvocationList();
                Console.WriteLine("Raising Event");

                IEnumerable<Task> tasks = eventListeners.Cast<EventHandler>().Select(handler =>
                    Task.Run(() =>
                    {
                        try
                        {
                            handler(this, EventArgs.Empty);
                        }
                        catch
                        {
                            Console.WriteLine("An event listener went kaboom!");
                        }
                    }));

                await Task.WhenAll(tasks);

                Console.WriteLine("Done Raising Event");
            }
        }

        private void StartSound()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey("AppEvents\\Schemes\\Apps\\.Default\\WindowsUAC\\.Current");
                new SoundPlayer((string)key.GetValue(null)).Play();
                return;
            }
            catch
            {
            }
            SystemSounds.Question.Play();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!CanClose)
            {
                e.Cancel = true;
            }
        }

        public new async void Close()
        {
            CanClose = true;
            DoubleAnimation animation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200.0));
            BeginAnimation(OpacityProperty, animation);
            DoubleAnimation scale_x = new DoubleAnimation
            {
                From = 1.0,
                To = 0.9,
                Duration = TimeSpan.FromMilliseconds(200.0)
            };
            DoubleAnimation scale_y = new DoubleAnimation
            {
                From = 1.0,
                To = 0.9,
                Duration = TimeSpan.FromMilliseconds(200.0)
            };
            UACScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale_x);
            UACScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale_y);
            await Task.Delay(200);
            base.Close();
        }
    }
}