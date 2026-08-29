using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.ViewModels;
using KTWirzade.Shared;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace KTWirzade.GUI.Windows
{
    public partial class AboutWindow : AcrylicWindow
    {

        public void Show(Window owner)
        {
            Owner = owner;
            Show();
        }

        public AboutWindow()
        {
            DataContext = new AboutWindowViewModel();
            InitializeComponent();
            VersionText.Text = "v" + Globals.CurrentVersion;
            OsInfoText.Text = GetOsLabel();
        }

        private static string GetOsLabel()
        {
            try
            {
                var v = Environment.OSVersion.Version;
                var name = v.Build >= 22000 ? "Windows 11" : "Windows 10";
                return $"{name} build {v.Build}";
            }
            catch
            {
                return "Windows";
            }
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
            CloseWindow(aboutscale);
        }

        private void WebsiteButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("https://github.com/kelvenapk");
            }
            catch (Exception)
            {
                MessageBox.Show(typeof(AboutWindow), "Link invalido.", "Aviso");
            }
        }

    }
}
