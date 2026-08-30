using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KTWirzade.GUI.Controls;
using KTWirzade.Shared;
using KTWirzade.Shared.Updates;

namespace KTWirzade.GUI.Windows
{
    public partial class UpdateCheckDialog : AcrylicWindow
    {
        public UpdateCheckDialog()
        {
            InitializeComponent();
            CurrentVersionRun.Text = "v" + Globals.CurrentVersion;
            Loaded += async (s, e) => await CheckForUpdates();
        }

        public void Show(Window owner)
        {
            Owner = owner;
            Show();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateChecker.OpenDownloadPage();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            var info = UpdateChecker.CurrentUpdateInfo;
            if (info == null || string.IsNullOrEmpty(info.LatestVersion)) return;

            try
            {
                KTWirzade.GUI.Utils.WizardConfig.Current?.SkippedUpdateVersion?.Set(info.LatestVersion);
                SetStatus($"Versão v{info.LatestVersion} ignorada. Você pode baixá-la quando quiser.", "Dismiss", "#e6a917");
            }
            catch { }
            SkipButton.Visibility = Visibility.Collapsed;
        }

        private async void RecheckButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdates();
        }

        private async System.Threading.Tasks.Task CheckForUpdates()
        {
            SetStatus("Verificando atualizacoes...", "ArrowSync", "#8b5cf6");
            ReleaseNotesBox.Visibility = Visibility.Collapsed;
            DownloadButton.Visibility = Visibility.Collapsed;
            RecheckButton.IsEnabled = false;

            try
            {
                var info = await UpdateChecker.CheckForUpdatesAsync();

                if (!string.IsNullOrEmpty(info.Error))
                {
                    SetStatus($"Erro ao verificar: {info.Error}", "ErrorCircle", "#c32b1d");
                    return;
                }

                if (info.UpdateAvailable)
                {
                    string skipped = null;
                    try { skipped = KTWirzade.GUI.Utils.WizardConfig.Current?.SkippedUpdateVersion?.Get(); } catch { }
                    bool wasSkipped = skipped == info.LatestVersion;

                    SetStatus($"Nova versão disponível: v{info.LatestVersion}" +
                        (wasSkipped ? " (você escolheu ignorar esta versão)" : ""), "ArrowDownload", "#3da35a");

                    ReleaseTitleText.Text = info.ReleaseName ?? $"v{info.LatestVersion}";
                    ReleaseDateText.Text = info.PublishedAt.ToString("yyyy-MM-dd");
                    ReleaseNotesText.Text = info.ReleaseNotes ?? "Sem notas de release.";
                    ReleaseNotesBox.Visibility = Visibility.Visible;
                    DownloadButton.Visibility = Visibility.Visible;
                    SkipButton.Visibility = wasSkipped ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    SetStatus($"Você está na versão mais recente (v{info.CurrentVersion}).", "Checkmark", "#3da35a");
                    SkipButton.Visibility = Visibility.Collapsed;
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("Tempo esgotado ao verificar atualizações.", "ErrorCircle", "#c32b1d");
            }
            catch (Exception ex)
            {
                SetStatus($"Erro: {ex.Message}", "ErrorCircle", "#c32b1d");
            }
            finally
            {
                RecheckButton.IsEnabled = true;
            }
        }

        private void SetStatus(string text, string iconSymbol, string colorHex)
        {
            StatusText.Text = text;
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            StatusIcon.Foreground = brush;
            SetIconSymbol(StatusIcon, iconSymbol);
        }

        private void SetIconSymbol(FluentIcons.Wpf.SymbolIcon icon, string symbolName)
        {
            var type = typeof(FluentIcons.Wpf.SymbolIcon);
            var prop = type.GetProperty("Symbol");
            if (prop == null) return;
            var enumType = prop.PropertyType;
            try
            {
                prop.SetValue(icon, Enum.Parse(enumType, symbolName));
            }
            catch { }
        }
    }
}
