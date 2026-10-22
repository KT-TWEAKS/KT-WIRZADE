using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KTWirzade.GUI.Controls;
using KTWirzade.Shared;
using KTWirzade.Shared.I18n;
using KTWirzade.Shared.Updates;

namespace KTWirzade.GUI.Windows
{
    public partial class UpdateCheckDialog : AcrylicWindow
    {
        // State machine: Loading -> UpToDate | UpdateAvailable | Error.
        private enum DialogState
        {
            Loading,
            UpToDate,
            UpdateAvailable,
            Error
        }

        // Transient statuses layered on top of the state (download lifecycle),
        // kept so a language switch can re-render them with current strings.
        private enum StatusOverride
        {
            None,
            Downloading,
            DownloadComplete,
            DownloadCancelled,
            DownloadFailed
        }

        private bool _isClosed;
        private bool _isSubscribedToDownloadProgress;
        private bool _isSubscribedToLanguageChanged;
        private bool _initialCheckStarted;
        private bool _autoCheckInitialized;
        private bool _isChecking;
        private bool _isDownloading;

        // Result of a check performed by the caller (e.g. the startup prompt); when
        // set, OnLoaded renders it instantly instead of firing a second API call.
        private readonly UpdateInfo _preloadedInfo;

        private DialogState _state = DialogState.Loading;
        private UpdateInfo _lastInfo;

        private StatusOverride _statusOverride = StatusOverride.None;
        private long _progressReceived;
        private long _progressTotal;
        private string _overrideDetail;
        private CancellationTokenSource _downloadCts;

        public UpdateCheckDialog(UpdateInfo preloadedInfo = null)
        {
            _preloadedInfo = preloadedInfo;
            InitializeComponent();
            ApplyLocalizedTexts();
            // IsChecked is NOT set here: the implicit CheckBox style uses
            // Trigger.EnterActions storyboards that address the template part
            // "FillBorder" by name. Before the template is applied those names
            // do not exist yet, and setting IsChecked in the constructor crashes
            // with "O nome 'FillBorder' não pode ser encontrado no escopo de
            // nome de ControlTemplate". OnLoaded initializes it safely.
            Loaded += OnLoaded;
            Closed += OnClosed;
            SubscribeToLanguageChanged();
        }

        public void Show(Window owner)
        {
            Owner = owner;
            Show();
        }

