using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using KTWirzade.GUI.Controls;

namespace KTWirzade.GUI
{
    public partial class MessageBox : AcrylicWindow
    {
        public MessageBoxButton Button;

        public MessageBoxImage Image;

        private MessageBoxResult _result = MessageBoxResult.Unset;

        private static string _logPath;

        public MessageBox()
        {
            InitializeComponent();
            Loaded += async delegate
            {
                SetForegroundWindow(new WindowInteropHelper(this).Handle);
            };
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        public MessageBoxResult ShowDialog(Window owner)
        {
            Owner = owner;
            if (Owner == null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            ShowDialog();
            return _result;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_result == MessageBoxResult.Unset && Parent != null)
            {
                e.Cancel = true;
            }
            else
            {
                OnClosing(e);
            }
        }

        public static MessageBoxResult Show(DependencyObject caller, string message, string title = null, 
            MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information, string monoText = null)
        {
            MessageBox box = new();
            box.TitleText.Text = (title == null) ? icon.ToString() : title;
            box.Title = (box.TitleText.Text.Length > 11) ? icon.ToString() : box.TitleText.Text;
            box.Button = button;
            box.Image = icon;
            box.DescriptionText.Text = message;
            box.MonoBox.Text = ((monoText == null) ? "" : monoText);
            box.MonoBox.Visibility = ((monoText == null) ? Visibility.Collapsed : Visibility.Visible);
            switch (button)
            {
                case MessageBoxButton.Exit:
                    box.MainButtonText.Text = "Exit";
                    box.SecondButton.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.YesNo:
                    box.MainButtonText.Text = "Yes";
                    box.SecondButtonText.Text = "No";
                    break;
                case MessageBoxButton.OKBypass:
                    box.MainButtonText.Text = "OK";
                    box.SecondButtonText.Text = "Bypass";
                    break;
                case MessageBoxButton.ImFineBypass:
                    box.MainButtonText.Text = "I'm Fine";
                    box.SecondButtonText.Text = "Bypass";
                    break;
                case MessageBoxButton.OK:
                    box.MainButtonText.Text = "OK";
                    box.SecondButton.Visibility = Visibility.Collapsed;
                    break;
            }
            if (icon == MessageBoxImage.NoImage)
            {
                box.DescriptionText.Width = 280.0;
            }
            box.DescriptionBox.Measure(new System.Windows.Size(348.0, double.MaxValue));
            System.Windows.Size visualSize0 = box.DescriptionBox.DesiredSize;
            box.DescriptionBox.Arrange(new Rect(new System.Windows.Point(0.0, 0.0), visualSize0));
            box.DescriptionBox.UpdateLayout();
            double descWidth = box.DescriptionBox.ActualWidth;
            double descHeight = box.DescriptionBox.ActualHeight;
            box.ContentStack.Measure(new System.Windows.Size(348.0, double.MaxValue));
            double contentHeight = box.ContentStack.DesiredSize.Height;
            if (contentHeight > SystemParameters.WorkArea.Height - 450.0)
            {
                box.ContentStack.Height = SystemParameters.WorkArea.Height - 450.0;
                box.MonoBox.Height = box.ContentStack.Height - (descHeight + 44.0) + 2.0;
            }
            else
            {
                box.MonoBox.Height = contentHeight - (descHeight + 44.0) + 2.0;
            }
            box.MainStack.Measure(new System.Windows.Size((monoText != null) ? 348.0 : descWidth, double.MaxValue));
            box.Height = box.MainStack.DesiredSize.Height + 2.0;
            box.Width = Math.Min(Math.Max(box.MainStack.DesiredSize.Width, 200.0) + 30.0, 350.0);
            Window window = (caller == null) ? null : GetWindow(GetWindow(caller));
            if (window != null && window.Topmost)
            {
                box.Topmost = true;
            }
            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() => box.ShowDialog(window));
            }
            return box.ShowDialog(window);
        }

