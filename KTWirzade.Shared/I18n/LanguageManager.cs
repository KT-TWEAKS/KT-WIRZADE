using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace KTWirzade.Shared.I18n
{
    public enum Language
    {
        PortugueseBR,
        English
    }

    public static class LanguageExtensions
    {
        public static string ToCode(this Language lang)
        {
            switch (lang)
            {
                case Language.PortugueseBR: return "pt-BR";
                case Language.English: return "en";
                default: return "en";
            }
        }

        public static string ToDisplayName(this Language lang)
        {
            switch (lang)
            {
                case Language.PortugueseBR: return "Portugues (BR)";
                case Language.English: return "English";
                default: return "English";
            }
        }

        public static Language FromCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return Language.PortugueseBR;
            if (code.StartsWith("pt", StringComparison.OrdinalIgnoreCase)) return Language.PortugueseBR;
            return Language.English;
        }
    }

    public class TranslationFile
    {
        public string Language { get; set; }
        public string Name { get; set; }
        public Dictionary<string, string> Strings { get; set; } = new Dictionary<string, string>();
    }

    public static class LanguageManager
    {
        public const string SettingsPath = @"C:\ProgramData\KTWirzade\language.json";
        private const string LegacySettingsPath = @"C:\ProgramData\AME\language.json";

        public static Language CurrentLanguage { get; private set; } = Language.PortugueseBR;
        public static TranslationFile CurrentTranslation { get; private set; }

        public static event EventHandler LanguageChanged;

        private static Dictionary<Language, TranslationFile> _cache = new Dictionary<Language, TranslationFile>();

        public static void Initialize()
        {
            try
            {
                var path = SettingsPath;
                if (!File.Exists(path) && File.Exists(LegacySettingsPath))
                {
                    // Backward compatibility: migrate the legacy AME-era preference
                    // file to the current location (best effort).
                    path = LegacySettingsPath;
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                        File.WriteAllText(SettingsPath, File.ReadAllText(LegacySettingsPath).Trim());
                    }
                    catch { }
                }

                if (File.Exists(path))
                {
                    var saved = File.ReadAllText(path).Trim();
                    CurrentLanguage = LanguageExtensions.FromCode(saved);
                }
                else
                {
                    CurrentLanguage = Language.PortugueseBR;
                }
            }
            catch
            {
                CurrentLanguage = Language.PortugueseBR;
            }

            LoadTranslation(CurrentLanguage);
        }

        public static void SetLanguage(Language lang)
        {
            if (lang == CurrentLanguage && CurrentTranslation != null)
                return;

            CurrentLanguage = lang;
            LoadTranslation(lang);
            SavePreference();

            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        public static string T(string key, string fallback = null)
        {
            if (CurrentTranslation == null || CurrentTranslation.Strings == null)
                return fallback ?? key;

            if (CurrentTranslation.Strings.TryGetValue(key, out var value))
                return value;

            return fallback ?? key;
        }

        public static IEnumerable<Language> AvailableLanguages => new[] { Language.PortugueseBR, Language.English };

        private static void LoadTranslation(Language lang)
        {
            if (_cache.ContainsKey(lang))
            {
                CurrentTranslation = _cache[lang];
                return;
            }

            TranslationFile translation = null;

            if (lang == Language.PortugueseBR)
            {
                translation = GetPortugueseBR();
            }
            else if (lang == Language.English)
            {
                translation = GetEnglish();
            }

            if (translation == null)
                translation = new TranslationFile { Language = lang.ToCode() };

            _cache[lang] = translation;
            CurrentTranslation = translation;
        }

        private static TranslationFile GetPortugueseBR()
        {
            return new TranslationFile
            {
                Language = "pt-BR",
                Name = "Portugues (BR)",
                Strings = new Dictionary<string, string>
                {
                    { "App.Title", "KT WIRZADE" },
                    { "App.Version", "Versao" },
                    { "Button.Cancel", "Cancelar" },
                    { "Button.Next", "Avançar" },
                    { "Button.Previous", "Voltar" },
                    { "Button.About", "Sobre" },
                    { "Button.Rollback", "Rollback" },
                    { "Button.Reboot", "Reiniciar agora" },
                    { "Button.Dashboard", "Inicio" },
                    { "Button.Refresh", "Atualizar" },
                    { "Button.Recheck", "Verificar novamente" },
                    { "Button.Download", "Baixar" },
                    { "Button.Confirm", "Confirmar" },
                    { "Button.Yes", "Sim" },
                    { "Button.No", "Nao" },
                    { "Status.Online", "Online" },
                    { "Status.Offline", "Offline" },
                    { "Status.Checking", "Verificando..." },
                    { "Status.Never", "Nunca" },
                    { "Status.UpToDate", "Voce esta na versao mais recente" },
                    { "Status.UpdateAvailable", "Nova versao disponivel" },
                    { "Status.Error", "Erro" },
                    { "Status.Warning", "Aviso" },
                    { "Status.Success", "Sucesso" },
                    { "Dashboard.Welcome", "Bem-vindo ao KT WIRZADE" },
                    { "Dashboard.Subtitle", "Sistema de otimizacao e personalizacao do Windows" },
                    { "Dashboard.QuickActions", "Acoes Rapidas" },
                    { "Dashboard.LoadPlaybook", "Carregar Playbook" },
                    { "Dashboard.LoadPlaybookDesc", "Aplicar um .apbx ao sistema" },
                    { "Dashboard.Rollback", "Reverter Alteracoes" },
                    { "Dashboard.RollbackDesc", "Desfazer playbook anterior" },
                    { "Dashboard.CheckUpdates", "Verificar Atualizacoes" },
                    { "Dashboard.CheckUpdatesDesc", "Buscar versao mais recente" },
                    { "Dashboard.OpenAdmin", "Admin Panel" },
                    { "Dashboard.OpenAdminDesc", "Gerenciar playbooks" },
                    { "Dashboard.CachedPlaybooks", "Playbooks em Cache" },
                    { "Dashboard.EmptyCache", "Nenhum playbook em cache" },
                    { "Dashboard.EmptyCacheHint", "Carregue um .apbx para comecar" },
                    { "Dashboard.StatusChecking", "Verificando" },
                    { "Dashboard.StatusOnline", "GitHub acessível" },
                    { "Dashboard.StatusOffline", "GitHub indisponível" },
                    { "Dashboard.LoadPlaybookSubtitle", "Abrir e revisar um arquivo .apbx" },
                    { "Dashboard.RecoveryTitle", "Recuperação" },
                    { "Dashboard.RecoverySubtitle", "Revisar e retomar rollbacks" },
                    { "Dashboard.UpdatesTitle", "Atualizações" },
                    { "Dashboard.UpdatesSubtitle", "Buscar versão mais recente" },
                    { "Dashboard.InstalledSection", "APBX instalados neste computador" },
                    { "Dashboard.InstalledEmpty", "Nenhum APBX instalado no dispositivo" },
                    { "Dashboard.InstalledHint", "APBX importados ficam disponíveis sem depender do cache online" },
                    { "Dashboard.HiddenSection", "APBX ocultos" },
                    { "Dashboard.HiddenEmpty", "Nenhum APBX oculto" },
                    { "Dashboard.RemovedSection", "Histórico de APBX removidos" },
                    { "Dashboard.RemovedEmpty", "Nenhum APBX removido do computador" },
                    { "Dashboard.VersionUnknown", "versão não informada" },
                    { "Dashboard.DetailsLocal", "disponível localmente" },
                    { "Dashboard.OpenItem", "Abrir" },
                    { "Dashboard.RestoreItem", "Restaurar" },
                    { "Update.AutoCheck", "Verificar ao iniciar" },
                    { "Rollback.Title", "Sessoes de Rollback" },
                    { "Rollback.SelectSession", "Selecione uma sessao para reverter" },
                    { "Rollback.AlreadyReverted", "Esta sessao ja foi revertida" },
                    { "Rollback.ConfirmTitle", "Confirmar Rollback" },
                    { "Rollback.ConfirmMessage", "Reverter '{0}'?\nIsso ira desfazer {1} acao(oes).\nEsta acao nao pode ser desfeita." },
                    { "Rollback.Success", "Rollback concluido com sucesso!" },
                    { "Rollback.Partial", "Rollback parcial" },
                    { "Update.Checking", "Verificando atualizacoes..." },
                    { "Update.Error", "Erro ao verificar atualizacoes: {0}" },
                    { "Update.Available", "Nova versao disponivel: v{0}\n\nDeseja abrir a pagina de download?" },
                    { "Update.NoUpdate", "Voce esta na versao mais recente (v{0})." },
                    { "Update.WindowTitle", "Atualizacoes - KT WIRZADE" },
                    { "Update.Title", "Atualizacoes" },
                    { "Update.CurrentVersion", "Versao atual:" },
                    { "Update.LatestVersion", "Ultima versao: {0}" },
                    { "Update.LoadingHint", "Aguarde enquanto procuramos novas versoes..." },
                    { "Update.ErrorTitle", "Nao foi possivel verificar" },
                    { "Update.UpToDateTitle", "Tudo atualizado" },
                    { "Update.AvailableMessage", "Uma nova versao do KT WIRZADE esta disponivel para download." },
                    { "Update.SkippedMessage", "Ha uma versao mais nova ({0}), mas voce escolheu ignora-la." },
                    { "Update.SkippedConfirmation", "Versao {0} ignorada. Voce pode baixa-la quando quiser." },
                    { "Update.NoNotes", "Sem notas de release." },
                    { "Update.ReleasedOn", "Publicado em {0}" },
                    { "Update.ChannelLabel", "Canal:" },
                    { "Update.ChannelStable", "Estavel" },
                    { "Update.ChannelPrerelease", "Pré-release" },
                    { "Update.DownloadExe", "Baixar EXE" },
                    { "Update.SkipVersion", "Ignorar esta versao" },
                    { "Update.CancelDownload", "Cancelar download" },
                    { "Update.Downloading", "Baixando atualizacao..." },
                    { "Update.Progress", "{0} / {1} ({2}%)" },
                    { "Update.DownloadComplete", "Download concluido e verificado: {0}" },
                    { "Update.DownloadCancelled", "Download cancelado" },
                    { "Update.DownloadFailed", "Nao foi possivel baixar o EXE. Abra a pagina da release para tentar novamente." },
                    { "Update.DownloadError", "Erro no download: {0}" },
                    { "Cache.NoCached", "Nenhum playbook em cache" },
                    { "Progress.Running", "Executando playbook..." },
                    { "Intro.Action.Run", "Executar ação" },
                    { "Intro.Action.RunAgain", "Executar novamente" },
                    { "Intro.Action.Upgrade", "Atualizar" },
                    { "Intro.Applied.Current", "Este APBX já está aplicado" },
                    { "Intro.Applied.Errors", "Este APBX foi aplicado com erros" },
                    { "Intro.Applied.Failed", "Falha ao aplicar este APBX" },
                    { "Intro.Guidance.Default", "Clique em “Avançar” para verificar os requisitos." },
                    { "Intro.Guidance.Applied", "Este APBX já está aplicado. Use “Executar novamente” para reaplicá-lo." },
                    { "Intro.Guidance.Antivirus", "Remova o antivírus de terceiros para continuar." },
                    { "Intro.Guidance.Security", "Conclua a ação de segurança acima para continuar." },
                    { "Intro.Guidance.Upgrade", "Use Atualizar para instalar a versão mais recente deste APBX." },
                    { "Intro.NextStep", "O próximo passo verificará se o sistema atende\naos requisitos para continuar." },
                    { "Intro.Security.Title", "Desativar segurança" },
                    { "Intro.Security.Description", "Permitir acesso ao sistema de arquivos do KT WIRZADE" },
                    { "Intro.Upgrade.Available", "Há uma atualização disponível para este APBX" },
                    { "Common.Loading", "Carregando..." },
                    { "Common.Cancel", "Cancelar" },
                    { "Common.Ok", "OK" }
                }
            };
        }

        private static TranslationFile GetEnglish()
        {
            return new TranslationFile
            {
                Language = "en",
                Name = "English",
                Strings = new Dictionary<string, string>
                {
                    { "App.Title", "KT WIRZADE" },
                    { "App.Version", "Version" },
                    { "Button.Cancel", "Cancel" },
                    { "Button.Next", "Next" },
                    { "Button.Previous", "Previous" },
                    { "Button.About", "About" },
                    { "Button.Rollback", "Rollback" },
                    { "Button.Reboot", "Restart now" },
                    { "Button.Dashboard", "Home" },
                    { "Button.Refresh", "Refresh" },
                    { "Button.Recheck", "Check again" },
                    { "Button.Download", "Download" },
                    { "Button.Confirm", "Confirm" },
                    { "Button.Yes", "Yes" },
                    { "Button.No", "No" },
                    { "Status.Online", "Online" },
                    { "Status.Offline", "Offline" },
                    { "Status.Checking", "Checking..." },
                    { "Status.Never", "Never" },
                    { "Status.UpToDate", "You are on the latest version" },
                    { "Status.UpdateAvailable", "New version available" },
                    { "Status.Error", "Error" },
                    { "Status.Warning", "Warning" },
                    { "Status.Success", "Success" },
                    { "Dashboard.Welcome", "Welcome to KT WIRZADE" },
                    { "Dashboard.Subtitle", "Windows optimization and customization system" },
                    { "Dashboard.QuickActions", "Quick Actions" },
                    { "Dashboard.LoadPlaybook", "Load Playbook" },
                    { "Dashboard.LoadPlaybookDesc", "Apply a .apbx to the system" },
                    { "Dashboard.Rollback", "Revert Changes" },
                    { "Dashboard.RollbackDesc", "Undo previous playbook" },
                    { "Dashboard.CheckUpdates", "Check for Updates" },
                    { "Dashboard.CheckUpdatesDesc", "Look for latest version" },
                    { "Dashboard.OpenAdmin", "Admin Panel" },
                    { "Dashboard.OpenAdminDesc", "Manage playbooks" },
                    { "Dashboard.CachedPlaybooks", "Cached Playbooks" },
                    { "Dashboard.EmptyCache", "No cached playbooks" },
                    { "Dashboard.EmptyCacheHint", "Load a .apbx to get started" },
                    { "Dashboard.StatusChecking", "Checking" },
                    { "Dashboard.StatusOnline", "GitHub reachable" },
                    { "Dashboard.StatusOffline", "GitHub unreachable" },
                    { "Dashboard.LoadPlaybookSubtitle", "Open and review an .apbx file" },
                    { "Dashboard.RecoveryTitle", "Recovery" },
                    { "Dashboard.RecoverySubtitle", "Review and resume rollbacks" },
                    { "Dashboard.UpdatesTitle", "Updates" },
                    { "Dashboard.UpdatesSubtitle", "Look for the latest release" },
                    { "Dashboard.InstalledSection", "APBX packages installed on this computer" },
                    { "Dashboard.InstalledEmpty", "No APBX packages installed on this device" },
                    { "Dashboard.InstalledHint", "Imported APBX packages stay available without the online cache" },
                    { "Dashboard.HiddenSection", "Hidden APBX packages" },
                    { "Dashboard.HiddenEmpty", "No hidden APBX packages" },
                    { "Dashboard.RemovedSection", "Removed APBX packages" },
                    { "Dashboard.RemovedEmpty", "No APBX packages removed from this computer" },
                    { "Dashboard.VersionUnknown", "version not reported" },
                    { "Dashboard.DetailsLocal", "available locally" },
                    { "Dashboard.OpenItem", "Open" },
                    { "Dashboard.RestoreItem", "Restore" },
                    { "Update.AutoCheck", "Check at startup" },
                    { "Rollback.Title", "Rollback Sessions" },
                    { "Rollback.SelectSession", "Select a session to revert" },
                    { "Rollback.AlreadyReverted", "This session has already been reverted" },
                    { "Rollback.ConfirmTitle", "Confirm Rollback" },
                    { "Rollback.ConfirmMessage", "Revert '{0}'?\nThis will undo {1} action(s).\nThis action cannot be undone." },
                    { "Rollback.Success", "Rollback completed successfully!" },
                    { "Rollback.Partial", "Partial rollback" },
                    { "Update.Checking", "Checking for updates..." },
                    { "Update.Error", "Error checking for updates: {0}" },
                    { "Update.Available", "New version available: v{0}\n\nOpen the download page?" },
                    { "Update.NoUpdate", "You are on the latest version (v{0})." },
                    { "Update.WindowTitle", "Updates - KT WIRZADE" },
                    { "Update.Title", "Updates" },
                    { "Update.CurrentVersion", "Current version:" },
                    { "Update.LatestVersion", "Latest version: {0}" },
                    { "Update.LoadingHint", "Wait while we look for new releases..." },
                    { "Update.ErrorTitle", "Could not check for updates" },
                    { "Update.UpToDateTitle", "Up to date" },
                    { "Update.AvailableMessage", "A new version of KT WIRZADE is available to download." },
                    { "Update.SkippedMessage", "A newer version ({0}) exists, but you chose to ignore it." },
                    { "Update.SkippedConfirmation", "Version {0} ignored. You can download it whenever you want." },
                    { "Update.NoNotes", "No release notes." },
                    { "Update.ReleasedOn", "Published on {0}" },
                    { "Update.ChannelLabel", "Channel:" },
                    { "Update.ChannelStable", "Stable" },
                    { "Update.ChannelPrerelease", "Prerelease" },
                    { "Update.DownloadExe", "Download EXE" },
                    { "Update.SkipVersion", "Ignore this version" },
                    { "Update.CancelDownload", "Cancel download" },
                    { "Update.Downloading", "Downloading update..." },
                    { "Update.Progress", "{0} / {1} ({2}%)" },
                    { "Update.DownloadComplete", "Download completed and verified: {0}" },
                    { "Update.DownloadCancelled", "Download cancelled" },
                    { "Update.DownloadFailed", "Could not download the EXE. Open the release page to try again." },
                    { "Update.DownloadError", "Download error: {0}" },
                    { "Cache.NoCached", "No cached playbooks" },
                    { "Progress.Running", "Running playbook..." },
                    { "Intro.Action.Run", "Run action" },
                    { "Intro.Action.RunAgain", "Run again" },
                    { "Intro.Action.Upgrade", "Update" },
                    { "Intro.Applied.Current", "This APBX is already applied" },
                    { "Intro.Applied.Errors", "This APBX was applied with errors" },
                    { "Intro.Applied.Failed", "This APBX failed to apply" },
                    { "Intro.Guidance.Default", "Click “Next” to check the requirements." },
                    { "Intro.Guidance.Applied", "This APBX is already applied. Use “Run again” to reapply it." },
                    { "Intro.Guidance.Antivirus", "Remove the third-party antivirus to continue." },
                    { "Intro.Guidance.Security", "Complete the security action above to continue." },
                    { "Intro.Guidance.Upgrade", "Use Update to install the latest version of this APBX." },
                    { "Intro.NextStep", "The next step will check whether your system meets\nthe requirements to continue." },
                    { "Intro.Security.Title", "Disable security" },
                    { "Intro.Security.Description", "Allow KT WIRZADE to access the file system" },
                    { "Intro.Upgrade.Available", "An update is available for this APBX" },
                    { "Common.Loading", "Loading..." },
                    { "Common.Cancel", "Cancel" },
                    { "Common.Ok", "OK" }
                }
            };
        }

        private static void SavePreference()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, CurrentLanguage.ToCode());
            }
            catch { }
        }
    }
}
