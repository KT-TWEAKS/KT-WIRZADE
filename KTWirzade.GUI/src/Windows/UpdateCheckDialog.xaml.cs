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

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadButton.IsEnabled = false;
            RecheckButton.IsEnabled = false;
            SkipButton.Visibility = Visibility.Collapsed;
            DownloadProgressBar.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;
            UpdateChecker.DownloadProgressChanged += UpdateChecker_DownloadProgressChanged;

            try
            {
                var path = await UpdateChecker.DownloadLatestAsync();
                if (!string.IsNullOrEmpty(path))
                {
                    SetStatus($"Download concluído e verificado: {System.IO.Path.GetFileName(path)}", "Checkmark", "#3da35a");
                    DownloadProgressBar.Value = 100;
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/select,\"" + path + "\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    SetStatus("Não foi possível baixar o EXE. Abra a página da release para tentar novamente.", "ErrorCircle", "#c32b1d");
                    UpdateChecker.OpenDownloadPage();
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Erro no download: {ex.Message}", "ErrorCircle", "#c32b1d");
                UpdateChecker.OpenDownloadPage();
            }
            finally
            {
                UpdateChecker.DownloadProgressChanged -= UpdateChecker_DownloadProgressChanged;
                DownloadButton.IsEnabled = true;
                RecheckButton.IsEnabled = true;
            }
        }

        private void UpdateChecker_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            DownloadProgressBar.Value = e.Progress.PercentComplete;
            SetStatus(e.Progress.Status, "ArrowDownload", "#8b5cf6");
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
            DownloadProgressBar.Visibility = Visibility.Collapsed;
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
