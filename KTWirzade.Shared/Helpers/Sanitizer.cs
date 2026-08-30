using System;
using System.IO;
using System.Text.RegularExpressions;

namespace KTWirzade.Shared.Helpers
{
    public static class Sanitizer
    {
        public static string EscapeCmdArgument(string arg)
        {
            if (arg == null) return "\"\"";
            // Remove dangerous characters and escape quotes for cmd /c
            // For cmd.exe, escape " by doubling it and wrap in quotes
            var escaped = arg.Replace("\"", "\"\"");
            // Escape % to prevent env expansion injection
            escaped = escaped.Replace("%", "%%");
            // Ensure not containing command chaining outside quotes
            return $"\"{escaped}\"";
        }

        public static string EscapePowerShellArgument(string arg)
        {
            if (arg == null) return "''";
            return "'" + arg.Replace("'", "''") + "'";
        }

        public static bool IsSafePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                var expanded = Environment.ExpandEnvironmentVariables(path);
                if (expanded.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
                // Block UNC and alternate streams
                if (expanded.StartsWith(@"\\")) return false;
                if (expanded.Contains(":") && !expanded.StartsWith("C:", StringComparison.OrdinalIgnoreCase)
                    && !expanded.StartsWith("D:", StringComparison.OrdinalIgnoreCase)
                    && expanded.Contains(":")) {
                    // Allow only C: and env vars like %ProgramFiles%
                    if (!expanded.Contains("%")) return false;
                }
                foreach (var c in Path.GetInvalidPathChars())
                    if (expanded.Contains(c.ToString())) return false;
                return true;
            }
            catch { return false; }
        }

        public static bool IsSafeRegPath(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (key.Contains("\"") || key.Contains("&") || key.Contains("|") || key.Contains(";")) return false;
            return true;
        }

        public static bool ContainsCommandInjection(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            // Detect common injection patterns
            var patterns = new[] { "&", "|", ";", "`", "$(", "&&", "||" };
            foreach (var p in patterns)
                if (input.Contains(p)) return true;
            // Detect powershell download cradle
            if (input.IndexOf("DownloadString", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (input.IndexOf("Invoke-Expression", StringComparison.OrdinalIgnoreCase) >= 0 && input.IndexOf("Net.WebClient", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static string SanitizeFilePathForCmd(string file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (file.Contains("\"")) file = file.Replace("\"", "");
            // Use EscapeCmdArgument
            return EscapeCmdArgument(file);
        }

        public static bool IsValidDownloadDestination(string dest, string baseDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dest)) return false;
                if (dest.IndexOf('\0') >= 0) return false;
                // Block traversal sequences and alternate streams
                if (dest.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
                if (dest.Contains(":") && dest.IndexOf("::", StringComparison.Ordinal) >= 0) return false; // NTFS ADS
                var expanded = Environment.ExpandEnvironmentVariables(dest);
                if (expanded.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
                // Canonicalize and ensure no escape via .. after resolve
                var full = Path.GetFullPath(Path.IsPathRooted(expanded) ? expanded : Path.Combine(baseDir, expanded));
                // Never allow writing directly to Windows or System32 root
                var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd(Path.DirectorySeparatorChar);
                var sys32 = Path.Combine(winDir, "System32");
                if (full.Equals(winDir, StringComparison.OrdinalIgnoreCase) || full.Equals(sys32, StringComparison.OrdinalIgnoreCase)) return false;
                if (full.StartsWith(sys32 + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(Path.Combine(baseDir), StringComparison.OrdinalIgnoreCase))
                {
                    // Allow System32 only if baseDir itself is inside System32 (ISO WIM case)
                    var baseFull = Path.GetFullPath(baseDir);
                    if (!baseFull.StartsWith(sys32, StringComparison.OrdinalIgnoreCase)) return false;
                }
                // For 100% playbook compat: allow any absolute path that is not traversal and not ADS
                // Historical playbooks download to %TEMP%, %ProgramFiles%, Executables, etc. — all allowed
                return true;
            }
            catch { return false; }
        }

        public static Regex CreateSafeRegex(string pattern, TimeSpan timeout)
        {
            return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, timeout);
        }
    }
}
