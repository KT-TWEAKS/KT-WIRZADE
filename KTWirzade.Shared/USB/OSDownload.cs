using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Core;
using Downloader;
using Microsoft.Win32;
using TimeZoneConverter;

namespace iso_mode
{
    public class OSDownload
    {
        public static string HumanReadableBytes(double input)
        {
            double size = input;
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };

            if (size == 0)
                return "0 MB";

            int order = 0;
            while (size >= 1024 && order < suffixes.Length - 1)
            {
                order++;
                size = size / 1024;
            }

            double roundedAndFormattedSize = Math.Round((double)size, 0);
            return $"{roundedAndFormattedSize} {suffixes[order]}";
        }

        private static readonly Dictionary<string, string> _languageDictionary = new Dictionary<string, string>
        {
            { "0401", "Arabic" },
            { "0416", "Brazilian Portuguese" },
            { "0402", "Bulgarian" },
            { "0804", "Chinese (Simplified)" },
            { "0404", "Chinese (Traditional)" },
            { "041A", "Croatian" },
            { "0405", "Czech" },
            { "0406", "Danish" },
            { "0413", "Dutch" },
            { "0409", "English (United States)" },
            { "0809", "English International" },
            { "0425", "Estonian" },
            { "040B", "Finnish" },
            { "040C", "French" },
            { "0C0C", "French Canadian" },
            { "0407", "German" },
            { "0408", "Greek" },
            { "040D", "Hebrew" },
            { "040E", "Hungarian" },
            { "0410", "Italian" },
            { "0411", "Japanese" },
            { "0412", "Korean" },
            { "0426", "Latvian" },
            { "0427", "Lithuanian" },
            { "0414", "Norwegian" },
            { "0415", "Polish" },
            { "0816", "Portuguese" },
            { "0418", "Romanian" },
            { "0419", "Russian" },
            { "081A", "Serbian Latin" },
            { "041B", "Slovak" },
            { "0424", "Slovenian" },
            { "0C0A", "Spanish" },
            { "080A", "Spanish (Mexico)" },
            { "041D", "Swedish" },
            { "041E", "Thai" },
            { "041F", "Turkish" },
            { "0422", "Ukrainian" }
        };

