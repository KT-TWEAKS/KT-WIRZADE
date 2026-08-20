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
using KTWirzade.Shared.Tasks;
using YamlDotNet.Serialization;

namespace KTWirzade.Shared.Actions
{
    
    public class SoftwareAction : TaskActionWithOutputProcessor, ITaskAction
    {
        public void RunTaskOnMainThread(Output.OutputWriter output) { throw new NotImplementedException(); }
        
        public enum SoftwareSource
        {
            Chocolatey = 0,
        }

        public class SoftwareFallback
        {
            [YamlMember(typeof(string), Alias = "name")]
            public string? Name { get; set; } = null;
            [YamlMember(typeof(SoftwareSource), Alias = "source")]
            public SoftwareSource Source { get; set; } = SoftwareSource.Chocolatey;
        }

        [YamlMember(typeof(string), Alias = "weight")]
        public int ProgressWeight { get; set; } = 50;
        public int GetProgressWeight() => ProgressWeight;
        public ErrorAction GetDefaultErrorAction() => Tasks.ErrorAction.Notify;
        public bool GetRetryAllowed() => false;

        public void ResetProgress() {}

        [YamlMember(typeof(ISOSetting), Alias = "iso")]
        public override ISOSetting ISO { get; set; } = ISOSetting.True;
        [YamlMember(typeof(OOBESetting?), Alias = "oobe")]
        public override OOBESetting? OOBE { get; set; } = OOBESetting.True;

        [YamlMember(typeof(string), Alias = "package")]
        public string Package { get; set; } = null;
        
        [YamlMember(typeof(string), Alias = "name")]
        public string Name { get; set; } = null;
        
       // [YamlMember(typeof(string), Alias = "cache")]
       // public string Cache { get; set; } = null;
        
        [YamlMember(typeof(bool), Alias = "upgrade")]
        public bool Upgrade { get; set; } = true;
        
        [YamlMember(typeof(SoftwareFallback), Alias = "fallback")]
        public SoftwareFallback Fallback { get; set; } = null;
        [YamlMember(typeof(SoftwareFallback[]), Alias = "fallbacks")]
        public SoftwareFallback[] Fallbacks { get; set; } = null;
        
        [YamlMember(typeof(SoftwareSource), Alias = "source")]
        public SoftwareSource Source { get; set; } = SoftwareSource.Chocolatey;
        public string ErrorString() => $"SoftwareAction failed to install '{Name}'.";

        public UninstallTaskStatus GetStatus(Output.OutputWriter output)
        {
            return HasFinished ? UninstallTaskStatus.Completed : UninstallTaskStatus.ToDo;
            //return GetPackage() == null ? UninstallTaskStatus.Completed : UninstallTaskStatus.ToDo;
        }
        private bool HasFinished = false;

