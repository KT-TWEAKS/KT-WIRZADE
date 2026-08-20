using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using KTWirzade.GUI.Controls;
using KTWirzade.GUI.ViewModels;
using System.Diagnostics;
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
            VersionText.Text = "KT WIRZADE v0.8.4 Beta";
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

        private void DiscordButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("https://discordapp.com/users/404770666416570368");
            }
            catch (Exception)
            {
                MessageBox.Show(typeof(AboutWindow), "Link is invalid.", "Warning");
            }
        }

    }
}