        public static void ShowDetached(string message, string title = null, KTWirzade.GUI.MessageBoxButton button = MessageBoxButton.OK, KTWirzade.GUI.MessageBoxImage icon = MessageBoxImage.Information, string monoText = null)
        {
            MessageBox box = new MessageBox();
            box.TitleText.Text = ((title == null) ? icon.ToString() : title);
            box.Title = ((box.TitleText.Text.Length > 11) ? icon.ToString() : box.TitleText.Text);
            box.Button = button;
            box.Image = icon;
            box.DescriptionText.Text = message;
            box.MonoBox.Text = ((monoText == null) ? "" : monoText);
            box.MonoBox.Visibility = ((monoText == null) ? Visibility.Collapsed : Visibility.Visible);
            switch (button)
            {
                case MessageBoxButton.Exit:
                    box.MainButtonText.Text = "Exit";
                    box.SecondButton.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.YesNo:
                    box.MainButtonText.Text = "Yes";
                    box.SecondButtonText.Text = "No";
                    break;
                case MessageBoxButton.OKBypass:
                    box.MainButtonText.Text = "OK";
                    box.SecondButtonText.Text = "Bypass";
                    break;
                case MessageBoxButton.ImFineBypass:
                    box.MainButtonText.Text = "I'm Fine";
                    box.SecondButtonText.Text = "Bypass";
                    break;
                case MessageBoxButton.OK:
                    box.MainButtonText.Text = "OK";
                    box.SecondButton.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.ShowLog:
                    box.MainButtonText.Text = "Show log";
                    box.SecondButton.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.ShowLogExit:
                    box.MainButtonText.Text = "Show log";
                    box.SecondButtonText.Text = "Exit";
                    break;
            }
            if (icon == MessageBoxImage.NoImage)
            {
                box.DescriptionText.Width = 280.0;
            }
            box.DescriptionBox.Measure(new System.Windows.Size(348.0, double.MaxValue));
            System.Windows.Size visualSize0 = box.DescriptionBox.DesiredSize;
            box.DescriptionBox.Arrange(new Rect(new System.Windows.Point(0.0, 0.0), visualSize0));
            box.DescriptionBox.UpdateLayout();
            double descWidth = box.DescriptionBox.ActualWidth;
            double descHeight = box.DescriptionBox.ActualHeight;
            box.ContentStack.Measure(new System.Windows.Size(348.0, double.MaxValue));
            double contentHeight = box.ContentStack.DesiredSize.Height;
            if (contentHeight > SystemParameters.WorkArea.Height - 450.0)
            {
                box.ContentStack.Height = SystemParameters.WorkArea.Height - 450.0;
                box.MonoBox.Height = Math.Max(box.ContentStack.Height - (descHeight + 44.0) + 2.0, 0.0);
            }
            else
            {
                box.MonoBox.Height = Math.Max(contentHeight - (descHeight + 44.0) + 2.0, 0.0);
            }
            box.MainStack.Measure(new System.Windows.Size((monoText != null) ? 348.0 : descWidth, double.MaxValue));
            box.Height = box.MainStack.DesiredSize.Height + 2.0;
            box.Width = Math.Min(Math.Max(box.MainStack.DesiredSize.Width, 200.0) + 30.0, 350.0);
            System.Windows.Application.Current.Dispatcher.InvokeAsync(delegate
            {
                box.Show();
            });
        }

