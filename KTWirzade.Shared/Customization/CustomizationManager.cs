using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Core;
using JetBrains.Annotations;
using Microsoft.Win32;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KTWirzade.Shared.Customization
{
    /// <summary>
    /// Defines what customizations an APBX playbook supports.
    /// The user can override these values before the playbook is applied.
    /// </summary>
    public class CustomizationProfile
    {
        /// <summary>
        /// Username for the local account. If null, the playbook doesn't customize this.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Path to profile picture (relative to APBX or absolute).
        /// </summary>
        public string ProfilePicturePath { get; set; }

        /// <summary>
        /// Path to desktop wallpaper (relative to APBX or absolute).
        /// </summary>
        public string WallpaperPath { get; set; }

        /// <summary>
        /// Path to lockscreen image (relative to APBX or absolute).
        /// </summary>
        public string LockscreenPath { get; set; }

        /// <summary>
        /// Account description shown on login screen.
        /// </summary>
        public string AccountDescription { get; set; }

        /// <summary>
        /// Whether to set the account password.
        /// </summary>
        public string AccountPassword { get; set; }

        /// <summary>
        /// Whether to elevate the account to administrator.
        /// </summary>
        public bool? ElevateToAdmin { get; set; }

        /// <summary>
        /// Custom computer name/hostname.
        /// </summary>
        public string ComputerName { get; set; }

        /// <summary>
        /// Custom accent color (hex format like "#FF5733").
        /// </summary>
        public string AccentColor { get; set; }

        /// <summary>
        /// Whether to enable/disable auto-logon.
        /// </summary>
        public bool? AutoLogon { get; set; }

        /// <summary>
        /// Theme mode: "dark", "light", or "system".
        /// </summary>
        public string ThemeMode { get; set; }

        /// <summary>
        /// Custom registry values to apply.
        /// </summary>
        public Dictionary<string, RegistryCustomization> CustomRegistry { get; set; }

        /// <summary>
        /// Returns true if this profile has any customizable fields defined.
        /// </summary>
        public bool HasCustomizations =>
            !string.IsNullOrEmpty(Username) ||
            !string.IsNullOrEmpty(ProfilePicturePath) ||
            !string.IsNullOrEmpty(WallpaperPath) ||
            !string.IsNullOrEmpty(LockscreenPath) ||
            !string.IsNullOrEmpty(AccountDescription) ||
            !string.IsNullOrEmpty(AccountPassword) ||
            ElevateToAdmin.HasValue ||
            !string.IsNullOrEmpty(ComputerName) ||
            !string.IsNullOrEmpty(AccentColor) ||
            AutoLogon.HasValue ||
            !string.IsNullOrEmpty(ThemeMode) ||
            (CustomRegistry != null && CustomRegistry.Count > 0);
    }

    /// <summary>
    /// Registry customization entry.
    /// </summary>
    public class RegistryCustomization
    {
        public string KeyPath { get; set; }
        public string ValueName { get; set; }
        public object Value { get; set; }
        public string ValueKind { get; set; } = "String"; // String, DWord, QWord, Binary, ExpandString, MultiString
    }

    /// <summary>
    /// User's choices for customization. Values null = use APBX default.
    /// </summary>
    public class UserCustomizationChoices
    {
        public string Username { get; set; }
        public bool UseUsername { get; set; }

        public string ProfilePicturePath { get; set; }
        public bool UseProfilePicture { get; set; }

        public string WallpaperPath { get; set; }
        public bool UseWallpaper { get; set; }

        public string LockscreenPath { get; set; }
        public bool UseLockscreen { get; set; }

        public string AccountDescription { get; set; }
        public bool UseAccountDescription { get; set; }

        public string AccountPassword { get; set; }
        public bool UseAccountPassword { get; set; }

        public bool? ElevateToAdmin { get; set; }
        public bool? UseElevateToAdmin { get; set; }

        public string ComputerName { get; set; }
        public bool UseComputerName { get; set; }

        public string AccentColor { get; set; }
        public bool UseAccentColor { get; set; }

        public bool? AutoLogon { get; set; }
        public bool? UseAutoLogon { get; set; }

        public string ThemeMode { get; set; }
        public bool UseThemeMode { get; set; }
    }

    /// <summary>
    /// Manages APBX customizations - reads from playbook and applies user choices.
    /// </summary>
    public static class CustomizationManager
    {
        private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        /// <summary>
        /// Loads the customization profile from an APBX's customizations.yml file.
        /// </summary>
        [CanBeNull]
        public static CustomizationProfile LoadProfile(string playbookPath)
        {
            try
            {
                var configPath = Path.Combine(playbookPath, "Configuration", "customizations.yml");
                if (!File.Exists(configPath))
                {
                    // Try alternative names
                    configPath = Path.Combine(playbookPath, "Configuration", "customize.yml");
                    if (!File.Exists(configPath))
                        return null;
                }

                var yaml = File.ReadAllText(configPath);
                var profile = YamlDeserializer.Deserialize<CustomizationProfile>(yaml);

                // Resolve relative paths to absolute paths
                ResolvePaths(profile, playbookPath);

                return profile;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "CustomizationManager: Failed to load profile.");
                return null;
            }
        }

        /// <summary>
        /// Applies user customization choices after the APBX has been executed.
        /// </summary>
        public static async Task<bool> ApplyCustomizations(
            CustomizationProfile profile,
            UserCustomizationChoices choices,
            string playbookPath,
            IProgress<string> progress = null)
        {
            try
            {
                if (profile == null || choices == null)
                    return true; // Nothing to customize

                // Apply username
                if (choices.UseUsername && !string.IsNullOrEmpty(choices.Username))
                {
                    progress?.Report($"Setting username to '{choices.Username}'...");
                    await SetUsername(choices.Username);
                }
                else if (!string.IsNullOrEmpty(profile.Username))
                {
                    progress?.Report($"Setting username to '{profile.Username}' (playbook default)...");
                    await SetUsername(profile.Username);
                }

                // Apply profile picture
                if (choices.UseProfilePicture && !string.IsNullOrEmpty(choices.ProfilePicturePath))
                {
                    progress?.Report("Setting custom profile picture...");
                    await SetProfilePicture(choices.ProfilePicturePath);
                }
                else if (!string.IsNullOrEmpty(profile.ProfilePicturePath))
                {
                    progress?.Report("Setting profile picture (playbook default)...");
                    await SetProfilePicture(profile.ProfilePicturePath);
                }

                // Apply wallpaper
                if (choices.UseWallpaper && !string.IsNullOrEmpty(choices.WallpaperPath))
                {
                    progress?.Report("Setting custom wallpaper...");
                    await SetWallpaper(choices.WallpaperPath);
                }
                else if (!string.IsNullOrEmpty(profile.WallpaperPath))
                {
                    progress?.Report("Setting wallpaper (playbook default)...");
                    await SetWallpaper(profile.WallpaperPath);
                }

                // Apply lockscreen
                if (choices.UseLockscreen && !string.IsNullOrEmpty(choices.LockscreenPath))
                {
                    progress?.Report("Setting custom lockscreen...");
                    await SetLockscreen(choices.LockscreenPath);
                }
                else if (!string.IsNullOrEmpty(profile.LockscreenPath))
                {
                    progress?.Report("Setting lockscreen (playbook default)...");
                    await SetLockscreen(profile.LockscreenPath);
                }

                // Apply account description
                if (choices.UseAccountDescription && !string.IsNullOrEmpty(choices.AccountDescription))
                {
                    progress?.Report($"Setting account description...");
                    await SetAccountDescription(choices.AccountDescription);
                }
                else if (!string.IsNullOrEmpty(profile.AccountDescription))
                {
                    progress?.Report("Setting account description (playbook default)...");
                    await SetAccountDescription(profile.AccountDescription);
                }

                // Apply password
                if (choices.UseAccountPassword && !string.IsNullOrEmpty(choices.AccountPassword))
                {
                    progress?.Report("Setting account password...");
                    await SetPassword(choices.Username ?? profile.Username, choices.AccountPassword);
                }
                else if (!string.IsNullOrEmpty(profile.AccountPassword) && !string.IsNullOrEmpty(profile.Username))
                {
                    progress?.Report("Setting account password (playbook default)...");
                    await SetPassword(profile.Username, profile.AccountPassword);
                }

                // Apply elevation
                bool elevate = false;
                if (choices.UseElevateToAdmin.HasValue)
                    elevate = choices.UseElevateToAdmin.Value && profile.ElevateToAdmin != false;
                else if (profile.ElevateToAdmin.HasValue)
                    elevate = profile.ElevateToAdmin.Value;

                if (elevate && !string.IsNullOrEmpty(choices.Username ?? profile.Username))
                {
                    progress?.Report("Elevating account to administrator...");
                    await ElevateUser(choices.Username ?? profile.Username);
                }

                // Apply computer name
                if (choices.UseComputerName && !string.IsNullOrEmpty(choices.ComputerName))
                {
                    progress?.Report($"Setting computer name to '{choices.ComputerName}'...");
                    await SetComputerName(choices.ComputerName);
                }
                else if (!string.IsNullOrEmpty(profile.ComputerName))
                {
                    progress?.Report($"Setting computer name to '{profile.ComputerName}' (playbook default)...");
                    await SetComputerName(profile.ComputerName);
                }

                // Apply accent color
                if (choices.UseAccentColor && !string.IsNullOrEmpty(choices.AccentColor))
                {
                    progress?.Report("Setting custom accent color...");
                    await SetAccentColor(choices.AccentColor);
                }
                else if (!string.IsNullOrEmpty(profile.AccentColor))
                {
                    progress?.Report("Setting accent color (playbook default)...");
                    await SetAccentColor(profile.AccentColor);
                }

                // Apply auto-logon
                bool autoLogon = false;
                if (choices.UseAutoLogon.HasValue)
                    autoLogon = choices.UseAutoLogon.Value;
                else if (profile.AutoLogon.HasValue)
                    autoLogon = profile.AutoLogon.Value;

                if (autoLogon)
                {
                    progress?.Report("Enabling auto-logon...");
                    string user = choices.Username ?? profile.Username;
                    string pass = choices.AccountPassword ?? profile.AccountPassword;
                    await SetAutoLogon(true, user, pass);
                }

                // Apply theme mode
                if (choices.UseThemeMode && !string.IsNullOrEmpty(choices.ThemeMode))
                {
                    progress?.Report($"Setting theme to '{choices.ThemeMode}'...");
                    await SetThemeMode(choices.ThemeMode);
                }
                else if (!string.IsNullOrEmpty(profile.ThemeMode))
                {
                    progress?.Report($"Setting theme to '{profile.ThemeMode}' (playbook default)...");
                    await SetThemeMode(profile.ThemeMode);
                }

                // Apply custom registry values
                if (profile.CustomRegistry != null && profile.CustomRegistry.Count > 0)
                {
                    progress?.Report("Applying custom registry values...");
                    foreach (var reg in profile.CustomRegistry)
                    {
                        await ApplyRegistryCustomization(reg.Key, reg.Value);
                    }
                }

                progress?.Report("Customizations applied successfully!");
                return true;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "CustomizationManager: Failed to apply customizations.");
                return false;
            }
        }

        #region Private Application Methods

        private static void ResolvePaths(CustomizationProfile profile, string playbookPath)
        {
            if (!string.IsNullOrEmpty(profile.ProfilePicturePath) && !Path.IsPathRooted(profile.ProfilePicturePath))
            {
                var resolved = Path.Combine(playbookPath, profile.ProfilePicturePath);
                if (File.Exists(resolved))
                    profile.ProfilePicturePath = resolved;
            }

            if (!string.IsNullOrEmpty(profile.WallpaperPath) && !Path.IsPathRooted(profile.WallpaperPath))
            {
                var resolved = Path.Combine(playbookPath, profile.WallpaperPath);
                if (File.Exists(resolved))
                    profile.WallpaperPath = resolved;
            }

            if (!string.IsNullOrEmpty(profile.LockscreenPath) && !Path.IsPathRooted(profile.LockscreenPath))
            {
                var resolved = Path.Combine(playbookPath, profile.LockscreenPath);
                if (File.Exists(resolved))
                    profile.LockscreenPath = resolved;
            }
        }

        private static async Task SetUsername(string newUsername)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(newUsername) || newUsername.Length > 20)
                        return;

                    var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split('\\')[1];
                    var safeCurrent = currentUser.Replace("'", "''");
                    var safeNew = newUsername.Replace("'", "''");

                    var psi = new System.Diagnostics.ProcessStartInfo("wmic",
                        $"useraccount where \"name='{safeCurrent}'\" rename '{safeNew}'")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };

                    using var proc = System.Diagnostics.Process.Start(psi);
                    proc.WaitForExit();
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(e, "CustomizationManager: Failed to set username.");
                }
            });
        }

        private static async Task SetProfilePicture(string imagePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(imagePath))
                        return;

                    // Use the SettingsPanel method
                    Settings.SettingsPanel.SetProfileImage(imagePath);
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(e, "CustomizationManager: Failed to set profile picture.");
                }
            });
        }

        private static async Task SetWallpaper(string imagePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(imagePath))
                        return;

                    // Copy to Windows wallpaper location
                    var destPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Microsoft", "Windows", "Themes", "TranscodedWallpaper");

                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    File.Copy(imagePath, destPath, true);

                    // Set registry values
                    using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop");
                    key.SetValue("Wallpaper", destPath, RegistryValueKind.String);
                    key.SetValue("WallpaperStyle", "10", RegistryValueKind.String); // Fill
                    key.SetValue("TileWallpaper", "0", RegistryValueKind.String);

                    // Refresh wallpaper
                    Native.SystemParametersInfo(Native.SPI_SETDESKWALLPAPER, 0, destPath, Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE);
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(e, "CustomizationManager: Failed to set wallpaper.");
                }
            });
        }

        private static async Task SetLockscreen(string imagePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(imagePath))
                        return;

                    Settings.SettingsPanel.SetLockScreenImage(imagePath);
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(e, "CustomizationManager: Failed to set lockscreen.");
                }
            });
        }

        private static async Task SetAccountDescription(string description)
        {
            await Task.Run(() =>
            {
                try
                {
                    var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split('\\')[1];
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_UserAccount WHERE Name = '{currentUser}'");

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        obj["Description"] = description;
                        obj.Put();
                    }
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(e, "CustomizationManager: Failed to set account description.");
                }
            });
        }

        private static async Task SetPassword(string username, string password)
        {
            await Task.Run(() =>
            {
                try
                {
                    Settings.SettingsPanel.ChangePassword(username, password);
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(e, "CustomizationManager: Failed to set password.");
                }
            });
        }

        private static async Task ElevateUser(string username)
        {
            await Task.Run(() =>
            {
                try
                {
                    Settings.SettingsPanel.ElevateUser(username);
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(e, "CustomizationManager: Failed to elevate user.");
                }
            });
        }

        private static async Task SetComputerName(string computerName)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Sanitize computer name (max 15 chars, alphanumeric and hyphens)
                    var sanitized = Regex.Replace(computerName, @"[^a-zA-Z0-9-]", "");
                    if (sanitized.Length > 15)
                        sanitized = sanitized.Substring(0, 15);

                    // Use WMI to rename computer
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        // Use the Rename method via InvokeMethod
                        var methodParams = obj.GetMethodParameters("Rename");
                        methodParams["Name"] = sanitized;
                        methodParams["Password"] = null;
                        methodParams["UserName"] = null;
                        obj.InvokeMethod("Rename", methodParams, null);
                    }
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(e, "CustomizationManager: Failed to set computer name.");
                }
            });
        }

        private static async Task SetAccentColor(string hexColor)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Parse hex color
                    if (hexColor.StartsWith("#"))
                        hexColor = hexColor.Substring(1);

                    if (hexColor.Length != 6)
                        return;

                    byte r = Convert.ToByte(hexColor.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hexColor.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hexColor.Substring(4, 2), 16);

                    // Windows uses 0x00BBGGRR format
                    int color = b << 16 | g << 8 | r;

                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
                    key.SetValue("AccentColorMenu", color, RegistryValueKind.DWord);

                    // Also set the Immersive color
                    using var immersiveKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\History\Colors");
                    immersiveKey.SetValue("ColorHistory0", color, RegistryValueKind.DWord);
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(e, "CustomizationManager: Failed to set accent color.");
                }
            });
        }

        private static async Task SetAutoLogon(bool enabled, string username, string password)
        {
            await Task.Run(() =>
            {
                try
                {
                    Settings.SettingsPanel.SetAutoLogon(enabled, username, password);
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(e, "CustomizationManager: Failed to set auto-logon.");
                }
            });
        }

        private static async Task SetThemeMode(string mode)
        {
            await Task.Run(() =>
            {
                try
                {
                    bool darkMode = mode?.ToLowerInvariant() == "dark";

                    // Apps theme
                    using var appsKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    appsKey.SetValue("AppsUseLightTheme", darkMode ? 0 : 1, RegistryValueKind.DWord);

                    // System theme (taskbar, etc.)
                    using var systemKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    systemKey.SetValue("SystemUsesLightTheme", darkMode ? 0 : 1, RegistryValueKind.DWord);
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(e, "CustomizationManager: Failed to set theme mode.");
                }
            });
        }

        private static async Task ApplyRegistryCustomization(string name, RegistryCustomization reg)
        {
            await Task.Run(() =>
            {
                try
                {
                    RegistryValueKind kind = RegistryValueKind.String;
                    switch (reg.ValueKind?.ToLowerInvariant())
                    {
                        case "dword":
                            kind = RegistryValueKind.DWord;
                            break;
                        case "qword":
                            kind = RegistryValueKind.QWord;
                            break;
                        case "binary":
                            kind = RegistryValueKind.Binary;
                            break;
                        case "expandstring":
                            kind = RegistryValueKind.ExpandString;
                            break;
                        case "multistring":
                            kind = RegistryValueKind.MultiString;
                            break;
                        default:
                            kind = RegistryValueKind.String;
                            break;
                    }

                    // Determine root key from path
                    RegistryKey rootKey = Registry.LocalMachine;
                    var keyPath = reg.KeyPath;

                    if (keyPath.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase) ||
                        keyPath.StartsWith(@"HKEY_CURRENT_USER\", StringComparison.OrdinalIgnoreCase))
                    {
                        rootKey = Registry.CurrentUser;
                        keyPath = keyPath.Substring(keyPath.IndexOf('\\') + 1);
                    }
                    else if (keyPath.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase) ||
                             keyPath.StartsWith(@"HKEY_LOCAL_MACHINE\", StringComparison.OrdinalIgnoreCase))
                    {
                        rootKey = Registry.LocalMachine;
                        keyPath = keyPath.Substring(keyPath.IndexOf('\\') + 1);
                    }

                    using var key = rootKey.CreateSubKey(keyPath);
                    key.SetValue(reg.ValueName, reg.Value, kind);
                }
                catch (Exception e)
                {
                    Log.WriteExceptionSafe(e, $"CustomizationManager: Failed to apply registry customization '{name}'.");
                }
            });
        }

        #endregion

        #region Native Methods

        private static class Native
        {
            public const uint SPI_SETDESKWALLPAPER = 0x0014;
            public const uint SPIF_UPDATEINIFILE = 0x01;
            public const uint SPIF_SENDCHANGE = 0x02;

            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            public static extern int SystemParametersInfo(uint uAction, uint uParam, string lpvParam, uint fuWinIni);
        }

        #endregion
    }
}
