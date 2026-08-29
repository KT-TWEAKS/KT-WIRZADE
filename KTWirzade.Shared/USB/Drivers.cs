using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SharpSevenZip;
using System.Linq;
using System.Text.Json.Serialization;
using Core;
using KTWirzade.Shared;

namespace iso_mode
{
    public class DriverManager
    {
        public class Drivers
        {
            public Driver[] Graphics { get; set; } = null!;
            public Driver[] Network { get; set; } = null!;
        }

        public class Driver
        {
            public string DisplayName { get; set; } = null!;
            public string Url { get; set; } = null!;
            public string FileName { get; set; } = null!;
            public long FileSize { get; set; }
            public string SHA256Hash { get; set; } = null!;
            public string ExecutableName { get; set; } = null!;
            public string Arguments { get; set; } = null!;
            public string Version { get; set; } = null!;

            public string[] ExcludedPaths { get; set; } = null!;
            public string[] InfFiles { get; set; } = null!;
            public string[] HardwareIDs { get; set; } = null!;
            public Dictionary<string, string>? Headers { get; set; }

            public bool RebootRequired { get; set; }

            public string? ScrapeUrl { get; set; }
            public string? ScrapeRegex { get; set; }
        }

        public static async Task HandleDrivers(Action<double> onProgressChanged, IProgress<string> reporter, bool graphics, bool network, bool system, string jsonUrl, string cache, string targetFolder)
        {
            if (!Directory.Exists(cache))
                Directory.CreateDirectory(cache);
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);

            var remoteGraphicsDrivers = Array.Empty<Driver>();
            var remoteNetworkDrivers = Array.Empty<Driver>();
            Exception exception = null;
            try
            {
                if (graphics || network)
                {
                    using var httpClient = new HttpProgressClient();
                    httpClient.Client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");

                    var json = await (await httpClient.GetAsync(jsonUrl)).Content.ReadAsStringAsync();
                    var drivers = (Drivers)JsonSerializer.Deserialize(json, typeof(Drivers))!;
                    File.WriteAllText(Path.Combine(targetFolder, "drivers.json"), json);

                    var graphicsWeight = drivers.Graphics.Sum(x => x.FileSize);
                    var networkWeight = drivers.Network.Sum(x => x.FileSize);
                    var totalWeight = (graphics ? graphicsWeight : 0) + (network ? networkWeight : 0) + (system ? (((graphics ? graphicsWeight : 0) + (network ? networkWeight : 0)) / 10) : 0);

                    if (graphics)
                    {
                        remoteGraphicsDrivers = drivers.Graphics;
                        await HandleRemoteDriverList(graphicsProgress => onProgressChanged.Invoke(((graphicsWeight * (graphicsProgress / 100)) / totalWeight) * 100), reporter, drivers.Graphics, httpClient,
                            cache, targetFolder);
                    }
                    if (network)
                    {
                        remoteNetworkDrivers = drivers.Network;
                        await HandleRemoteDriverList(
                            networkProgress => onProgressChanged.Invoke((((networkWeight * (networkProgress / 100)) / totalWeight) * 100) + (graphics ? ((double)graphicsWeight / totalWeight) * 100 : 0)),
                            reporter, drivers.Network, httpClient, cache, targetFolder);
                    }
                }
            }
            catch (Exception e)
            {
                exception = e;
            }
            if (system)
            {
                reporter.Report("Copying PC Drivers");

                var systemDrivers = GetDriverPathsFromOEMInfs(); //.Where(x => 
                //!remoteGraphicsDrivers.Any(y => y.InfFiles.Any(z => Path.GetFileName(x).StartsWith(z, StringComparison.OrdinalIgnoreCase))) &&
                //!remoteNetworkDrivers.Any(y => y.InfFiles.Any(z => Path.GetFileName(x).StartsWith(z, StringComparison.OrdinalIgnoreCase)))).ToArray();

                var totalWeight = systemDrivers.Length;

                if (!Directory.Exists(Path.Combine(targetFolder, "UserDrivers")))
                    Directory.CreateDirectory(Path.Combine(targetFolder, "UserDrivers"));

                foreach (string systemDriver in systemDrivers)
                {
                    CopyDirectory(systemDriver, Path.Combine(targetFolder, "UserDrivers", Path.GetFileName(systemDriver)));
                    onProgressChanged.Invoke(graphics || network ? ((((Array.IndexOf(systemDrivers, systemDriver) / (double)totalWeight) * (1 - (90d / 100d))) * 100) + 90) :
                        (Array.IndexOf(systemDrivers, systemDriver) / (double)totalWeight) * 100);
                }
            }
            File.Create(Path.Combine(targetFolder, "pending.bool")).Dispose();

