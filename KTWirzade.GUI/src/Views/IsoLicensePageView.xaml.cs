using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using KTWirzade.GUI.Models;
using KTWirzade.GUI.ViewModels;

namespace KTWirzade.GUI.Views
{
    public partial class IsoLicensePageView : System.Windows.Controls.UserControl
    {
        public struct RECT
        {
            public int Left;

            public int Top;

            public int Right;

            public int Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32")]
        private static extern int SetWindowPos(IntPtr hWnd, int hwndInsertAfter, int x, int y, int cx, int cy, int wFlags);

        public IsoLicensePageView()
        {
            InitializeComponent();
            base.DataContextChanged += async delegate
            {
                if (base.DataContext != null)
                {
                    var vm = base.DataContext as IsoLicensePageViewModel;
                    if (vm != null)
                        vm.MainNextButtonCommand = new GlobalsGUI.CommandHandler(Next, () => true);
                    //((IsoLicensePageViewModel)base.DataContext).MainNextButtonCommand = new GlobalsGUI.CommandHandler(Next, () => true);
                }
            };
            Popup.Opened += delegate
            {
                IntPtr handle = ((HwndSource)PresentationSource.FromVisual(Popup.Child)).Handle;
                if (GetWindowRect(handle, out var lpRect))
                {
                    SetWindowPos(handle, -2, lpRect.Left, lpRect.Top, (int)base.Width, (int)base.Height, 0);
                }
            };
            base.Loaded += async delegate
            {
                await GUIDropDown.Close();
                await CoreDropDown.Open();
            };
        }

        private void Next()
        {
            Popup.IsOpen = true;
        }

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://creativecommons.org/publicdomain/zero/1.0/legalcode");
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            ToggleButton button = (ToggleButton)sender;
            if (base.DataContext is ViewModelBase viewModel)
            {
                viewModel.MainNextButtonActive = button.IsChecked == true;
            }
        }

        private async void CoreDropDown_OnClick(object sender, RoutedEventArgs e)
        {
            await GUIDropDown.Close();
        }

        private async void GUIDropDown_OnClick(object sender, RoutedEventArgs e)
        {
            await CoreDropDown.Close();
        }

        private void AgreeButton_OnClick(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            Popup.IsOpen = false;
            MainWindow.CurrentDispatcher.Invoke(delegate
            {
                MainWindow mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().First();
                IsoModePageViewModel isoModePageViewModel = new IsoModePageViewModel(new IsoModePage());
                GlobalsGUI.Current.ISO.CurrentPage = isoModePageViewModel;
                ((MainWindowViewModel)mainWindow.DataContext).CurrentViewModel = isoModePageViewModel;
            });
        }

        private void DisagreeButton_OnClick(object sender, RoutedEventArgs e)
        {
            FocusWindow(this, new EventArgs());
            Popup.IsOpen = false;
        }

        private void FocusWindow(object sender, EventArgs e)
        {
            try
            {
                MainWindow.CurrentDispatcher.Invoke(delegate
                {
                    SetForegroundWindow(new WindowInteropHelper(System.Windows.Application.Current.Windows.OfType<MainWindow>().First()).Handle);
                });
            }
            catch
            {
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}