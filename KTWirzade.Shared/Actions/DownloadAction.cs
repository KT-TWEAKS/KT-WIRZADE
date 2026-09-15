using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Core;
using JetBrains.Annotations;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using KTWirzade.Shared.Tasks;
using YamlDotNet.Serialization;

namespace KTWirzade.Shared.Actions
{
    public class DownloadAction : TaskActionWithOutputProcessor, ITaskAction
    {
        public void RunTaskOnMainThread(Output.OutputWriter output) { throw new NotImplementedException(); }

        [YamlMember(typeof(string), Alias = "weight")]
        public int ProgressWeight { get; set; } = 150;
        public int GetProgressWeight() => ProgressWeight;
        public ErrorAction GetDefaultErrorAction() => Tasks.ErrorAction.Notify;
        public bool GetRetryAllowed() => false;

        public void ResetProgress() { }

        [YamlMember(typeof(ISOSetting), Alias = "iso")]
        public override ISOSetting ISO { get; set; } = ISOSetting.True;
        
        [YamlMember(typeof(string), Alias = "package")]
        public string Package { get; set; } = null;

        [YamlMember(typeof(string), Alias = "destination")]
        public string Destination { get; set; } = null;
        [YamlMember(typeof(bool), Alias = "overwrite")]
        public bool Overwrite { get; set; } = false;

        [YamlMember(typeof(string), Alias = "url")]
        public string Url { get; set; } = null;
        [YamlMember(typeof(string), Alias = "git")]
        public string Git { get; set; } = null;
        [YamlMember(typeof(string), Alias = "hash")]
        public string Hash { get; set; } = null;
        [YamlMember(typeof(string), Alias = "regex")]
        public string Regex { get; set; } = null;
        public string ErrorString() => $"DownloadAction failed to download '{Path.GetFileName(Destination)}'.";

        private static string ResolveDestinationPath(string allowedDirectory, string destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
                throw new ArgumentException("Destination must be specified on DownloadAction");

            var expanded = Environment.ExpandEnvironmentVariables(destination);
            var allowedBase = Path.GetFullPath(string.IsNullOrWhiteSpace(allowedDirectory) ? Directory.GetCurrentDirectory() : allowedDirectory);
            var fullPath = Path.IsPathRooted(expanded)
                ? Path.GetFullPath(expanded)
                : Path.GetFullPath(Path.Combine(allowedBase, expanded));

            var allowedRoot = allowedBase.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullRoot = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!string.Equals(fullPath, allowedBase, StringComparison.OrdinalIgnoreCase) &&
                !fullRoot.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Destination resolves outside the allowed playbook directory.");
            }

            return fullPath;
        }

        public UninstallTaskStatus GetStatus(Output.OutputWriter output)
        {
            return HasFinished ? UninstallTaskStatus.Completed : UninstallTaskStatus.ToDo;
            //return GetPackage() == null ? UninstallTaskStatus.Completed : UninstallTaskStatus.ToDo;
        }
        private bool HasFinished = false;