        private static string T(string key, string fallback) => LanguageManager.T(key, fallback);

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Deferred IsChecked initialization: at this point the template has been
            // applied, so the "FillBorder" storyboard target names resolve correctly.
            // Guarded (async-void hardening): a failure here must not crash the app.
            if (!_autoCheckInitialized)
            {
                _autoCheckInitialized = true;
                try
                {
                    AutoCheckCheckBox.IsChecked = KTWirzade.GUI.Utils.WizardConfig.GetAutoCheckEnabled();
                }
                catch (Exception)
                {
                    // Preference remains whatever the runtime resolved to; the
                    // checkbox stays interactive for a manual toggle.
                }
            }
            if (_initialCheckStarted) return;
            _initialCheckStarted = true;
            if (_preloadedInfo != null)
            {
                // The caller already spoke to GitHub: render that snapshot instead of
                // spending a second API request (and rate-limit budget) per open.
                ApplyInfo(_preloadedInfo);
                return;
            }
            await CheckForUpdates();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _isClosed = true;
            UnsubscribeFromDownloadProgress();
            UnsubscribeFromLanguageChanged();
            try
            {
                // Stop an in-flight download instead of leaving it run invisibly.
                _downloadCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        // ------------------------------------------------------------------
        // Localization
        // ------------------------------------------------------------------

        private void ApplyLocalizedTexts()
        {
            Title = T("Update.WindowTitle", "Atualizacoes - KT WIRZADE");
            TitleBarText.Text = T("Update.Title", "Atualizacoes");
            CurrentVersionLabelRun.Text = T("Update.CurrentVersion", "Versao atual:") + " ";
            ChannelLabel.Text = T("Update.ChannelLabel", "Canal:");
            ChannelToggleText.Text = GetChannelText(KTWirzade.GUI.Utils.WizardConfig.GetUpdateChannel());
            DownloadButtonText.Text = T("Update.DownloadExe", "Baixar EXE");
            SkipButtonText.Text = T("Update.SkipVersion", "Ignorar esta versao");
            RecheckButtonText.Text = T("Button.Recheck", "Verificar novamente");
            CancelDownloadButtonText.Text = T("Update.CancelDownload", "Cancelar download");
            AutoCheckText.Text = T("Update.AutoCheck", "Verificar ao iniciar");
            RenderState();
        }

        private static string GetChannelText(string channel) =>
            string.Equals(channel, "prerelease", StringComparison.OrdinalIgnoreCase)
                ? T("Update.ChannelPrerelease", "Pré-release")
                : T("Update.ChannelStable", "Estavel");

        private void SubscribeToLanguageChanged()
        {
            if (_isClosed || _isSubscribedToLanguageChanged)
                return;

            LanguageManager.LanguageChanged += OnLanguageChanged;
            _isSubscribedToLanguageChanged = true;
        }

        private void UnsubscribeFromLanguageChanged()
        {
            if (!_isSubscribedToLanguageChanged)
                return;

            LanguageManager.LanguageChanged -= OnLanguageChanged;
            _isSubscribedToLanguageChanged = false;
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (_isClosed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            if (!Dispatcher.CheckAccess())
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!_isClosed) ApplyLocalizedTexts();
                    }));
                }
                catch (InvalidOperationException)
                {
                    // The dialog is closing and no longer accepts dispatcher work.
                }
                return;
            }

            ApplyLocalizedTexts();
        }

        // ------------------------------------------------------------------
        // Check
        // ------------------------------------------------------------------

        private async Task CheckForUpdates()
        {
            if (_isClosed || _isChecking) return;

            _isChecking = true;
            _state = DialogState.Loading;
            _statusOverride = StatusOverride.None;
            _overrideDetail = null;
            RenderState();

            UpdateInfo info = null;
            try
            {
                // Persisted settings are injected here because Shared cannot
                // reference WizardConfig directly.
                UpdateChecker.UpdateChannel = KTWirzade.GUI.Utils.WizardConfig.GetUpdateChannel();
                UpdateChecker.SkippedVersion = KTWirzade.GUI.Utils.WizardConfig.GetSkippedVersion();

                info = await UpdateChecker.CheckForUpdatesAsync();
            }
            catch (Exception ex)
            {
                info = new UpdateInfo { CurrentVersion = Globals.CurrentVersion, Error = ex.Message };
            }
            finally
            {
                _isChecking = false;
            }

            if (_isClosed) return;
            ApplyInfo(info);
        }

        // Shared tail of both the live check and the preloaded snapshot: decides the
        // dialog state from an UpdateInfo and re-renders.
        private void ApplyInfo(UpdateInfo info)
        {
            if (info == null)
                info = new UpdateInfo { CurrentVersion = Globals.CurrentVersion, Error = "Resposta vazia do GitHub." };

            _lastInfo = info;
            if (!string.IsNullOrEmpty(info.Error))
                _state = DialogState.Error;
            else if (info.UpdateAvailable || info.Skipped)
                _state = DialogState.UpdateAvailable;
            else
                _state = DialogState.UpToDate;

            RenderState();
        }

        private async void RecheckButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdates();
        }

        private async void ChannelToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosed || _isChecking || _isDownloading) return;

            // Async void: any exception here would crash the process. The channel
            // switch must never be able to take the app down - report inside the dialog.
            string next;
            try
            {
                string current = KTWirzade.GUI.Utils.WizardConfig.GetUpdateChannel();
                next = string.Equals(current, "prerelease", StringComparison.OrdinalIgnoreCase) ? "stable" : "prerelease";
                KTWirzade.GUI.Utils.WizardConfig.SetUpdateChannel(next);
            }
            catch (Exception ex)
            {
                RenderChannelSwitchError(ex.Message);
                return;
            }

            try
            {
                UpdateChecker.UpdateChannel = next;
            }
            catch (Exception ex)
            {
                // A failed static initialization of UpdateChecker is cached by the
                // runtime; show the reason instead of crashing (async void).
                RenderChannelSwitchError(ex.Message);
                return;
            }

            ChannelToggleText.Text = GetChannelText(next);

            await CheckForUpdates();
        }

        private void RenderChannelSwitchError(string detail)
        {
            RenderStatus(T("Status.Error", "Erro"),
                         string.Format(T("Update.Error", "Erro ao verificar atualizacoes: {0}"), detail ?? string.Empty),
                         "ErrorCircle", "#c32b1d");
        }

        // Persists the startup auto-check preference; used by started and user toggles.
        private void AutoCheckCheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isClosed) return;
            KTWirzade.GUI.Utils.WizardConfig.SetAutoCheckEnabled(AutoCheckCheckBox.IsChecked == true);
        }

        // ------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------

        private void RenderState()
        {
            if (_isClosed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            if (!Dispatcher.CheckAccess())
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(RenderState));
                }
                catch (InvalidOperationException)
                {
                    // The dialog is closing and no longer accepts dispatcher work.
                }
                return;
            }

            RenderVersions();

            bool busy = _isChecking || _isDownloading;
            RecheckButton.IsEnabled = !busy;
            ChannelToggleButton.IsEnabled = !busy;
            DownloadButton.IsEnabled = !_isDownloading;
            CancelDownloadButton.IsEnabled = _isDownloading;

            UpdateInfo info = _lastInfo;
            switch (_state)
            {
                case DialogState.Loading:
                    RenderStatus(T("Update.Checking", "Verificando atualizacoes..."),
                                 T("Update.LoadingHint", "Aguarde enquanto procuramos novas versoes..."),
                                 "ArrowSync", "#8b5cf6");
                    ReleaseNotesBox.Visibility = Visibility.Collapsed;
                    DownloadButton.Visibility = Visibility.Collapsed;
                    CancelDownloadButton.Visibility = Visibility.Collapsed;
                    SkipButton.Visibility = Visibility.Collapsed;
                    DownloadProgressPanel.Visibility = Visibility.Collapsed;
                    break;

                case DialogState.Error:
                    RenderStatus(T("Update.ErrorTitle", "Nao foi possivel verificar"),
                                 string.Format(T("Update.Error", "Erro ao verificar atualizacoes: {0}"), info?.Error ?? string.Empty),
                                 "ErrorCircle", "#c32b1d");
                    ReleaseNotesBox.Visibility = Visibility.Collapsed;
                    DownloadButton.Visibility = Visibility.Collapsed;
                    CancelDownloadButton.Visibility = Visibility.Collapsed;
                    SkipButton.Visibility = Visibility.Collapsed;
                    DownloadProgressPanel.Visibility = Visibility.Collapsed;
                    break;

                case DialogState.UpToDate:
                    RenderStatus(T("Update.UpToDateTitle", "Tudo atualizado"),
                                 string.Format(T("Update.NoUpdate", "Voce esta na versao mais recente (v{0})."),
                                     info?.CurrentVersion ?? Globals.CurrentVersion),
                                 "Checkmark", "#3da35a");
                    RenderReleaseInfo(info);
                    DownloadButton.Visibility = Visibility.Collapsed;
                    CancelDownloadButton.Visibility = Visibility.Collapsed;
                    SkipButton.Visibility = Visibility.Collapsed;
                    DownloadProgressPanel.Visibility = Visibility.Collapsed;
                    break;

                case DialogState.UpdateAvailable:
                    bool skipped = info != null && (info.Skipped || UpdateChecker.IsVersionSkipped(info.LatestVersion));
                    if (skipped)
                    {
                        RenderStatus(T("Status.UpdateAvailable", "Nova versao disponivel"),
                                     string.Format(T("Update.SkippedMessage", "Ha uma versao mais nova ({0}), mas voce escolheu ignora-la."),
                                         "v" + info.LatestVersion),
                                     "Dismiss", "#e6a917");
                    }
                    else
                    {
                        RenderStatus(T("Status.UpdateAvailable", "Nova versao disponivel"),
                                     T("Update.AvailableMessage", "Uma nova versao do KT WIRZADE esta disponivel para download."),
                                     "ArrowDownload", "#3da35a");
                    }
                    RenderReleaseInfo(info);
                    DownloadButton.Visibility = Visibility.Visible;
                    DownloadButton.IsEnabled = !_isDownloading;
                    CancelDownloadButton.Visibility = _isDownloading ? Visibility.Visible : Visibility.Collapsed;
                    SkipButton.Visibility = (_isDownloading || skipped) ? Visibility.Collapsed : Visibility.Visible;
                    DownloadProgressPanel.Visibility = _isDownloading ? Visibility.Visible : Visibility.Collapsed;
                    break;
            }

            // Download lifecycle messages win over the per-state text.
            if (_statusOverride != StatusOverride.None)
                RenderOverrideStatus();
        }

        private void RenderOverrideStatus()
        {
            switch (_statusOverride)
            {
                case StatusOverride.Downloading:
                    RenderStatus(T("Update.Downloading", "Baixando atualizacao..."),
                                 FormatProgress(_progressReceived, _progressTotal),
                                 "ArrowDownload", "#8b5cf6");
                    break;
                case StatusOverride.DownloadComplete:
                    RenderStatus(T("Status.Success", "Sucesso"),
                                 string.Format(T("Update.DownloadComplete", "Download concluido e verificado: {0}"),
                                     _overrideDetail ?? string.Empty),
                                 "Checkmark", "#3da35a");
                    break;
                case StatusOverride.DownloadCancelled:
                    RenderStatus(T("Status.Warning", "Aviso"),
                                 T("Update.DownloadCancelled", "Download cancelado"),
                                 "Dismiss", "#e6a917");
                    break;
                case StatusOverride.DownloadFailed:
                    RenderStatus(T("Status.Error", "Erro"),
                                 string.IsNullOrEmpty(_overrideDetail)
                                     ? T("Update.DownloadFailed", "Nao foi possivel baixar o EXE. Abra a pagina da release para tentar novamente.")
                                     : string.Format(T("Update.DownloadError", "Erro no download: {0}"), _overrideDetail),
                                 "ErrorCircle", "#c32b1d");
                    break;
            }
        }

        private void RenderVersions()
        {
            CurrentVersionRun.Text = "v" + Globals.CurrentVersion;
            UpdateInfo info = _lastInfo;
            string latest = string.IsNullOrEmpty(info?.LatestVersion) ? "—" : "v" + info.LatestVersion;
            LatestVersionText.Text = string.Format(T("Update.LatestVersion", "Ultima versao: {0}"), latest);
        }

        private void RenderReleaseInfo(UpdateInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.LatestVersion))
            {
                ReleaseNotesBox.Visibility = Visibility.Collapsed;
                return;
            }

            ReleaseTitleText.Text = string.IsNullOrEmpty(info.ReleaseName) ? "v" + info.LatestVersion : info.ReleaseName;
            ReleaseDateText.Text = string.Format(T("Update.ReleasedOn", "Publicado em {0}"), info.PublishedAt.ToString("yyyy-MM-dd"));
            ReleaseNotesText.Text = string.IsNullOrWhiteSpace(info.ReleaseNotes)
                ? T("Update.NoNotes", "Sem notas de release.")
                : info.ReleaseNotes;
            ReleaseNotesBox.Visibility = Visibility.Visible;
        }

        private void RenderStatus(string title, string message, string iconSymbol, string colorHex)
        {
            if (_isClosed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            if (!Dispatcher.CheckAccess())
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(() => RenderStatus(title, message, iconSymbol, colorHex)));
                }
                catch (InvalidOperationException)
                {
                    // The dialog is closing and no longer accepts dispatcher work.
                }
                return;
            }

            StatusTitle.Text = title;
            StatusText.Text = message ?? string.Empty;
            StatusText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
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

        // ------------------------------------------------------------------
        // Download
        // ------------------------------------------------------------------

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosed || _isChecking || _isDownloading) return;

            _isDownloading = true;
            CancellationTokenSource downloadCts = new CancellationTokenSource();
            _downloadCts = downloadCts;
            _statusOverride = StatusOverride.Downloading;
            _progressReceived = 0;
            _progressTotal = 0;
            _overrideDetail = null;
            DownloadProgressBar.Value = 0;
            DownloadProgressText.Text = string.Empty;
            SubscribeToDownloadProgress();
            RenderState();

            string path = null;
            Exception failure = null;
            bool cancelled = false;
            try
            {
                path = await UpdateChecker.DownloadLatestAsync(null, downloadCts.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                UnsubscribeFromDownloadProgress();
            }

            cancelled |= downloadCts.IsCancellationRequested;
            _isDownloading = false;
            if (ReferenceEquals(_downloadCts, downloadCts)) _downloadCts = null;
            downloadCts.Dispose();

            if (_isClosed) return;

            if (cancelled)
            {
                _statusOverride = StatusOverride.DownloadCancelled;
                DownloadProgressBar.Value = 0;
            }
            else if (!string.IsNullOrEmpty(path))
            {
                _statusOverride = StatusOverride.DownloadComplete;
                _overrideDetail = System.IO.Path.GetFileName(path);
                DownloadProgressBar.Value = 100;
            }
            else
            {
                // UpdateChecker already reported the underlying failure through
                // DownloadProgressChanged; the override gives it a localized frame.
                _statusOverride = StatusOverride.DownloadFailed;
                _overrideDetail = failure?.Message;
            }

            RenderState();

            if (cancelled) return;

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/select,\"" + path + "\"",
                        UseShellExecute = true
                    });
                }
                catch { }
            }
            else
            {
                UpdateChecker.OpenDownloadPage();
            }
        }

        private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            CancelDownloadButton.IsEnabled = false;
            try
            {
                _downloadCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void UpdateChecker_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (_isClosed || e?.Progress == null || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            var progress = e.Progress;
            Action updateControls = () =>
            {
                // A progress callback may already be queued when the dialog closes or
                // when the download finishes - never overwrite the final status then.
                if (_isClosed || !_isDownloading || !IsLoaded || !IsVisible)
                    return;

                _progressReceived = progress.BytesReceived;
                _progressTotal = progress.TotalBytes;
                _statusOverride = StatusOverride.Downloading;

                string text = FormatProgress(progress.BytesReceived, progress.TotalBytes);
                DownloadProgressBar.Value = progress.PercentComplete;
                DownloadProgressText.Text = text;
                RenderStatus(T("Update.Downloading", "Baixando atualizacao..."), text, "ArrowDownload", "#8b5cf6");
            };

            if (Dispatcher.CheckAccess())
            {
                updateControls();
                return;
            }

            try
            {
                Dispatcher.BeginInvoke(updateControls);
            }
            catch (InvalidOperationException)
            {
                // The dispatcher can shut down between the check above and enqueueing.
            }
        }

        private void SubscribeToDownloadProgress()
        {
            if (_isClosed || _isSubscribedToDownloadProgress)
                return;

            UpdateChecker.DownloadProgressChanged += UpdateChecker_DownloadProgressChanged;
            _isSubscribedToDownloadProgress = true;
        }

        private void UnsubscribeFromDownloadProgress()
        {
            if (!_isSubscribedToDownloadProgress)
                return;

            UpdateChecker.DownloadProgressChanged -= UpdateChecker_DownloadProgressChanged;
            _isSubscribedToDownloadProgress = false;
        }

        // ------------------------------------------------------------------
        // Skip this version
        // ------------------------------------------------------------------

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateInfo info = _lastInfo ?? UpdateChecker.CurrentUpdateInfo;
            if (info == null || string.IsNullOrEmpty(info.LatestVersion)) return;

            KTWirzade.GUI.Utils.WizardConfig.SetSkippedVersion(info.LatestVersion);
            UpdateChecker.SkippedVersion = info.LatestVersion;

            SkipButton.Visibility = Visibility.Collapsed;
            RenderStatus(T("Status.Warning", "Aviso"),
                         string.Format(T("Update.SkippedConfirmation", "Versao {0} ignorada. Voce pode baixa-la quando quiser."),
                             "v" + info.LatestVersion),
                         "Dismiss", "#e6a917");
            _ = CloseAfterSkipConfirmation();
        }

        private async Task CloseAfterSkipConfirmation()
        {
            try
            {
                // Keep the confirmation visible for a moment before closing.
                await Task.Delay(1200);
            }
            catch (Exception)
            {
            }

            if (!_isClosed) Close();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private string FormatProgress(long received, long total)
        {
            if (total > 0)
            {
                long percent = Math.Min(100, (received * 100) / total);
                return string.Format(T("Update.Progress", "{0} / {1} ({2}%)"),
                    FormatBytes(received), FormatBytes(total), percent);
            }

            return FormatBytes(received);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 MB";

            double megabytes = bytes / (1024.0 * 1024.0);
            if (megabytes >= 1.0) return megabytes.ToString("0.0") + " MB";

            double kilobytes = bytes / 1024.0;
            if (kilobytes >= 1.0) return kilobytes.ToString("0.0") + " KB";

            return bytes + " B";
        }
    }
}
