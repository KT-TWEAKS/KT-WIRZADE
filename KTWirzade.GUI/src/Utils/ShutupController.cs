using Core;
using Core.Actions;
using Interprocess;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;
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
    public static class ShutupController
    {
        public class NamespaceIgnorantXmlTextReader : System.Xml.XmlTextReader
        {
            public NamespaceIgnorantXmlTextReader(TextReader reader) : base(reader)
            {
            }

            public override string NamespaceURI
            {
                get
                {
                    return "";
                }
            }
        }

        public static (string Key, RegistryValueAction Action)[] _shutup10SettingsToCheck;

        [InterprocessMethod(Level.Administrator)]
        public static void ShutupRefresh()
        {
            try
            {
                string cfgFile = null;
                string[] ignore = ["Default User", "Public", "All Users", "Public", "Default"];
                foreach (string item in from x in Directory.GetDirectories(Environment.ExpandEnvironmentVariables("%SYSTEMDRIVE%\\Users"))
                                        where !ignore.Contains(x) && Directory.Exists(x + "\\AppData\\Local\\OO Software")
                                        select x)
                {
                    cfgFile = Directory.GetFiles(item + "\\AppData\\Local\\OO Software", "*.cfg", SearchOption.AllDirectories).FirstOrDefault();
                }

                if (cfgFile != null)
                {
                    //XmlSerializer serializer = new XmlSerializer(typeof(Shutup10.Settings));
                    //string[] availableSettings;
                    //using (StreamReader reader = new StreamReader(cfgFile))
                    //{
                    //    availableSettings = (from setting in ((Shutup10.Settings)serializer.Deserialize(new NamespaceIgnorantXmlTextReader(reader))).InitialState
                    //                         select setting.Name).ToArray<string>();
                    //}
                    //_shutup10SettingsToCheck = (from x in Shutup10.FactoryDefaultValues
                    //                                             where availableSettings.Contains(x.Key)
                    //                                             select new ValueTuple<string, RegistryValueAction>(x.Key, x.Value)).ToArray<ValueTuple<string, RegistryValueAction>>();
                }
                else
                {
                    _shutup10SettingsToCheck = [];
                }
            }
            catch (Exception)
            {
                _shutup10SettingsToCheck = [];
            }
        }

        [InterprocessMethod(Level.Administrator)]
        public static void ShutupReset()
        {
            foreach (RegistryValueAction taskAction in _shutup10SettingsToCheck.Select(((string Key, RegistryValueAction Action) x) => x.Action))
            {
                Wrap.ExecuteSafe(delegate
                {
                    taskAction.RunTask(true);
                }, false, null);
            }
            Wrap.ExecuteSafe(delegate
            {
                Process.GetProcesses().FirstOrDefault((Process x) => x.ProcessName.StartsWith("OOSU"))?.Kill();
            }, false, null);
            Wrap.ExecuteSafe(delegate
            {
                string[] ignore = ["Default User", "Public", "All Users", "Public", "Default"];
                foreach (string current in from x in Directory.GetDirectories(Environment.ExpandEnvironmentVariables("%SYSTEMDRIVE%\\Users"))
                                           where !ignore.Contains(x)
                                           select x)
                {
                    if (Directory.Exists(current + "\\AppData\\Local\\OO Software"))
                    {
                        Directory.Delete(current + "\\AppData\\Local\\OO Software", recursive: true);
                    }
                }
            }, false, null);
        }

        [InterprocessMethod(Level.Administrator)]
        public static void ShutupKill()
        {
            Process.GetProcesses().FirstOrDefault((Process x) => x.ProcessName.StartsWith("OOSU"))?.Kill();
        }

        [InterprocessMethod(Level.Administrator)]
        public static bool ShutupCheckIfDefaultSettings()
        {
            return _shutup10SettingsToCheck.Any(((string Key, RegistryValueAction Action) x) => (int)x.Action.GetStatus() > 0);
        }
    }
}