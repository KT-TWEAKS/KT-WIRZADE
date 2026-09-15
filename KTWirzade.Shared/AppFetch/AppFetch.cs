using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Downloader;
using Newtonsoft.Json.Linq;

namespace KTWirzade.Shared.AppFetch
{
    /// <summary>
    /// KT WIRZADE App Fetcher - Baixa apps da Microsoft Store sem precisar dos serviços da loja.
    /// Integra com o sistema de cache e download existente do KT WIRZADE.
    /// </summary>
    public static class AppFetch
    {
        private const string StoreApiBaseUrl = "https://storeedgefd.dsx.mp.microsoft.com/v9.0";
        private const string StoreCatalogUrl = "https://displaycatalog.mp.microsoft.com/v7.0/products/";

        private static readonly HttpClient Client = new HttpClient();

        /// <summary>
        /// Informação de um app da Microsoft Store.
        /// </summary>
        public class StoreAppInfo
        {
            public string PackageFamilyName { get; set; }
            public string PackageName { get; set; }
            public string PackageVersion { get; set; }
            public string DisplayName { get; set; }
            public string Publisher { get; set; }
            public string Architecture { get; set; }
            public long SizeBytes { get; set; }
            public string DownloadUrl { get; set; }
            public string LicenseDownloadUrl { get; set; }

            public override string ToString()
            {
                return $"{DisplayName} ({PackageName}) - v{PackageVersion} [{Architecture}]";
            }
        }

        /// <summary>
        /// Progresso do download.
        /// </summary>
        public class AppFetchProgress
        {
            public AppFetchPhase Phase { get; set; }
            public int PercentComplete { get; set; }
            public long BytesReceived { get; set; }
            public long TotalBytes { get; set; }
            public string Message { get; set; }
            public string AppName { get; set; }
        }

        public enum AppFetchPhase
        {
            Idle,
            Searching,
            Downloading,
            Installing,
            Complete,
            Error
        }

        /// <summary>
        /// Evento de progresso para binding com a GUI.
        /// </summary>
        public static event EventHandler<AppFetchProgress> ProgressChanged;

        /// <summary>
        /// Pesquisa apps no catálogo da Microsoft Store.
        /// </summary>
        public static async Task<List<StoreAppInfo>> SearchApps(string query, int maxResults = 20)
        {
            var results = new List<StoreAppInfo>();

            try
            {
                ReportProgress(AppFetchPhase.Searching, 0, $"Pesquisando por '{query}'...");

                var url = $"{StoreCatalogUrl}?query={Uri.EscapeDataString(query)}&fieldsTemplate=MountWindows11Market&market=US&deviceFamily=Windows.Desktop&count={maxResults}";

                var response = await Client.GetStringAsync(url);
                var json = JObject.Parse(response);

                var items = json["Items"] as JArray;
                if (items == null)
                    return results;

                foreach (var item in items)
                {
                    try
                    {
                        var app = new StoreAppInfo
                        {
                            PackageFamilyName = item["PackageFamilyName"]?.ToString(),
                            DisplayName = item["LocalizedProperties"]?[0]?["ProductTitle"]?.ToString(),
                            Publisher = item["LocalizedProperties"]?[0]?["PublisherName"]?.ToString(),
                            PackageVersion = item["MarketSpecificOfferProperties"]?[0]?["Packages"]?[0]?["Version"]?.ToString(),
                            Architecture = item["MarketSpecificOfferProperties"]?[0]?["Packages"]?[0]?["Architecture"]?.ToString() ?? "neutral"
                        };

                        if (!string.IsNullOrEmpty(app.PackageFamilyName) && !string.IsNullOrEmpty(app.DisplayName))
                        {
                            results.Add(app);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteExceptionSafe(ex, "AppFetch: Error parsing search result item.");
                    }
                }

                ReportProgress(AppFetchPhase.Searching, 100, $"Encontrados {results.Count} apps.");
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "AppFetch: Search failed.");
                ReportProgress(AppFetchPhase.Error, 0, $"Erro na pesquisa: {e.Message}");
            }

            return results;
        }

