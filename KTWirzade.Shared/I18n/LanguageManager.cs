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
        public const string SettingsPath = @"C:\ProgramData\AME\language.json";

        public static Language CurrentLanguage { get; private set; } = Language.PortugueseBR;
        public static TranslationFile CurrentTranslation { get; private set; }

        public static event EventHandler LanguageChanged;

        private static Dictionary<Language, TranslationFile> _cache = new Dictionary<Language, TranslationFile>();

        public static void Initialize()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var saved = File.ReadAllText(SettingsPath).Trim();
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
                    { "Button.Next", "Proximo" },
                    { "Button.Previous", "Voltar" },
                    { "Button.About", "Sobre" },
                    { "Button.Rollback", "Rollback" },
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
                    { "Cache.NoCached", "Nenhum playbook em cache" },
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
                    { "Cache.NoCached", "No cached playbooks" },
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
