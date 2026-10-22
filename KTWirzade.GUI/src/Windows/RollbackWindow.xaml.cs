using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KTWirzade.GUI.Controls;
using KTWirzade.Shared.Rollback;

namespace KTWirzade.GUI.Windows
{
    public partial class RollbackWindow : AcrylicWindow
    {
        public ObservableCollection<RollbackSessionViewModel> Sessions { get; set; } = new ObservableCollection<RollbackSessionViewModel>();
        private bool _isBusy;
        private string _activeSessionId;
        private int _ignoredSessionCount;

        public RollbackWindow()
        {
            DataContext = this;
            InitializeComponent();
            RollbackManager.ProgressChanged += RollbackProgressChanged;
            Closing += RollbackWindow_Closing;
            Closed += RollbackWindow_Closed;
            LoadSessions();
        }

        public void Show(Window owner)
        {
            Owner = owner;
            Show();
        }

        private void LoadSessions()
        {
            Sessions.Clear();
            _ignoredSessionCount = 0;

            if (!Directory.Exists(RollbackPaths.BaseDir))
            {
                EmptySessionsState.Visibility = Visibility.Visible;
                SessionsList.Visibility = Visibility.Collapsed;
                StatusText.Text = "Nenhuma sessão encontrada.";
                UpdateProgressSummary(null);
                return;
            }

            var sessionDirs = Directory.GetDirectories(RollbackPaths.BaseDir)
                .Where(d => Guid.TryParseExact(Path.GetFileName(d), "D", out _))
                .OrderByDescending(d => Directory.GetCreationTime(d));

            foreach (var dir in sessionDirs)
            {
                var sessionFile = Path.Combine(dir, "session.json");
                if (!File.Exists(sessionFile))
                {
                    _ignoredSessionCount++;
                    continue;
                }

                try
                {
                    var json = File.ReadAllText(sessionFile);
                    var session = Newtonsoft.Json.JsonConvert.DeserializeObject<RollbackSession>(json);
                    if (session == null || session.Entries == null)
                    {
                        _ignoredSessionCount++;
                        continue;
                    }

                    var pendingCount = session.Entries.Count(e => !e.RollbackCompleted);
                    var actionCount = session.Entries.Count;
                    var rolledBack = actionCount > 0 && RollbackManager.IsFullyReverted(session);
                    var completedCount = actionCount - pendingCount;

                    Sessions.Add(new RollbackSessionViewModel
                    {
                        SessionId = session.SessionId,
                        PlaybookName = string.IsNullOrEmpty(session.PlaybookName) ? "(desconhecido)" : session.PlaybookName,
                        DirectoryPath = dir,
                        StartedAt = session.StartedAt,
                        IsComplete = session.CompletedAt.HasValue,
                        ActionCount = actionCount,
                        IsReverted = rolledBack,
                        PendingCount = pendingCount,
                        HasRollbackProgress = session.Entries.Any(e => e.RollbackCompleted),
                        CompletedCount = completedCount,
                        ProgressPercent = actionCount == 0 ? 0 : (int)Math.Round((completedCount / (double)actionCount) * 100d)
                    });
                }
                catch
                {
                    _ignoredSessionCount++;
                }
            }

            SessionsList.ItemsSource = Sessions;
            EmptySessionsState.Visibility = Sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SessionsList.Visibility = Sessions.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (Sessions.Count > 0)
            {
                SessionsList.SelectedIndex = 0;
            }
            StatusText.Text = FormatSessionCount();
            UpdateProgressSummary(SessionsList.SelectedItem as RollbackSessionViewModel);
        }

        private string FormatSessionCount()
        {
            string count = $"{Sessions.Count} sess{(Sessions.Count != 1 ? "ões" : "ão")} encontrada{(Sessions.Count != 1 ? "s" : "")}";
            return _ignoredSessionCount == 0 ? count : count + $" · {_ignoredSessionCount} pasta(s) com registro inválido";
        }

        private void SessionsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateProgressSummary(SessionsList.SelectedItem as RollbackSessionViewModel);
        }

        private void UpdateProgressSummary(RollbackSessionViewModel session)
        {
            if (session == null)
            {
                RollbackProgressBar.Value = 0;
                RollbackProgressLabel.Text = "Selecione uma sessão";
                RollbackActionButton.IsEnabled = false;
                RollbackActionText.Text = "Reverter Selecionado";
                OpenSessionFolderButton.IsEnabled = false;
                return;
            }

            var progress = Math.Max(0, Math.Min(100, session.ProgressPercent));
            RollbackProgressBar.Value = progress;
            RollbackProgressLabel.Text = session.IsComplete
                ? $"{progress}%  ·  {session.CompletedCount}/{session.ActionCount} ações"
                : "Aplicação incompleta";
            RollbackActionButton.IsEnabled = !_isBusy && session.CanRollback;
            RollbackActionText.Text = session.IsComplete ? "Reverter Selecionado" : "Aguardando conclusão";
            OpenSessionFolderButton.IsEnabled = !_isBusy && !string.IsNullOrWhiteSpace(session.DirectoryPath) && Directory.Exists(session.DirectoryPath);
        }

