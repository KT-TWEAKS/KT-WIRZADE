using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using Newtonsoft.Json;

namespace KTWirzade.Shared.Cache
{
    public class CachedPlaybook
    {
        public string Name { get; set; }
        public string SourcePath { get; set; }
        public string CachedPath { get; set; }
        public string Username { get; set; }
        public string Version { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public string Hash { get; set; }
        public long SizeBytes { get; set; }
    }

    public class PlaybookCacheIndex
    {
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public List<CachedPlaybook> Playbooks { get; set; } = new List<CachedPlaybook>();
    }

    public static class PlaybookCachePaths
    {
        public const string BaseDir = @"C:\ProgramData\AME\PlaybookCache";
        public const string IndexFile = "index.json";

        public static string GetCacheDir()
        {
            Directory.CreateDirectory(BaseDir);
            return BaseDir;
        }

        public static string GetIndexPath()
        {
            return Path.Combine(BaseDir, IndexFile);
        }

        public static string GetPlaybookCachePath(string name)
        {
            return Path.Combine(BaseDir, name + ".apbx");
        }
    }

    public static class PlaybookCacheManager
    {
        private static PlaybookCacheIndex _index;
        private static readonly object _lock = new object();

        public static PlaybookCacheIndex Index
        {
            get
            {
                if (_index == null)
                    LoadIndex();
                return _index;
            }
        }

        public static bool IsOnline { get; private set; }
        public static DateTime LastOnlineCheck { get; private set; }

        public static event EventHandler<NetworkStatusChangedEventArgs> NetworkStatusChanged;

        public static void LoadIndex()
        {
            lock (_lock)
            {
                if (!File.Exists(PlaybookCachePaths.GetIndexPath()))
                {
                    _index = new PlaybookCacheIndex();
                    SaveIndex();
                    return;
                }

                try
                {
                    var json = File.ReadAllText(PlaybookCachePaths.GetIndexPath());
                    _index = JsonConvert.DeserializeObject<PlaybookCacheIndex>(json) ?? new PlaybookCacheIndex();
                }
                catch
                {
                    _index = new PlaybookCacheIndex();
                }
            }
        }

        public static void SaveIndex()
        {
            lock (_lock)
            {
                if (_index == null) return;
                Directory.CreateDirectory(PlaybookCachePaths.BaseDir);
                File.WriteAllText(
                    PlaybookCachePaths.GetIndexPath(),
                    JsonConvert.SerializeObject(_index, Formatting.Indented));
            }
        }

        public static CachedPlaybook CachePlaybook(string sourcePath, string name = null, string username = null)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Playbook nao encontrado.", sourcePath);

            name = name ?? Path.GetFileNameWithoutExtension(sourcePath);

            lock (_lock)
            {
                if (_index == null) LoadIndex();

                var existing = _index.Playbooks.FirstOrDefault(p =>
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    try
                    {
                        if (File.Exists(existing.CachedPath))
                            File.Delete(existing.CachedPath);
                    }
                    catch { }
                    _index.Playbooks.Remove(existing);
                }

                var cachedPath = PlaybookCachePaths.GetPlaybookCachePath(name);
                File.Copy(sourcePath, cachedPath, true);

                var fileInfo = new FileInfo(cachedPath);
                var entry = new CachedPlaybook
                {
                    Name = name,
                    SourcePath = sourcePath,
                    CachedPath = cachedPath,
                    Username = username ?? "",
                    Version = "1.0",
                    CachedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow,
                    Hash = ComputeFileHash(cachedPath),
                    SizeBytes = fileInfo.Length
                };

                _index.Playbooks.Add(entry);
                SaveIndex();

                return entry;
            }
        }

        public static void RemoveFromCache(string name)
        {
            lock (_lock)
            {
                if (_index == null) LoadIndex();

                var entry = _index.Playbooks.FirstOrDefault(p =>
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

                if (entry == null) return;

                try
                {
                    if (File.Exists(entry.CachedPath))
                        File.Delete(entry.CachedPath);
                }
                catch { }

                _index.Playbooks.Remove(entry);
                SaveIndex();
            }
        }

        public static CachedPlaybook GetCached(string name)
        {
            lock (_lock)
            {
                if (_index == null) LoadIndex();

                var entry = _index.Playbooks.FirstOrDefault(p =>
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    entry.LastAccessedAt = DateTime.UtcNow;
                    SaveIndex();
                }

                return entry;
            }
        }

        public static IEnumerable<CachedPlaybook> ListCached()
        {
            if (_index == null) LoadIndex();
            return _index.Playbooks.Where(p => File.Exists(p.CachedPath)).ToList();
        }

        public static bool IsCached(string name)
        {
            if (_index == null) LoadIndex();
            return _index.Playbooks.Any(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(p.CachedPath));
        }

        public static bool CheckOnlineStatus()
        {
            var wasOnline = IsOnline;
            IsOnline = CheckInternet();

            if (wasOnline != IsOnline)
            {
                NetworkStatusChanged?.Invoke(null, new NetworkStatusChangedEventArgs { IsOnline = IsOnline });
            }

            LastOnlineCheck = DateTime.UtcNow;
            return IsOnline;
        }

        public static bool CheckInternet()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("https://api.github.com");
                request.Timeout = 5000;
                request.UserAgent = "KT-WIRZADE";
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string ComputeFileHash(string filePath)
        {
            try
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                return "";
            }
        }
    }

    public class NetworkStatusChangedEventArgs : EventArgs
    {
        public bool IsOnline { get; set; }
    }
}
