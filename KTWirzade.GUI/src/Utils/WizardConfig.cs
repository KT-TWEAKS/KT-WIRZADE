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

        public static readonly string ConfigPath = Environment.ExpandEnvironmentVariables("%PROGRAMDATA%\\KTWirzade\\ktwirzade.conf");

        private static Thread _configThread = null;

        private static CancellationTokenSource _configThreadCancel = null;

        private static readonly ManualResetEventSlim Pause = new ManualResetEventSlim();

        private static object _lockObject = new object();

        public static Config Current { get; set; } = null;

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
                    Log.EnqueueExceptionSafe(ex2, []);
                }
            }
        }
    }
}