            if (exception != null)
                throw exception;
        }
        private static async Task HandleRemoteDriverList(Action<double> onProgressChanged, IProgress<string> reporter, Driver[] drivers, HttpProgressClient httpClient, string cache, string targetFolder)
        {
            var totalWeight = drivers.Sum(x => x.FileSize);
            var currentWeight = 0L;
            for (var i = 0; i < drivers.Length; i++)
            {
                try
                {
                    if (i <= 0 || drivers[i - 1].DisplayName != drivers[i].DisplayName)
                    {
                        reporter.Report($"Downloading {drivers[i].DisplayName + (i != drivers.Length - 1 && drivers[i + 1].DisplayName == drivers[i].DisplayName ? "s" : null)}");
                    }

                    var destination = Path.Combine(cache, drivers[i].FileName);
                    var extractDestination = Path.Combine(targetFolder, Path.GetFileNameWithoutExtension(destination));

                    if (drivers[i].Headers != null)
                    {
                        drivers[i].Headers!.ToList().ForEach(x => httpClient.Client.DefaultRequestHeaders.Add(x.Key, x.Value));
                    }

                    if (File.Exists(destination))
                    {
                        if (GetSHA256(destination) == drivers[i].SHA256Hash)
                        {
                            // Already downloaded
                        }
                        else
                        {
                            if (Directory.Exists(extractDestination))
                                Directory.Delete(extractDestination, true);

                            httpClient.ProgressChanged += (size, downloaded, percentage) =>
                            {
                                // ReSharper disable once AccessToModifiedClosure
                                onProgressChanged.Invoke(((currentWeight + downloaded) / (double)totalWeight) * 100);
                            };
                            await httpClient.StartDownload(drivers[i].Url, destination, drivers[i].FileSize);
                        }
                    }
                    else
                    {
                        await httpClient.StartDownload(drivers[i].Url, destination);
                        if (GetSHA256(destination) != drivers[i].SHA256Hash)
                            throw new Exception("Hash mismatch!");
                    }

                    if (drivers[i].Headers != null)
                    {
                        drivers[i].Headers!.ToList().ForEach(x => httpClient.Client.DefaultRequestHeaders.Remove(x.Key));
                    }
                    if (destination.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(destination, Path.Combine(targetFolder, Path.GetFileName(destination)));
                    }
                    else if (!Directory.Exists(extractDestination))
                    {
                        if (TryParseEnum(Path.GetExtension(destination).TrimStart('.'), out InArchiveFormat archiveFormat))
                            ExtractArchive(destination, extractDestination, (InArchiveFormat)archiveFormat);
                        else
                            throw new Exception("Unknown file type");

                        foreach (string excludedPath in drivers[i].ExcludedPaths ?? Array.Empty<string>())
                        {
                            if (File.Exists(Path.Combine(extractDestination, excludedPath)))
                                File.Delete(Path.Combine(extractDestination, excludedPath));
                            else if (Directory.Exists(Path.Combine(extractDestination, excludedPath)))
                                Directory.Delete(Path.Combine(extractDestination, excludedPath), true);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.EnqueueExceptionSafe(e);
                }
                currentWeight += drivers[i].FileSize;
                onProgressChanged.Invoke((currentWeight / (double)totalWeight) * 100);
            }
        }

        public static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct
        {
            result = default;
            if (string.IsNullOrEmpty(value)) return false;
            try
            {
                result = (TEnum)Enum.Parse(typeof(TEnum), value, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ExtractArchive(string filePath, string extractDestination, InArchiveFormat format)
        {
            SharpSevenZipBase.SetLibraryPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.dll"));
            using var extractor = new SharpSevenZipExtractor(filePath, format);
            extractor.ExtractArchive(extractDestination);
        }

        static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);

            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }

        private static string GetSHA256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var fileStream = File.OpenRead(filePath);
            var fileHash = sha256.ComputeHash(fileStream);
            return BitConverter.ToString(fileHash).Replace("-", "").ToUpperInvariant();
        }

        const int DI_FLAGSEX_INSTALLEDDRIVER = 0x04000000;
        public static string[] GetDriverPathsFromInstalled()
        {
            var paths = new List<string>();
            IntPtr devices = Win32.SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, (uint)(Win32.DiGetClassFlags.DIGCF_ALLCLASSES));
            if (devices == Win32.INVALID_HANDLE_VALUE)
                throw new Exception("SetupDiGetClassDevs failed with error: " + Marshal.GetLastWin32Error());

            Win32.SP_DEVINFO_DATA deviceInfoData = new Win32.SP_DEVINFO_DATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(Win32.SP_DEVINFO_DATA))
            };