        public static string GetInstalledLanguageName()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Nls\Language"))
            {
                if (key != null)
                {
                    var installLanguage = key.GetValue("InstallLanguage") as string;
                    if (installLanguage != null && _languageDictionary.ContainsKey(installLanguage))
                    {
                        return _languageDictionary[installLanguage];
                    }
                }

                return "English (United States)";
            }
        }

        public enum OS
        {
            Windows,
            Ubuntu,
            Arch,
            SteamOS,
        }

        public static async Task<(string Link, string? Version, string? Hash)> GetDownloadLinkAsyncResilient(OS os)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i == 2)
                    return await GetOSDownloadLinkTask(os);
                else
                {
                    try
                    {
                        return await GetOSDownloadLinkTask(os);
                    }
                    catch (Exception e)
                    {
                        Log.WriteExceptionSafe(e);
                        if (e.Message == "Microsoft blocked the automated download request based on your IP address.")
                            throw;
                    }

                    Thread.Sleep(500);
                }
            }

            throw new Exception("Could not fetch download link.");
        }

        private static Task<(string Link, string? Version, string? Hash)> GetOSDownloadLinkTask(OS os) => os switch
        {
            OS.Windows => GetWindowsDownloadLinkAsync(),
            OS.Ubuntu => GetUbuntuDownloadLinkAsync(),
            OS.Arch => GetArchDownloadLinkAsync(),
            OS.SteamOS => GetSteamOSDownloadLinkAsync(),
            _ => throw new Exception("Unknown OS")
        };

        public static async Task DownloadISOAsync(string isoDownloadLink, string filePath, string? hash, CancellationToken cancellationToken, Action<int, string> onProgressChanged)
        {
            using (var downloader = new DownloadService(new DownloadConfiguration()
                   {
                       Timeout = 5000,
                       MaxTryAgainOnFailover = 5,
                       RequestConfiguration = new RequestConfiguration()
                       {
                           UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36",
                       }
                   }))
            {
                int progress = 0;
                string speedString = "0 MB";
                downloader.DownloadProgressChanged += (sender, args) =>
                {
                    var newProgress = (int)Math.Round(args.ProgressPercentage);
                    var newSpeedString = HumanReadableBytes(args.BytesPerSecondSpeed);
                    if (newProgress != progress || newSpeedString != speedString)
                        onProgressChanged.Invoke(newProgress, newSpeedString);

                    progress = newProgress;
                    speedString = newSpeedString;
                };

                await downloader.DownloadFileTaskAsync(isoDownloadLink, filePath, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Wrap.ExecuteSafe(() => File.Delete(filePath), true);
                return;
            }
        }

        private static async Task<(string Link, string? Version, string? Hash)> GetWindowsDownloadLinkAsync()
        {
            string language = GetInstalledLanguageName();

            string isoDownloadLink;
            using var httpClient = new HttpClient()
            {
                Timeout = TimeSpan.FromMinutes(2)
            };

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
            string sessionId = Guid.NewGuid().ToString();
            string url = "https://www.microsoft.com/en-us/software-download/windows11";

            string html = await httpClient.GetStringAsync(url);
            var match = Regex.Match(html, @"<option value=""(\d+)"">Windows");
            if (!match.Success)
                throw new Exception("Could not find product edition ID");

            string productEditionId = match.Groups[1].Value;

            if (!long.TryParse(productEditionId, out _))
                throw new Exception("Product edition ID is not numeric");

            (await httpClient.GetAsync($"https://vlscppe.microsoft.com/tags?org_id=y6jn8c31&session_id={sessionId}")).EnsureSuccessStatusCode();

            string profile = "606624d44113";
            string skuUrl =
                $"https://www.microsoft.com/software-download-connector/api/getskuinformationbyproductedition?profile={profile}&ProductEditionId={productEditionId}&SKU=undefined&friendlyFileName=undefined&Locale=en-US&sessionID={sessionId}";

            var skuDoc = JsonDocument.Parse(await httpClient.GetStringAsync(skuUrl));

            var sku = skuDoc.RootElement.GetProperty("Skus").EnumerateArray()
                .FirstOrDefault(s => s.GetProperty("LocalizedLanguage").GetString() == language || s.GetProperty("Language").GetString() == language);

            if (sku.ValueKind == JsonValueKind.Undefined)
                throw new Exception($"Could not find SKU for language: {language}");

            string skuId = sku.GetProperty("Id").GetString()!;
            string downloadLinkUrl =
                $"https://www.microsoft.com/software-download-connector/api/GetProductDownloadLinksBySku?profile={profile}&productEditionId=undefined&SKU={skuId}&friendlyFileName=undefined&Locale=en-US&sessionID={sessionId}";

            var request = new HttpRequestMessage(HttpMethod.Get, downloadLinkUrl);
            request.Headers.Add("Referer", url);

            string? isoDownloadLinkJson = await (await httpClient.SendAsync(request)).EnsureSuccessStatusCode().Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(isoDownloadLinkJson))
                throw new Exception($"Automated download request failed.");
            if (isoDownloadLinkJson.Contains("Sentinel marked this request as rejected."))
                throw new Exception("Microsoft blocked the automated download request based on your IP address.");

            var downloadDoc = JsonDocument.Parse(isoDownloadLinkJson);

            var arch = Core.Win32.SystemInfoEx.SystemArchitecture;
            var isoOption = downloadDoc.RootElement.GetProperty("ProductDownloadOptions").EnumerateArray()
                .FirstOrDefault(o => o.GetProperty("Uri").GetString()!.Contains(arch.ToString().ToLowerInvariant()));
            if (isoOption.ValueKind == JsonValueKind.Undefined)
                throw new Exception("Could not find x64 ISO download link");

            isoDownloadLink = isoOption.GetProperty("Uri").GetString()!;
            if (string.IsNullOrEmpty(isoDownloadLink))
                throw new Exception($"Could not find download link. Please manually download the ISO from: {url}");

            string? version = null;
            var name = isoOption.GetProperty("ProductDisplayName").GetString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                var nameMatch = Regex.Match(name, @"^Windows [0-9]+ ([A-Z0-9]+)$");
                if (nameMatch.Success)
                    version = nameMatch.Groups[1].Value;
            }

            return (isoDownloadLink, version, null);
        }

        private static async Task<string> GetUbuntuGeoMirror(HttpClient httpClient, string version, string fileName)
        {
            var result = $"https://releases.ubuntu.com/{version}";
            
            var tz = Wrap.ExecuteSafe(() => TZConvert.WindowsToIana(TimeZoneInfo.Local.Id), true);
            if (string.IsNullOrWhiteSpace(tz.Value))
                return result;
            
            var countryJson = await Wrap.ExecuteSafeAsync(async token => await httpClient.GetStringAsync($"https://ubuntu.com/user-country-tz.json?tz={Uri.EscapeDataString(tz.Value)}"), logExceptions: true);
            var countryCode = Wrap.ExecuteSafe(() => JsonDocument.Parse(countryJson.Value).RootElement.GetProperty("country_code").GetString(), true);
            if (string.IsNullOrWhiteSpace(countryCode.Value))
                return result;
            
            var mirrorJson = await Wrap.ExecuteSafeAsync(async token => await httpClient.GetStringAsync($"https://ubuntu.com/mirrors.json?local=true&country_code={Uri.EscapeDataString(countryCode.Value)}"), logExceptions: true);
            var links = Wrap.ExecuteSafe(() => JsonDocument.Parse(mirrorJson.Value).RootElement.EnumerateArray().Select(e => e.GetProperty("link").GetString()?.TrimEnd('/')).ToList(), true);
            if (links.Value == null || !links.Value.Any())
                return result;
            
            var random = new Random();
            foreach (var link in links.Value.OrderBy(x => random.Next()).Take(3))
            {
                var response = await Wrap.ExecuteSafeAsync(async token => await httpClient.GetAsync($"{link}/{version}/{fileName}", HttpCompletionOption.ResponseHeadersRead));
                if (response.Failed || !response.Value.IsSuccessStatusCode)
                    continue;

                result = link;
                break;
            }
            
            return result;
        }
        
        public static async Task<(string Link, string Version, string Hash)> GetUbuntuDownloadLinkAsync()
        {
            using var httpClient = new HttpClient()
            {
                Timeout = TimeSpan.FromMinutes(1)
            };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");

            string html = await httpClient.GetStringAsync("https://changelogs.ubuntu.com/meta-release-lts");
       
            /*
            var matches = Regex.Matches(html, @"^Dist: ([a-zA-Z0-9\-_]+)$", RegexOptions.Multiline);
            if (matches.Count == 0)
            {
                Log.WriteSafe(LogType.Error, "Could not find ubuntu release info.", null, ("Data", html));
                throw new Exception("Could not find ubuntu release info.");
            }

            var release = matches[matches.Count - 1].Groups[1].Value;
            */

            var versionMatches = Regex.Matches(html, @"^Version: ([a-zA-Z0-9\-_\.]+ LTS)$", RegexOptions.Multiline);
            if (versionMatches.Count <= 0)
            {
                Log.WriteSafe(LogType.Error, "Could not find ubuntu release info.", null, ("Data", html));
                throw new Exception("Could not find ubuntu release info.");
            }
            var version = versionMatches[versionMatches.Count - 1].Groups[1].Value.Replace("LTS", string.Empty).Trim();

            string fileName = $"ubuntu-{version}-desktop-amd64.iso";
            var mirror = await GetUbuntuGeoMirror(httpClient, version, fileName);

            string? hash = null;

            await Wrap.ExecuteSafeAsync(async token =>
            {
                string fileSums = await httpClient.GetStringAsync($"{mirror}/{version}/SHA256SUMS");
                var targetSumString = fileSums.SplitByLine().FirstOrDefault();
                if (targetSumString == null)
                {
                    Log.WriteSafe(LogType.Error, "Could not find ubuntu hash info.", null, ("Data", fileSums));
                    throw new Exception("Could not find ubuntu hash info.");
                }

                foreach (string sumString in fileSums.SplitByLine())
                {
                    if (sumString.Contains($"*{fileName}"))
                    {
                        targetSumString = sumString;
                        break;
                    }
                }

                var split = targetSumString.Split(' ');
                if (split.Length != 2)
                {
                    Log.WriteSafe(LogType.Error, "Unexpected ubuntu hash info split.", null, ("Data", fileSums));
                    throw new Exception("Unexpected ubuntu hash info split.");
                }

                hash = split[0].Trim().ToUpperInvariant();
            }, logExceptions: true);

            return ($"{mirror}/{version}/" + fileName, version, hash);
        }

        public static async Task<(string Link, string? Version, string? Hash)> GetArchDownloadLinkAsync()
        {
            string? hash = null;
            try
            {
                using var httpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromMinutes(1)
                };
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");

                string fileSums = await httpClient.GetStringAsync($"https://geo.mirror.pkgbuild.com/iso/latest/sha256sums.txt");

                var match = Regex.Match(fileSums, @"^([a-zA-Z0-9]*)[ ]+archlinux-x86_64\.iso$", RegexOptions.Multiline);
                if (match.Success)
                    hash = match.Groups[1].Value.ToUpperInvariant();
                else
                {
                    Log.WriteSafe(LogType.Warning, "Could not find arch hash info.", null, ("Data", fileSums));
                }
            }
            catch (Exception e)
            {
                Log.EnqueueExceptionSafe(e);
            }

            return ("https://geo.mirror.pkgbuild.com/iso/latest/archlinux-x86_64.iso", null, hash);
        }

        public static async Task<(string Link, string? Version, string? Hash)> GetSteamOSDownloadLinkAsync() =>
            ("https://steamdeck-images.steamos.cloud/recovery/steamdeck-repair-latest.img.bz2", null, null);

        public static string GetSHA256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var fileStream = File.OpenRead(filePath);
            var fileHash = sha256.ComputeHash(fileStream);
            return BitConverter.ToString(fileHash).Replace("-", "").ToUpperInvariant();
        }
    }
}