        private static Window GetWindow(Window owner)
        {
            try
            {
                Window window = null;
                List<Window> windows = System.Windows.Application.Current.Windows.Cast<Window>().ToList();
                if (owner != null)
                {
                    window = windows.LastOrDefault((Window x) => x.GetType() == owner.GetType());
                }
                if (window == null)
                {
                    window = windows.LastOrDefault((Window x) => x.GetType() != typeof(MessageBox));
                }
                return window;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Window GetWindowFromType(Type windowType)
        {
            try
            {
                Window window = null;
                List<Window> windows = System.Windows.Application.Current.Windows.Cast<Window>().ToList();
                if (windowType != null)
                {
                    window = windows.LastOrDefault((Window x) => x.GetType() == windowType);
                }
                if (window == null)
                {
                    window = windows.LastOrDefault((Window x) => x.GetType() != typeof(MessageBox));
                }
                return window;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static MessageBoxResult Show(Type windowType, string message, string title = null, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information, string monoText = null, string logPath = null)
        {
            _logPath = logPath;
            MessageBox box = new MessageBox();
            box.TitleText.Text = ((title == null) ? icon.ToString() : title);
            box.Title = ((box.TitleText.Text.Length > 11) ? icon.ToString() : box.TitleText.Text);
            box.Button = button;
            box.Image = icon;
            box.DescriptionText.Text = message;
            box.MonoBox.Text = ((monoText == null) ? "" : monoText);
            box.MonoBox.Visibility = ((monoText == null) ? Visibility.Collapsed : Visibility.Visible);
            switch (button)
            {
                case MessageBoxButton.Exit:
                    box.MainButtonText.Text = "Exit";
                    box.SecondButton.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.YesNo:
                    box.MainButtonText.Text = "Yes";
                    box.SecondButtonText.Text = "No";
                    break;
                case MessageBoxButton.OKBypass:
                    box.MainButtonText.Text = "OK";
                    box.SecondButtonText.Text = "Bypass";
                    break;
                case MessageBoxButton.ImFineBypass:
                    box.MainButtonText.Text = "I'm Fine";
                    box.SecondButtonText.Text = "Bypass";  
                    break;
                case MessageBoxButton.OK:
                    box.MainButtonText.Text = "OK";
                    box.SecondButton.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.ShowLog:
                    box.MainButtonText.Text = "Show log";
                    box.SecondButton.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.ShowLogExit:
                    box.MainButtonText.Text = "Show log";
                    box.SecondButtonText.Text = "Exit";
                    break;
            }
            if (icon == MessageBoxImage.NoImage)
            {
                box.DescriptionText.Width = 280.0;
            }
            box.DescriptionBox.Measure(new System.Windows.Size(348.0, double.MaxValue));
            System.Windows.Size visualSize0 = box.DescriptionBox.DesiredSize;
            box.DescriptionBox.Arrange(new Rect(new System.Windows.Point(0.0, 0.0), visualSize0));
            box.DescriptionBox.UpdateLayout();
            double descWidth = box.DescriptionBox.ActualWidth;
            double descHeight = box.DescriptionBox.ActualHeight;
            box.ContentStack.Measure(new System.Windows.Size(348.0, double.MaxValue));
            double contentHeight = box.ContentStack.DesiredSize.Height;
            if (contentHeight > SystemParameters.WorkArea.Height - 450.0)
            {
                box.ContentStack.Height = SystemParameters.WorkArea.Height - 450.0;
                box.MonoBox.Height = Math.Max(box.ContentStack.Height - (descHeight + 44.0) + 2.0, 0.0);
            }
            else
            {
                box.MonoBox.Height = Math.Max(contentHeight - (descHeight + 44.0) + 2.0, 0.0);
            }
            box.MainStack.Measure(new System.Windows.Size((monoText != null) ? 348.0 : descWidth, double.MaxValue));
            box.Height = box.MainStack.DesiredSize.Height + 2.0;
            box.Width = Math.Min(Math.Max(box.MainStack.DesiredSize.Width, 200.0) + 30.0, 350.0);
            Window window = GetWindowFromType(windowType);
            if (window != null && window.Topmost)
            {
                box.Topmost = true;
            }
            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() => box.ShowDialog(window));
            }
            return box.ShowDialog(window);
        }

        private void MainButton_OnClick(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Default;
            switch (Button)
            {
                case MessageBoxButton.YesNo:
                    _result = MessageBoxResult.Yes;
                    break;
                case MessageBoxButton.ShowLog:
                case MessageBoxButton.ShowLogExit:
                    if (Directory.Exists(_logPath))
                    {
                        Process.Start(new ProcessStartInfo(_logPath)
                        {
                            Verb = "open",
                            UseShellExecute = true
                        });
                    }
                    else if (File.Exists(_logPath))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo("notepad.exe", "\"" + _logPath + "\""));
                        }
                        catch (Exception)
                        {
                            Process.Start(new ProcessStartInfo(_logPath)
                            {
                                Verb = "open",
                                UseShellExecute = true
                            });
                        }
                    }
                    break;
            }
            Close();
        }

        private void SecondButton_OnClick(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Default;
            switch (Button)
            {
                case MessageBoxButton.OKBypass:
                case MessageBoxButton.ImFineBypass:
                    _result = MessageBoxResult.Bypass;
                    break;
                case MessageBoxButton.YesNo:
                    _result = MessageBoxResult.No;
                    break;
            }
            Close();
        }
    }
}