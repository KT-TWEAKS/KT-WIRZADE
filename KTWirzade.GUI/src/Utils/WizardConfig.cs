using Core;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using KTWirzade.Shared;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Newtonsoft.Json;
using KTWirzade.GUI;


namespace KTWirzade.GUI.Utils
{
    public static class WizardConfig
    {
        public class Config
        {
            public List<Item> Items
            {
                get
                {
                    return [.. GlobalsGUI.Current.Items.Select((IDragItem x) => new Item(x))];
                }
                set
                {
                    GlobalsGUI.Current.Items = new ObservableCollection<IDragItem>(value.Select((Item x) => x.DragItem));
                    GlobalsGUI.Current.Items.CollectionChanged += delegate
                    {
                        Pause.Set();
                    };
                }
            }

            public ConfigObject<string> LastSelectedItem { get; set; } = new ConfigObject<string>(null, "LastSelectedItem");

            public ConfigObject<bool> IgnoreRemnants { get; set; } = new ConfigObject<bool>(value: false, "IgnoreRemnants");

            public ConfigObject<bool> LiveServicePackageApplied { get; set; } = new ConfigObject<bool>(value: false, "LiveServicePackageApplied");

            public VersionNumber LastVersion { get; set; } = Globals.CurrentVersionNumber;

            public ConfigObject<string> PendingUpdate { get; set; } = new ConfigObject<string>(null, "PendingUpdate");

            public ConfigObject<DateTime> LastChecked { get; set; } = new ConfigObject<DateTime>(default(DateTime), "LastChecked");

            // Version the user chose to ignore when prompted about an update.
            public ConfigObject<string> SkippedUpdateVersion { get; set; } = new ConfigObject<string>(null, "SkippedUpdateVersion");

            // Update channel: "stable" (default) or "prerelease". Optional element, so
            // older config files without it keep loading with the default above.
            public ConfigObject<string> UpdateChannel { get; set; } = new ConfigObject<string>("stable", "UpdateChannel");

            // Automatic update check on startup (default: enabled). Same optional-element
            // backward compatibility as the fields above.
            public ConfigObject<bool> AutoCheckUpdates { get; set; } = new ConfigObject<bool>(value: true, "AutoCheckUpdates");

            // Keep sidebar visibility separate from the on-disk APBX inventory.
            // The JSON string keeps this state backward-compatible with older XML configs.
            public ConfigObject<string> ApbxUiState { get; set; } = new ConfigObject<string>(null, "ApbxUiState");

            public Config()
            {
                GlobalsGUI.Current.Items.CollectionChanged += delegate
                {
                    Pause.Set();
                };
            }
        }

        [Serializable]
        public class ConfigObject<TType> : IXmlSerializable
        {
            private TType _localValue;

            private string _name;

            public ConfigObject(TType value, string name)
            {
                _localValue = value;
                _name = name;
            }

            protected ConfigObject()
            {
            }

            public void Set(TType value)
            {
                _localValue = value;
                Pause.Set();
            }

            public TType Get()
            {
                return _localValue;
            }

            public void WriteXml(XmlWriter writer)
            {
                if (_localValue != null)
                {
                    writer.WriteValue(_localValue);
                }
            }

            public void ReadXml(XmlReader reader)
            {
                _localValue = (TType)reader.ReadElementContentAs(typeof(TType), null);
                if (_localValue is string stringValue && string.IsNullOrEmpty(stringValue))
                {
                    _localValue = default(TType);
                }
            }

            public XmlSchema GetSchema()
            {
                return null;
            }
        }

        public class Item : IXmlSerializable
        {
            public IDragItem DragItem;

            [XmlAttribute]
            public string Type { get; set; }

            public Item()
            {
            }

            public Item(IDragItem dragItem)
            {
                DragItem = dragItem;
                Type = dragItem.GetType().Name;
            }

