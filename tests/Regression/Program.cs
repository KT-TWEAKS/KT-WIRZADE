using System;
using KTWirzade.Shared.Actions;
using KTWirzade.Shared.Parser;
using KTWirzade.Shared.Tasks;
using KTWirzade.Shared.Updates;
using Newtonsoft.Json;

internal static class Program
{
    private static int passed;

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        passed++;
        Console.WriteLine("PASS " + name);
    }

    private static void RejectDownload(DownloadAction action, string name)
    {
        try { action.RunTask(null).GetAwaiter().GetResult(); }
        catch (ArgumentException) { Check(true, name); return; }
        throw new Exception(name + " was not rejected");
    }

    private static int Main()
    {
        try
        {
            // GitHub's wire format, including fields that differ from C# names.
            var release = JsonConvert.DeserializeObject<GitHubRelease>(
                "{\"tag_name\":\"v1.0.1\",\"name\":\"Release\",\"body\":\"Fixes\","
                + "\"html_url\":\"https://github.com/KT-TWEAKS/KT-WIRZADE/releases/tag/v1.0.1\","
                + "\"published_at\":\"2026-09-06T12:00:00Z\",\"prerelease\":false,"
                + "\"assets\":[{\"name\":\"KT-WIRZADE-win-x64.zip\",\"size\":123,"
                + "\"browser_download_url\":\"https://example.invalid/release.zip\"}]}");
            Check(release.TagName == "v1.0.1", "GitHub tag_name");
            Check(release.HtmlUrl.EndsWith("/v1.0.1"), "GitHub html_url");
            Check(release.PublishedAt.Year == 2026, "GitHub published_at");
            Check(release.Assets[0].BrowserDownloadUrl == "https://example.invalid/release.zip", "GitHub asset URL");
            Check(UpdateChecker.IsNewerVersion("1.0.1", "1.0.0"), "new patch available");
            Check(!UpdateChecker.IsNewerVersion("1.0.0", "1.0.0"), "same version not an update");
            Check(!UpdateChecker.IsNewerVersion("0.9.9", "1.0.0"), "older version not an update");

            foreach (var tag in new[] { "!registryKey", "!regKey", "!registryKey:", "!regKey:" })
                Check(PlaybookParser.Deserializer.Deserialize<UninstallTask>("actions:\n  - " + tag + " {}\n").Actions[0] is RegistryKeyAction, tag);
            foreach (var tag in new[] { "!registryValue", "!regValue", "!registryValue:", "!regValue:" })
                Check(PlaybookParser.Deserializer.Deserialize<UninstallTask>("actions:\n  - " + tag + " {}\n").Actions[0] is RegistryValueAction, tag);
            foreach (var tag in new[] { "!powerShell", "!powershell", "!powerShell:", "!powershell:" })
                Check(PlaybookParser.Deserializer.Deserialize<UninstallTask>("actions:\n  - " + tag + " {}\n").Actions[0] is PowerShellAction, tag);

            // Validation must happen before filesystem, network, or system access.
            RejectDownload(new DownloadAction { Destination = "unused.bin" }, "missing download source");
            RejectDownload(new DownloadAction { Url = " ", Destination = "unused.bin" }, "blank download source");
            RejectDownload(new DownloadAction { Url = "https://example.invalid/a", Git = "https://example.invalid/b", Destination = "unused.bin" }, "ambiguous download source");
            RejectDownload(new DownloadAction { Url = "https://example.invalid/a", Destination = " " }, "blank download destination");
            ReliabilityTests.Run(Check);
            Console.WriteLine(passed + " regression checks passed. No playbook was executed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