        public override string? IsISOCompatible() => ISO == ISOSetting.Only ? "SoftwareAction does not support 'iso: only'. Use 'iso: false' instead to only run it during the OOBE. Otherwise keep the default of 'iso: true' to cache package during ISO mastering, and install it from the cache during OOBE." : null;
        public async Task<bool> RunTask(Output.OutputWriter output)
        {
            if (AmeliorationUtil.ISO)
            {
                await new WriteStatusAction() { Status = $"Downloading {Name}" }.RunTask(output);
            }
            
            if (AmeliorationUtil.ISO && !Directory.Exists(@$"{AmeliorationUtil.WimPath}\ProgramData\KTWirzade\OOBE\Software")) 
                Directory.CreateDirectory(@$"{AmeliorationUtil.WimPath}\ProgramData\KTWirzade\OOBE\Software");
            
            /*
            if (Source == SoftwareSource.Executable)
            {
                if (AmeliorationUtil.ISO)
                {
                    if (!Directory.Exists(@$"{AmeliorationUtil.WimPath}\ProgramData\KTWirzade\OOBE\Software"))
                        Directory.CreateDirectory(@$"{AmeliorationUtil.WimPath}\ProgramData\KTWirzade\OOBE\Software");
                    File.Copy(Path.Combine(AmeliorationUtil.Playbook.Path, "Executables", Name), @$"{AmeliorationUtil.WimPath}\ProgramData\KTWirzade\OOBE\Software\" + Name);
                    HasFinished = true;
                    return true;
                }
                var action = new RunAction()
                {
                    Exe = Name,
                    Arguments = Arguments,
                };
                action.RunTaskOnMainThread(output);
                HasFinished = true;
                return true;
            }
            */
            
            if (AmeliorationUtil.ISO)
            {
                if (!File.Exists(@$"{AmeliorationUtil.WimPath}\ProgramData\chocolatey\bin\choco.exe"))
                    await InstallISO(output);
                await InstallToCache(output, Name);
                HasFinished = true;
                return true;
            }
            
            if (!File.Exists(Environment.ExpandEnvironmentVariables(@$"%PROGRAMDATA%\chocolatey\bin\choco.exe")))
                await Install(output);
            
            try
            {
                await RunChoco(output, $"install -y --allow-empty-checksums \"{Name}\"{(null == null ? null : $" --source=\"'{Source}'\"")}");
            }
            catch (Exception e)
            {
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(1000 * i);
                    
                    try
                    {
                        await RunChoco(output, $"install -y --allow-empty-checksums \"{Name}\"");
                        break;
                    }
                    catch (Exception exception)
                    {
                        Log.EnqueueExceptionSafe(exception);
                    }
                }
            }
            try
            {
                if (Upgrade)
                    await RunChoco(output, $"upgrade -y --allow-empty-checksums \"{Name}\"");
            }
            catch (Exception e)
            {
                Log.EnqueueExceptionSafe(e);
            }