        /// <summary>
        /// Obtém informações de download de um app pelo PackageFamilyName.
        /// </summary>
        public static async Task<StoreAppInfo> GetAppDownloadInfo(string packageFamilyName)
        {
            try
            {
                var url = $"{StoreApiBaseUrl}/packages/{packageFamilyName}?market=US&deviceFamily=Windows.Desktop";

                var response = await Client.GetStringAsync(url);
                var json = JObject.Parse(response);

                var appInfo = new StoreAppInfo
                {
                    PackageFamilyName = packageFamilyName,
                    DisplayName = json["Title"]?.ToString() ?? packageFamilyName,
                    Publisher = json["Publisher"]?.ToString()
                };

                // Get download URLs from the response
                var packages = json["Packages"] as JArray;
                if (packages != null)
                {
                    foreach (var package in packages)
                    {
                        try
                        {
                            var arch = package["Architecture"]?.ToString() ?? "neutral";
                            var downloadUrl = package["DownloadUrl"]?.ToString();
                            var licenseUrl = package["LicenseDownloadUrl"]?.ToString();

                            if (!string.IsNullOrEmpty(downloadUrl))
                            {
                                appInfo.Architecture = arch;
                                appInfo.DownloadUrl = downloadUrl.StartsWith("http") ? downloadUrl : $"https:{downloadUrl}";
                                appInfo.LicenseDownloadUrl = !string.IsNullOrEmpty(licenseUrl) && licenseUrl.StartsWith("http")
                                    ? licenseUrl
                                    : (string.IsNullOrEmpty(licenseUrl) ? null : $"https:{licenseUrl}");
                                appInfo.PackageVersion = package["Version"]?.ToString() ?? "1.0.0";
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.WriteExceptionSafe(ex, $"AppFetch: Error parsing package for {packageFamilyName}.");
                        }
                    }
                }

                return appInfo;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, $"AppFetch: Failed to get download info for {packageFamilyName}.");
                return null;
            }
        }

        /// <summary>
        /// Baixa e salva um app da Microsoft Store.
        /// Usa o diretório de cache do KT WIRZADE.
        /// </summary>
        public static async Task<string> DownloadApp(StoreAppInfo appInfo, string destinationFolder = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (appInfo == null || string.IsNullOrEmpty(appInfo.DownloadUrl))
                {
                    Log.WriteSafe(LogType.Error, "AppFetch: No download URL available.", null);
                    return null;
                }

            // Use cache folder if no destination specified
                destinationFolder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AME", "Cache", "AppFetch");
                Directory.CreateDirectory(destinationFolder);

                // Build filename
                var safeName = string.Join("_", appInfo.PackageFamilyName.Split(Path.GetInvalidFileNameChars()));
                var fileName = $"{safeName}_{appInfo.PackageVersion}_{appInfo.Architecture}.appx";
                var filePath = Path.Combine(destinationFolder, fileName);

                // Skip if already downloaded
                if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
                {
                    ReportProgress(AppFetchPhase.Downloading, 100, $"{appInfo.DisplayName} já está em cache.");
                    return filePath;
                }

                ReportProgress(AppFetchPhase.Downloading, 0, $"Baixando {appInfo.DisplayName}...", appInfo.DisplayName);

                // Use Downloader library for better download experience
                var downloader = new DownloadService(new DownloadConfiguration()
                {
                    Timeout = 30000,
                    MaxTryAgainOnFailover = 3,
                    RequestConfiguration = new RequestConfiguration()
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36",
                    }
                });

                int lastProgress = 0;
                downloader.DownloadProgressChanged += (sender, args) =>
                {
                    var newProgress = (int)Math.Round(args.ProgressPercentage);
                    if (newProgress != lastProgress)
                    {
                        ReportProgress(AppFetchPhase.Downloading, newProgress,
                            $"Baixando {appInfo.DisplayName}... {newProgress}%",
                            appInfo.DisplayName);
                        lastProgress = newProgress;
                    }
                };

                await downloader.DownloadFileTaskAsync(appInfo.DownloadUrl, filePath, cancellationToken);

                if (File.Exists(filePath))
                {
                    ReportProgress(AppFetchPhase.Downloading, 100, $"Download completo: {appInfo.DisplayName}", appInfo.DisplayName);
                    return filePath;
                }