            public void WriteXml(XmlWriter writer)
            {
                writer.WriteAttributeString("Type", Type);
                string type = Type;
                PropertyInfo[] properties;
                if (!(type == "PlaybookGUI"))
                {
                    if (!(type == "ISO"))
                    {
                        throw new InvalidOperationException("Unsupported type: " + Type);
                    }
                    properties = typeof(ISO).GetProperties();
                }
                else
                {
                    properties = typeof(PlaybookGUI).GetProperties();
                }
                PropertyInfo[] typeProperties = properties;
                foreach (PropertyInfo prop in from x in typeof(IDragItem).GetProperties()
                                              where Attribute.GetCustomAttribute(x, typeof(XmlIgnoreAttribute)) == null && typeProperties.Any((PropertyInfo typeX) => typeX.Name == x.Name && Attribute.GetCustomAttribute(typeX, typeof(XmlIgnoreAttribute)) == null)
                                              select x)
                {
                    object value = prop.GetValue(DragItem);
                    if (value != null)
                    {
                        writer.WriteStartElement(prop.Name);
                        writer.WriteString(value.ToString());
                        writer.WriteEndElement();
                    }
                }
            }

            public void ReadXml(XmlReader reader)
            {
                reader.MoveToContent();
                Type = reader.GetAttribute("Type");
                string type = Type;
                IDragItem dragItem;
                if (!(type == "PlaybookGUI"))
                {
                    if (!(type == "ISO"))
                    {
                        throw new InvalidOperationException("Unsupported type: " + Type);
                    }
                    dragItem = new ISO();
                }
                else
                {
                    dragItem = new PlaybookGUI(new Playbook());
                }
                IDragItem item = dragItem;
                List<PropertyInfo> properties = (from x in typeof(IDragItem).GetProperties()
                                                 where Attribute.GetCustomAttribute(x, typeof(XmlIgnoreAttribute)) == null
                                                 select x).ToList();
                reader.ReadStartElement();
                while (reader.NodeType != XmlNodeType.EndElement)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        PropertyInfo prop = properties.FirstOrDefault((PropertyInfo p) => p.Name == reader.LocalName);
                        if (prop != null)
                        {
                            string value = reader.ReadElementContentAsString();
                            if (prop.PropertyType == typeof(Guid))
                            {
                                prop.SetValue(item, Guid.Parse(value));
                            }
                            else if (prop.PropertyType == typeof(Guid?))
                            {
                                if (string.IsNullOrWhiteSpace(value))
                                {
                                    prop.SetValue(item, null);
                                }
                                else
                                {
                                    prop.SetValue(item, Guid.Parse(value));
                                }
                            }
                            else
                            {
                                prop.SetValue(item, value);
                            }
                        }
                        else
                        {
                            reader.ReadElementContentAsString();
                        }
                    }
                    else
                    {
                        reader.Read();
                    }
                }
                DragItem = item;
                if (GlobalsGUI.Current.Items.All((IDragItem x) => x.FileNameWithoutExtension != DragItem.FileNameWithoutExtension))
                {
                    GlobalsGUI.Current.Items.Add(DragItem);
                }
                reader.ReadEndElement();
            }

