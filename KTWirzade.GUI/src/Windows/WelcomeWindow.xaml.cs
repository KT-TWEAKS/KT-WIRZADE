using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KTWirzade.GUI.Controls;
using KTWirzade.Shared;

namespace KTWirzade.GUI.Windows
{
    public partial class WelcomeWindow : AcrylicWindow
    {
        private const string GitHubAvatarUrl = "https://github.com/kelvenapk.png";
        private const string GitHubProfileUrl = "https://github.com/kelvenapk";

        private class AvatarWebClient : System.Net.WebClient
        {
            protected override System.Net.WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);
                request.Timeout = 10000;
                return request;
            }
        }

        public WelcomeWindow()
        {
            InitializeComponent();
            VersionText.Text = "v" + Globals.CurrentVersion;
            LoadGitHubAvatar();
        }

        private async void LoadGitHubAvatar()
        {
            try
            {
                await Task.Run(() =>
                {
                    using (var client = new AvatarWebClient())
                    {
                        byte[] data = client.DownloadData(GitHubAvatarUrl);
                        Dispatcher.Invoke(() =>
                        {
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            using var stream = new MemoryStream(data);
                            image.StreamSource = stream;
                            image.EndInit();
                            image.Freeze();

                            var brush = new ImageBrush(image);
                            brush.Stretch = Stretch.UniformToFill;
                            brush.Freeze();
                            AvatarEllipse.Fill = brush;
                        });
                    }
                });
            }
            catch (Exception)
            {
                Dispatcher.Invoke(() =>
                {
                    var fallback = new BitmapImage(new Uri("pack://application:,,,/Icons/ICO.ico"));
                    fallback.Freeze();
                    var brush = new ImageBrush(fallback);
                    brush.Freeze();
                    AvatarEllipse.Fill = brush;
                });
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            Owner = mainWindow;
            mainWindow.Closed += (s, args) => this.Close();
            this.Hide();
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(GitHubProfileUrl);
            }
            catch (Exception)
            {
                KTWirzade.GUI.MessageBox.Show(typeof(WelcomeWindow), "Link invalido.", "Aviso");
            }
        }
    }
}
