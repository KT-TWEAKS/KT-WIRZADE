using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KTWirzade.Shared.Cache;
using KTWirzade.Shared.Updates;
using KTWirzade.GUI.Utils;

namespace KTWirzade.GUI.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            Loaded += DashboardView_Loaded;
        }

        private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCachedPlaybooks();
            await UpdateSubtitleAsync();
        }

        private async Task UpdateSubtitleAsync()
        {
            var version = GlobalsGUI.CurrentVersion;
            SubtitleText.Text = $"v{version} - Verificando...";
            bool isOnline;
            try
            {
                isOnline = await Task.Run(() => PlaybookCacheManager.CheckOnlineStatus());
            }
            catch (Exception)
            {
                isOnline = false;
            }
            var onlineText = isOnline ? "Online" : "Offline (usando cache)";
            SubtitleText.Text = $"v{version}   •   {GetOsLabel()}   •   {onlineText}";
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

        private void LoadCachedPlaybooks()
        {
            var cached = PlaybookCacheManager.ListCached();

            if (cached == null || !System.Linq.Enumerable.Any(cached))
            {
                EmptyState.Visibility = Visibility.Visible;
                CachedPlaybooksList.Visibility = Visibility.Collapsed;
                return;
            }

            CachedPlaybooksList.ItemsSource = cached;
            EmptyState.Visibility = Visibility.Collapsed;
            CachedPlaybooksList.Visibility = Visibility.Visible;
        }

        private void QuickAction_LoadPlaybook(object sender, RoutedEventArgs e)
        {
            GlobalsGUI.MainWindowDragBoxClick();
        }

        private void QuickAction_Rollback(object sender, RoutedEventArgs e)
        {
            var main = Window.GetWindow(this) as KTWirzade.GUI.MainWindow;
            if (main != null)
                main.RollbackButton_OnClick(sender, e);
        }

        private async void QuickAction_CheckUpdates(object sender, RoutedEventArgs e)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                var task = UpdateChecker.CheckForUpdatesAsync();
                var delayTask = Task.Delay(Timeout.Infinite, cts.Token);
                var completedTask = await Task.WhenAny(task, delayTask);

                if (completedTask != task)
                {
                    KTWirzade.GUI.MessageBox.Show(this, "Tempo esgotado ao verificar atualizações.", "Erro");
                    return;
                }

                // Observe the losing delay task so its cancellation is not unobserved.
                cts.Cancel();
                try { await delayTask; } catch (OperationCanceledException) { }

                var info = await task;
                var main = Window.GetWindow(this) as KTWirzade.GUI.MainWindow;

                if (!string.IsNullOrEmpty(info.Error))
                {
                    KTWirzade.GUI.MessageBox.Show(this, "Erro ao verificar atualizações: " + info.Error, "Erro");
                    return;
                }

                if (info.UpdateAvailable)
                {
                    string skipped = null;
                    try { skipped = Utils.WizardConfig.Current?.SkippedUpdateVersion?.Get(); } catch { }
                    bool wasSkipped = skipped == info.LatestVersion;

                    var message = $"Nova versão disponível: v{info.LatestVersion}" +
                        (wasSkipped ? "\n\n(Você escolheu ignorar esta versão anteriormente.)" : "") +
                        "\n\nDeseja abrir a página de download?";
                    var result = KTWirzade.GUI.MessageBox.Show(
                        this,
                        message,
                        "Atualização disponível",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes)
                        UpdateChecker.OpenDownloadPage();
                }
                else
                {
                    KTWirzade.GUI.MessageBox.Show(this, $"Você está na versão mais recente (v{info.CurrentVersion}).", "Sem atualizações");
                }
            }
            catch (OperationCanceledException)
            {
                KTWirzade.GUI.MessageBox.Show(this, "Tempo esgotado ao verificar atualizações.", "Erro");
            }
            catch (Exception ex)
            {
                KTWirzade.GUI.MessageBox.Show(this, "Erro: " + ex.Message, "Erro");
            }
        }

    }
}