        private void OpenSessionFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(SessionsList.SelectedItem is RollbackSessionViewModel session) || string.IsNullOrWhiteSpace(session.DirectoryPath))
            {
                StatusText.Text = "Selecione uma sessão para abrir a pasta.";
                return;
            }

            if (!Directory.Exists(session.DirectoryPath))
            {
                StatusText.Text = "A pasta desta sessão não existe mais (pode ter sido removida).";
                UpdateProgressSummary(session);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{session.DirectoryPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Não foi possível abrir a pasta: {ex.Message}";
            }
        }

        private void SetBusy(bool isBusy, string statusText = null)
        {
            _isBusy = isBusy;
            SessionsList.IsEnabled = !isBusy;
            RefreshButton.IsEnabled = !isBusy;
            OpenSessionFolderButton.IsEnabled = !isBusy && (SessionsList.SelectedItem as RollbackSessionViewModel) != null;
            ClearOldButton.IsEnabled = !isBusy;
            ClearAllButton.IsEnabled = !isBusy;
            CloseButton.IsEnabled = !isBusy;
            RollbackActionButton.IsEnabled = !isBusy && (SessionsList.SelectedItem as RollbackSessionViewModel)?.CanRollback == true;
            if (!string.IsNullOrWhiteSpace(statusText)) StatusText.Text = statusText;
        }

        private void RollbackProgressChanged(object sender, RollbackProgressEventArgs e)
        {
            if (!string.Equals(_activeSessionId, e.SessionId, StringComparison.OrdinalIgnoreCase)) return;
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_isBusy && SessionsList.SelectedItem is RollbackSessionViewModel selected &&
                    string.Equals(selected.SessionId, e.SessionId, StringComparison.OrdinalIgnoreCase))
                {
                    int total = Math.Max(0, e.Total);
                    int processed = Math.Max(0, Math.Min(total, e.Processed));
                    RollbackProgressBar.Value = total == 0 ? 0 : Math.Round(processed * 100d / total);
                    RollbackProgressLabel.Text = $"{processed}/{total} avaliadas · {e.Reverted} revertidas";
                    StatusText.Text = $"Revertendo: {processed} de {total} ações avaliadas";
                }
            }));
        }

        private void RollbackWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_isBusy) return;
            e.Cancel = true;
            StatusText.Text = "Aguarde a operação atual terminar para fechar esta janela.";
        }

        private void RollbackWindow_Closed(object sender, EventArgs e)
        {
            RollbackManager.ProgressChanged -= RollbackProgressChanged;
            Closing -= RollbackWindow_Closing;
            Closed -= RollbackWindow_Closed;
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
            Close();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSessions();
        }

        private async void DeleteSession_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is System.Windows.Controls.Button btn))
                return;

            var sessionId = (btn.DataContext as RollbackSessionViewModel)?.SessionId
                ?? (SessionsList.SelectedItem as RollbackSessionViewModel)?.SessionId;

            if (string.IsNullOrEmpty(sessionId))
                return;

            var confirm = KTWirzade.GUI.MessageBox.Show(
                this,
                "Excluir esta sessão de rollback?\n\nIsso apaga APENAS o registro/backups da sessão — não reverte nenhuma alteração feita pelo playbook.",
                "Excluir sessão",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            bool ok = false;
            string resultText;
            SetBusy(true, "Excluindo sessão...");
            try
            {
                ok = await Task.Run(() => RollbackManager.DeleteSession(sessionId));
                resultText = ok ? "Sessão excluída." : "A sessão está em uso ou não pôde ser excluída.";
            }
            catch (Exception ex)
            {
                Core.Log.WriteExceptionSafe(ex, "Error deleting rollback session.");
                resultText = "Não foi possível excluir a sessão.";
            }
            finally
            {
                SetBusy(false);
            }
            LoadSessions();
            StatusText.Text = resultText;
        }

        private async void ClearOldButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(RollbackPaths.BaseDir))
            {
                LoadSessions();
                return;
            }

            var confirm = KTWirzade.GUI.MessageBox.Show(
                this,
                "Limpar sessões antigas de rollback?\n\n" +
                "Serão removidas:\n" +
                "• Sessões com mais de 30 dias (concluídas ou revertidas)\n" +
                "• Sessões abandonadas (processo morreu no meio, 30+ dias)\n" +
                "• Pastas órfãs sem registro válido (30+ dias)\n\n" +
                "As 5 sessões mais recentes são sempre preservadas.",
                "Limpar sessões antigas",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            StatusText.Text = "Limpando...";
            SetBusy(true, "Limpando sessões antigas...");

            try
            {
                var removed = await System.Threading.Tasks.Task.Run(() => KTWirzade.Shared.Rollback.RollbackManager.CleanupOldSessionsNow());
                StatusText.Text = $"{removed} sessao(oes) removida(s)";
            }
            catch (Exception ex)
            {
                Core.Log.WriteExceptionSafe(ex, "Error cleaning old rollback sessions.");
                StatusText.Text = "Erro ao limpar sessoes.";
            }
            finally
            {
                SetBusy(false);
            }

            string resultText = StatusText.Text;
            LoadSessions();
            StatusText.Text = resultText;
        }

        private async void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(RollbackPaths.BaseDir) || !RollbackPaths.ListSessions().Any())
            {
                LoadSessions();
                return;
            }

            var sessions = RollbackPaths.ListSessions().ToList();
            var count = sessions.Count;
            var confirm = KTWirzade.GUI.MessageBox.Show(
                this,
                $"Excluir todas as {count} sessões válidas de rollback?\n\n" +
                "Isso apaga os registros e backups dessas sessões. Nenhuma alteração será revertida.\n" +
                "Pastas sem registro válido ficam listadas como inválidas e não são incluídas aqui.",
                "Limpar todas as sessões",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            StatusText.Text = "Limpando todas...";
            SetBusy(true, "Limpando sessões...");

            string resultText;
            try
            {
                int failed = await Task.Run(() => sessions.Count(session =>
                {
                    try { return !KTWirzade.Shared.Rollback.RollbackManager.DeleteSession(session.SessionId); }
                    catch { return true; }
                }));
                resultText = failed == 0
                    ? "Todas as sessões removidas."
                    : $"{count - failed} sessão(ões) removida(s); {failed} não puderam ser excluídas.";
            }
            catch
            {
                resultText = "Erro ao limpar sessões.";
            }
            finally
            {
                SetBusy(false);
            }

            LoadSessions();
            StatusText.Text = resultText;
        }

        private async void RollbackActionButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = SessionsList.SelectedItem as RollbackSessionViewModel;
            if (selected == null)
            {
                KTWirzade.GUI.MessageBox.Show(this, "Selecione uma sessão para reverter.", "Aviso");
                return;
            }

            if (selected.IsReverted)
            {
                KTWirzade.GUI.MessageBox.Show(this, "Esta sessão já foi revertida.", "Aviso");
                return;
            }

            if (!selected.IsComplete)
            {
                KTWirzade.GUI.MessageBox.Show(this, "A execução deste APBX ainda não foi concluída. Aguarde antes de iniciar o rollback.", "Execução incompleta", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = KTWirzade.GUI.MessageBox.Show(
                this,
                $"Reverter '{selected.PlaybookName}'?\n\nSerá feita uma tentativa de reverter {selected.PendingCount} ação(ões) pendente(s). Apps removidos e comandos podem não ser reversíveis.\nEsta ação não pode ser desfeita.",
                "Confirmar Rollback",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            StatusText.Text = "Revertendo...";
            _activeSessionId = selected.SessionId;
            SetBusy(true, "Preparando rollback...");

            try
            {
                var result = await Task.Run(() => RollbackManager.RollbackSession(selected.SessionId));

                string resultText;
                if (result.Success)
                {
                    resultText = result.Message;
                    KTWirzade.GUI.MessageBox.Show(this, $"Rollback concluído!\n\n{result.Message}", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    resultText = result.Message;
                    KTWirzade.GUI.MessageBox.Show(this, $"Rollback parcial:\n\n{result.Message}\n\nErros:\n{result.Error}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                LoadSessions();
                StatusText.Text = resultText;
            }
            catch (Exception ex)
            {
                StatusText.Text = "Erro ao reverter.";
                KTWirzade.GUI.MessageBox.Show(this, $"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _activeSessionId = null;
                SetBusy(false);
                UpdateProgressSummary(SessionsList.SelectedItem as RollbackSessionViewModel);
            }
        }
    }

    public class RollbackSessionViewModel
    {
        public string SessionId { get; set; }
        public string PlaybookName { get; set; }
        // Absolute folder of this rollback session (logs, snapshots and session.json);
        // used by the "open session folder" action in the window footer.
        public string DirectoryPath { get; set; }
        public DateTime StartedAt { get; set; }
        public bool IsComplete { get; set; }
        public string DateDisplay => StartedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        public int ActionCount { get; set; }
        public int CompletedCount { get; set; }
        public int ProgressPercent { get; set; }
        public bool IsReverted { get; set; }
        public int PendingCount { get; set; }
        public bool HasRollbackProgress { get; set; }
        public bool CanRollback => IsComplete && ActionCount > 0 && PendingCount > 0 && !IsReverted;
        public string StatusDisplay => !IsComplete ? "Aplicação incompleta" : ActionCount == 0 ? "Sem ações reversíveis" : IsReverted ? "Revertido" : HasRollbackProgress ? $"Parcial ({PendingCount} pendentes)" : "Não revertido";

        public string RelativeDateDisplay
        {
            get
            {
                var age = DateTime.UtcNow - StartedAt;
                if (age.TotalMinutes < 1) return "agora mesmo";
                if (age.TotalHours < 1) return $"há {(int)age.TotalMinutes} min";
                if (age.TotalDays < 1) return $"há {(int)age.TotalHours} h";
                return $"há {(int)age.TotalDays} dia(s)";
            }
        }
    }
}
