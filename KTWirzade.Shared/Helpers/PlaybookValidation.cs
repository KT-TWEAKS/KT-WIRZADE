using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KTWirzade.Shared.Helpers
{
    public static class PlaybookValidation
    {
        // Positive alternatives retain the legacy OR behavior; exclusions all apply.
        public static bool MatchesFilters(string[] filters, Func<string, bool> matches)
        {
            if (filters == null || filters.Length == 0) return true;
            if (filters.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Empty playbook filter.");
            var positive = filters.Where(f => !f.StartsWith("!")).ToArray();
            return (positive.Length == 0 || positive.Any(matches)) &&
                   filters.Where(f => f.StartsWith("!")).All(matches);
        }

        public static string ResolveInclude(string root, string file)
        {
            if (string.IsNullOrWhiteSpace(file) || Path.IsPathRooted(file) || file.Contains(":"))
                throw new InvalidDataException("YAML include must be a relative path: " + file);
            var basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Path.Combine(basePath, file));
            if (!path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("YAML include leaves Configuration: " + file);
            // Reject links/junctions in the entire resolved chain, including the root.
            for (var part = path; !string.IsNullOrEmpty(part); part = Path.GetDirectoryName(part))
                if ((File.Exists(part) || Directory.Exists(part)) &&
                    (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("YAML includes cannot traverse links: " + file);
            return path;
        }

        public static void EnterInclude(string path, ISet<string> active)
        {
            if (active.Count >= 64)
                throw new InvalidDataException("YAML include depth exceeds 64 files: " + path);
            if (!active.Add(path))
                throw new InvalidDataException("Circular YAML include: " + path);
        }
    }
}