            for (uint i = 0; Win32.SetupDiEnumDeviceInfo(devices, i, ref deviceInfoData); i++)
            {
                var installParams = new Win32.SP_DEVINSTALL_PARAMS();
                installParams.Initialize();
                if (!Win32.SetupDiGetDeviceInstallParams(devices, ref deviceInfoData, ref installParams))
                    continue;

                installParams.FlagsEx |= DI_FLAGSEX_INSTALLEDDRIVER;
                if (!Win32.SetupDiSetDeviceInstallParams(devices, ref deviceInfoData, ref installParams))
                    continue;

                if (Win32.SetupDiBuildDriverInfoList(devices, ref deviceInfoData, Win32.SPDIT.COMPATDRIVER))
                {
                    var driverInfoData = new Win32.SP_DRVINFO_DATA { cbSize = Marshal.SizeOf(typeof(Win32.SP_DRVINFO_DATA)) };

                    Win32.SetupDiEnumDriverInfo(devices, ref deviceInfoData, Win32.SPDIT.COMPATDRIVER, 0, ref driverInfoData);

                    for (int j = 0; Win32.SetupDiEnumDriverInfo(devices, ref deviceInfoData, Win32.SPDIT.COMPATDRIVER, (int)j, ref driverInfoData); j++)
                    {
                        Win32.SP_DRVINFO_DETAIL_DATA driverInfoDetailData = new Win32.SP_DRVINFO_DETAIL_DATA
                        {
                            cbSize = Marshal.SizeOf(typeof(Win32.SP_DRVINFO_DETAIL_DATA))
                        };

                        if (driverInfoData.ProviderName == "Microsoft")
                            continue;
                        if (Win32.SetupDiGetDriverInfoDetail(devices, ref deviceInfoData, ref driverInfoData, ref driverInfoDetailData, Marshal.SizeOf(driverInfoDetailData), out _) ||
                            Marshal.GetLastWin32Error() == 122)
                        {
                            if (!string.IsNullOrWhiteSpace(driverInfoDetailData.InfFileName))
                            {
                                if (!Win32.SetupGetInfDriverStoreLocation(driverInfoDetailData.InfFileName, 0, 0, null!, 0, out int RequiredSize))
                                {
                                    StringBuilder returnBuffer = new StringBuilder(RequiredSize);
                                    if (Win32.SetupGetInfDriverStoreLocation(driverInfoDetailData.InfFileName, 0, 0, returnBuffer, returnBuffer.Capacity, out RequiredSize))
                                    {
                                        var storeInf = returnBuffer.ToString();
                                        if (storeInf.IndexOf(@":\WINDOWS\System32\DriverStore\FileRepository\", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            var storeDir = Path.Combine(String.Join("\\", storeInf.Split('\\').Take(6)));
                                            paths.Add(storeDir);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Win32.SetupDiDestroyDeviceInfoList(devices);
            return paths.Distinct().ToArray();
        }

        public static string[] GetDriverPathsFromGlobal()
        {
            var paths = new List<string>();
            IntPtr devices = Win32.SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
            if (devices == Win32.INVALID_HANDLE_VALUE)
                throw new Exception("SetupDiCreateDeviceInfoList failed with error: " + Marshal.GetLastWin32Error());

            var deviceInfoData = IntPtr.Zero;
            if (Win32.SetupDiBuildDriverInfoList(devices, deviceInfoData, Win32.SPDIT.CLASSDRIVER))
            {
                var driverInfoData = new Win32.SP_DRVINFO_DATA { cbSize = Marshal.SizeOf(typeof(Win32.SP_DRVINFO_DATA)) };

                for (int j = 0; Win32.SetupDiEnumDriverInfo(devices, deviceInfoData, Win32.SPDIT.CLASSDRIVER, (int)j, ref driverInfoData); j++)
                {
                    Win32.SP_DRVINFO_DETAIL_DATA driverInfoDetailData = new Win32.SP_DRVINFO_DETAIL_DATA
                    {
                        cbSize = Marshal.SizeOf(typeof(Win32.SP_DRVINFO_DETAIL_DATA))
                    };

                    if (driverInfoData.ProviderName == "Microsoft")
                        continue;
                    if (Win32.SetupDiGetDriverInfoDetail(devices, deviceInfoData, ref driverInfoData, ref driverInfoDetailData, Marshal.SizeOf(driverInfoDetailData), out _) ||
                        Marshal.GetLastWin32Error() == 122)
                    {
                        if (!string.IsNullOrWhiteSpace(driverInfoDetailData.InfFileName) && driverInfoDetailData.InfFileName.Contains(@"INF\oem"))
                        {
                            if (!Win32.SetupGetInfDriverStoreLocation(driverInfoDetailData.InfFileName, 0, 0, null!, 0, out int RequiredSize))
                            {
                                StringBuilder returnBuffer = new StringBuilder(RequiredSize);
                                if (Win32.SetupGetInfDriverStoreLocation(driverInfoDetailData.InfFileName, 0, 0, returnBuffer, returnBuffer.Capacity, out RequiredSize))
                                {
                                    var storeInf = returnBuffer.ToString();
                                    if (storeInf.IndexOf(@":\WINDOWS\System32\DriverStore\FileRepository\", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        var storeDir = Path.Combine(String.Join("\\", storeInf.Split('\\').Take(6)));
                                        paths.Add(storeDir);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Win32.SetupDiDestroyDeviceInfoList(devices);
            return paths.Distinct().ToArray();
        }

        public static string[] GetDriverPathsFromOEMInfs()
        {
            var paths = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, @"Windows\INF"), "*.inf"))
            {
                if (!Path.GetFileName(file).StartsWith("oem", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!Win32.SetupGetInfDriverStoreLocation(file, 0, 0, null!, 0, out int RequiredSize))
                {
                    StringBuilder returnBuffer = new StringBuilder(RequiredSize);
                    if (Win32.SetupGetInfDriverStoreLocation(file, 0, 0, returnBuffer, returnBuffer.Capacity, out RequiredSize))
                    {
                        var storeInf = returnBuffer.ToString();
                        if (storeInf.IndexOf(@":\WINDOWS\System32\DriverStore\FileRepository\", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var storeDir = Path.Combine(String.Join("\\", storeInf.Split('\\').Take(6)));
                            paths.Add(storeDir);
                        }
                    }
                }
            }

            return paths.Distinct().ToArray();
        }

        public class HttpProgressClient : IDisposable
        {
            private string _downloadUrl;
            private string _destinationFilePath;

            public HttpClient Client;

            public delegate void ProgressChangedHandler(long? totalFileSize, long totalBytesDownloaded,
                double? progressPercentage);

            public event ProgressChangedHandler ProgressChanged;

            public HttpProgressClient()
            {
                Client = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
            }

            public async Task StartDownload(string downloadUrl, string destinationFilePath, long? size = null)
            {
                try
                {
                    _downloadUrl = downloadUrl;
                    _destinationFilePath = destinationFilePath;

                    using (var response = await Client.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                        await DownloadFileFromHttpResponseMessage(response, size);
                }
                finally
                {
                    ProgressChanged = null;
                }
            }

            public Task<HttpResponseMessage> GetAsync(string link)
            {
                return Client.GetAsync(link);
            }

            private async Task DownloadFileFromHttpResponseMessage(HttpResponseMessage response, long? size)
            {
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != 0)
                    size = response.Content.Headers.ContentLength;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                    await ProcessContentStream(size, contentStream);
            }

            private async Task ProcessContentStream(long? totalDownloadSize, Stream contentStream)
            {
                var totalBytesRead = 0L;
                var readCount = 0L;
                var buffer = new byte[8192];
                var isMoreToRead = true;

                using (var fileStream = new FileStream(_destinationFilePath, FileMode.Create, FileAccess.Write,
                           FileShare.None, 8192, true))
                {
                    do
                    {
                        var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            isMoreToRead = false;
                            TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                            continue;
                        }

                        await fileStream.WriteAsync(buffer, 0, bytesRead);

                        totalBytesRead += bytesRead;
                        readCount += 1;

                        if (readCount % 50 == 0)
                            TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                    } while (isMoreToRead);
                }
            }

            private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead)
            {
                if (ProgressChanged == null)
                    return;

                double? progressPercentage = null;
                if (totalDownloadSize.HasValue)
                {
                    progressPercentage = Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 2);
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
