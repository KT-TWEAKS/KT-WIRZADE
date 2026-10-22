using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace KTWirzade.GUI.Utils
{
    /// <summary>
    /// Persistent store for playbook hashes the user explicitly chose to trust
    /// (manual bypass, ONLY ever applied to Unverified playbooks). Malicious
    /// playbooks are never consultable through this store by design: callers
    /// must check the verification level first.
    /// Thread-safe and exception-safe: a missing/corrupt/unwritable file never
    /// throws and never yields trust that was not explicitly granted.
    /// </summary>
    public static class PlaybookTrustStore
    {
        private static readonly object Sync = new object();

        // null until first load; an empty set means "nothing trusted".
        private static HashSet<string> _trusted;

        private static string StorePath
        {
            get
            {
                return Path.Combine(
                    Environment.ExpandEnvironmentVariables("%PROGRAMDATA%"),
                    "KTWirzade",
                    "trusted_playbooks.json");
            }
        }

        private static HashSet<string> Load()
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string path = StorePath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    JToken parsed = JToken.Parse(json);
                    JArray array = parsed as JArray ?? parsed?["hashes"] as JArray;
                    if (array != null)
                    {
                        foreach (JToken token in array)
                        {
                            string hash = token?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(hash))
                            {
                                set.Add(hash);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Corrupt or unreadable store: behave as "nothing trusted".
                set.Clear();
            }
            return set;
        }

        private static HashSet<string> GetSet()
        {
            lock (Sync)
            {
                if (_trusted == null)
                {
                    _trusted = Load();
                }
                return _trusted;
            }
        }

        private static void Save()
        {
            // Caller holds Sync.
            try
            {
                string path = StorePath;
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                string json = new JObject
                {
                    ["hashes"] = new JArray(_trusted.OrderBy(h => h, StringComparer.OrdinalIgnoreCase).ToArray())
                }.ToString();
                // Atomic write: temp file + move/replace so a crash mid-write
                // cannot leave a truncated store behind.
                string temp = path + ".tmp";
                File.WriteAllText(temp, json);
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch (Exception)
            {
                // Best effort: losing the persisted copy only means the user
                // has to re-grant trust later; never crash the UI over it.
            }
        }

        public static bool IsTrusted(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }
            lock (Sync)
            {
                return GetSet().Contains(hash.Trim());
            }
        }

        public static void Trust(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return;
            }
            lock (Sync)
            {
                if (GetSet().Add(hash.Trim()))
                {
                    Save();
                }
            }
        }

        public static void Revoke(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return;
            }
            lock (Sync)
            {
                if (GetSet().Remove(hash.Trim()))
                {
                    Save();
                }
            }
        }

        public static IReadOnlyList<string> List()
        {
            lock (Sync)
            {
                return GetSet().OrderBy(h => h, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
    }
}
