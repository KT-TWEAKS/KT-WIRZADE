using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KTWirzade.Shared;
using KTWirzade.Shared.Helpers;
using KTWirzade.Shared.Rollback;
using KTWirzade.Shared.Updates;

internal static class ReliabilityTests
{
    private static Action<bool, string> check;
    private static void Reject<T>(Action operation, string name) where T : Exception
    {
        try { operation(); }
        catch (T) { check(true, name); return; }
        throw new Exception("Expected " + typeof(T).Name + ": " + name);
    }

    public static void Run(Action<bool, string> assert)
    {
        check = assert;
        var tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "KTW-regression-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(tempRoot);
        try
        {
            TestParser(tempRoot);
            TestDownloads(tempRoot);
            TestRollback(tempRoot);
            TestExecution(tempRoot);
            TestUpdateAssets();
            Reject<ArgumentException>(() => Interprocess.InterLink.InitializeSession("short", null), "reject short IPC secret");
            Interprocess.InterLink.InitializeSession();
            var secret = Interprocess.InterLink.BuildSessionArgs().Split(' ')[2];
            check(secret.Length == 43, "IPC session retains 256 bits");
            Reject<InvalidOperationException>(() => Interprocess.InterLink.InitializeSession(new string('A', 43), null), "reject IPC session replacement");
        }
        finally
        {
            if (!tempRoot.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unexpected test output location.");
            Directory.Delete(tempRoot, true);
        }
    }

    private static void TestParser(string root)
    {
        AmeliorationUtil.Playbook = new Playbook();
        var selected = new List<string> { "gaming" };
        Func<string, List<KTWirzade.Shared.Tasks.ITaskAction>> parse = file =>
            AmeliorationUtil.ParseActions(root, null, null, null, selected, file, null);
        File.WriteAllText(Path.Combine(root, "leaf.yml"), "actions:\n  - !writeStatus {status: ready}\n");
        File.WriteAllText(Path.Combine(root, "main.yml"), "tasks: [leaf.yml, leaf.yml]\n");
        check(parse("main.yml").Count == 2, "repeat include in independent branches");
        File.WriteAllText(Path.Combine(root, "a.yml"), "tasks: [b.yml]\n");
        File.WriteAllText(Path.Combine(root, "b.yml"), "tasks: [a.yml]\n");
        Reject<InvalidDataException>(() => parse("a.yml"), "indirect include cycle");
        File.WriteAllText(Path.Combine(root, "self.yml"), "actions:\n  - !task {path: self.yml}\n");
        Reject<InvalidDataException>(() => parse("self.yml"), "task include self cycle");
        Reject<InvalidDataException>(() => parse("../outside.yml"), "include traversal");
        Reject<InvalidDataException>(() => parse(Path.Combine(root, "leaf.yml")), "absolute include");
        Reject<InvalidDataException>(() => parse("leaf.yml:stream"), "include alternate stream");
        Reject<FileNotFoundException>(() => parse("missing.yml"), "missing root YAML");
        File.WriteAllText(Path.Combine(root, "empty.yml"), "");
        Reject<InvalidDataException>(() => parse("empty.yml"), "empty YAML");
        for (int i = 0; i < 65; i++)
            File.WriteAllText(Path.Combine(root, "deep" + i + ".yml"), i == 64 ? "actions: []" : "tasks: [deep" + (i + 1) + ".yml]");
        Reject<InvalidDataException>(() => parse("deep0.yml"), "include depth limit");

        foreach (var item in new[] {
            new { Filter = "['!telemetry']", Count = 1 },
            new { Filter = "['!gaming']", Count = 0 },
            new { Filter = "[gaming, unknown]", Count = 1 },
            new { Filter = "[unknown]", Count = 0 },
            new { Filter = "[gaming, '!gaming']", Count = 0 },
            new { Filter = "[]", Count = 1 } })
        {
            File.WriteAllText(Path.Combine(root, "filter.yml"), "options: " + item.Filter + "\nactions:\n  - !writeStatus {status: ready}\n");
            check((parse("filter.yml")?.Count ?? 0) == item.Count, "task options " + item.Filter);
            File.WriteAllText(Path.Combine(root, "filter.yml"), "actions:\n  - !writeStatus {status: ready, options: " + item.Filter + "}\n");
            check(parse("filter.yml").Count == item.Count, "action options " + item.Filter);
        }
        check(PlaybookValidation.MatchesFilters(new[] { "!19045" }, f => f == "!19045"), "negative build filter allowed");
        check(!PlaybookValidation.MatchesFilters(new[] { "!22621" }, f => false), "negative build filter excluded");
        Reject<ArgumentException>(() => PlaybookValidation.MatchesFilters(new[] { " " }, f => true), "blank filter rejected");
        check(parse("main.yml").Count == 2, "parser reusable after failed includes");
    }

    private static void TestDownloads(string root)
    {
        var path = Path.Combine(root, "download.bin");
        var bytes = new byte[] { 1, 2, 3, 4 };
        File.WriteAllText(path, "old");
        Func<Stream, long?, string, CancellationToken, string> save = (stream, length, hash, token) =>
            AtomicDownload.SaveAsync(stream, path, length, hash, null, token).GetAwaiter().GetResult();
        Reject<InvalidDataException>(() => save(new MemoryStream(bytes), 5, null, CancellationToken.None), "truncated download rejected");
        check(File.ReadAllText(path) == "old", "truncated download preserves destination");
        Reject<InvalidDataException>(() => save(new MemoryStream(bytes), 3, null, CancellationToken.None), "oversize download rejected");
        Reject<InvalidDataException>(() => save(new MemoryStream(bytes), 4, new string('0', 64), CancellationToken.None), "hash mismatch rejected");
        check(File.ReadAllText(path) == "old", "hash mismatch preserves destination");
        Reject<OperationCanceledException>(() => save(new MemoryStream(bytes), 4, null, new CancellationToken(true)), "download cancellation");
        check(File.ReadAllText(path) == "old", "cancel preserves destination");
        check(!Directory.GetFiles(root, "*.part").Any(), "failed downloads clean temporary files");
        var hashResult = save(new MemoryStream(bytes), 4, null, CancellationToken.None);
        check(File.ReadAllBytes(path).SequenceEqual(bytes), "successful download replaces destination");
        check(hashResult == AtomicDownload.HashFile(path), "download returns SHA256");
        save(new MemoryStream(bytes), null, hashResult, CancellationToken.None);
        check(File.ReadAllBytes(path).SequenceEqual(bytes), "unknown length with expected hash");
        Reject<ArgumentException>(() => AtomicDownload.ValidateHash("bad"), "invalid hash rejected before download");
    }

    private static void TestRollback(string root)
    {
        var session = new RollbackSession();
        session.Entries.Add(new RollbackEntry { RollbackCompleted = true });
        session.Entries.Add(new RollbackEntry { ActionType = RollbackActionType.Appx });
        session.WasRolledBack = true; // Old versions could incorrectly save this flag.
        check(!RollbackManager.IsFullyReverted(session), "legacy completion flag cannot hide pending entries");
        session.Entries[1].RollbackCompleted = true;
        check(RollbackManager.IsFullyReverted(session), "all entries completed");
        var disk = new RollbackSession { SessionId = session.SessionId };
        disk.Entries.Add(new RollbackEntry());
        RollbackManager.MergeSessionEntries(session, disk);
        check(session.Entries.Count == 3 && !RollbackManager.IsFullyReverted(session), "merge keeps child entries");
        RollbackManager.MergeSessionEntries(session, disk);
        check(session.Entries.Count == 3, "merge is idempotent");
        Reject<InvalidDataException>(() => RollbackManager.MergeSessionEntries(session, new RollbackSession()), "cannot mix session IDs");
        Reject<ArgumentException>(() => RollbackPaths.GetSessionDir("../outside"), "session traversal rejected");
        var lockPath = Path.Combine(root, "journal.lock");
        Task waiting;
        using (RollbackManager.AcquireJournalLock(lockPath))
        {
            waiting = Task.Run(() => { using (RollbackManager.AcquireJournalLock(lockPath)) { } });
            check(!waiting.Wait(150), "journal writers serialize across file handles");
        }
        check(waiting.Wait(3000), "journal writer resumes after lock release");
    }

    private static void TestUpdateAssets()
    {
        Func<GitHubAsset> asset = () => new GitHubAsset {
            Name = "KT-WIRZADE-v1.0.1-win-x64.zip", Size = 12,
            Digest = "sha256:" + new string('a', 64),
            BrowserDownloadUrl = "https://github.com/KT-TWEAKS/KT-WIRZADE/releases/download/v1.0.1/app.zip"
        };
        UpdateChecker.ValidateAsset(asset());
        check(true, "official update asset accepted");
        var invalid = asset(); invalid.Name = "../app.zip";
        Reject<InvalidDataException>(() => UpdateChecker.ValidateAsset(invalid), "unsafe asset filename");
        invalid = asset(); invalid.Digest = null;
        Reject<InvalidDataException>(() => UpdateChecker.ValidateAsset(invalid), "missing update digest");
        invalid = asset(); invalid.BrowserDownloadUrl = "https://example.invalid/app.zip";
        Reject<InvalidDataException>(() => UpdateChecker.ValidateAsset(invalid), "unofficial update host");
        invalid = asset(); invalid.BrowserDownloadUrl = "http://github.com/KT-TWEAKS/KT-WIRZADE/releases/download/v1.0.1/app.zip";
        Reject<InvalidDataException>(() => UpdateChecker.ValidateAsset(invalid), "non HTTPS update");
    }

    private sealed class IncompleteAction : KTWirzade.Shared.Tasks.TaskAction, KTWirzade.Shared.Tasks.ITaskAction
    {
        public int Attempts;
        public bool ExplicitRetries;
        public KTWirzade.Shared.Tasks.ErrorAction Policy;
        public KTWirzade.Shared.Tasks.ErrorAction GetDefaultErrorAction() => Policy;
        public bool GetRetryAllowed() => ExplicitRetries;
        public int GetProgressWeight() => 1;
        public void ResetProgress() { }
        public string ErrorString() => "Intentional test failure";
        public KTWirzade.Shared.Tasks.UninstallTaskStatus GetStatus(Core.Output.OutputWriter output) => KTWirzade.Shared.Tasks.UninstallTaskStatus.ToDo;
        public Task<bool> RunTask(Core.Output.OutputWriter output)
        {
            Attempts++;
            if (ExplicitRetries && Attempts > 1)
                throw new KTWirzade.Shared.Exceptions.ErrorHandlingException(ExitCodeAction.Retry, "Retry after an odd attempt count");
            return Task.FromResult(false);
        }
        public void RunTaskOnMainThread(Core.Output.OutputWriter output) { }
    }

    private static void TestExecution(string root)
    {
        var action = new IncompleteAction { Policy = KTWirzade.Shared.Tasks.ErrorAction.Notify };
        var errors = AmeliorationUtil.DoActions(new List<KTWirzade.Shared.Tasks.ITaskAction> { action }, root, _ => { }).GetAwaiter().GetResult();
        check(errors && action.Attempts == 1, "retry disabled honors incomplete status");
        action = new IncompleteAction { Policy = KTWirzade.Shared.Tasks.ErrorAction.Notify, ExplicitRetries = true };
        errors = AmeliorationUtil.DoActions(new List<KTWirzade.Shared.Tasks.ITaskAction> { action }, root, _ => { }).GetAwaiter().GetResult();
        check(errors && action.Attempts <= 6, "explicit retries cannot overshoot error detection");
        action = new IncompleteAction { Policy = KTWirzade.Shared.Tasks.ErrorAction.Halt };
        Reject<Exception>(() => AmeliorationUtil.DoActions(new List<KTWirzade.Shared.Tasks.ITaskAction> { action }, root, _ => { }).GetAwaiter().GetResult(), "halt aborts incomplete action");
    }
}
