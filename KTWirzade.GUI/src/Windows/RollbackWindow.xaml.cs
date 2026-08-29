using System;
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

        public RollbackWindow()
        {
            DataContext = this;
            InitializeComponent();
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

            if (!Directory.Exists(RollbackPaths.BaseDir))
            {
                StatusText.Text = "Nenhuma sessão encontrada.";
                return;
            }

            var sessionDirs = Directory.GetDirectories(RollbackPaths.BaseDir)
                .OrderByDescending(d => Directory.GetCreationTime(d));

            foreach (var dir in sessionDirs)
            {
                var sessionFile = Path.Combine(dir, "session.json");
                if (!File.Exists(sessionFile)) continue;

                try
                {
                    var json = File.ReadAllText(sessionFile);
                    var session = Newtonsoft.Json.JsonConvert.DeserializeObject<RollbackSession>(json);
                    if (session == null) continue;

                    var rolledBack = session.WasRolledBack || (session.Entries.Count > 0 && session.Entries.All(e => e.RollbackCompleted));

                    Sessions.Add(new RollbackSessionViewModel
                    {
                        SessionId = session.SessionId,
                        PlaybookName = string.IsNullOrEmpty(session.PlaybookName) ? "(desconhecido)" : session.PlaybookName,
                        StartedAt = session.StartedAt,
                        ActionCount = session.Entries.Count,
                        IsReverted = rolledBack
                    });
                }
                catch
                {
                }
            }

            SessionsList.ItemsSource = Sessions;
            StatusText.Text = $"{Sessions.Count} sess{(Sessions.Count != 1 ? "ões" : "ão")} encontrada{(Sessions.Count != 1 ? "s" : "")}";
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

        private void DeleteSession_Click(object sender, RoutedEventArgs e)
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
            try
            {
                ok = Task.Run(() => RollbackManager.DeleteSession(sessionId)).GetAwaiter().GetResult();
            }
            catch
            {
                ok = false;
            }

            StatusText.Text = ok ? "Sessão excluída." : "Não foi possível excluir (arquivos em uso?).";
            LoadSessions();
        }

        private void ClearOldButton_Click(object sender, RoutedEventArgs e)
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
            RollbackActionButton.IsEnabled = false;

            try
            {
                var removed = System.Threading.Tasks.Task.Run(() => KTWirzade.Shared.Rollback.RollbackManager.CleanupOldSessionsNow()).GetAwaiter().GetResult();
                StatusText.Text = $"{removed} sessão(ões) removida(s)";
            }
            catch
            {
                StatusText.Text = "Erro ao limpar sessões.";
            }
            finally
            {
                RollbackActionButton.IsEnabled = true;
            }

            LoadSessions();
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(RollbackPaths.BaseDir) || !RollbackPaths.ListSessions().Any())
            {
                LoadSessions();
                return;
            }

            var count = RollbackPaths.ListSessions().Count();
            var confirm = KTWirzade.GUI.MessageBox.Show(
                this,
                $"Excluir TODAS as {count} sessão(ões) de rollback?\n\n" +
                "Isso apaga todos os registros e backups de rollback.\n" +
                "Nenhuma alteração será revertida — apenas o histórico será apagado.",
                "Limpar todas as sessões",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            StatusText.Text = "Limpando todas...";
            RollbackActionButton.IsEnabled = false;

            try
            {
                foreach (var session in RollbackPaths.ListSessions().ToList())
                {
                    try { KTWirzade.Shared.Rollback.RollbackManager.DeleteSession(session.SessionId); } catch { }
                }
                StatusText.Text = "Todas as sessões removidas.";
            }
            catch
            {
                StatusText.Text = "Erro ao limpar sessões.";
            }
            finally
            {
                RollbackActionButton.IsEnabled = true;
            }

            LoadSessions();
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

            var confirm = KTWirzade.GUI.MessageBox.Show(
                this,
                $"Reverter '{selected.PlaybookName}'?\n\nIsso irá desfazer {selected.ActionCount} ação(ões).\nEsta ação não pode ser desfeita.",
                "Confirmar Rollback",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            StatusText.Text = "Revertendo...";
            RollbackActionButton.IsEnabled = false;
            RefreshButton.IsEnabled = false;

            try
            {
                var result = await Task.Run(() => RollbackManager.RollbackSession(selected.SessionId));

                if (result.Success)
                {
                    StatusText.Text = result.Message;
                    KTWirzade.GUI.MessageBox.Show(this, $"Rollback concluído!\n\n{result.Message}", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = result.Message;
                    KTWirzade.GUI.MessageBox.Show(this, $"Rollback parcial:\n\n{result.Message}\n\nErros:\n{result.Error}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                LoadSessions();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Erro ao reverter.";
                KTWirzade.GUI.MessageBox.Show(this, $"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RollbackActionButton.IsEnabled = true;
                RefreshButton.IsEnabled = true;
            }
        }
    }

    public class RollbackSessionViewModel
    {
        public string SessionId { get; set; }
        public string PlaybookName { get; set; }
        public DateTime StartedAt { get; set; }
        public string DateDisplay => StartedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        public int ActionCount { get; set; }
        public bool IsReverted { get; set; }
        public string StatusDisplay => IsReverted ? "Revertido" : "Ativo";

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
