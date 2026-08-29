using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
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
                var request = (HttpWebRequest)WebRequest.Create(GitHubApiUrl);
                request.Timeout = 10000;
                request.ReadWriteTimeout = 10000;
                request.UserAgent = "KT-WIRZADE-Updater";
                request.Accept = "application/vnd.github.v3+json";

                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var json = await reader.ReadToEndAsync();
                    var release = JsonConvert.DeserializeObject<GitHubRelease>(json);

                    if (release == null)
                    {
                        info.Error = "Resposta inválida do GitHub.";
                        return info;
                    }

                    info.LatestVersion = NormalizeVersion(release.TagName);
                    info.ReleaseName = release.Name;
                    info.ReleaseNotes = release.Body;
                    info.DownloadUrl = release.HtmlUrl;
                    info.PublishedAt = release.PublishedAt;
                    info.IsPrerelease = release.Prerelease;
                    info.Assets = release.Assets ?? Array.Empty<GitHubAsset>();

                    info.UpdateAvailable = IsNewerVersion(info.LatestVersion, info.CurrentVersion);
                }
            }
            catch (WebException ex)
            {
                info.Error = $"Erro de conexão: {ex.Message}";
            }
            catch (Exception ex)
            {
                info.Error = $"Erro: {ex.Message}";
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
                var request = (HttpWebRequest)WebRequest.Create(asset.BrowserDownloadUrl);
                request.UserAgent = "KT-WIRZADE-Updater";
                request.Timeout = 30000;
                request.ReadWriteTimeout = 30000;

                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                using (var fileStream = File.Create(targetPath))
                {
                    var buffer = new byte[8192];
                    long total = response.ContentLength;
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

                DownloadProgressChanged?.Invoke(null, new DownloadProgressChangedEventArgs
                {
                    Progress = new DownloadProgress
                    {
                        BytesReceived = asset.Size,
                        TotalBytes = asset.Size,
                        Status = "Concluído"
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
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
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
                var latestVer = new Version(latest.Split('-')[0]);
                var currentVer = new Version(current.Split('-')[0]);
                return latestVer > currentVer;
            }
            catch
            {
                return false;
            }
        }

        public static void OpenDownloadPage()
        {
            if (CurrentUpdateInfo != null && !string.IsNullOrEmpty(CurrentUpdateInfo.DownloadUrl))
            {
                System.Diagnostics.Process.Start(CurrentUpdateInfo.DownloadUrl);
            }
            else
            {
                System.Diagnostics.Process.Start("https://github.com/KT-TWEAKS/KT-WIRZADE/releases");
            }
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