                ReportProgress(AppFetchPhase.Error, 0, $"Falha no download: {appInfo.DisplayName}", appInfo.DisplayName);
                return null;
            }
            catch (OperationCanceledException)
            {
                Log.WriteSafe(LogType.Info, $"AppFetch: Download cancelled for {appInfo.DisplayName}", null);
                return null;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, $"AppFetch: Download failed for {appInfo.DisplayName}.");
                ReportProgress(AppFetchPhase.Error, 0, $"Erro: {e.Message}", appInfo.DisplayName);
                return null;
            }
        }

        /// <summary>
        /// Instala um app baixado usando DISM (não precisa do serviço da Store).
        /// </summary>
        public static async Task<bool> InstallApp(string packagePath)
        {
            try
            {
                if (!File.Exists(packagePath))
                {
                    Log.WriteSafe(LogType.Error, $"AppFetch: Package not found: {packagePath}", null);
                    return false;
                }

                ReportProgress(AppFetchPhase.Installing, 0, $"Instalando {Path.GetFileName(packagePath)}...");

                // .appx/.msix packages are installed with Add-AppxPackage; DISM
                // /add-package is for .cab servicing packages and used to fail here.
                bool isServicingPackage = packagePath.EndsWith(".cab", StringComparison.OrdinalIgnoreCase) ||
                                          packagePath.EndsWith(".msu", StringComparison.OrdinalIgnoreCase);

                string command = isServicingPackage
                    ? $"dism /online /add-package /packagepath:\"{packagePath}\" /quiet /norestart"
                    : $"powershell -NoProfile -Command \"Add-AppxPackage -Path '{packagePath}'\"";

                var cmdAction = new Actions.CmdAction { Command = command };

                // RunTaskOnMainThread(null) dereferenced the null writer and crashed;
                // use RunTask with a real writer and honor its result.
                bool installed = await cmdAction.RunTask(new Output.OutputWriter(
                    "AppFetch", Path.Combine(Path.GetTempPath(), "AME-AppFetch", "install-output.txt")));

                if (!installed)
                {
                    Log.WriteSafe(LogType.Error, $"AppFetch: Installation failed for {packagePath}", null);
                    return false;
                }

                ReportProgress(AppFetchPhase.Complete, 100, $"Instalado: {Path.GetFileName(packagePath)}");
                return true;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, $"AppFetch: Installation failed for {packagePath}");
                ReportProgress(AppFetchPhase.Error, 0, $"Falha na instalação: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Baixa e instala um app em um passo.
        /// </summary>
        public static async Task<bool> DownloadAndInstallApp(StoreAppInfo appInfo, CancellationToken cancellationToken = default)
        {
            var packagePath = await DownloadApp(appInfo, cancellationToken: cancellationToken);
            if (packagePath == null)
                return false;

            return await InstallApp(packagePath);
        }

        /// <summary>
        /// Baixa as dependências comuns de apps UWP.
        /// </summary>
        public static async Task<List<string>> DownloadCommonDependencies(string destinationFolder = null, CancellationToken cancellationToken = default)
        {
            var downloaded = new List<string>();

            var dependencies = new List<(string Name, string FamilyName)>
            {
                ("VCLibs", "Microsoft.VCLibs.140.00.UWPDesktop"),
                ("UI.Xaml", "Microsoft.UI.Xaml.2.8"),
                ("NET Runtime", "Microsoft.NET.Native.Runtime.2.2"),
                ("NET Framework", "Microsoft.NET.Native.Framework.2.2"),
                ("Store Engagement", "Microsoft.Services.Store.Engagement"),
                ("WinJS", "Microsoft.WinJS.2.0")
            };

            foreach (var (name, familyName) in dependencies)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ReportProgress(AppFetchPhase.Downloading, 0, $"Baixando dependência: {name}...", name);

                    var depInfo = await GetAppDownloadInfo(familyName);
                    if (depInfo?.DownloadUrl != null)
                    {
                        var path = await DownloadApp(depInfo, destinationFolder, cancellationToken);
                        if (path != null)
                            downloaded.Add(path);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.WriteExceptionSafe(ex, $"AppFetch: Failed to download dependency {name}.");
                    // Dependencies are optional - continue
                }
            }

            return downloaded;
        }

        /// <summary>
        /// Instala múltiplos pacotes (app + dependências).
        /// </summary>
        public static async Task<bool> InstallPackageWithDependencies(StoreAppInfo appInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                // First download common dependencies
                ReportProgress(AppFetchPhase.Downloading, 0, "Verificando dependências...");
                var depFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AME", "Cache", "AppFetch", "Dependencies");
                var deps = await DownloadCommonDependencies(depFolder, cancellationToken);

                // Install dependencies first
                foreach (var dep in deps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await InstallApp(dep);
                }

                // Then download and install the main app
                return await DownloadAndInstallApp(appInfo, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Log.WriteSafe(LogType.Info, "AppFetch: Installation cancelled by user.", null);
                return false;
            }
            catch (Exception ex)
            {
                Log.WriteExceptionSafe(ex, $"AppFetch: Failed to install package with dependencies.");
                ReportProgress(AppFetchPhase.Error, 0, $"Erro: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lista de apps comumente necessários após aplicar um playbook.
        /// </summary>
        public static List<StoreAppInfo> GetCommonApps()
        {
            return new List<StoreAppInfo>
            {
                new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsStore", DisplayName = "Microsoft Store" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsCalculator", DisplayName = "Calculadora" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.Windows.Photos", DisplayName = "Fotos" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.MicrosoftStickyNotes", DisplayName = "Sticky Notes" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.ScreenSketch", DisplayName = "Ferramenta de Captura" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.Paint", DisplayName = "Paint" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsNotepad", DisplayName = "Bloco de Notas" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsTerminal", DisplayName = "Windows Terminal" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.MicrosoftEdge.Stable", DisplayName = "Microsoft Edge" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.DesktopAppInstaller", DisplayName = "Instalador de App (winget)" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.Windows.DevHome", DisplayName = "Dev Home" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsCamera", DisplayName = "Câmera" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsSoundRecorder", DisplayName = "Gravador de Som" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsAlarms", DisplayName = "Alarmes e Relógio" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.People", DisplayName = "Pessoas" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsMaps", DisplayName = "Mapas" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.ZuneMusic", DisplayName = "Media Player" },
                new StoreAppInfo { PackageFamilyName = "Microsoft.ZuneVideo", DisplayName = "Filmes e TV" }
            };
        }

        /// <summary>
        /// Gets apps by category for the UI.
        /// </summary>
        public static Dictionary<string, List<StoreAppInfo>> GetAppsByCategory()
        {
            return new Dictionary<string, List<StoreAppInfo>>
            {
                ["Essenciais"] = new List<StoreAppInfo>
                {
                    new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsStore", DisplayName = "Microsoft Store" },
                    new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsCalculator", DisplayName = "Calculadora" },
                    new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsNotepad", DisplayName = "Bloco de Notas" },
                    new StoreAppInfo { PackageFamilyName = "Microsoft.ScreenSketch", DisplayName = "Ferramenta de Captura" }
                },
                ["Multimídia"] = new List<StoreAppInfo>
                {
                    new StoreAppInfo { PackageFamilyName = "Microsoft.Windows.Photos", DisplayName = "Fotos" },
                    new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsCamera", DisplayName = "Câmera" },
                    new StoreAppInfo { PackageFamilyName = "Microsoft.ZuneMusic", DisplayName = "Media Player" },
                    new StoreAppInfo { PackageFamilyName = "Microsoft.ZuneVideo", DisplayName = "Filmes e TV" },
                    new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsSoundRecorder", DisplayName = "Gravador de Som" }
                },
                ["Produtividade"] = new List<StoreAppInfo>
                {
                    new StoreAppInfo { PackageFamilyName = "Microsoft.People", DisplayName = "Pessoas" },
                    new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsAlarms", DisplayName = "Alarmes e Relógio" },
                    new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsMaps", DisplayName = "Mapas" },
                    new StoreAppInfo { PackageFamilyName = "Microsoft.MicrosoftStickyNotes", DisplayName = "Sticky Notes" }
                },
                ["Desenvolvedor"] = new List<StoreAppInfo>
                {
                    new StoreAppInfo { PackageFamilyName = "Microsoft.WindowsTerminal", DisplayName = "Windows Terminal" },
                    new StoreAppInfo { PackageFamilyName = "Microsoft.Windows.DevHome", DisplayName = "Dev Home" },
                    new StoreAppInfo { PackageFamilyName = "Microsoft.DesktopAppInstaller", DisplayName = "Instalador de App (winget)" }
                },
                ["Navegador"] = new List<StoreAppInfo>
                {
                    new StoreAppInfo { PackageFamilyName = "Microsoft.MicrosoftEdge.Stable", DisplayName = "Microsoft Edge" }
                }
            };
        }

        private static void ReportProgress(AppFetchPhase phase, int percent, string message, string appName = null)
        {
            ProgressChanged?.Invoke(null, new AppFetchProgress
            {
                Phase = phase,
                PercentComplete = percent,
                Message = message,
                AppName = appName
            });
        }
    }
}