            HasFinished = true;
            return true;
        }

        private async Task InstallToCache(Output.OutputWriter output, string name, [CanBeNull] string version = null)
        {
            using var httpClient = new HttpProgressClient();
            httpClient.Client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/7.55.1"); //Required for GitHub

            var queryString = Uri.EscapeUriString($"((Id eq '{name}') and (not IsPrerelease)) and IsLatestVersion");
            var queryUrl = @$"https://community.chocolatey.org/api/v2/Packages?$filter={queryString}";

            output.WriteLineSafe("Info", $"Querying '{queryUrl}'...");
            
            string downloadUrl = null;
            XNamespace ns = "http://www.w3.org/2005/Atom";
            try
            {
                string xml;
                for (int i = 0; true; i++)
                {
                    await Task.Delay(1000 * i);
                    try
                    {
                        using (var response = await httpClient.GetAsync(queryUrl))
                        {
                            response.EnsureSuccessStatusCode();

                            xml = await response.Content.ReadAsStringAsync();
                        }
                        break;
                    }
                    catch (Exception e)
                    {
                        if (i == 3)
                            throw;
                    }
                }

                downloadUrl = XDocument.Parse(xml!).Root!.Element(ns + "entry")!.Element(ns + "content")!.Attribute("src")!.Value;
            }
            catch (Exception e)
            {
                output.WriteLineSafe("Info", $"Package page not found, trying search...");
                
                // In rare cases, the name passed from a dependency does not match the case, so we use search instead which is case insensitive
                queryUrl =
                    $"https://community.chocolatey.org/api/v2/Search()?$filter=IsLatestVersion&$skip=0&$top=1&searchTerm=%27{Uri.EscapeUriString(name)}%27&targetFramework=%27%27&includePrerelease=false";

                output.WriteLineSafe("Info", $"Querying '{queryUrl}'...");
                
                string xml;
                for (int i = 0; true; i++)
                {
                    await Task.Delay(1000 * i);
                    try
                    {
                        var response = await httpClient.GetAsync(queryUrl);
                        response.EnsureSuccessStatusCode();
                        
                        xml = await response.Content.ReadAsStringAsync();
                        response.Dispose();
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (i == 3)
                            throw;
                    }
                }
                
                downloadUrl = XDocument.Parse(xml).Root!.Element(ns + "entry")!.Element(ns + "content")!.Attribute("src")!.Value;

                var realName = downloadUrl.Split('/')[downloadUrl.Split('/').Length - 2];
                if (!string.Equals(realName, name, StringComparison.OrdinalIgnoreCase))
                    throw;
                name = realName;
                
                output.WriteLineSafe("Info", "Package found via search");
            }
            if (version == null)
                version = downloadUrl.Split('/').Last();
            else
                downloadUrl = downloadUrl.Replace(downloadUrl.Split('/').Last(), version);
            
            output.WriteLineSafe("Info", $"Downloading package from {downloadUrl}...");
            
            var downloadDir = System.IO.Path.Combine(AmeliorationUtil.WimPath, "ProgramData\\KTWirzade\\OOBE\\Software");
            Directory.CreateDirectory(downloadDir);
            var downloadPath = System.IO.Path.Combine(downloadDir, $"{name}.{version}.nupkg");
            if (File.Exists(downloadPath))
                return;

            try
            {
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    httpClient.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) => {
                        if (progressPercentage.HasValue)
                            Console.WriteLine(progressPercentage.Value);
                    };
                
                    await httpClient.StartDownload(downloadUrl, downloadPath, 300000);
                }

                var extractDir = Path.Combine(downloadDir, Path.GetFileNameWithoutExtension(downloadPath));
                Directory.CreateDirectory(extractDir);
                RunCommand("7za.exe", $"x \"{downloadPath}\" \"{name}.nuspec\" \"tools/*.ps1\" -o\"{extractDir}\" -y");
                try
                {
                    var toolsDir = Path.Combine(extractDir, "tools");
                    Directory.CreateDirectory(toolsDir);

                    bool installCommandFound = false;
                    string extension = "exe";
                    foreach (var script in Directory.GetFiles(toolsDir, "*.ps1").Where(x => !x.EndsWith("chocolateyUninstall.ps1") && !x.EndsWith("update.ps1")))
                    {
                        string[] lines = File.ReadAllLines(script);

                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].Contains("Install-ChocolateyPackage"))
                                installCommandFound = true;
                        
                            string pattern = @"^.*filetype[ ]*=[ ]*[""']([a-zA-Z]{3})[""'].*";
                            var match = Regex.Match(lines[i], pattern, RegexOptions.IgnoreCase);
                            if (match.Success)
                                extension = match.Groups[1].Value;
                            else
                            {
                                pattern = @"^.*Install-ChocolateyPackage[ ]+[^ ]+[ ]+[""']([a-zA-Z]{3})[""'].*";
                                match = Regex.Match(lines[i], pattern);
                                if (match.Success)
                                    extension = match.Groups[1].Value;
                            }
                        }
                
                    }

                    if (installCommandFound)
                    {
                        List<(string Url, string Path, string Hash)> files = new List<(string Url, string Path, string Hash)>();
                        bool urlReplaced = false;
                        foreach (var script in Directory.GetFiles(toolsDir, "*.ps1").Where(x => !x.EndsWith("chocolateyUninstall.ps1") && !x.EndsWith("update.ps1")))
                        {
                            string[] lines = File.ReadAllLines(script);

                            for (int i = 0; i < lines.Length; i++)
                            {
                                string pattern = @"(^.*(url64|64biturl|url64bit|url|url32|32biturl|url32bit)[ ]*=[ ]*)[""']([^'""]*)[""'](.*)";
                                var match = Regex.Match(lines[i], pattern, RegexOptions.IgnoreCase);
                                if (!match.Success)
                                    continue;

                                string prefix = match.Groups[1].Value;
                                string url = match.Groups[3].Value;
                                string suffix = "";
                                if (match.Groups.Count > 4)
                                    suffix = match.Groups[4].Value;

                                url = url.Replace("${locale}", "en-US");
                                if (url.Contains("${"))
                                {
                                    output.WriteLineSafe("Info", $"Unrecognized variable in url '{url}, skipping download");
                                    continue;
                                }

                                var matchingFile = files.FirstOrDefault(x => x.Url == url);

                                string destination = matchingFile.Path ?? Path.Combine(downloadDir, name + "_" + i + "." + extension);

                                if (matchingFile.Path == null)
                                {
                                    try
                                    {
                                        httpClient.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                                        {
                                            if (progressPercentage.HasValue)
                                                output.WriteLineSafe("Progress", $"Downloaded {StringUtils.HumanReadableBytes((ulong)totalBytesDownloaded)}");
                                        };

                                        var hash = await httpClient.StartDownload(url, destination, 300000);
                                        if (files.Any(x => x.Hash == hash))
                                        {
                                            try { File.Delete(destination); }
                                            catch (Exception e)
                                            {
                                            }
                                            destination = files.First(x => x.Hash == hash).Path;
                                        }
                                        files.Add((url, destination, hash));
                                    }
                                    catch (Exception e)
                                    {
                                        Log.EnqueueExceptionSafe(LogType.Warning, e);
                                        continue;
                                    }
                                }

                                urlReplaced = true;

                                string modifiedUrl = destination.Replace($@"{AmeliorationUtil.WimPath}\", @"C:\");
                                lines[i] = Regex.Replace(lines[i], pattern, _ => { return $"{prefix}'{modifiedUrl}'{suffix}"; }, RegexOptions.IgnoreCase);
                            }


                            File.WriteAllLines(script, lines);
                        }

                        if (urlReplaced)
                        {
                            foreach (var script in Directory.GetFiles(toolsDir, "*.ps1").Where(x => !x.EndsWith("chocolateyUninstall.ps1") && !x.EndsWith("update.ps1")))
                            {
                                string text = File.ReadAllText(script).Replace("Install-ChocolateyPackage", "Install-ChocolateyPackage -UseOriginalLocation");
                                File.WriteAllText(script, text);
                            }
                        }

                        RunCommand("7za.exe", $"a \"{downloadPath}\" \"{extractDir}\\*.ps1\" -r -y");
                    }
                    
                    await InstallDependencies(output, File.ReadAllText(Path.Combine(extractDir, $"{name}.nuspec")), name);
                }
                finally
                {
                    try
                    {
                        Directory.Delete(extractDir, true);
                    }
                    catch (Exception e)
                    {
                        Log.EnqueueExceptionSafe(LogType.Warning, e);
                    }
                }
            }
            catch (Exception e)
            {
                try
                {
                    File.Delete(downloadPath);
                }
                catch (Exception exception)
                {
                    Log.EnqueueExceptionSafe(LogType.Warning, exception);
                }

                throw;
            }

            try
            {
                var imageQuery = Uri.EscapeUriString($"{name}.{version}.svg");
                await httpClient.StartDownload($"https://community.chocolatey.org/content/packageimages/{imageQuery}", Path.Combine(downloadDir, $"{name}.svg"));
            }
            catch (Exception e)
            {
                //Console.WriteLine(e);
            }
        }
        

        private async Task InstallDependencies(Output.OutputWriter output, string xml, string parent)
        {
            var dependencies = XDocument.Parse(xml).Root!.Elements().First(x => x.Name.LocalName == "metadata").Elements().FirstOrDefault(x => x.Name.LocalName == "dependencies");
            if (dependencies == null)
                return;
            foreach (var dep in dependencies.Elements().Where(x => x.Name.LocalName == "dependency"))
            {
                string id = (string)dep.Attribute("id");
                string version = (string)dep.Attribute("version");

                
                output.WriteLineSafe("Info", $"Installing dependency '{id + (version == null ? "" : "." + version)}' for '{parent}'");
                if (version != null && (version.StartsWith("[") && version.EndsWith("]")))
                    await InstallToCache(output, id, version.Trim('[', ']'));
                else
                    await InstallToCache(output, id);
            }
        }


        private static async Task RunChoco(Output.OutputWriter output, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Normal,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                FileName = Environment.ExpandEnvironmentVariables(@$"%PROGRAMDATA%\chocolatey\bin\choco.exe"),
                Arguments = arguments,
            };
            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            using (var handler = new OutputHandler("Process", process, output))
            {
                handler.StartProcess();

                bool exited = process.WaitForExit(30000);

                // WaitForExit alone seems to not be entirely reliable
                while (!exited && ExeRunning(process.ProcessName, process.Id))
                {
                    exited = process.WaitForExit(30000);
                }
            }
            if (process.ExitCode != 0)
                throw new Exception($"Chocolatey exited with code {process.ExitCode}");
        }
        
        private static async Task InstallISO(Output.OutputWriter output)
        {
            string installDir = @$"{AmeliorationUtil.WimPath}\ProgramData\chocolatey";
            
            var userChocoCache = Environment.GetEnvironmentVariable("ChocolateyInstall", EnvironmentVariableTarget.User);
            var systemChocoCache = Environment.GetEnvironmentVariable("ChocolateyInstall", EnvironmentVariableTarget.Machine);
            var processChocoCache = Environment.GetEnvironmentVariable("ChocolateyInstall", EnvironmentVariableTarget.Process);

            try
            {
                Environment.SetEnvironmentVariable("ChocolateyInstall", null, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable("ChocolateyInstall", installDir, EnvironmentVariableTarget.Machine);
                Environment.SetEnvironmentVariable("ChocolateyInstall", installDir, EnvironmentVariableTarget.Process);

                await Install(output);
                
                foreach (var rootKeyRaw in Registry.Users.GetSubKeyNames().Where(x => x.StartsWith("HKLM-SYSTEM-" + AmeliorationUtil.ISOGuid)).Select(x => Registry.Users.OpenSubKey(x, true)))
                {
                    using var rootKey = rootKeyRaw;
                    using var key = rootKey!.OpenSubKey(@"ControlSet001\Control\Session Manager\Environment", true);
                    var path = key!.GetValue("Path") as string;
                    key!.SetValue("Path", path!.TrimEnd(';') + @$";C:\ProgramData\chocolatey\bin", RegistryValueKind.String);
                    key!.SetValue("ChocolateyInstall", @"C:\ProgramData\chocolatey", RegistryValueKind.String);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("ChocolateyInstall", userChocoCache, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable("ChocolateyInstall", systemChocoCache, EnvironmentVariableTarget.Machine);
                Environment.SetEnvironmentVariable("ChocolateyInstall", processChocoCache, EnvironmentVariableTarget.Process);
                
                string pathToRemove = installDir + "\\bin";

                string currentPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine);
                if (currentPath != null)
                {
                    var paths = currentPath.Split(';')
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToList();

                    if (paths.Contains(pathToRemove))
                    {
                        paths.Remove(pathToRemove);
                        string newPath = string.Join(";", paths);
                        Environment.SetEnvironmentVariable("Path", newPath, EnvironmentVariableTarget.Machine);
                    }
                    else
                    {
                        Log.EnqueueSafe(LogType.Warning, "Path not found in system PATH.", null);
                    }
                }
                else
                {
                    Log.EnqueueSafe(LogType.Warning, "Failed to retrieve the system PATH variable.", null);
                }
            }
        }
        
        private static async Task Install(Output.OutputWriter output)
        {
            var env = Environment.GetEnvironmentVariable("ChocolateyInstall");
            if (!string.IsNullOrEmpty(env))
                output.WriteLineSafe("Info", $"Installing Chocolatey to '{env}'...");
            else
                output.WriteLineSafe("Info", $"Installing Chocolatey...");

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var httpClient = new HttpProgressClient())
            {
                httpClient.Client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/7.55.1");

                var queryString = Uri.EscapeUriString("((Id eq 'chocolatey') and (not IsPrerelease)) and IsLatestVersion");
                var queryUrl = @$"https://community.chocolatey.org/api/v2/Packages?$filter={queryString}";

                var xml = await Wrap.Retry().Execute(async () =>
                {
                    var response = await httpClient.GetAsync(queryUrl);
                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadAsStringAsync();
                });

                string downloadUrl = XDocument.Parse(xml).Root!.Elements().First(x => x.Name.LocalName == "entry").Elements().First(x => x.Name.LocalName == "content").Attribute("src")!.Value;
                var version = downloadUrl.Split('/').Last();
                
                var downloadPath = Path.GetTempFileName();

                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    httpClient.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                    {
                        if (progressPercentage.HasValue)
                            output.WriteLineSafe("Progress", $"Downloaded {StringUtils.HumanReadableBytes((ulong)totalBytesDownloaded)}");
                    };

                    await httpClient.StartDownload(downloadUrl, downloadPath, 300000);
                }

                var temp = Path.GetTempPath() + "\\KTWirzade-CHOCO-" + new Random().Next(10000, 700000);
                ExtractArchive(downloadPath, temp);

                var startInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Normal,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    FileName = "powershell.exe",
                    Arguments = $"-NoP -ExecutionPolicy Bypass -File \"{Path.Combine(temp, "tools\\chocolateyInstall.ps1")}\"",
                };
                using var process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                using (var handler = new OutputHandler("Process", process, output))
                {
                    handler.StartProcess();

                    bool exited = process.WaitForExit(30000);

                    // WaitForExit alone seems to not be entirely reliable
                    while (!exited && ExeRunning(process.ProcessName, process.Id))
                    {
                        exited = process.WaitForExit(30000);
                    }
                }

                try
                {
                    File.Delete(downloadPath);
                    Directory.Delete(temp, true);
                }
                catch (Exception e)
                {
                    Log.EnqueueExceptionSafe(e);
                }
            }
        }
        
        
        public static void ExtractArchive(string file, string targetDir)
        {
            string sevenZipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7za.exe");
            if (!File.Exists(sevenZipPath))
            {
                sevenZipPath = "7za.exe";
            }
            RunCommand(sevenZipPath, $"x \"{file}\" -o\"{targetDir}\" -y -aos");
        }
        private static void RunCommand(string exe, string command, bool printOutput = false)
        {
            using var proc = new Process();
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Normal,
                Arguments = command,
                FileName = exe,
                RedirectStandardError = true,
                RedirectStandardOutput = printOutput
            };

            proc.StartInfo = startInfo;

            try
            {
                if (!proc.Start())
                {
                    throw new InvalidOperationException($"Failed to start process '{exe}'");
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException($"Could not find executable '{exe}'. Make sure it is in the same directory as KTWirzade.CLI.exe.", ex);
            }
            StringBuilder errorOutput = new StringBuilder("");

            proc.ErrorDataReceived += (sender, args) => { errorOutput.Append("\r\n" + args.Data); };
            proc.BeginErrorReadLine();

            if (printOutput)
            {
                proc.OutputDataReceived += (sender, args) => { Console.WriteLine(args.Data); };
                proc.BeginOutputReadLine();
            }

            proc.WaitForExit();

            proc.CancelErrorRead();

            // TODO:
            //if (proc.ExitCode == 1)
            //    Log.EnqueueSafe(LogType.Error, "Warning while running 7zip: " + errorOutput.ToString(), null, ("Command", command));
            if (proc.ExitCode > 1 || (!exe.EndsWith("7za.exe") && proc.ExitCode != 0))
                throw new ArgumentOutOfRangeException($"Error running '{exe.Split('\\').Last()}' ({proc.ExitCode}): " + errorOutput.ToString());
        }
        
    public class HttpProgressClient : IDisposable
    {
        private string _downloadUrl;
        private string _destinationFilePath;

        public HttpClient Client;

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
                    if (response.StatusCode == HttpStatusCode.ServiceUnavailable) {
                        Log.WriteSafe(LogType.Warning, "Received 503 service outage error, retrying...", null);
                        continue;
                    }
                    if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                    {
                        Log.WriteSafe(LogType.Warning, $"Received {response.StatusCode} response error, retrying...", null);
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
            var totalBytesRead = 0L;
            var readCount = 0L;
            var buffer = new byte[8192];
            var isMoreToRead = true;

            using (var md5 = MD5.Create())
            {
                using (var fileStream = new FileStream(_destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
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
                        md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);

                        await fileStream.WriteAsync(buffer, 0, bytesRead);

                        totalBytesRead += bytesRead;
                        readCount += 1;

                        if (readCount % 50 == 0)
                            TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                    } while (isMoreToRead);
                     
                }
            
                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(md5.Hash).Replace("-", "").ToUpper();
            }
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
