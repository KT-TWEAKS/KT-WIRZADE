using Core;
using DiscUtils.Iso9660;
using DiscUtils.Streams;
using DiscUtils.Udf;
using DiscUtils.Vfs;
using DiscUtils.Wim;
using Interprocess;
using Microsoft.Win32;
using System.IO;
using System.Linq.Expressions;
using System.Security;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using KTWirzade.GUI.Utils;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace KTWirzade.GUI
{
    public class ImageParsers
    {
        public static class Windows
        {
            public static ISO TryGetInfo(FileStream fileStream)
            {
                ISO result = new ISO
                {
                    Username = "Microsoft",
                    IsWindows = true
                };
                return Wrap.ExecuteSafe<ISO>(delegate
                {
                    VfsFileSystemFacade reader = null;
                    if (Wrap.ExecuteSafe(delegate
                    {
                        reader = new UdfReader(fileStream);
                    }, false, null) != null)
                    {
                        reader = new CDReader(fileStream, true);
                        if ((reader).GetFileSystemEntries("\\" + ((reader).Root).Name).All((string x) => x.TrimStart('\\').Equals("README.TXT", StringComparison.OrdinalIgnoreCase)))
                        {
                            (reader).Dispose();
                            throw new Exception("UDF open failed and CDReader README found.");
                        }
                    }
                    ISO value = Wrap.ExecuteSafe<ISO>(delegate
                    {
                        result.Architecture = reader.FileExists("efi\\boot\\bootx64.efi") ? new ImageArchitecture?(ImageArchitecture.x64) :
                        (reader.FileExists("efi\\boot\\bootia32.efi") ? new ImageArchitecture?(ImageArchitecture.x86) :
                        (reader.FileExists("efi\\boot\\bootarm.efi") ? new ImageArchitecture?(ImageArchitecture.Arm32) :
                        (reader.FileExists("efi\\boot\\bootaa64.efi") ? new ImageArchitecture?(ImageArchitecture.Arm64) :
                        null)));

                        if (!(reader).FileExists("sources\\install.wim") && !(reader).FileExists("sources\\install.esd"))
                        {
                            return null;
                        }

                        if (reader.FileExists("iso.conf"))
                        {
                            Shared.ISO iso = TryGetIsoConfiguration(reader);
                            if (iso != null)
                            {
                                result.Configuration = iso;
                                result.Name = iso.Name;
                                result.Username = iso.Creator;
                                int winVer;
                                if (int.TryParse(iso.WindowsVersion, out winVer))
                                {
                                    result.WinVer = new int?(winVer);
                                }
                                int winUpdateVer;
                                if (int.TryParse(iso.WindowsUpdateVersion, out winUpdateVer))
                                {
                                    result.WinUpdateVer = new int?(winUpdateVer);
                                }
                                result.Version = iso.Version;
                                result.Title = result.Name + ((result.Version == null) ? "" : (" v" + result.Version.TrimStart(new char[]
                                {
                                    'v'
                                }))) + ((result.Architecture == null) ? "" : (" " + result.Architecture.Value.ToString())) + " ISO";
                            }
                        }
                        if (result.WinVer.HasValue)
                        {
                            string majorVersionFromBuildNumber = GetMajorVersionFromBuildNumber(result.WinVer.Value);
                            result.ShortDescription = "Modified Windows " + majorVersionFromBuildNumber + " ISO file";
                            return result;
                        }
                        if (Wrap.ExecuteSafe(delegate
                        {
                            GetWindowsVersion(reader, result);
                        }, true, null) != null)
                        {
                            result.Name = result.Name ?? "Windows ISO";
                            result.ShortDescription = "Unrecognized Windows ISO file";
                            result.Title = "Windows" + ((!result.Architecture.HasValue) ? "" : (" " + result.Architecture.Value)) + " ISO";
                            return result;
                        }
                        string majorVersionFromBuildNumber2 = GetMajorVersionFromBuildNumber(result.WinVer.Value);
                        result.WinMajorVer = majorVersionFromBuildNumber2;
                        if (result.Name != null)
                        {
                            result.Title = result.Name + " " + ((result.Version == null) ? "" : (" " + result.Version)) + ((!result.Architecture.HasValue) ? "" : (" " + result.Architecture.Value)) + " ISO";
                            result.ShortDescription = "Modified Windows " + majorVersionFromBuildNumber2 + " ISO file";
                            return result;
                        }
                        result.IsWindows11 = majorVersionFromBuildNumber2 != "10" && majorVersionFromBuildNumber2 != "8" && majorVersionFromBuildNumber2 != "8.1" && majorVersionFromBuildNumber2 != "7";
                        result.Title = "Windows " + majorVersionFromBuildNumber2 + ((result.Version == null) ? "" : (" " + result.Version)) + ((!result.Architecture.HasValue) ? "" : (" " + result.Architecture.Value)) + " ISO";
                        result.Name = "Windows " + majorVersionFromBuildNumber2;
                        result.ShortDescription = "Standard Windows " + majorVersionFromBuildNumber2 + " ISO file";
                        return result;
                    }, false, null).Value;
                    (reader).Dispose();
                    return value;
                }, true, null).Value;
            }

            private static Shared.ISO TryGetIsoConfiguration(VfsFileSystemFacade reader)
            {
                return Wrap.ExecuteSafe<Shared.ISO>(delegate
                {
                    SparseStream val = (reader).OpenFile("iso.conf", FileMode.Open);
                    try
                    {
                        XmlSerializer xmlSerializer = new XmlSerializer(typeof(ISO));
                        using XmlReader xmlReader = XmlReader.Create((string)(object)val);
                        return (Shared.ISO)xmlSerializer.Deserialize(xmlReader);
                    }
                    finally
                    {
                        ((IDisposable)val)?.Dispose();
                    }
                }, true, null).Value;
            }

            private static string GetMajorVersionFromBuildNumber(int buildNumber)
            {
                if (IsBetween(buildNumber, 7601, 9199))
                {
                    return "7";
                }
                if (IsBetween(buildNumber, 9200, 9599))
                {
                    return "8";
                }
                if (IsBetween(buildNumber, 9600, 10239))
                {
                    return "8.1";
                }
                if (IsBetween(buildNumber, 10240, 21999))
                {
                    return "10";
                }
                if (buildNumber >= 22000)
                {
                    return "11";
                }
                throw new Exception("Could not get major version from build number: " + buildNumber);
            }

            private static bool IsBetween(int x, int low, int high)
            {
                if (x >= low)
                {
                    return x <= high;
                }
                return false;
            }

            private static void GetWindowsVersion(VfsFileSystemFacade reader, ISO result)
            {
                try
                {
                    if (!(reader).FileExists("sources\\install.wim"))
                    {
                        throw new OperationCanceledException();
                    }
                    SparseStream wimStream = (reader).OpenFile("sources\\install.wim", FileMode.Open);
                    try
                    {
                        WimFile val = new WimFile((Stream)(object)wimStream);
                        WimFileSystem wimFs = val.GetImage(val.BootImage);
                        try
                        {
                            string hivePath = Path.Combine(Path.GetTempPath(), "KTWirzade_ISO_Hive-" + Guid.NewGuid().ToString());
                            SparseStream hiveStream = (wimFs).OpenFile("Windows\\System32\\config\\SOFTWARE", FileMode.Open);
                            try
                            {
                                byte[] buffer = new byte[4096];
                                using FileStream destination = File.Create(hivePath);
                                int bytesRead;
                                while ((bytesRead = hiveStream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    destination.Write(buffer, 0, bytesRead);
                                }
                            }
                            finally
                            {
                                ((IDisposable)hiveStream)?.Dispose();
                            }
                            string hiveName = Path.GetFileName(hivePath);
                            while (!MainWindow.HasLoaded)
                            {
                                Thread.Sleep(100);
                            }
                            for (int i = 1; i < 5 || !MainWindow.HasLoaded; i++)
                            {
                                try
                                {
                                    InterLink.Execute((Expression<Action>)(() => RegistryManager.HookHive(hivePath, hiveName)), false, -1);
                                }
                                catch (SecurityException)
                                {
                                    Thread.Sleep(1250);
                                    continue;
                                }
                                break;
                            }
                            try
                            {
                                RegistryKey key = Registry.Users.OpenSubKey(hiveName + "\\Microsoft\\Windows NT\\CurrentVersion");
                                try
                                {
                                    if (key == null)
                                    {
                                        throw new Exception("CurrentVersion key not found.");
                                    }
                                    result.Version = key.GetValue("DisplayVersion") as string;
                                    result.WinVer = int.Parse(key.GetValue("CurrentBuildNumber")?.ToString());
                                    result.WinUpdateVer = Wrap.ExecuteSafe<int?>((Func<int?>)(() => (int?)key.GetValue("UBR")), false, null).Value;
                                }
                                finally
                                {
                                    if (key != null)
                                    {
                                        ((IDisposable)key).Dispose();
                                    }
                                }
                            }
                            finally
                            {
                                InterLink.ExecuteSafe((Expression<Action>)(() => RegistryManager.UnhookHive(hiveName)), true, -1);
                                Wrap.ExecuteSafe(delegate
                                {
                                    File.Delete(hivePath);
                                }, true, null);
                            }
                        }
                        finally
                        {
                            ((IDisposable)wimFs)?.Dispose();
                        }
                    }
                    finally
                    {
                        ((IDisposable)wimStream)?.Dispose();
                    }
                }
                catch (Exception ex2)
                {
                    if (!(ex2 is OperationCanceledException))
                    {
                        Log.EnqueueExceptionSafe((LogType)1, ex2, Array.Empty<(string, object)>());
                    }
                    string file = ((reader).FileExists("sources\\install.wim") ? "sources\\install.wim" : "sources\\install.esd");
                    SparseStream wimStream2 = (reader).OpenFile(file, FileMode.Open);
                    try
                    {
                        WIMInformationXML.WIM xml = WIMInformationXML.DeserializeWIM(new WimFile((Stream)(object)wimStream2).Manifest);
                        result.Version = xml.IMAGE.First().WINDOWS.VERSION.BUILD;
                        result.WinVer = int.Parse(xml.IMAGE.First().WINDOWS.VERSION.BUILD);
                        result.WinUpdateVer = int.Parse(xml.IMAGE.First().WINDOWS.VERSION.SPBUILD);
                    }
                    finally
                    {
                        ((IDisposable)wimStream2)?.Dispose();
                    }
                }
            }
        }

        public interface IOSParser
        {
            ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null);

            ISO MatchFileName(string fileName);
        }

        public class SteamOS : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                ISO result = new ISO
                {
                    Username = "Valve"
                };
                string[] matches = GetRegex(fileName, "^steamdeck-[a-z]+-[0-9\\.]+-([0-9\\.]{1,16})\\.img.*");
                if (matches != null)
                {
                    result.Version = matches[0];
                    result.Name = "SteamOS";
                    result.ShortDescription = (ContainsSegment(fileName, "repair") ? "Repair image for a Steam Deck" : "Installation image for a Steam Deck");
                    result.Title = (ContainsSegment(fileName, "repair") ? ("SteamOS " + result.Version + " Repair Image") : ("SteamOS " + result.Version));
                }
                else
                {
                    if (!Regex.IsMatch(fileName, ".*steam[\\-_ ]?(deck|os).*", RegexOptions.IgnoreCase))
                    {
                        return null;
                    }
                    result.Name = "SteamOS";
                    result.ShortDescription = (ContainsSegment(fileName, "repair") ? "SteamOS repair image" : "Installation image for a Steam Deck");
                    result.Title = (ContainsSegment(fileName, "repair") ? ("SteamOS Repair " + FileTypeToReadableString(fileName)) : ("SteamOS " + FileTypeToReadableString(fileName)));
                }
                return result;
            }
        }

        public class Ubuntu : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                InfoFile info = GetInfoFile(reader);
                if (info.Name != null)
                {
                    ISO result = iso ?? new ISO
                    {
                        Username = "Canonical"
                    };
                    if (info.Name.Equals("Ubuntu", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Name = "Ubuntu";
                        result.ShortDescription = "Standard Ubuntu " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                        result.Title = result.Name + " " + FileTypeToReadableString(fileName);
                    }
                    else
                    {
                        if (!Regex.IsMatch(info.Name, "Ubuntu[\\-_ \\.]Server", RegexOptions.IgnoreCase))
                        {
                            return null;
                        }
                        result.Name = "Ubuntu Server";
                        result.ShortDescription = "Standard Ubuntu Server " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                        result.Title = result.Name + " " + FileTypeToReadableString(fileName);
                    }
                    if (info.Version != null)
                    {
                        result.Version = info.Version;
                        result.Title = result.Name + " " + info.Version + " " + FileTypeToReadableString(fileName);
                    }
                    result.Architecture = info.Architecture;
                    return result;
                }
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                ISO result = new ISO
                {
                    Username = "Canonical"
                };
                string[] matches = GetRegex(fileName, "^ubuntu-([0-9\\.(rc)]+)-(live-server|desktop)-([a-zA-Z0-9]+)\\..*");
                if (matches != null)
                {
                    result.Version = matches[0];
                    result.Name = (matches[1].Equals("desktop", StringComparison.OrdinalIgnoreCase) ? "Ubuntu" : "Ubuntu Server");
                    result.ShortDescription = "Standard " + result.Name + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                    result.Title = result.Name + " " + result.Version + " " + FileTypeToReadableString(fileName);
                    result.Architecture = TryParseArchitecture(matches[2]);
                    return result;
                }
                if (ContainsSegment(fileName, "ubuntu"))
                {
                    if (ContainsSegment(fileName, "server"))
                    {
                        result.Name = "Ubuntu Server";
                        result.ShortDescription = "Standard " + result.Name + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                        result.Title = "Ubuntu Server " + FileTypeToReadableString(fileName);
                        return result;
                    }
                    result.Name = "Ubuntu";
                    result.ShortDescription = "Standard " + result.Name + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                    result.Title = "Ubuntu " + FileTypeToReadableString(fileName);
                    return result;
                }
                return null;
            }
        }

        public class Mint : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Debian : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Fedora : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Arch : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                using CDFileStreamReader stream = GetFileReader(reader, "boot\\grub\\grubenv");
                if (stream == null)
                {
                    return null;
                }
                ISO result = new ISO
                {
                    Username = "Arch",
                    Name = "Arch Linux",
                    ShortDescription = "Standard Arch Linux " + FileTypeToReadableString(fileName, titleCase: false) + " file",
                    Title = "Arch Linux " + FileTypeToReadableString(fileName)
                };
                bool isArchLinux = false;
                string line;
                while ((line = stream.ReadLine()) != null)
                {
                    if (Regex.IsMatch(line, "^NAME=archlinux$"))
                    {
                        isArchLinux = true;
                        continue;
                    }
                    string[] versionMatch = GetRegex(line, "^VERSION=([0-9\\.(rc)]+)$");
                    if (versionMatch != null)
                    {
                        result.Version = versionMatch[0];
                        result.Title = result.Name + " " + result.Version + " " + FileTypeToReadableString(fileName);
                    }
                    else
                    {
                        string[] architectureMatch = GetRegex(line, "^ARCH=([a-zA-Z0-9_]+)$");
                        if (architectureMatch != null)
                        {
                            result.Architecture = TryParseArchitecture(architectureMatch[0]);
                        }
                    }
                }
                if (!isArchLinux)
                {
                    return null;
                }
                return result;
            }

            public ISO MatchFileName(string fileName)
            {
                ISO result = new ISO
                {
                    Username = "Arch"
                };
                string[] matches = GetRegex(fileName, "^archlinux-([0-9\\.(rc)]+)-([a-zA-Z0-9_]+)\\.[^\\.]*$");
                if (matches != null)
                {
                    result.Version = matches[0];
                    result.Name = "Arch Linux";
                    result.ShortDescription = "Standard " + result.Name + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                    result.Title = result.Name + " " + result.Version + " " + FileTypeToReadableString(fileName);
                    result.Architecture = TryParseArchitecture(matches[1]);
                    return result;
                }
                if (ContainsSegment(fileName, "archlinux") || ContainsSegment(fileName, "arch_linux") || ContainsSegment(fileName, "arch-linux"))
                {
                    result.Name = "Arch Linux";
                    result.ShortDescription = "Standard " + result.Name + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                    result.Title = "Arch Linux " + FileTypeToReadableString(fileName);
                    return result;
                }
                return null;
            }
        }

        public class Manjaro : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class OpenSUSE : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Zorin : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Gentoo : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class CentOS : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Kali : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class PopOS : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Void : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Solus : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Garuda : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class MX : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Lubuntu : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class RHEL : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Slackware : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Rocky : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class Kubuntu : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class elementaryOS : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class pfSense : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class opnSense : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class UNRAID : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class TrueNAS : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                ISO result = new ISO
                {
                    Username = "iXsystems",
                    Name = "TrueNAS",
                    ShortDescription = "Standard TrueNAS " + FileTypeToReadableString(fileName, titleCase: false) + " file",
                    Title = "TrueNAS " + FileTypeToReadableString(fileName)
                };
                using (CDFileStreamReader grubStream = GetFileReader(reader, "BOOT\\GRUB\\GRUB.CFG"))
                {
                    if (grubStream != null)
                    {
                        string line;
                        while ((line = grubStream.ReadLine()) != null)
                        {
                            string[] nameMatch = GetRegex(line, "^[ ]*menuentry [a-z\\-=_ ]* 'Start (TrueNAS [A-Z]+|TrueNAS|TrueNAS Enterprise) Installation'( |$).*");
                            if (nameMatch != null)
                            {
                                string fullName = nameMatch[0].Replace("SCALE", "Scale").Replace("ENTERPRISE", "Enterprise");
                                result.Title = fullName + (string.IsNullOrWhiteSpace(iso?.Version) ? "" : (" " + iso.Version)) + " " + FileTypeToReadableString(fileName);
                                result.ShortDescription = "Standard " + fullName + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                                return result;
                            }
                        }
                    }
                }
                using (CDFileStreamReader manifestStream = GetFileReader(reader, "TRUENAS_MANIFEST"))
                {
                    if (manifestStream != null)
                    {
                        string line2;
                        while ((line2 = manifestStream.ReadLine()) != null)
                        {
                            string[] nameAndVersionMatch = GetRegex(line2, "^    \"Version\": \"(TrueNAS|TrueNAS-[A-Z]+)-([0-9\\.]+-U[0-9\\.]+|[0-9\\.]+)\"(,$|$)");
                            if (nameAndVersionMatch != null)
                            {
                                string fullName2 = ((nameAndVersionMatch[0].Replace('-', ' ') == "TrueNAS") ? "TrueNAS Core" : nameAndVersionMatch[0].Replace('-', ' ').Replace("SCALE", "Scale").Replace("ENTERPRISE", "Enterprise"));
                                result.Version = nameAndVersionMatch[1];
                                result.Title = fullName2 + " " + result.Version + " " + FileTypeToReadableString(fileName);
                                result.ShortDescription = "Standard " + fullName2 + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                                break;
                            }
                        }
                        return result;
                    }
                }
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                ISO result = new ISO
                {
                    Username = "iXsystems"
                };
                string[] editionedMatches = GetRegex(fileName, "^TrueNAS-([A-Z]+)-([0-9\\.]+-U[0-9\\.]+|[0-9\\.]+)\\.[^\\.]*$");
                if (editionedMatches != null)
                {
                    string fullName = ("TrueNAS " + editionedMatches[0]).Replace("SCALE", "Scale").Replace("ENTERPRISE", "Enterprise");
                    result.Version = editionedMatches[1];
                    result.Name = "TrueNAS";
                    result.ShortDescription = "Standard " + fullName + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                    result.Title = fullName + " " + result.Version + " " + FileTypeToReadableString(fileName);
                    return result;
                }
                string[] matches = GetRegex(fileName, "^TrueNAS-([0-9\\.]+-U[0-9\\.]+|[0-9\\.]+)\\.[^\\.]*$");
                if (matches != null)
                {
                    result.Version = matches[0];
                    result.Name = "TrueNAS Core";
                    result.ShortDescription = "Standard " + result.Name + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                    result.Title = result.Name + " " + result.Version + " " + FileTypeToReadableString(fileName);
                    return result;
                }
                if (ContainsSegment(fileName, "TrueNAS-SCALE"))
                {
                    result.Name = "TrueNAS";
                    result.ShortDescription = "Standard TrueNAS Scale " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                    result.Title = "TrueNAS Scale " + FileTypeToReadableString(fileName);
                    return result;
                }
                if (ContainsSegment(fileName, "TrueNAS-ENTERPRISE"))
                {
                    result.Name = "TrueNAS";
                    result.ShortDescription = "Standard TrueNAS Enterprise " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                    result.Title = "TrueNAS Enterprise " + FileTypeToReadableString(fileName);
                    return result;
                }
                if (ContainsSegment(fileName, "TrueNAS"))
                {
                    result.Name = "TrueNAS";
                    result.ShortDescription = "Standard " + result.Name + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                    result.Title = "TrueNAS " + FileTypeToReadableString(fileName);
                    return result;
                }
                return null;
            }
        }

        public class QubesOS : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                return null;
            }
        }

        public class MemTest86 : IOSParser
        {
            public ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                return null;
            }

            public ISO MatchFileName(string fileName)
            {
                if (ContainsSegment(fileName, "memtest") || ContainsSegment(fileName, "memtest86+") || ContainsSegment(fileName, "memtest86plus"))
                {
                    return new ISO
                    {
                        Name = "Memtest86+",
                        Username = "Samuel D.",
                        ShortDescription = "Standard Memtest86+ " + FileTypeToReadableString(fileName, titleCase: false) + " file",
                        Title = "Memtest86+ " + FileTypeToReadableString(fileName)
                    };
                }
                if (ContainsSegment(fileName, "memtest86"))
                {
                    return new ISO
                    {
                        Name = "Memtest86",
                        Username = "PassMark",
                        ShortDescription = "Standard Memtest86 " + FileTypeToReadableString(fileName, titleCase: false) + " file",
                        Title = "Memtest86 " + FileTypeToReadableString(fileName)
                    };
                }
                return null;
            }
        }

        public static class Linux
        {
            public static ISO TryGetInfo(CDReader reader, string fileName, ISO iso = null)
            {
                ISO result = Result(fileName);
                InfoFile info = GetInfoFile(reader);
                if (info.Name != null)
                {
                    result.Name = info.Name.Replace('-', ' ');
                    result.ShortDescription = result.Name + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                    result.Title = result.Name + " " + FileTypeToReadableString(fileName);
                    if (info.Version != null)
                    {
                        result.Version = info.Version;
                        result.Title = result.Name + " " + info.Version + " " + FileTypeToReadableString(fileName);
                    }
                    result.Architecture = info.Architecture;
                    return result;
                }
                if ((reader).FileExists(".treeinfo"))
                {
                    SparseStream stream = (reader).OpenFile(".treeinfo", FileMode.Open, FileAccess.Read);
                    try
                    {
                        using StreamReader readStream = new(stream);
                        IniParser parser = new IniParser(readStream);
                        string version = parser.TryGetValue("general", "version");
                        if (version != null && version.Length <= 16)
                        {
                            result.Version = version;
                        }
                        string arch = parser.TryGetValue("general", "arch");
                        if (arch != null)
                        {
                            result.Architecture = TryParseArchitecture(arch);
                        }
                        string name = parser.TryGetValue("general", "name");
                        if (name != null && name.Length <= 13)
                        {
                            result.Name = RemoveSegment(name, result.Version).Replace('-', ' ');
                            result.Title = result.Name + " " + ((result.Version == null) ? "" : (result.Version + " ")) + FileTypeToReadableString(fileName);
                            result.ShortDescription = result.Name + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                            return result;
                        }
                        string family = parser.TryGetValue("general", "family");
                        if (family != null && family.Length <= 13)
                        {
                            result.Name = RemoveSegment(family, result.Version).Replace('-', ' ');
                            result.Title = result.Name + " " + ((result.Version == null) ? "" : (result.Version + " ")) + FileTypeToReadableString(fileName);
                            result.ShortDescription = result.Name + " " + FileTypeToReadableString(fileName, titleCase: false) + " file";
                            return result;
                        }
                    }
                    finally
                    {
                        ((IDisposable)stream)?.Dispose();
                    }
                }
                else if ((reader).DirectoryExists("boot\\grub") || (reader).DirectoryExists("boot\\grub2") || (reader).DirectoryExists("boot\\syslinux"))
                {
                    return result;
                }
                return null;
            }

            public static ISO MatchFileName(string fileName)
            {
                if (Regex.IsMatch(fileName, ".*[_\\- \\.]Linux.*", RegexOptions.IgnoreCase) || Regex.IsMatch(fileName, "^Linux[_\\- \\.].*", RegexOptions.IgnoreCase))
                {
                    return Result(fileName);
                }
                return null;
            }

            private static ISO Result(string fileName)
            {
                string name = "Linux " + FileTypeToReadableString(fileName);
                return new ISO
                {
                    Name = name,
                    ShortDescription = "Unrecognized GNU/Linux " + FileTypeToReadableString(fileName, titleCase: false) + " file",
                    Title = "GNU/" + name,
                    Username = "Unknown"
                };
            }
        }

        public static class Unknown
        {
            public static ISO TryGetInfo(string fileName)
            {
                string type = FileTypeToReadableString(fileName, titleCase: false);
                return new ISO
                {
                    Name = ((type == "ISO") ? "ISO File" : "Image File"),
                    ShortDescription = "Unrecognized " + type + " file",
                    Title = ((type == "ISO") ? "Unrecognized ISO File" : "Unrecognized Image File"),
                    Username = "Unknown"
                };
            }
        }

        private struct InfoFile
        {
            internal string Name;

            internal string Version;

            internal ImageArchitecture? Architecture;
        }

        private class CDFileStreamReader : IDisposable
        {
            private SparseStream _cdFileStream;

            public StreamReader Reader { get; }

            public CDFileStreamReader(CDReader reader, string filePath)
            {
                _cdFileStream = reader.OpenFile(filePath, FileMode.Open, FileAccess.Read);
                Reader = new StreamReader(_cdFileStream);
            }

            public string ReadLine()
            {
                return Reader.ReadLine();
            }

            public void Dispose()
            {
                StreamReader reader = Reader;
                if (reader != null)
                {
                    reader.Dispose();
                }
                SparseStream cdFileStream = _cdFileStream;
                if (cdFileStream == null)
                {
                    return;
                }
                cdFileStream.Dispose();
            }
        }

        public enum ImageArchitecture
        {
            x86,
            x64,
            Arm32,
            Arm64
        }

        private class IniParser
        {
            private Dictionary<string, Dictionary<string, string>> data;

            public IniParser(StreamReader reader)
            {
                data = ParseFile(reader);
            }

            public string TryGetValue(string section, string key)
            {
                if (!data.ContainsKey(section))
                {
                    return null;
                }
                if (!data[section].TryGetValue(key, out var result))
                {
                    return null;
                }
                return result;
            }

            private Dictionary<string, Dictionary<string, string>> ParseFile(StreamReader reader)
            {
                Dictionary<string, Dictionary<string, string>> result = new Dictionary<string, Dictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase);
                string currentSection = "";
                while (true)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2);
                        result[currentSection] = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                        continue;
                    }
                    string[] keyValue = trimmed.Split(new char[1] { '=' }, 2);
                    if (keyValue.Length == 2)
                    {
                        result[currentSection][keyValue[0].Trim()] = keyValue[1].Trim();
                    }
                }
                return result;
            }
        }

        public static readonly IOSParser[] OSParsers = new IOSParser[29]
        {
        new Ubuntu(),
        new Mint(),
        new Debian(),
        new Fedora(),
        new Arch(),
        new Manjaro(),
        new OpenSUSE(),
        new Zorin(),
        new Gentoo(),
        new CentOS(),
        new Kali(),
        new PopOS(),
        new Void(),
        new Solus(),
        new Garuda(),
        new MX(),
        new Lubuntu(),
        new RHEL(),
        new Slackware(),
        new Rocky(),
        new Kubuntu(),
        new elementaryOS(),
        new pfSense(),
        new opnSense(),
        new UNRAID(),
        new TrueNAS(),
        new QubesOS(),
        new SteamOS(),
        new MemTest86()
        };

        private static InfoFile GetInfoFile(CDReader reader)
        {
            InfoFile result = default(InfoFile);
            string line = GetLineFromFile(reader, ".disk\\info", 1u);
            if (line != null)
            {
                string[] split = line.Split(' ');
                if (split.Length != 0 && split[0].Length <= 13)
                {
                    result.Name = split[0];
                }
                if (split.Length > 1 && split[1].Length <= 16)
                {
                    string version = split[1];
                    if (version.Equals("GNU/Linux", StringComparison.OrdinalIgnoreCase) && split.Length > 2)
                    {
                        version = split[2];
                    }
                    if (version.Equals("OS", StringComparison.OrdinalIgnoreCase))
                    {
                        if (split.Length > 2)
                        {
                            version = split[2];
                        }
                        result.Name += "OS";
                    }
                    if (Regex.IsMatch(version, "[0-9\\.\\-_(rc)]+"))
                    {
                        result.Version = version;
                    }
                }
                result.Architecture = TryGetArchitectureFromLine(line);
            }
            return result;
        }

        private static string GetLineFromFile(CDReader reader, string filePath, uint line)
        {
            if (!(reader).FileExists(filePath))
            {
                return null;
            }
            SparseStream stream = (reader).OpenFile(filePath, FileMode.Open, FileAccess.Read);
            try
            {
                using StreamReader readStream = new StreamReader(stream);
                for (int i = 0; i < line - 1; i++)
                {
                    readStream.ReadLine();
                }
                return readStream.ReadLine();
            }
            finally
            {
                ((IDisposable)stream)?.Dispose();
            }
        }

        private static CDFileStreamReader GetFileReader(CDReader reader, string filePath)
        {
            if (!(reader).FileExists(filePath))
            {
                return null;
            }
            return new CDFileStreamReader(reader, filePath);
        }

        private static string[] GetRegex(string input, string regex, RegexOptions options = RegexOptions.IgnoreCase)
        {
            Match match = new Regex(regex, options).Match(input);
            if (match.Success)
            {
                string[] result = new string[Math.Max(0, match.Groups.Count - 1)];
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    result[i - 1] = match.Groups[i].Value;
                }
                return result;
            }
            return null;
        }

        private static bool ContainsSegment(string input, string segment, RegexOptions options = RegexOptions.IgnoreCase)
        {
            return GetRegex(input, ".*(^|[\\-_ \\.])" + Regex.Escape(segment) + "($|[\\-_ \\.]).*", options) != null;
        }

        private static string RemoveSegment(string input, string segment, RegexOptions options = RegexOptions.IgnoreCase)
        {
            if (segment == null)
            {
                return input;
            }
            string[] matches = GetRegex(input, "(.*)(^|[\\-_ \\.])" + Regex.Escape(segment) + "($|[\\-_ \\.])(.*)", options);
            if (matches == null)
            {
                return input;
            }
            return matches[0] + matches[3];
        }

        private static string ImageArchitectureToReadable(ImageArchitecture arch)
        {
            return arch.ToString().Replace("_", "/");
        }

        private static ImageArchitecture? TryParseArchitecture(string arch)
        {
            return arch.ToLower() switch
            {
                "amd64" => ImageArchitecture.x64,
                "x64" => ImageArchitecture.x64,
                "x86_64" => ImageArchitecture.x64,
                "86x64" => ImageArchitecture.x64,
                "amd64_intel_36" => ImageArchitecture.x64,
                "64bit" => ImageArchitecture.x64,
                "i386" => ImageArchitecture.x86,
                "x86" => ImageArchitecture.x86,
                "32bit" => ImageArchitecture.x86,
                "arm64" => ImageArchitecture.Arm64,
                "arm32" => ImageArchitecture.Arm32,
                "armel" => ImageArchitecture.Arm32,
                "armhf" => ImageArchitecture.Arm32,
                _ => null,
            };
        }

        private static ImageArchitecture? TryGetArchitectureFromLine(string line)
        {
            string[] array = new string[13]
            {
            "amd64", "x64", "x86_64", "86x64", "amd64_intel_36", "64bit", "i386", "x86", "32bit", "arm64",
            "arm32", "armel", "armhf"
            };
            foreach (string match in array)
            {
                if (ContainsSegment(line, match))
                {
                    return TryParseArchitecture(match);
                }
            }
            return null;
        }

        public static string FileTypeToReadableString(string fileName, bool titleCase = true)
        {
            if (!Regex.IsMatch(fileName, ".*(\\.iso|\\.iso\\.gz|\\.iso\\.bz2|\\.iso\\.bzip2)$", RegexOptions.IgnoreCase))
            {
                if (!titleCase)
                {
                    return "image";
                }
                return "Image";
            }
            return "ISO";
        }
    }
}
