using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Core;
using Microsoft.Win32;

namespace KTWirzade.Shared.Settings
{
    /// <summary>
    /// Post-installation settings panel for KT WIRZADE.
    /// Provides system configuration options similar to AME Settings CLI.
    /// </summary>
    public static class SettingsPanel
    {
        #region User Management

        /// <summary>
        /// Changes the current user's password.
        /// </summary>
        public static bool ChangePassword(string username, string newPassword)
        {
            try
            {
                var psi = new ProcessStartInfo("net", $"user \"{username}\" \"{newPassword}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to change password.");
                return false;
            }
        }

        /// <summary>
        /// Renames a user account.
        /// </summary>
        public static bool RenameUser(string currentUsername, string newUsername)
        {
            try
            {
                var psi = new ProcessStartInfo("wmic", $"useraccount where \"name='{currentUsername}'\" rename '{newUsername}'")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to rename user.");
                return false;
            }
        }

        /// <summary>
        /// Elevates a user to administrator.
        /// </summary>
        public static bool ElevateUser(string username)
        {
            try
            {
                var psi = new ProcessStartInfo("net", $"localgroup administrators \"{username}\" /add")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to elevate user.");
                return false;
            }
        }

        /// <summary>
        /// De-elevates a user from administrator.
        /// </summary>
        public static bool DeElevateUser(string username)
        {
            try
            {
                var psi = new ProcessStartInfo("net", $"localgroup administrators \"{username}\" /delete")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to de-elevate user.");
                return false;
            }
        }

        #endregion

        #region System Toggles

        /// <summary>
        /// Enables or disables Windows Script Host (WSH).
        /// </summary>
        public static bool SetWindowsScriptHost(bool enabled)
        {
            try
            {
                // HKCU
                using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows Script Host\Settings"))
                {
                    key.SetValue("Enabled", enabled ? 1 : 0, RegistryValueKind.DWord);
                }

                // HKLM
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows Script Host\Settings"))
                {
                    key.SetValue("Enabled", enabled ? 1 : 0, RegistryValueKind.DWord);
                }

                return true;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to set WSH state.");
                return false;
            }
        }

        /// <summary>
        /// Enables or disables VBScript file association.
        /// </summary>
        public static bool SetVBScriptAssociation(bool enabled)
        {
            try
            {
                if (enabled)
                {
                    var psi = new ProcessStartInfo("cmd", "/c assoc .vbs=VBSFile")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var proc = Process.Start(psi);
                    proc.WaitForExit();
                }
                else
                {
                    var psi = new ProcessStartInfo("cmd", "/c assoc .vbs=")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var proc = Process.Start(psi);
                    proc.WaitForExit();
                }

                return true;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to set VBScript association.");
                return false;
            }
        }

