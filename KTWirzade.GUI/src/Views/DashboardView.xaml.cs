using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using KTWirzade.GUI;
using KTWirzade.Shared.Cache;
using KTWirzade.Shared;
using KTWirzade.Shared.I18n;
using KTWirzade.GUI.Utils;
using Path = System.IO.Path;

namespace KTWirzade.GUI.Views
{
    public partial class DashboardView : UserControl, INotifyPropertyChanged
    {
        private bool _isSubscribedToInventoryChanges;
        private bool _isSubscribedToLanguageChanges;
        // Connection badge state, kept so a language switch can re-render the text
        // in place without re-testing the network.
        private bool? _lastConnectionOnline;

        public event PropertyChangedEventHandler PropertyChanged;

        // Localized captions for buttons living inside item templates (the entries
        // are rebuilt on demand, so plain text would survive a language switch).
        private string _openButtonText = "Abrir";
        public string OpenButtonText
        {
            get { return _openButtonText; }
            set
            {
                _openButtonText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpenButtonText)));
            }
        }

        private string _restoreButtonText = "Restaurar";
        public string RestoreButtonText
        {
            get { return _restoreButtonText; }
            set
            {
                _restoreButtonText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RestoreButtonText)));
            }
        }

        public DashboardView()
        {
            InitializeComponent();
            Loaded += DashboardView_Loaded;
            Unloaded += DashboardView_Unloaded;
        }

        private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isSubscribedToInventoryChanges)
            {
                App.PlaybookInventoryChanged += PlaybookInventoryChanged;
                _isSubscribedToInventoryChanges = true;
            }
            if (!_isSubscribedToLanguageChanges)
            {
                LanguageManager.LanguageChanged += OnDashboardLanguageChanged;
                _isSubscribedToLanguageChanges = true;
            }
            ApplyLocalizedLabels();
            LoadLocalPlaybooks();
            LoadApbxVisibilityHistory();
            await UpdateSubtitleAsync();
        }

        private void DashboardView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_isSubscribedToInventoryChanges)
            {
                App.PlaybookInventoryChanged -= PlaybookInventoryChanged;
                _isSubscribedToInventoryChanges = false;
            }
            if (_isSubscribedToLanguageChanges)
            {
                LanguageManager.LanguageChanged -= OnDashboardLanguageChanged;
                _isSubscribedToLanguageChanges = false;
            }
        }

        private void OnDashboardLanguageChanged(object sender, EventArgs e)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnDashboardLanguageChanged(sender, e)));
                return;
            }

            ApplyLocalizedLabels();
        }

        private static string T(string key, string fallback) => LanguageManager.T(key, fallback);

        private void ApplyLocalizedLabels()
        {
            QuickActionsText.Text = T("Dashboard.QuickActions", "Ações rápidas");
            LoadPlaybookTitleText.Text = T("Dashboard.LoadPlaybook", "Carregar Playbook");
            LoadPlaybookSubtitleText.Text = T("Dashboard.LoadPlaybookSubtitle", "Abrir e revisar um arquivo .apbx");
            RecoveryTitleText.Text = T("Dashboard.RecoveryTitle", "Recuperação");
            RecoverySubtitleText.Text = T("Dashboard.RecoverySubtitle", "Revisar e retomar rollbacks");
            UpdatesTitleText.Text = T("Dashboard.UpdatesTitle", "Atualizações");
            UpdatesSubtitleText.Text = T("Dashboard.UpdatesSubtitle", "Buscar versão mais recente");
            InstalledSectionText.Text = T("Dashboard.InstalledSection", "APBX instalados neste computador");
            InstallEmptyTitleText.Text = T("Dashboard.InstalledEmpty", "Nenhum APBX instalado no dispositivo");
            InstallEmptyHintText.Text = T("Dashboard.InstalledHint", "APBX importados ficam disponíveis sem depender do cache online");
            HiddenSectionText.Text = T("Dashboard.HiddenSection", "APBX ocultos");
            HiddenPlaybooksEmpty.Text = T("Dashboard.HiddenEmpty", "Nenhum APBX oculto");
            RemovedSectionText.Text = T("Dashboard.RemovedSection", "Histórico de APBX removidos");
            RemovedPlaybooksEmpty.Text = T("Dashboard.RemovedEmpty", "Nenhum APBX removido do computador");
            OpenButtonText = T("Dashboard.OpenItem", "Abrir");
            RestoreButtonText = T("Dashboard.RestoreItem", "Restaurar");
            RenderConnectionStatus();
        }

        // Connection badge text only; colors are applied by UpdateSubtitleAsync.
        private void RenderConnectionStatus()
        {
            string text;
            if (_lastConnectionOnline == null)
                text = T("Dashboard.StatusChecking", "Verificando");
            else if (_lastConnectionOnline == true)
                text = T("Dashboard.StatusOnline", "GitHub acessível");
            else
                text = T("Dashboard.StatusOffline", "GitHub indisponível");
            ConnectionStatusText.Text = text;
        }

        private void PlaybookInventoryChanged(object sender, EventArgs e)
        {
            if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadLocalPlaybooks();
                    LoadApbxVisibilityHistory();
                }));
        }

        private void LoadApbxVisibilityHistory()
        {
            var hidden = WizardConfig.GetHiddenPlaybooks();
            HiddenPlaybooksList.ItemsSource = hidden;
            HiddenPlaybooksList.Visibility = hidden.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            HiddenPlaybooksEmpty.Visibility = hidden.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            var removed = WizardConfig.GetRemovedPlaybooks();
            RemovedPlaybooksList.ItemsSource = removed;
            RemovedPlaybooksList.Visibility = removed.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            RemovedPlaybooksEmpty.Visibility = removed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void RestoreHiddenPlaybook_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is WizardConfig.HiddenPlaybookEntry hidden)) return;
            button.IsEnabled = false;

            try
            {
                string filePath = Path.Combine(
                    Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks"),
                    Path.GetFileName(hidden.FileName ?? string.Empty));
                if (!File.Exists(filePath))
                {
                    WizardConfig.RecordPlaybookRemoved(null, hidden.FileName, "Arquivo APBX não encontrado ao restaurar item oculto");
                    LoadApbxVisibilityHistory();
                    return;
                }

                PlaybookGUI playbook = await Task.Run(() => APBX.GetData(filePath));
                ((Playbook)playbook).Path = Path.Combine(Directory.GetCurrentDirectory(), "Playbooks", playbook.FileNameWithoutExtension);
                playbook.VerificationTask = Task.Run(() => playbook.GetStatus());
                playbook.Checked = true;

                var mainWindow = Window.GetWindow(this) as KTWirzade.GUI.MainWindow;
                bool alreadyLoaded = GlobalsGUI.Current.Items.OfType<PlaybookGUI>()
                    .Any(x => x.FileNameWithoutExtension == playbook.FileNameWithoutExtension);
                if (mainWindow != null && !alreadyLoaded)
                {
                    mainWindow.AddItem(playbook);
                }
                else if (mainWindow == null && !alreadyLoaded)
                {
                    GlobalsGUI.Current.Items.Add(playbook);
                }
                WizardConfig.RestorePlaybook(hidden);
                LoadApbxVisibilityHistory();
            }
            catch (Exception ex)
            {
                KTWirzade.GUI.MessageBox.Show(this, "Não foi possível restaurar o APBX: " + ex.Message, "Erro");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private async Task UpdateSubtitleAsync()
        {
            var version = GlobalsGUI.CurrentVersion;
            SubtitleText.Text = $"v{version}   •   {GetOsLabel()}";
            ConnectionStatusBadge.SetResourceReference(Border.BackgroundProperty, "StatusInfoSoftBrush");
            ConnectionStatusDot.SetResourceReference(Shape.FillProperty, "ProgressBarBrush");
            ConnectionStatusText.Text = T("Dashboard.StatusChecking", "Verificando");
            bool isOnline;
            try
            {
                isOnline = await Task.Run(() => PlaybookCacheManager.CheckOnlineStatus());
            }
            catch (Exception)
            {
                isOnline = false;
            }
            _lastConnectionOnline = isOnline;
            RenderConnectionStatus();
            ConnectionStatusBadge.SetResourceReference(Border.BackgroundProperty, isOnline ? "StatusSuccessSoftBrush" : "StatusWarningSoftBrush");
            ConnectionStatusDot.SetResourceReference(Shape.FillProperty, isOnline ? "StatusSuccessBrush" : "StatusWarningBrush");
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

        private void LoadLocalPlaybooks()
        {
            string playbookDirectory = Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\Playbooks");
            var installed = GlobalsGUI.Current.Items.OfType<PlaybookGUI>()
                .Select(playbook => LocalPlaybookEntry.Create(playbook, playbookDirectory))
                .Where(entry => entry != null)
                .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (installed.Count == 0)
            {
                LocalPlaybooksEmpty.Visibility = Visibility.Visible;
                LocalPlaybooksList.Visibility = Visibility.Collapsed;
                return;
            }

            LocalPlaybooksList.ItemsSource = installed;
            LocalPlaybooksEmpty.Visibility = Visibility.Collapsed;
            LocalPlaybooksList.Visibility = Visibility.Visible;
        }

        private void OpenLocalPlaybook_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is PlaybookGUI playbook)) return;
            var mainWindow = Window.GetWindow(this) as KTWirzade.GUI.MainWindow;
            mainWindow?.FocusPlaybook(playbook);
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

        private void QuickAction_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Space) return;
            e.Handled = true;
            switch ((sender as FrameworkElement)?.Tag as string)
            {
                case "load": QuickAction_LoadPlaybook(sender, e); break;
                case "rollback": QuickAction_Rollback(sender, e); break;
                case "updates": QuickAction_CheckUpdates(sender, e); break;
            }
        }

        private void QuickAction_CheckUpdates(object sender, RoutedEventArgs e)
        {
            // Same singleton dialog as the title-bar button: it injects the persisted
            // channel/skip settings and renders the full download+verify flow.
            Window owner = Window.GetWindow(this);
            var main = owner as KTWirzade.GUI.MainWindow;
            if (main != null)
            {
                main.ShowUpdatesDialog();
                return;
            }
            new Windows.UpdateCheckDialog().Show(owner);
        }

        public sealed class LocalPlaybookEntry
        {
            public PlaybookGUI Playbook { get; private set; }
            public string DisplayName { get; private set; }
            public string DetailsText { get; private set; }

            public static LocalPlaybookEntry Create(PlaybookGUI playbook, string playbookDirectory)
            {
                if (playbook == null || string.IsNullOrWhiteSpace(playbook.FileNameWithoutExtension)) return null;
                string filePath = Path.Combine(playbookDirectory, Path.GetFileName(playbook.FileNameWithoutExtension) + ".apbx");
                if (!File.Exists(filePath)) return null;

                var file = new FileInfo(filePath);
                string displayName = string.IsNullOrWhiteSpace(playbook.Username)
                    ? (playbook.Name ?? Path.GetFileNameWithoutExtension(filePath))
                    : $"{playbook.Username} — {playbook.Name}";
                string version = string.IsNullOrWhiteSpace(playbook.Version)
                    ? LanguageManager.T("Dashboard.VersionUnknown", "versão não informada")
                    : "v" + playbook.Version;
                string size = file.Length < 1024 * 1024
                    ? $"{Math.Max(1, file.Length / 1024)} KB"
                    : $"{file.Length / (1024d * 1024d):0.#} MB";

                return new LocalPlaybookEntry
                {
                    Playbook = playbook,
                    DisplayName = displayName,
                    DetailsText = $"{version}  •  {size}  •  {LanguageManager.T("Dashboard.DetailsLocal", "disponível localmente")}"
                };
            }
        }

    }
}
