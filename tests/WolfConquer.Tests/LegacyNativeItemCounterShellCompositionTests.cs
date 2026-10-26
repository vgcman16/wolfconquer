using System.Globalization;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WindowsShellCompositionFactAttribute : FactAttribute
{
    public WindowsShellCompositionFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires actual Windows INI profile APIs.";
    }
}

public sealed class WindowsShellCompositionTheoryAttribute : TheoryAttribute
{
    public WindowsShellCompositionTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires actual Windows INI profile APIs.";
    }
}

// Reuses the existing nonparallel counter collection, not a runtime lock.
// Constructor/order tests are inert; Windows-only tests use an owned temporary dir.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemCounterShellCompositionTests
{
    [Fact]
    public void StartupOpenUsesLiteralCwdSuffixAndNeverReadsDbLocation()
    {
        var events = new List<string>();
        var ini = LegacyNativeItemCounterShellComposition.OpenForInitialization(
            () => { events.Add("directory"); return @"C:\inert-only"; },
            () => throw new InvalidOperationException("DbLocation must be bypassed"));
        Assert.Equal(@"C:\inert-only\shell.ini", ini.FileName);
        Assert.Equal(new[] { "directory" }, events);
    }

    [Fact]
    public void StartupOpenPreservesNullAndTrailingSeparatorConcatenation()
    {
        var fromNull = LegacyNativeItemCounterShellComposition.OpenForInitialization(
            () => null!, null!);
        var trailing = LegacyNativeItemCounterShellComposition.OpenForInitialization(
            () => @"C:\inert-only\", null!);
        Assert.Equal(@"\shell.ini", fromNull.FileName);
        Assert.Equal(@"C:\inert-only\\shell.ini", trailing.FileName);
        // Neither inert path is passed to a native API.
    }

    [Fact]
    public void StartupDirectoryFailureEscapesBeforeDbRead()
    {
        var expected = new InvalidOperationException("directory");
        var actual = Assert.Throws<InvalidOperationException>(() =>
            LegacyNativeItemCounterShellComposition.OpenForInitialization(
                () => throw expected,
                () => throw new InvalidOperationException("unexpected DbLocation")));
        Assert.Same(expected, actual);
    }

    [Fact]
    public void StartupOpenAcceptsNullDbGetterBecauseOverrideBypassesIt()
    {
        var ini = LegacyNativeItemCounterShellComposition.OpenForInitialization(
            () => "inert-relative-directory", null!);
        Assert.Equal(@"inert-relative-directory\shell.ini", ini.FileName);
    }

    [Fact]
    public void SaveFactoryEvaluatesDbBeforeDirectoryAndOverwritesItsInitialPath()
    {
        var events = new List<string>();
        var ini = LegacyNativeItemCounterShellComposition.CreateForSave(
            () => { events.Add("directory"); return @"C:\inert-only"; },
            () => { events.Add("db"); return null; });
        Assert.Equal(new[] { "db", "directory" }, events);
        Assert.Equal(@"C:\inert-only\shell.ini", ini.FileName);
    }

    [Fact]
    public void SaveFactoryDbFailurePreventsDirectoryEvaluation()
    {
        var expected = new InvalidOperationException("db");
        var actual = Assert.Throws<InvalidOperationException>(() =>
            LegacyNativeItemCounterShellComposition.CreateForSave(
                () => throw new InvalidOperationException("unexpected directory"),
                () => throw expected));
        Assert.Same(expected, actual);
    }

    [Fact]
    public void SaveFactoryDirectoryFailureOccursAfterDbEvaluation()
    {
        var events = new List<string>();
        var expected = new InvalidOperationException("directory");
        var actual = Assert.Throws<InvalidOperationException>(() =>
            LegacyNativeItemCounterShellComposition.CreateForSave(
                () => { events.Add("directory"); throw expected; },
                () => { events.Add("db"); return "overwritten"; }));
        Assert.Equal(new[] { "db", "directory" }, events);
        Assert.Same(expected, actual);
    }

    [Fact]
    public void SaveFactoryReevaluatesBothLiveGettersOnEveryCall()
    {
        var reads = 0;
        var dbReads = 0;
        string Current() => "inert-" + ++reads;
        string? Database() { dbReads++; return "discarded"; }
        var first = LegacyNativeItemCounterShellComposition.CreateForSave(Current, Database);
        var second = LegacyNativeItemCounterShellComposition.CreateForSave(Current, Database);
        Assert.NotSame(first, second);
        Assert.Equal(@"inert-1\shell.ini", first.FileName);
        Assert.Equal(@"inert-2\shell.ini", second.FileName);
        Assert.Equal(2, dbReads);
    }

    [Fact]
    public void SaveNotFullyLoadedDoesNotUseEvenNullDependencies()
    {
        Assert.Null(Record.Exception(() =>
            LegacyNativeItemCounterShellComposition.SaveAtNativeShellPhase(
                false, null!, null!, null!, null!, null!)));
    }

    [Fact]
    public void SavePrefixFailureSkipsBothGettersAndTail()
    {
        var expected = new InvalidOperationException("prefix");
        var reports = new List<Exception>();
        LegacyNativeItemCounterShellComposition.SaveAtNativeShellPhase(
            true,
            () => throw expected,
            () => throw new InvalidOperationException("unexpected directory"),
            () => throw new InvalidOperationException("unexpected db"),
            _ => throw new InvalidOperationException("unexpected tail"),
            reports.Add);
        Assert.Same(expected, Assert.Single(reports));
    }

    [Fact]
    public void SaveDbFailureIsCaughtByExistingSaveEnvelope()
    {
        var events = new List<string>();
        var expected = new InvalidOperationException("db");
        var reports = new List<Exception>();
        LegacyNativeItemCounterShellComposition.SaveAtNativeShellPhase(
            true,
            () => events.Add("declared-prefix"),
            () => throw new InvalidOperationException("unexpected directory"),
            () => { events.Add("db"); throw expected; },
            _ => throw new InvalidOperationException("unexpected tail"),
            reports.Add);
        Assert.Equal(new[] { "declared-prefix", "db" }, events);
        Assert.Same(expected, Assert.Single(reports));
    }

    [Fact]
    public void SaveDirectoryFailureIsCaughtAfterDb()
    {
        var events = new List<string>();
        var expected = new InvalidOperationException("directory");
        var reports = new List<Exception>();
        LegacyNativeItemCounterShellComposition.SaveAtNativeShellPhase(
            true,
            () => events.Add("declared-prefix"),
            () => { events.Add("directory"); throw expected; },
            () => { events.Add("db"); return "discarded"; },
            _ => throw new InvalidOperationException("unexpected tail"),
            reports.Add);
        Assert.Equal(new[] { "declared-prefix", "db", "directory" }, events);
        Assert.Same(expected, Assert.Single(reports));
    }

    [Fact]
    public void SaveReporterFailureEscapesWithoutSecondReport()
    {
        var saveError = new InvalidOperationException("prefix");
        var reportError = new InvalidOperationException("report");
        var calls = 0;
        var actual = Assert.Throws<InvalidOperationException>(() =>
            LegacyNativeItemCounterShellComposition.SaveAtNativeShellPhase(
                true, () => throw saveError, null!, null!, null!,
                error =>
                {
                    calls++;
                    Assert.Same(saveError, error);
                    throw reportError;
                }));
        Assert.Same(reportError, actual);
        Assert.Equal(1, calls);
    }

    [WindowsShellCompositionTheory]
    [InlineData(0u, 1u, 2u)]
    [InlineData(40u, 41u, 42u)]
    [InlineData(uint.MaxValue, 0u, 1u)]
    public void PhysicalStartupReadOccursOnlyAtLaterCounterPhase(
        uint persisted, uint afterStartup, uint firstConsumer)
    {
        using var owned = new OwnedShell();
        WithCounter(new LegacyNativeCounter(900u), counter =>
        {
            var ini = LegacyNativeItemCounterShellComposition.OpenForInitialization(
                () => owned.RootDirectory,
                () => throw new InvalidOperationException("startup override"));
            Assert.Equal(owned.ShellPath, ini.FileName);
            Assert.False(File.Exists(owned.ShellPath));
            Assert.Equal(900u, counter.Count);

            // Declared intervening phase: the factory must not initialize eagerly.
            owned.Seed(persisted);
            LegacyNativeItemCounterShellLifecycle.InitializeAtNativeShellPhase(ini);

            Assert.Equal(afterStartup, counter.Count);
            Assert.Equal(firstConsumer, counter.Next);
        });
    }

    [WindowsShellCompositionFact]
    public void PhysicalComposedSaveUsesPostFactoryCurrentCounterCount()
    {
        using var owned = new OwnedShell();
        owned.Seed(99u);
        WithCounter(new LegacyNativeCounter(100u), original =>
        {
            var replacement = new LegacyNativeCounter(222u);
            var events = new List<string>();
            var reports = new List<Exception>();
            LegacyNativeItemCounterShellComposition.SaveAtNativeShellPhase(
                true,
                () => { events.Add("declared-prefix"); original.Set(111u); },
                () =>
                {
                    events.Add("directory");
                    replacement.Set(333u);
                    return owned.RootDirectory;
                },
                () =>
                {
                    events.Add("db");
                    LegacyNativePoolItems.ItemCounter = replacement;
                    return "discarded-not-a-physical-path";
                },
                ini =>
                {
                    events.Add("declared-tail");
                    var concrete = Assert.IsType<LegacyNativeItemCounterShellIni>(ini);
                    Assert.Equal(owned.ShellPath, concrete.FileName);
                    Assert.Equal(333u, concrete.ReadUInt32("Database", "ItemUID"));
                },
                reports.Add);
            Assert.Equal(new[] { "declared-prefix", "db", "directory", "declared-tail" }, events);
            Assert.Empty(reports);
            Assert.Equal(111u, original.Count);
            Assert.Equal(333u, replacement.Count);
            Assert.Contains("ItemUID=333", File.ReadAllText(owned.ShellPath));
        });
    }

    [WindowsShellCompositionFact]
    public void PhysicalTailFailureOccursAfterItemUidPersistence()
    {
        using var owned = new OwnedShell();
        owned.Seed(99u);
        WithCounter(new LegacyNativeCounter(42u), counter =>
        {
            var expected = new InvalidOperationException("declared-tail");
            var reports = new List<Exception>();
            LegacyNativeItemCounterShellComposition.SaveAtNativeShellPhase(
                true, () => { }, () => owned.RootDirectory, () => "discarded",
                ini =>
                {
                    Assert.Equal(42u, ini.ReadUInt32("Database", "ItemUID"));
                    throw expected;
                },
                reports.Add);
            Assert.Same(expected, Assert.Single(reports));
            Assert.Equal(42u, counter.Count);
            Assert.Contains("ItemUID=42", File.ReadAllText(owned.ShellPath));
        });
    }

    [WindowsShellCompositionFact]
    public void PhysicalNativeWriteFailureStillReachesTail()
    {
        using var owned = new OwnedShell();
        WithCounter(new LegacyNativeCounter(42u), counter =>
        {
            var reports = new List<Exception>();
            var tails = 0;
            owned.Validate(owned.MissingShellPath);
            LegacyNativeItemCounterShellComposition.SaveAtNativeShellPhase(
                true, () => { }, () => owned.MissingDirectory, () => "discarded",
                ini =>
                {
                    tails++;
                    Assert.Equal(owned.MissingShellPath,
                        Assert.IsType<LegacyNativeItemCounterShellIni>(ini).FileName);
                },
                reports.Add);
            Assert.Empty(reports);
            Assert.Equal(1, tails);
            Assert.Equal(42u, counter.Count);
            Assert.False(File.Exists(owned.MissingShellPath));
            Assert.False(Directory.Exists(owned.MissingDirectory));
        });
    }

    [WindowsShellCompositionFact]
    public void PhysicalStartupParserReplacementRetainsReceiverThenRereadsField()
    {
        using var owned = new OwnedShell();
        owned.Seed(40u);
        WithCounter(new LegacyNativeCounter(400u), original =>
        {
            var replacement = new LegacyNativeCounter(800u);
            var parser = LegacyNativeItemCounterShellIni.ToUInt32;
            try
            {
                var ini = LegacyNativeItemCounterShellComposition.OpenForInitialization(
                    () => owned.RootDirectory, null!);
                LegacyNativeItemCounterShellIni.ToUInt32 = text =>
                {
                    LegacyNativePoolItems.ItemCounter = replacement;
                    return uint.Parse(text, CultureInfo.InvariantCulture);
                };
                LegacyNativeItemCounterShellLifecycle.InitializeAtNativeShellPhase(ini);
                Assert.Equal(40u, original.Count);
                Assert.Same(replacement, LegacyNativePoolItems.ItemCounter);
                Assert.Equal(801u, replacement.Count);
            }
            finally { LegacyNativeItemCounterShellIni.ToUInt32 = parser; }
        });
    }

    private static void WithCounter(LegacyNativeCounter declared, Action<LegacyNativeCounter> body)
    {
        var original = LegacyNativePoolItems.ItemCounter;
        LegacyNativePoolItems.ItemCounter = declared;
        try { body(declared); }
        finally { LegacyNativePoolItems.ItemCounter = original; }
    }

    // No process-wide cwd mutation. Keep native INI paths independent of checkout depth.
    private sealed class OwnedShell : IDisposable
    {
        private readonly string _root;
        public string RootDirectory => _root;
        public string ShellPath => _root + "\\shell.ini";
        public string MissingDirectory { get; }
        public string MissingShellPath => MissingDirectory + "\\shell.ini";

        public OwnedShell()
        {
            var parent = Path.GetFullPath(Path.GetTempPath());
            NoReparse(parent);
            _root = Path.Combine(parent, "wolf-shell-" + Guid.NewGuid().ToString("N"));
            MissingDirectory = Path.Combine(_root, "absent-" + Guid.NewGuid().ToString("N"));
            // The compatibility profile APIs are not long-path-aware. Refuse an
            // unsupported fixture path rather than silently testing default reads.
            if (ShellPath.Length >= 260 || MissingShellPath.Length >= 260)
                throw new IOException("Owned INI fixture exceeds the native profile path limit.");
            if (File.Exists(_root) || Directory.Exists(_root)) throw new IOException("Root exists.");
            Directory.CreateDirectory(_root);
            NoReparse(_root);
            Validate(ShellPath);
            Validate(MissingShellPath);
        }

        public void Seed(uint value)
        {
            Validate(ShellPath);
            using var stream = new FileStream(ShellPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.Read);
            var text = "[Database]\r\nItemUID=" +
                value.ToString(CultureInfo.InvariantCulture) + "\r\nMarker=keep\r\n";
            stream.Write(Encoding.ASCII.GetBytes(text));
            stream.Flush(true);
        }

        public void Validate(string path)
        {
            var full = Path.GetFullPath(path);
            if (!string.Equals(full, path, StringComparison.OrdinalIgnoreCase) ||
                !full.StartsWith(_root + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new IOException("Outside owned fixture.");
            NoReparse(full);
        }

        private static void NoReparse(string path)
        {
            string? full = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(full))
            {
                try
                {
                    if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Reparse path refused.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                full = Path.GetDirectoryName(full);
            }
        }

        public void Dispose()
        {
            NoReparse(_root);
            // Validate the entire tiny owned root before any deletion. Never recurse.
            foreach (var entry in Directory.EnumerateFileSystemEntries(_root))
            {
                Validate(entry);
                if (!string.Equals(entry, ShellPath, StringComparison.OrdinalIgnoreCase) ||
                    Directory.Exists(entry))
                    throw new IOException("Unexpected contents preserved.");
            }
            Validate(ShellPath);
            if (File.Exists(ShellPath)) File.Delete(ShellPath);
            Directory.Delete(_root, recursive: false);
        }
    }
}