            public XmlSchema GetSchema()
            {
                return null;
            }
        }

        public sealed class HiddenPlaybookEntry
        {
            public string Identity { get; set; }
            public string FileName { get; set; }
            public string Name { get; set; }
            public string Username { get; set; }
            public Guid? UniqueId { get; set; }
            public DateTime HiddenAtUtc { get; set; }

            [JsonIgnore]
            public string DisplayName => string.IsNullOrWhiteSpace(Username) ? Name : $"{Username} — {Name}";
        }

        public sealed class ApbxRemovalEntry
        {
            public string Identity { get; set; }
            public string FileName { get; set; }
            public string Name { get; set; }
            public string Username { get; set; }
            public Guid? UniqueId { get; set; }
            public DateTime RemovedAtUtc { get; set; }
            public string Reason { get; set; }

            [JsonIgnore]
            public string DisplayName => string.IsNullOrWhiteSpace(Username) ? (Name ?? FileName) : $"{Username} — {Name}";

            [JsonIgnore]
            public string RemovedAtDisplay => RemovedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        }

        private sealed class ApbxUiStateData
        {
            public List<HiddenPlaybookEntry> HiddenPlaybooks { get; set; } = new List<HiddenPlaybookEntry>();
            public List<ApbxRemovalEntry> RemovedPlaybooks { get; set; } = new List<ApbxRemovalEntry>();
        }

        public static readonly string ConfigPath = Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\ktwirzade.conf");

        private static Thread _configThread = null;

        private static readonly object _apbxUiStateLock = new object();

        private static string _apbxUiStateCorruptJson;

        private static bool _apbxUiStateBackupFailed;

        private static CancellationTokenSource _configThreadCancel = null;

        private static readonly ManualResetEventSlim Pause = new ManualResetEventSlim();

        private static object _lockObject = new object();

        public static Config Current { get; set; } = null;

        private static ApbxUiStateData ReadApbxUiState()
        {
            lock (_apbxUiStateLock)
            {
                string json = Current?.ApbxUiState?.Get();
                if (string.IsNullOrWhiteSpace(json)) return new ApbxUiStateData();
                if (string.Equals(json, _apbxUiStateCorruptJson, StringComparison.Ordinal))
                {
                    return new ApbxUiStateData();
                }
                try
                {
                    ApbxUiStateData state = JsonConvert.DeserializeObject<ApbxUiStateData>(json) ?? new ApbxUiStateData();
                    if (state.HiddenPlaybooks == null) state.HiddenPlaybooks = new List<HiddenPlaybookEntry>();
                    if (state.RemovedPlaybooks == null) state.RemovedPlaybooks = new List<ApbxRemovalEntry>();
                    state.HiddenPlaybooks.RemoveAll(entry => entry == null);
                    state.RemovedPlaybooks.RemoveAll(entry => entry == null);
                    return state;
                }
                catch (Exception ex)
                {
                    Log.EnqueueExceptionSafe(ex, "Could not read APBX visibility history.", Array.Empty<(string, object)>());
                    _apbxUiStateCorruptJson = json;
                    try
                    {
                        string backupPath = ConfigPath + ".apbx-ui-state-corrupt." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + ".bak";
                        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                        File.WriteAllText(backupPath, json, new UTF8Encoding(false));
                        _apbxUiStateBackupFailed = false;
                    }
                    catch (Exception backupException)
                    {
                        _apbxUiStateBackupFailed = true;
                        Log.EnqueueExceptionSafe(backupException, "Could not preserve the damaged APBX visibility history; updates will be kept in memory only.", Array.Empty<(string, object)>());
                    }
                    return new ApbxUiStateData();
                }
            }
        }

        private static void WriteApbxUiState(ApbxUiStateData state)
        {
            lock (_apbxUiStateLock)
            {
                if (Current?.ApbxUiState == null) return;
                if (_apbxUiStateBackupFailed && string.Equals(Current.ApbxUiState.Get(), _apbxUiStateCorruptJson, StringComparison.Ordinal))
                {
                    return;
                }
                Current.ApbxUiState.Set(JsonConvert.SerializeObject(state));
                _apbxUiStateCorruptJson = null;
                _apbxUiStateBackupFailed = false;
            }
        }

        private static string GetPlaybookIdentity(PlaybookGUI playbook)
        {
            if (playbook.UniqueId.HasValue)
            {
                return "id:" + playbook.UniqueId.Value.ToString("N");
            }

            if (string.IsNullOrWhiteSpace(playbook.Username) && string.IsNullOrWhiteSpace(playbook.Name))
            {
                return "file:" + GetPlaybookFileName(playbook).ToUpperInvariant();
            }

            return "name:" + (playbook.Username ?? string.Empty).Trim().ToUpperInvariant() + "|" +
                (playbook.Name ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string GetPlaybookFileName(PlaybookGUI playbook) => playbook.FileNameWithoutExtension + ".apbx";

        public static bool IsPlaybookHidden(PlaybookGUI playbook)
        {
            if (playbook == null) return false;
            string identity = GetPlaybookIdentity(playbook);
            string fileName = GetPlaybookFileName(playbook);
            return ReadApbxUiState().HiddenPlaybooks.Any(entry =>
                string.Equals(entry.Identity, identity, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsPlaybookFileHidden(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            return ReadApbxUiState().HiddenPlaybooks.Any(entry =>
                string.Equals(entry.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        }

        public static List<HiddenPlaybookEntry> GetHiddenPlaybooks() =>
            ReadApbxUiState().HiddenPlaybooks.OrderByDescending(entry => entry.HiddenAtUtc).ToList();

        public static List<ApbxRemovalEntry> GetRemovedPlaybooks() =>
            ReadApbxUiState().RemovedPlaybooks.OrderByDescending(entry => entry.RemovedAtUtc).ToList();

        public static void HidePlaybook(PlaybookGUI playbook)
        {
            if (playbook == null) return;
            ApbxUiStateData state = ReadApbxUiState();
            string identity = GetPlaybookIdentity(playbook);
            string fileName = GetPlaybookFileName(playbook);
            if (state.HiddenPlaybooks.Any(entry =>
                string.Equals(entry.Identity, identity, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.FileName, fileName, StringComparison.OrdinalIgnoreCase))) return;

            state.HiddenPlaybooks.Add(new HiddenPlaybookEntry
            {
                Identity = identity,
                FileName = fileName,
                Name = playbook.Name,
                Username = playbook.Username,
                UniqueId = playbook.UniqueId,
                HiddenAtUtc = DateTime.UtcNow
            });
            WriteApbxUiState(state);
        }

        public static void RestorePlaybook(HiddenPlaybookEntry hidden)
        {
            if (hidden == null) return;
            ApbxUiStateData state = ReadApbxUiState();
            state.HiddenPlaybooks.RemoveAll(entry =>
                (!string.IsNullOrWhiteSpace(hidden.Identity) && string.Equals(entry.Identity, hidden.Identity, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(hidden.FileName) && string.Equals(entry.FileName, hidden.FileName, StringComparison.OrdinalIgnoreCase)));
            WriteApbxUiState(state);
        }

        public static void RestorePlaybook(PlaybookGUI playbook)
        {
            if (playbook == null) return;
            ApbxUiStateData state = ReadApbxUiState();
            string identity = GetPlaybookIdentity(playbook);
            string fileName = GetPlaybookFileName(playbook);
            int removed = state.HiddenPlaybooks.RemoveAll(entry =>
                string.Equals(entry.Identity, identity, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.FileName, fileName, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) WriteApbxUiState(state);
        }

        public static void RefreshHiddenPlaybook(PlaybookGUI playbook, string physicalFileName = null)
        {
            if (playbook == null) return;
            ApbxUiStateData state = ReadApbxUiState();
            string identity = GetPlaybookIdentity(playbook);
            string fileName = Path.GetFileName(physicalFileName ?? GetPlaybookFileName(playbook));
            HiddenPlaybookEntry hidden = state.HiddenPlaybooks.FirstOrDefault(entry =>
                string.Equals(entry.Identity, identity, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.FileName, fileName, StringComparison.OrdinalIgnoreCase));
            if (hidden == null) return;

            if (string.Equals(hidden.Identity, identity, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(hidden.FileName, fileName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(hidden.Name, playbook.Name, StringComparison.Ordinal) &&
                string.Equals(hidden.Username, playbook.Username, StringComparison.Ordinal) &&
                hidden.UniqueId == playbook.UniqueId) return;

            hidden.Identity = identity;
            hidden.FileName = fileName;
            hidden.Name = playbook.Name;
            hidden.Username = playbook.Username;
            hidden.UniqueId = playbook.UniqueId;
            WriteApbxUiState(state);
        }

        public static void RecordPlaybookRemoved(PlaybookGUI playbook, string fileName, string reason)
        {
            ApbxUiStateData state = ReadApbxUiState();
            string identity = playbook == null ? null : GetPlaybookIdentity(playbook);
            string normalizedFileName = Path.GetFileName(fileName ?? (playbook == null ? string.Empty : GetPlaybookFileName(playbook)));
            HiddenPlaybookEntry hidden = state.HiddenPlaybooks.FirstOrDefault(entry =>
                string.Equals(entry.FileName, normalizedFileName, StringComparison.OrdinalIgnoreCase) ||
                (identity != null && string.Equals(entry.Identity, identity, StringComparison.OrdinalIgnoreCase)));

            state.HiddenPlaybooks.RemoveAll(entry =>
                string.Equals(entry.FileName, normalizedFileName, StringComparison.OrdinalIgnoreCase) ||
                (identity != null && string.Equals(entry.Identity, identity, StringComparison.OrdinalIgnoreCase)));

            string name = playbook?.Name ?? hidden?.Name;
            string username = playbook?.Username ?? hidden?.Username;
            Guid? uniqueId = playbook?.UniqueId ?? hidden?.UniqueId;
            if (!string.IsNullOrWhiteSpace(normalizedFileName))
            {
                state.RemovedPlaybooks.RemoveAll(entry =>
                    string.Equals(entry.FileName, normalizedFileName, StringComparison.OrdinalIgnoreCase));
                state.RemovedPlaybooks.Insert(0, new ApbxRemovalEntry
                {
                    Identity = identity ?? hidden?.Identity,
                    FileName = normalizedFileName,
                    Name = name,
                    Username = username,
                    UniqueId = uniqueId,
                    RemovedAtUtc = DateTime.UtcNow,
                    Reason = reason
                });
                if (state.RemovedPlaybooks.Count > 100)
                {
                    state.RemovedPlaybooks = state.RemovedPlaybooks.Take(100).ToList();
                }
            }

            WriteApbxUiState(state);
        }

        public static void RecordMissingHiddenPlaybooks(string playbookDirectory)
        {
            ApbxUiStateData state = ReadApbxUiState();
            List<HiddenPlaybookEntry> missing = state.HiddenPlaybooks
                .Where(entry => string.IsNullOrWhiteSpace(entry.FileName) ||
                    !File.Exists(Path.Combine(playbookDirectory, Path.GetFileName(entry.FileName))))
                .ToList();
            foreach (HiddenPlaybookEntry entry in missing)
            {
                RecordPlaybookRemoved(null, entry.FileName, "Arquivo APBX não encontrado no computador");
            }
        }

        // ---- Persisted update settings ------------------------------------
        // All helpers are exception-safe (never throw, fall back to defaults) and
        // persist through ConfigObject<T>.Set, which wakes the config thread exactly
        // like WriteApbxUiState/HidePlaybook do.

        /// <summary>"stable" (default) or "prerelease"; anything else falls back to "stable".</summary>
        public static string GetUpdateChannel()
        {
            try
            {
                string channel = Current?.UpdateChannel?.Get();
                return string.Equals(channel, "prerelease", StringComparison.OrdinalIgnoreCase) ? "prerelease" : "stable";
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Could not read the update channel; falling back to stable.", Array.Empty<(string, object)>());
                return "stable";
            }
        }

        public static void SetUpdateChannel(string channel)
        {
            try
            {
                string normalized = string.Equals(channel, "prerelease", StringComparison.OrdinalIgnoreCase) ? "prerelease" : "stable";
                Current?.UpdateChannel?.Set(normalized);
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Could not persist the update channel.", Array.Empty<(string, object)>());
            }
        }

        /// <summary>Version tag the user chose to ignore; "" when none (default).</summary>
        public static string GetSkippedVersion()
        {
            try
            {
                return Current?.SkippedUpdateVersion?.Get() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Could not read the skipped update version.", Array.Empty<(string, object)>());
                return string.Empty;
            }
        }

        public static void SetSkippedVersion(string version)
        {
            try
            {
                Current?.SkippedUpdateVersion?.Set(version ?? string.Empty);
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Could not persist the skipped update version.", Array.Empty<(string, object)>());
            }
        }

        /// <summary>Run the update check on startup (default: true).</summary>
        public static bool GetAutoCheckEnabled()
        {
            try
            {
                return Current?.AutoCheckUpdates?.Get() ?? true;
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Could not read the auto-update setting; assuming enabled.", Array.Empty<(string, object)>());
                return true;
            }
        }

        public static void SetAutoCheckEnabled(bool enabled)
        {
            try
            {
                Current?.AutoCheckUpdates?.Set(enabled);
            }
            catch (Exception ex)
            {
                Log.EnqueueExceptionSafe(ex, "Could not persist the auto-update setting.", Array.Empty<(string, object)>());
            }
        }

        public static void GetConfig()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Config));
            if (File.Exists(ConfigPath))
            {
                try
                {
                    using XmlReader reader = XmlReader.Create(ConfigPath);
                    Config config = (Config)serializer.Deserialize(reader);
                    GlobalsGUI.Current.WizardPlaybook.PendingUpdate = config.PendingUpdate.Get();
                    GlobalsGUI.Current.WizardPlaybook.LastChecked = config.LastChecked.Get();
                    Current = config;
                    return;
                }
                catch (Exception)
                {
                }
            }
            Config newConfig = new Config();
            newConfig.LastSelectedItem.Set(null);
            try
            {
                using XmlWriter writer = XmlWriter.Create(ConfigPath, new XmlWriterSettings
                {
                    Indent = true
                });
                serializer.Serialize(writer, newConfig);
            }
            catch
            {
            }
            GlobalsGUI.Current.WizardPlaybook.PendingUpdate = newConfig.PendingUpdate.Get();
            GlobalsGUI.Current.WizardPlaybook.LastChecked = newConfig.LastChecked.Get();
            Current = newConfig;
        }

        public static void StartConfigThread()
        {
            lock (_lockObject)
            {
                if (_configThread != null)
                {
                    throw new Exception("Only one logging instance allowed.");
                }
                try
                {
                    if (!Directory.Exists(Path.GetDirectoryName(ConfigPath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                    }
                }
                catch (Exception ex)
                {
                    Log.EnqueueExceptionSafe(ex, Array.Empty<(string, object)>());
                }
                _configThreadCancel = new CancellationTokenSource();
                _configThread = new Thread(ThreadLoop)
                {
                    IsBackground = false,
                    CurrentUICulture = CultureInfo.InvariantCulture
                };
                _configThread.Start();
            }
        }

        public static void EndConfigThread()
        {
            lock (_lockObject)
            {
                // Flush config synchronously BEFORE cancelling the background thread.
                // This ensures the latest state (e.g. playbook removals) is always
                // persisted, even if the user closes the app immediately after an edit.
                try
                {
                    if (Current != null && !string.IsNullOrEmpty(ConfigPath))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(Config));
                        using XmlWriter writer = XmlWriter.Create(ConfigPath, new XmlWriterSettings
                        {
                            Indent = true
                        });
                        serializer.Serialize(writer, Current);
                    }
                }
                catch (Exception ex)
                {
                    Log.EnqueueExceptionSafe(ex, "Failed to flush config on exit.", Array.Empty<(string, object)>());
                }

                CancellationTokenSource configThreadCancel = _configThreadCancel;
                if (configThreadCancel != null && !configThreadCancel.IsCancellationRequested)
                {
                    _configThreadCancel.Cancel();
                    if (!_configThread.Join(2000))
                    {
                        Log.EnqueueSafe(LogType.Warning, "Config thread took too long to exit; abandoning.", null, Array.Empty<(string, object)>());
                    }
                    _configThread = null;
                }
            }
        }

        private static void ThreadLoop()
        {
            while (true)
            {
                try
                {
                    Pause.Wait(_configThreadCancel.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                Pause.Reset();
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Config));
                    using XmlWriter writer = XmlWriter.Create(ConfigPath, new XmlWriterSettings
                    {
                        Indent = true
                    });
                    serializer.Serialize(writer, Current);
                }
                catch (Exception ex2)
                {
                    Log.EnqueueExceptionSafe(ex2, Array.Empty<(string, object)>());
                }
            }
        }
    }
}
