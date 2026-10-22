using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KTWirzade.Shared.Updates
{
    public class GitHubRelease
    {
        [JsonProperty("tag_name")]
        public string TagName { get; set; }
        public string Name { get; set; }
        public string Body { get; set; }
        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; }
        [JsonProperty("published_at")]
        public DateTime PublishedAt { get; set; }
        public bool Prerelease { get; set; }
        [JsonProperty("draft")]
        public bool Draft { get; set; }
        public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
    }

    public class GitHubAsset
    {
        public string Name { get; set; }
        [JsonProperty("browser_download_url")]
        public string BrowserDownloadUrl { get; set; }
        public long Size { get; set; }
        [JsonProperty("digest")]
        public string Digest { get; set; }
    }

    public class UpdateInfo
    {
        public bool UpdateAvailable { get; set; }
        // True when a newer release exists but the user asked to ignore this exact
        // version (UpdateAvailable is forced to false in that case).
        public bool Skipped { get; set; }
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
        // Prerelease channel: /releases/latest never returns prereleases, so the list endpoint is used instead.
        private const string GitHubReleasesApiUrl = "https://api.github.com/repos/KT-TWEAKS/KT-WIRZADE/releases?per_page=30";
        private const string GitHubReleasesPage = "https://github.com/KT-TWEAKS/KT-WIRZADE/releases";

        // The official release ships a checksum bundle (scripts/build-release.ps1).
        // It verifies the downloaded EXE when the GitHub API does not expose a
        // "digest" field for the asset (older releases, partial API responses).
        public const string ChecksumsFileName = "SHA256SUMS.txt";
        private const int MaxChecksumsFileSize = 256 * 1024;

        // Retry policy for transient check failures (network errors, timeouts, 5xx):
        // 3 attempts total with 1s/3s backoff. 404/403/429 are never retried.
        private const int MaxCheckAttempts = 3;
        private static readonly int[] RetryBackoffMs = { 1000, 3000 };

        // Shared client to avoid socket exhaustion from per-call HttpClient instances.
        // Built lazily and defensively on first use: if System.Net.Http fails to load or
        // initialize (assembly-binding surprises on net48), a throwing static constructor
        // would poison the whole type — .NET Framework caches that failure and every
        // later access rethrows TypeInitializationException. Lazy construction keeps the
        // type alive so failures surface as regular update errors instead of crashes.
        private static readonly object ClientLock = new object();
        private static HttpClient _client;

        private static HttpClient GetClient()
        {
            if (_client != null) return _client;
            lock (ClientLock)
            {
                if (_client != null) return _client;
                var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                try
                {
                    // Default headers are configured once: mutating DefaultRequestHeaders
                    // on a shared client per call would race between concurrent checks.
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("KT-WIRZADE-Updater/" + Globals.CurrentVersion);
                    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
                }
                catch (Exception)
                {
                    // Best-effort headers; a fallback request still works for
                    // github.com downloads where the User-Agent is not enforced.
                }
                _client = client;
                return client;
            }
        }

        public static UpdateInfo CurrentUpdateInfo { get; private set; }

        // Injection points for persisted GUI settings (Shared cannot reference GUI types).
        // The GUI sets both right before calling CheckForUpdatesAsync:
        //   UpdateChannel  - "stable" (default) or "prerelease"; selects /releases/latest vs /releases.
        //   SkippedVersion - version tag the user chose to ignore; see IsVersionSkipped().
        public static string UpdateChannel { get; set; } = "stable";
        public static string SkippedVersion { get; set; }

        public static event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

        /// <summary>
        /// Checks the configured channel for a newer release. Reads the static
        /// UpdateChannel/SkippedVersion settings injected by the GUI.
        /// Transient failures (network, timeout, 5xx) are retried up to 3 attempts
        /// (1s/3s backoff); 404/403 are reported immediately. Never throws.
        /// </summary>
        public static async Task<UpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            var info = new UpdateInfo
            {
                CurrentVersion = Globals.CurrentVersion
            };

            try
            {
                var (release, error) = await FetchLatestReleaseAsync(cancellationToken);
                if (!string.IsNullOrEmpty(error))
                {
                    info.Error = error;
                }
                else if (release != null)
                {
                    info.LatestVersion = NormalizeVersion(release.TagName);
                    info.ReleaseName = release.Name;
                    info.ReleaseNotes = release.Body;
                    info.DownloadUrl = release.HtmlUrl;
                    info.PublishedAt = release.PublishedAt;
                    info.IsPrerelease = release.Prerelease;
                    info.Assets = (release.Assets ?? Array.Empty<GitHubAsset>())
                        .Where(a => a != null && a.Name != null &&
                                    // Keep installer-like assets plus the checksums manifest
                                    // (ResolveHashWithChecksumsFallbackAsync searches this list).
                                    (a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                     a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(a.Name, ChecksumsFileName, StringComparison.OrdinalIgnoreCase)))
                        .ToArray();

                    info.UpdateAvailable = IsNewerVersion(info.LatestVersion, info.CurrentVersion);
                    if (info.UpdateAvailable && IsVersionSkipped(info.LatestVersion))
                    {
                        // The user explicitly ignores this exact version: report "no update"
                        // but keep Skipped so the UI can explain why.
                        info.UpdateAvailable = false;
                        info.Skipped = true;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation reaches here from the startup timeout too; phrase it
                // so a timeout does not read as if the user pressed cancel.
                info.Error = "Tempo esgotado ou verificacao cancelada.";
            }
            catch (Exception ex)
            {
                info.Error = $"Erro inesperado: {ex.Message}";
            }

            CurrentUpdateInfo = info;
            return info;
        }

        // Fetches the newest release honoring UpdateChannel: "stable" uses /releases/latest
        // (never returns prereleases; filtered again defensively), anything else uses the
        // /releases list and picks the newest non-draft entry (stable or prerelease).
        private static async Task<(GitHubRelease Release, string Error)> FetchLatestReleaseAsync(CancellationToken cancellationToken)
        {
            bool prereleaseChannel = string.Equals(UpdateChannel?.Trim(), "prerelease", StringComparison.OrdinalIgnoreCase);
            string url = prereleaseChannel ? GitHubReleasesApiUrl : GitHubApiUrl;
            string lastError = "Tempo esgotado ao verificar atualizacoes.";

            for (int attempt = 1; attempt <= MaxCheckAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                HttpResponseMessage response;
                try
                {
                    response = await GetClient().GetAsync(url, cancellationToken);
                }
                catch (HttpRequestException)
                {
                    lastError = "Nao foi possivel conectar ao GitHub. Verifique sua conexao com a internet.";
                    if (attempt >= MaxCheckAttempts) break;
                    await DelayBeforeRetryAsync(attempt, cancellationToken);
                    continue;
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested) throw;
                    lastError = "Tempo esgotado ao verificar atualizacoes.";
                    if (attempt >= MaxCheckAttempts) break;
                    await DelayBeforeRetryAsync(attempt, cancellationToken);
                    continue;
                }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        int statusCode = (int)response.StatusCode;
                        if (statusCode == 404)
                            return (null, "Nenhuma release encontrada no repositorio.");
                        // 403 is the classic unauthenticated rate limit, 429 an explicit
                        // throttle; both are final for this check (retrying would hammer).
                        if (statusCode == 403 || statusCode == 429)
                            return (null, "Limite de requisicoes do GitHub atingido. Tente novamente mais tarde.");

                        lastError = $"GitHub retornou erro {statusCode}.";
                        // Only 5xx is transient; every other 4xx is final.
                        if (statusCode < 500 || attempt >= MaxCheckAttempts) break;
                        await DelayBeforeRetryAsync(attempt, cancellationToken);
                        continue;
                    }

                    string json;
                    try
                    {
                        json = await response.Content.ReadAsStringAsync();
                    }
                    catch (HttpRequestException)
                    {
                        lastError = "Resposta vazia do GitHub.";
                        if (attempt >= MaxCheckAttempts) break;
                        await DelayBeforeRetryAsync(attempt, cancellationToken);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(json))
                        return (null, "Resposta vazia do GitHub.");

                    GitHubRelease release;
                    try
                    {
                        release = prereleaseChannel
                            ? SelectReleaseFromList(json)
                            : JsonConvert.DeserializeObject<GitHubRelease>(json);
                    }
                    catch (JsonException)
                    {
                        return (null, "Formato de resposta invalido do GitHub.");
                    }

                    if (release == null || string.IsNullOrEmpty(release.TagName))
                        return (null, prereleaseChannel
                            ? "Nenhuma release encontrada no repositorio."
                            : "Formato de resposta invalido do GitHub.");
                    // The stable channel must never surface a draft/prerelease entry.
                    if (!prereleaseChannel && (release.Draft || release.Prerelease))
                        return (null, "Nenhuma release encontrada no repositorio.");
                    return (release, null);
                }
            }

            return (null, lastError);
        }

        private static GitHubRelease SelectReleaseFromList(string json)
        {
            var releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(json);
            if (releases == null)
                return null;
            // /releases is ordered by creation date, not publication: a draft created
            // earlier and published later would shadow the real newest release.
            return releases
                .Where(r => r != null && !r.Draft && !string.IsNullOrEmpty(r.TagName))
                .OrderByDescending(r => r.PublishedAt)
                .FirstOrDefault();
        }

        private static Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken) =>
            Task.Delay(RetryBackoffMs[Math.Min(attempt - 1, RetryBackoffMs.Length - 1)], cancellationToken);

        public static async Task<string> DownloadLatestAsync(string targetPath = null, CancellationToken cancellationToken = default)
        {
            if (CurrentUpdateInfo == null || !CurrentUpdateInfo.UpdateAvailable)
                await CheckForUpdatesAsync(cancellationToken);
            var update = CurrentUpdateInfo;
            if (update == null || !update.UpdateAvailable) return null;
            var asset = FindBestAsset(update.Assets);
            if (asset == null) return null;
            // The assets list also carries SHA256SUMS.txt (hash fallback source).
            // If the release shipped no installer at all, FindBestAsset could only
            // return that manifest — never treat it as the update package.
            if (string.Equals(asset.Name, ChecksumsFileName, StringComparison.OrdinalIgnoreCase)) return null;
            try
            {
                ValidateAssetBasic(asset);
                string expectedHash;
                if (!string.IsNullOrEmpty(asset.Digest))
                {
                    // Strict path: the API published a digest. Enforce the full
                    // contract (sha256: + valid hex) exactly like before.
                    ValidateAsset(asset);
                    expectedHash = ParseSha256Digest(asset.Digest);
                }
                else
                {
                    // API omitted the digest (possible on older releases): fall back to
                    // the checksums bundle published with the release. Either way no
                    // byte is written to disk without a verified hash.
                    expectedHash = await ResolveHashWithChecksumsFallbackAsync(asset, update.Assets, cancellationToken);
                    if (string.IsNullOrEmpty(expectedHash))
                        throw new InvalidDataException("Nenhum hash SHA-256 publicado para esta atualizacao. Abra a pagina da release para baixar manualmente.");
                }
                if (string.IsNullOrEmpty(targetPath))
                    targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", asset.Name);
                using (var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    downloadTimeout.CancelAfter(TimeSpan.FromMinutes(10));
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("KT-WIRZADE-Updater/" + Globals.CurrentVersion);
                        using (var response = await client.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, downloadTimeout.Token))
                        {
                            response.EnsureSuccessStatusCode();
                            if (response.RequestMessage.RequestUri.Scheme != Uri.UriSchemeHttps)
                                throw new InvalidDataException("Update redirect must use HTTPS.");
                            using (var stream = await response.Content.ReadAsStreamAsync())
                                await Helpers.AtomicDownload.SaveAsync(stream, targetPath, asset.Size, expectedHash,
                                    received => ReportDownload(received, asset.Size, "Baixando..."), downloadTimeout.Token);
                        }
                    }
                }

                // SaveAsync already enforces both checks while streaming; re-verify the
                // file that actually landed on disk so nothing short-circuits silently.
                var written = new FileInfo(targetPath);
                if (asset.Size > 0 && written.Length != asset.Size)
                    throw new InvalidDataException($"Tamanho do arquivo baixado ({written.Length} bytes) difere do publicado ({asset.Size} bytes).");
                if (!string.IsNullOrEmpty(expectedHash))
                {
                    string actualHash = Helpers.AtomicDownload.HashFile(targetPath);
                    if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("SHA-256 do arquivo baixado diferente do publicado pelo GitHub.");
                }

                ReportDownload(asset.Size, asset.Size, "Concluído e verificado");
                return targetPath;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // User-initiated cancel: report it and rethrow so callers can tell
                // "cancelled" apart from a genuine download failure.
                ReportDownload(0, asset.Size, "Download cancelado");
                throw;
            }
            catch (Exception ex)
            {
                ReportDownload(0, asset.Size, "Erro: " + ex.Message);
                // The dialog is the only caller and renders ex.Message through its
                // DownloadFailed state; rethrowing keeps the real reason visible
                // instead of collapsing it into the generic fallback text.
                throw;
            }
        }

        private static string ParseSha256Digest(string digest)
        {
            if (string.IsNullOrEmpty(digest) ||
                !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                return null;
            return digest.Substring(7).Trim();
        }

        private static void ReportDownload(long received, long total, string status) =>
            DownloadProgressChanged?.Invoke(null, new DownloadProgressChangedEventArgs
            {
                Progress = new DownloadProgress { BytesReceived = received, TotalBytes = total, Status = status }
            });

        // Structural checks only: name, size, HTTPS URL on the official repository.
        // Applies to every asset we download from a release, including the checksum file.
        public static void ValidateAssetBasic(GitHubAsset asset)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.Name) || asset.Name != Path.GetFileName(asset.Name) ||
                asset.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || asset.Size <= 0)
                throw new InvalidDataException("Invalid update filename or size.");
            if (!Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com" || !uri.IsDefaultPort ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !uri.AbsolutePath.StartsWith("/KT-TWEAKS/KT-WIRZADE/releases/download/", StringComparison.Ordinal))
                throw new InvalidDataException("Update must come from the official GitHub repository.");
        }

        public static void ValidateAsset(GitHubAsset asset)
        {
            ValidateAssetBasic(asset);
            if (asset.Digest == null || !asset.Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("No SHA-256 published for this update. Use the release page.");
            Helpers.AtomicDownload.ValidateHash(asset.Digest.Substring(7));
        }

        /// <summary>
        /// Standard sha256sum format ("&lt;64 hex chars&gt;  &lt;filename&gt;"). Returns the hash
        /// for <paramref name="assetName"/>, trimmed of the "&lt;hash&gt; *name" binary marker,
        /// or null when no valid unambiguous entry matches. Fail-closed: any doubt returns null.
        /// </summary>
        public static string ResolveExpectedHashFromChecksums(string checksums, string assetName)
        {
            if (string.IsNullOrEmpty(checksums) || string.IsNullOrEmpty(assetName))
                return null;

            foreach (string rawLine in checksums.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r').Trim();
                if (line.Length == 0)
                    continue;

                int nameIndex = line.IndexOf(assetName, StringComparison.OrdinalIgnoreCase);
                if (nameIndex <= 0)
                    continue;

                // The name must be the last thing on the line: a longer file whose name
                // merely contains ours must not resolve to this hash.
                if (line.Substring(nameIndex + assetName.Length).Trim().Length != 0)
                    continue;

                string hash = line.Substring(0, nameIndex).Trim().TrimEnd('*').Trim();
                if (hash.Length != 64 || !hash.All(IsHexDigit))
                    return null;

                return hash;
            }

            return null;
        }

        private static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        /// <summary>
        /// Resolves the expected SHA-256 for <paramref name="package"/> through the release's
        /// SHA256SUMS.txt when the API did not publish a digest. Throws (fail-closed) when the
        /// checksum file exists but cannot be fetched/relied on; returns null when there is none.
        /// </summary>
        private static async Task<string> ResolveHashWithChecksumsFallbackAsync(GitHubAsset package, GitHubAsset[] assets, CancellationToken cancellationToken)
        {
            if (assets == null)
                return null;

            var checksumsAsset = assets.FirstOrDefault(a => a != null &&
                string.Equals(a.Name, ChecksumsFileName, StringComparison.OrdinalIgnoreCase) && a.Size > 0);
            if (checksumsAsset == null)
                return null;

            try
            {
                ValidateAssetBasic(checksumsAsset);
                using (var response = await GetClient().GetAsync(checksumsAsset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    response.EnsureSuccessStatusCode();
                    if (response.RequestMessage.RequestUri.Scheme != Uri.UriSchemeHttps)
                        throw new InvalidDataException("Checksum fallback download must use HTTPS.");

                    long declared = response.Content.Headers.ContentLength ?? -1;
                    if (declared > MaxChecksumsFileSize)
                        throw new InvalidDataException("SHA256SUMS.txt excede o tamanho permitido.");

                    var buffer = new MemoryStream();
                    var chunk = new byte[4096];
                    while (true)
                    {
                        int read = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken);
                        if (read == 0) break;
                        if (buffer.Length + read > MaxChecksumsFileSize)
                            throw new InvalidDataException("SHA256SUMS.txt excede o tamanho permitido.");
                        buffer.Write(chunk, 0, read);
                    }

                    string text = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
                    string hash = ResolveExpectedHashFromChecksums(text, package.Name);
                    if (string.IsNullOrEmpty(hash))
                        throw new InvalidDataException("SHA256SUMS.txt nao contem o hash de " + package.Name + ".");
                    return hash;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (ex is InvalidDataException)
                    throw;
                // Network or protocol failure on the fallback itself: never download without
                // a verifiable hash, and tell the user exactly which step failed.
                throw new InvalidDataException("Falha ao conferir o hash publicado (" + ChecksumsFileName + "): " + ex.Message, ex);
            }
        }

        private static GitHubAsset FindBestAsset(GitHubAsset[] assets)
        {
            if (assets == null || assets.Length == 0) return null;

            var preferred = assets.FirstOrDefault(a =>
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains("win-x64"));

            if (preferred != null) return preferred;

            preferred = assets.FirstOrDefault(a =>
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

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
            if (string.IsNullOrEmpty(latest) || string.IsNullOrEmpty(current))
                return false;

            try
            {
                // Compare the base numeric components element-wise, so "1.0.10" sorts
                // above "1.0.9", "1.0.2-beta" is not an update for "1.0.2" and extra
                // segments (1.0.2.1) still count. Unparseable segments fail closed (0).
                var latestParts = latest.Split('-')[0].Split('.');
                var currentParts = current.Split('-')[0].Split('.');

                int length = Math.Max(latestParts.Length, currentParts.Length);
                for (int i = 0; i < length; i++)
                {
                    int latestValue = NumericPart(latestParts, i);
                    int currentValue = NumericPart(currentParts, i);
                    if (latestValue != currentValue)
                        return latestValue > currentValue;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static int NumericPart(string[] parts, int index)
        {
            if (index >= parts.Length) return 0;
            return int.TryParse(parts[index], out int value) ? value : 0;
        }

        /// <summary>
        /// True when <paramref name="version"/> equals the version the user chose to
        /// ignore (SkippedVersion, injected by the GUI). Both sides are normalized so
        /// "v1.0.5" and "1.0.5" compare equal.
        /// </summary>
        public static bool IsVersionSkipped(string version)
        {
            string skipped = SkippedVersion;
            if (string.IsNullOrWhiteSpace(skipped) || string.IsNullOrWhiteSpace(version))
                return false;

            return string.Equals(NormalizeVersion(skipped), NormalizeVersion(version), StringComparison.OrdinalIgnoreCase);
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