        /// <summary>
        /// Enables or disables NCSI Active Probing.
        /// </summary>
        public static bool SetNCSIActiveProbing(bool enabled)
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\NlaSvc\Parameters\Internet");
                key.SetValue("EnableActiveProbing", enabled ? 1 : 0, RegistryValueKind.DWord);
                return true;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to set NCSI probing.");
                return false;
            }
        }

        /// <summary>
        /// Enables or disables hibernation.
        /// </summary>
        public static bool SetHibernation(bool enabled)
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", enabled ? "/HIBERNATE /TYPE FULL" : "/HIBERNATE OFF")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to set hibernation.");
                return false;
            }
        }

        /// <summary>
        /// Enables or disables the Notification Center.
        /// </summary>
        public static bool SetNotificationCenter(bool enabled)
        {
            try
            {
                var sid = WindowsIdentity.GetCurrent().User?.Value;
                if (string.IsNullOrEmpty(sid))
                    return false;

                using var key = Registry.Users.CreateSubKey($@"{sid}\Software\Policies\Microsoft\Windows\Explorer");
                key.SetValue("DisableNotificationCenter", enabled ? 0 : 1, RegistryValueKind.DWord);
                return true;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to set Notification Center.");
                return false;
            }
        }

        /// <summary>
        /// Enables or disables desktop toast notifications.
        /// </summary>
        public static bool SetDesktopNotifications(bool enabled)
        {
            try
            {
                var sid = WindowsIdentity.GetCurrent().User?.Value;
                if (string.IsNullOrEmpty(sid))
                    return false;

                using var key = Registry.Users.CreateSubKey($@"{sid}\SOFTWARE\Microsoft\Windows\CurrentVersion\PushNotifications");
                key.SetValue("ToastEnabled", enabled ? 1 : 0, RegistryValueKind.DWord);
                return true;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to set desktop notifications.");
                return false;
            }
        }

        /// <summary>
        /// Enables or disables the username login requirement.
        /// </summary>
        public static bool SetUsernameLoginRequired(bool required)
        {
            try
            {
                if (required)
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
                    if (key != null)
                    {
                        key.DeleteValue("dontdisplaylastusername", false);
                    }
                }
                else
                {
                    using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                    key.SetValue("dontdisplaylastusername", 1, RegistryValueKind.DWord);
                }

                return true;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to set username login requirement.");
                return false;
            }
        }

        /// <summary>
        /// Enables or disables AutoLogon for the current user.
        /// </summary>
        public static bool SetAutoLogon(bool enabled, string username = null, string password = null)
        {
            try
            {
                if (enabled)
                {
                    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                        return false;

                    using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon");
                    key.SetValue("AutoAdminLogon", "1", RegistryValueKind.String);
                    key.SetValue("DefaultUserName", username, RegistryValueKind.String);
                    key.SetValue("DefaultPassword", password, RegistryValueKind.String);
                }
                else
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", true);
                    if (key != null)
                    {
                        key.SetValue("AutoAdminLogon", "0", RegistryValueKind.String);
                        key.DeleteValue("DefaultPassword", false);
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to set AutoLogon.");
                return false;
            }
        }

        /// <summary>
        /// Enables or disables .NET 3.5 (requires Windows ISO or boot drive).
        /// </summary>
        public static bool SetDotNet35(bool enabled, string windowsIsoPath = null)
        {
            try
            {
                if (enabled)
                {
                    if (string.IsNullOrEmpty(windowsIsoPath) || !File.Exists(Path.Combine(windowsIsoPath, @"sources\sxs\microsoft-windows-netfx3-ondemand-package.cab")))
                    {
                        // Try DISM online install
                        var psi = new ProcessStartInfo("dism", "/online /enable-feature /featurename:NetFx3 /all")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        using var proc = Process.Start(psi);
                        proc.WaitForExit();
                        return proc.ExitCode == 0;
                    }
                    else
                    {
                        var psi = new ProcessStartInfo("dism", $"/online /enable-feature /featurename:NetFx3 /all /source:\"{windowsIsoPath}\\sources\\sxs\" /limitaccess")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        using var proc = Process.Start(psi);
                        proc.WaitForExit();
                        return proc.ExitCode == 0;
                    }
                }
                else
                {
                    var psi = new ProcessStartInfo("dism", "/online /disable-feature /featurename:NetFx3")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var proc = Process.Start(psi);
                    proc.WaitForExit();
                    return proc.ExitCode == 0;
                }
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to set .NET 3.5.");
                return false;
            }
        }

        #endregion

        #region Profile & Personalization

        /// <summary>
        /// Changes the user's profile image (PFP).
        /// </summary>
        public static bool SetProfileImage(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                    return false;

                var sid = WindowsIdentity.GetCurrent().User?.Value;
                if (string.IsNullOrEmpty(sid))
                    return false;

                // Copy image to account pictures location
                var accountPicturesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Microsoft", "User Account Pictures", $"{sid}.bmp");

                Directory.CreateDirectory(Path.GetDirectoryName(accountPicturesPath));

                // Convert and resize image to required sizes
                ConvertAndSaveImage(imagePath, accountPicturesPath, 448, 448);

                // Set registry values
                using var key = Registry.Users.CreateSubKey($@"{sid}\Software\Microsoft\Windows\CurrentVersion\AccountPicture");
                key.SetValue("SourceImage", accountPicturesPath, RegistryValueKind.String);

                return true;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to set profile image.");
                return false;
            }
        }

        /// <summary>
        /// Changes the lock screen image.
        /// </summary>
        public static bool SetLockScreenImage(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                    return false;

                var sid = WindowsIdentity.GetCurrent().User?.Value;
                if (string.IsNullOrEmpty(sid))
                    return false;

                // Copy to lock screen assets
                var lockScreenPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Microsoft", "Windows", "SystemData", $"{sid}", "ReadOnly", "LockScreen_Z");

                // Take ownership and replace
                var destPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Windows", "Themes", "TranscodedWallpaper");

                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                File.Copy(imagePath, destPath, true);

                // Update registry
                using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop");
                key.SetValue("Wallpaper", destPath, RegistryValueKind.String);

                return true;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to set lock screen image.");
                return false;
            }
        }

        private static void ConvertAndSaveImage(string sourcePath, string destPath, int width, int height)
        {
            try
            {
                using var bitmap = new System.Drawing.Bitmap(sourcePath);
                using var resized = new System.Drawing.Bitmap(bitmap, new System.Drawing.Size(width, height));
                resized.Save(destPath, System.Drawing.Imaging.ImageFormat.Bmp);
            }
            catch
            {
                // Fallback: just copy the file
                File.Copy(sourcePath, destPath, true);
            }
        }

        #endregion

        #region Keyboard Language

        /// <summary>
        /// Adds a keyboard language input method.
        /// </summary>
        public static bool AddKeyboardLanguage(string languageRegionId, string keyboardIdentifier, bool setAsDefault = false)
        {
            try
            {
                var tip = $"{languageRegionId}:{keyboardIdentifier}";

                var encodedAdd = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(
                    $"$NewLangs=Get-WinUserLanguageList; $NewLangs[0].InputMethodTips.Add('{tip}'); Set-WinUserLanguageList $NewLangs -Force"));
                var psi = new ProcessStartInfo("powershell",
                    $"-NoProfile -ExecutionPolicy Bypass -NonInteractive -EncodedCommand {encodedAdd}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                proc.WaitForExit();

                if (setAsDefault && proc.ExitCode == 0)
                {
                    var encodedDefault = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(
                        $"Set-WinDefaultInputMethodOverride -InputTip '{tip}'"));
                    var psi2 = new ProcessStartInfo("powershell",
                        $"-NoProfile -ExecutionPolicy Bypass -NonInteractive -EncodedCommand {encodedDefault}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var proc2 = Process.Start(psi2);
                    proc2.WaitForExit();
                }

                return proc.ExitCode == 0;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to add keyboard language.");
                return false;
            }
        }

        /// <summary>
        /// Removes a keyboard language input method.
        /// </summary>
        public static bool RemoveKeyboardLanguage(string languageRegionId, string keyboardIdentifier)
        {
            try
            {
                var tip = $"{languageRegionId}:{keyboardIdentifier}";

                var encodedRemove = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(
                    $"$NewLangs=Get-WinUserLanguageList; $NewLangs[0].InputMethodTips.Remove('{tip}'); Set-WinUserLanguageList $NewLangs -Force"));
                var psi = new ProcessStartInfo("powershell",
                    $"-NoProfile -ExecutionPolicy Bypass -NonInteractive -EncodedCommand {encodedRemove}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch (Exception e)
            {
                Log.WriteExceptionSafe(e, "SettingsPanel: Failed to remove keyboard language.");
                return false;
            }
        }

        #endregion
    }
}