        public async Task<bool> RunTask(Output.OutputWriter output)
        {
            if (string.IsNullOrWhiteSpace(Git) && string.IsNullOrWhiteSpace(Url))
                throw new ArgumentException("Url or Git must be specified on DownloadAction");
            if (Git != null && Url != null)
                throw new ArgumentException("Cannot specify both Git and Url on DownloadAction");
            if (string.IsNullOrWhiteSpace(Destination))
                throw new ArgumentException("Destination must be specified on DownloadAction");
            if (Git != null && Regex == null)
                throw new ArgumentException("Regex must be specified with git on DownloadAction");
            if (Url != null && (!Uri.TryCreate(Url, UriKind.Absolute, out var urlUri) || (urlUri.Scheme != Uri.UriSchemeHttp && urlUri.Scheme != Uri.UriSchemeHttps)))
                throw new ArgumentException("Download URL must be a valid HTTP or HTTPS absolute URL.");
            if (Git != null && (!Uri.TryCreate(Git, UriKind.Absolute, out var gitUri) || (gitUri.Scheme != Uri.UriSchemeHttp && gitUri.Scheme != Uri.UriSchemeHttps)))
                throw new ArgumentException("Git URL must be a valid HTTP or HTTPS absolute URL.");

            Helpers.AtomicDownload.ValidateHash(Hash);
            var dir = AmeliorationUtil.ISO
                ? $@"{AmeliorationUtil.WimPath}\ProgramData\KTWirzade\OOBE\Playbook\Executables"
                : string.IsNullOrWhiteSpace(AmeliorationUtil.Playbook?.Path)
                    ? Directory.GetCurrentDirectory()
                    : Path.Combine(AmeliorationUtil.Playbook.Path, "Executables");

            Destination = Environment.ExpandEnvironmentVariables(Destination);
            var realDestination = ResolveDestinationPath(dir, Destination);
            if (!Directory.Exists(Path.GetDirectoryName(realDestination)))
                Directory.CreateDirectory(Path.GetDirectoryName(realDestination)!);

            if (AmeliorationUtil.ISO && Path.IsPathRooted(realDestination))
                realDestination = realDestination.Replace(Path.GetPathRoot(realDestination), Path.GetPathRoot(AmeliorationUtil.WimPath));
            
            if (File.Exists(realDestination))
            {
                if (!Overwrite)
                {
                    if (Hash != null && !Helpers.AtomicDownload.HashFile(realDestination).Equals(Hash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Existing download does not match its expected SHA-256.");
                    output.WriteLineSafe("Info", $"File '{Path.GetFileName(Destination)}' already exists, skipping download. Use 'overwrite: true' to overwrite.");
                    HasFinished = true;
                    return true;
                }
            }

            if (Url != null)
                await DownloadUrl(output, Url, realDestination);
            else if (Git != null)
                await DownloadGit(output, Git, realDestination);

            output.WriteLineSafe("Info", $"Downloaded file '{Path.GetFileName(Destination)}'");

            HasFinished = true;
            return true;
        }

        private async Task DownloadUrl(Output.OutputWriter output, string url, string destination)
        {
            output.WriteLineSafe("Info", $"Downloading file from '{url}'...");

            using var httpClient = new HttpProgressClient();
            httpClient.Client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/7.55.1");

            httpClient.ExpectedHash = Hash;
            await httpClient.StartDownload(url, destination);
        }
        private async Task DownloadGit(Output.OutputWriter output, string git, string destination)
        {
            if (Git.Contains("/download/"))
                throw new ArgumentException("DownloadAction Git link must not be a direct download link. Use url instead, or specify a regex with a base git url.");

            string releaseUrl = git.Contains("/releases/") ? git.EndsWith("/releases/") ? git + "latest" : git.EndsWith("/releases") ? git + "/latest" : git : git.TrimEnd('/') + "/releases/latest";
            string apiReleaseUrl = releaseUrl.Replace("://github.com/", "://api.github.com/repos/");
            
            output.WriteLineSafe("Info", $"Downloading file from '{apiReleaseUrl.TrimEnd('/')}' with filter '{Regex}'...");
            
            using (var httpClient = new HttpProgressClient())
            {
                string downloadUrl = null;
                long? size = null;

                try
                {
                    httpClient.Client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/7.55.1");

                    var response = await httpClient.GetAsync(apiReleaseUrl);
                    response.EnsureSuccessStatusCode();

                    var releasesContent = await response.Content.ReadAsStringAsync();
                    var release = JObject.Parse(releasesContent);

                    if (release?.SelectToken("assets") is JArray assets)
                    {
                        var asset = assets.FirstOrDefault(a => System.Text.RegularExpressions.Regex.IsMatch(a["name"].ToString(), Regex));
                        if (asset != null)
                        {
                            downloadUrl = asset["browser_download_url"]?.ToString();

                            if (asset["size"] != null)
                                { if (long.TryParse(asset["size"].ToString(), out var assetSize)) size = assetSize; }
                        }
                        else
                        {
                            throw new Exception($"No asset found matching regex '{Regex}', but found the following files:\r\n" + string.Join("\r\n", assets.Select(a => a["name"].ToString())));;
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("Failed to fetch git release info: " + e.Message, e);
                }

                if (downloadUrl == null)
                    throw new Exception("Download link unavailable.");

                // TODO: Add proper progress reporting and figure out how to reliably fetch the file size universally.
                httpClient.ExpectedHash = Hash;
                await httpClient.StartDownload(downloadUrl, destination, size);
            }
        }

        public class HttpProgressClient : IDisposable
        {
            private string _downloadUrl;
            private string _destinationFilePath;

            public HttpClient Client;
            public string ExpectedHash { get; set; }

            public delegate void ProgressChangedHandler(long? totalFileSize, long totalBytesDownloaded, double? progressPercentage);

            public event ProgressChangedHandler ProgressChanged;

            public HttpProgressClient()
            {
                Client = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
            }

            public async Task<string> StartDownload(string downloadUrl, string destinationFilePath, long? size = null)
            {
                _downloadUrl = downloadUrl;
                _destinationFilePath = destinationFilePath;

                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(1000 * i);
                    using (var response = await Client.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                            continue;
                        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                        {
                            i = 9;
                            continue;
                        }

                        return await DownloadFileFromHttpResponseMessage(response, size);
                    }
                }

                throw new Exception("Unexpected end of StartDownload.");
            }

            public Task<HttpResponseMessage> GetAsync(string link)
            {
                return Client.GetAsync(link);
            }

            private async Task<string> DownloadFileFromHttpResponseMessage(HttpResponseMessage response, long? size)
            {
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != 0)
                    size = response.Content.Headers.ContentLength;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                    return await ProcessContentStream(size, contentStream);
            }

            private async Task<string> ProcessContentStream(long? totalDownloadSize, Stream contentStream)
            {
                using (var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(10)))
                    return await Helpers.AtomicDownload.SaveAsync(contentStream, _destinationFilePath,
                        totalDownloadSize, ExpectedHash,
                        received => TriggerProgressChanged(totalDownloadSize, received), timeout.Token);
            }

            private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead)
            {
                if (ProgressChanged == null)
                    return;

                double? progressPercentage = null;
                if (totalDownloadSize.HasValue)
                {
                    progressPercentage = Math.Min(Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 2), 100);
                }


                ProgressChanged(totalDownloadSize, totalBytesRead, progressPercentage);
            }

            public void Dispose()
            {
                Client?.Dispose();
            }
        }
    }
}
