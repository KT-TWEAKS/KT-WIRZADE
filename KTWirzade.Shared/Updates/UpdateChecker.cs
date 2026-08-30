using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KTWirzade.Shared.Updates
{
    public class GitHubRelease
    {
        public string TagName { get; set; }
        public string Name { get; set; }
        public string Body { get; set; }
        public string HtmlUrl { get; set; }
        public DateTime PublishedAt { get; set; }
        public bool Prerelease { get; set; }
        public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
    }

    public class GitHubAsset
    {
        public string Name { get; set; }
        public string BrowserDownloadUrl { get; set; }
        public long Size { get; set; }
    }

    public class UpdateInfo
    {
        public bool UpdateAvailable { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string ReleaseName { get; set; }
        public string ReleaseNotes { get; set; }
        public string DownloadUrl { get; set; }
        public DateTime PublishedAt { get; set; }
        public bool IsPrerelease { get; set; }
        public string Error { get; set; }
        public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
    }

    public class DownloadProgress
    {
        public long BytesReceived { get; set; }
        public long TotalBytes { get; set; }
        public int PercentComplete => TotalBytes > 0 ? (int)((BytesReceived * 100) / TotalBytes) : 0;
        public string Status { get; set; }
    }

    public class DownloadProgressChangedEventArgs : EventArgs
    {
        public DownloadProgress Progress { get; set; }
    }

    public static class UpdateChecker
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/KT-TWEAKS/KT-WIRZADE/releases/latest";
        private const string GitHubReleasesPage = "https://github.com/KT-TWEAKS/KT-WIRZADE/releases";

        public static UpdateInfo CurrentUpdateInfo { get; private set; }

        public static event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

        public static async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var info = new UpdateInfo
            {
                CurrentVersion = Globals.CurrentVersion
            };

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    client.DefaultRequestHeaders.UserAgent.Clear();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("KT-WIRZADE-Updater/" + Globals.CurrentVersion);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");

                    HttpResponseMessage response = null;
                    try
                    {
                        response = await client.GetAsync(GitHubApiUrl);
                    }
                    catch (HttpRequestException ex)
                    {
                        info.Error = "Nao foi possivel conectar ao GitHub. Verifique sua conexao com a internet.";
                        CurrentUpdateInfo = info;
                        return info;
                    }
                    catch (TaskCanceledException)
                    {
                        info.Error = "Tempo esgotado ao verificar atualizacoes.";
                        CurrentUpdateInfo = info;
                        return info;
                    }

                    if (response == null)
                    {
                        info.Error = "Resposta vazia do GitHub.";
                        CurrentUpdateInfo = info;
                        return info;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            info.Error = "Nenhuma release encontrada no repositorio.";
                        }
                        else if (response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            info.Error = "Limite de requisicoes do GitHub atingido. Tente novamente mais tarde.";
                        }
                        else
                        {
                            info.Error = $"GitHub retornou erro {(int)response.StatusCode}.";
                        }
                        CurrentUpdateInfo = info;
                        return info;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        info.Error = "Resposta vazia do GitHub.";
                        CurrentUpdateInfo = info;
                        return info;
                    }

                    var release = JsonConvert.DeserializeObject<GitHubRelease>(json);
                    if (release == null || string.IsNullOrEmpty(release.TagName))
                    {
                        info.Error = "Formato de resposta invalido do GitHub.";
                        CurrentUpdateInfo = info;
                        return info;
                    }

                    info.LatestVersion = NormalizeVersion(release.TagName);
                    info.ReleaseName = release.Name;
                    info.ReleaseNotes = release.Body;
                    info.DownloadUrl = release.HtmlUrl;
                    info.PublishedAt = release.PublishedAt;
                    info.IsPrerelease = release.Prerelease;
                    info.Assets = (release.Assets ?? Array.Empty<GitHubAsset>())
                        .Where(a => a.Name != null &&
                                    (a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                     a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                        .ToArray();

                    info.UpdateAvailable = IsNewerVersion(info.LatestVersion, info.CurrentVersion);
                }
            }
            catch (Exception ex)
            {
                info.Error = $"Erro inesperado: {ex.Message}";
            }

            CurrentUpdateInfo = info;
            return info;
        }

        public static async Task<string> DownloadLatestAsync(string targetPath = null)
        {
            if (CurrentUpdateInfo == null || !CurrentUpdateInfo.UpdateAvailable)
                await CheckForUpdatesAsync();

            if (CurrentUpdateInfo == null || !CurrentUpdateInfo.UpdateAvailable)
                return null;

            var asset = FindBestAsset(CurrentUpdateInfo.Assets);
            if (asset == null)
                return null;

            if (string.IsNullOrEmpty(targetPath))
            {
                var downloadsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");
                Directory.CreateDirectory(downloadsDir);
                targetPath = Path.Combine(downloadsDir, asset.Name);
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(10);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("KT-WIRZADE-Updater/" + Globals.CurrentVersion);

                    using (var response = await client.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        var total = response.Content.Headers.ContentLength ?? asset.Size;
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = File.Create(targetPath))
                        {
                            var buffer = new byte[8192];
                            long received = 0;
                            int read;

                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, read);
                                received += read;

                                DownloadProgressChanged?.Invoke(null, new DownloadProgressChangedEventArgs
                                {
                                    Progress = new DownloadProgress
                                    {
                                        BytesReceived = received,
                                        TotalBytes = total,
                                        Status = "Baixando..."
                                    }
                                });
                            }
                        }
                    }
                }

                DownloadProgressChanged?.Invoke(null, new DownloadProgressChangedEventArgs
                {
                    Progress = new DownloadProgress
                    {
                        BytesReceived = asset.Size,
                        TotalBytes = asset.Size,
                        Status = "Concluido"
                    }
                });

                return targetPath;
            }
            catch (Exception ex)
            {
                DownloadProgressChanged?.Invoke(null, new DownloadProgressChangedEventArgs
                {
                    Progress = new DownloadProgress { Status = "Erro: " + ex.Message }
                });
                return null;
            }
        }

        private static GitHubAsset FindBestAsset(GitHubAsset[] assets)
        {
            if (assets == null || assets.Length == 0) return null;

            var preferred = assets.FirstOrDefault(a =>
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains("win-x64"));

            if (preferred != null) return preferred;

            preferred = assets.FirstOrDefault(a =>
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            return preferred ?? assets[0];
        }

        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "0.0.0";

            var v = version.Trim();
            if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                v = v.Substring(1);

            return v;
        }

        public static bool IsNewerVersion(string latest, string current)
        {
            try
            {
                var latestParts = latest.Split('-')[0].Split('.');
                var currentParts = current.Split('-')[0].Split('.');

                int majorL = latestParts.Length > 0 ? int.Parse(latestParts[0]) : 0;
                int minorL = latestParts.Length > 1 ? int.Parse(latestParts[1]) : 0;
                int patchL = latestParts.Length > 2 ? int.Parse(latestParts[2]) : 0;

                int majorC = currentParts.Length > 0 ? int.Parse(currentParts[0]) : 0;
                int minorC = currentParts.Length > 1 ? int.Parse(currentParts[1]) : 0;
                int patchC = currentParts.Length > 2 ? int.Parse(currentParts[2]) : 0;

                if (majorL != majorC) return majorL > majorC;
                if (minorL != minorC) return minorL > minorC;
                return patchL > patchC;
            }
            catch
            {
                return false;
            }
        }

        public static void OpenDownloadPage()
        {
            var url = !string.IsNullOrEmpty(CurrentUpdateInfo?.DownloadUrl)
                ? CurrentUpdateInfo.DownloadUrl
                : GitHubReleasesPage;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public static void LaunchInstaller(string installerPath)
        {
            if (!File.Exists(installerPath))
                throw new FileNotFoundException("Instalador nao encontrado.", installerPath);

            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(info);
        }
    }
}
