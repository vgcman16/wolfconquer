using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class CharacterSnapshotFileCoordinatorTests : IDisposable
{
    private const long CharacterUid = 1_293_600;

    private readonly string _usersRoot = Path.Combine(
        Path.GetTempPath(),
        "wolf-character-file-coordinator-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EquivalentPathsShareOneCriticalSection()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = Path.Combine(_usersRoot, CharacterUid + ".ini");
        var equivalentPath = Path.Combine(_usersRoot, ".", CharacterUid + ".ini");
        using var firstEntered = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var secondEntered = new ManualResetEventSlim();

        var first = StartLongRunning(() => CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
        {
            firstEntered.Set();
            if (!releaseFirst.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Timed out waiting to release the first file lock.");
            }

            return true;
        }));
        Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(5)));

        var second = StartLongRunning(() => CharacterSnapshotFileCoordinator.ExecuteLocked(equivalentPath, () =>
        {
            secondEntered.Set();
            return true;
        }));

        bool overlapped;
        try
        {
            overlapped = secondEntered.Wait(TimeSpan.FromMilliseconds(250));
        }
        finally
        {
            releaseFirst.Set();
        }

        Assert.False(overlapped);
        Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(await second.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task CharacterWrapperAndGenericOwnerShareOneCriticalSection()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = Path.Combine(_usersRoot, CharacterUid + ".ini");
        var equivalentPath = Path.Combine(_usersRoot, ".", CharacterUid + ".ini");
        using var genericEntered = new ManualResetEventSlim();
        using var releaseGeneric = new ManualResetEventSlim();
        using var characterEntered = new ManualResetEventSlim();

        var generic = StartLongRunning(() => SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
        {
            genericEntered.Set();
            if (!releaseGeneric.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Timed out waiting to release the generic file lock.");
            }

            return true;
        }));
        Assert.True(genericEntered.Wait(TimeSpan.FromSeconds(5)));

        var character = StartLongRunning(() => CharacterSnapshotFileCoordinator.ExecuteLocked(
            equivalentPath,
            () =>
            {
                characterEntered.Set();
                return true;
            }));

        bool overlapped;
        try
        {
            overlapped = characterEntered.Wait(TimeSpan.FromMilliseconds(250));
        }
        finally
        {
            releaseGeneric.Set();
        }

        Assert.False(overlapped);
        Assert.True(await generic.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(await character.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ReverseOrderPathSetsShareOneCriticalSection()
    {
        Directory.CreateDirectory(_usersRoot);
        var firstPath = Path.Combine(_usersRoot, "first.ini");
        var secondPath = Path.Combine(_usersRoot, "second.ini");
        using var firstEntered = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var secondEntered = new ManualResetEventSlim();

        var first = StartLongRunning(() => CharacterSnapshotFileCoordinator.ExecuteLocked(
            [secondPath, firstPath],
            () =>
            {
                firstEntered.Set();
                if (!releaseFirst.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Timed out waiting to release the first path set.");
                }

                return true;
            }));
        Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(5)));

        var second = StartLongRunning(() => CharacterSnapshotFileCoordinator.ExecuteLocked(
            [firstPath, secondPath],
            () =>
            {
                secondEntered.Set();
                return true;
            }));

        bool overlapped;
        try
        {
            overlapped = secondEntered.Wait(TimeSpan.FromMilliseconds(250));
        }
        finally
        {
            releaseFirst.Set();
        }

        Assert.False(overlapped);
        Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(await second.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void EquivalentDuplicatePathsExecuteActionOnce()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = Path.Combine(_usersRoot, CharacterUid + ".ini");
        var equivalentPath = Path.Combine(_usersRoot, ".", CharacterUid + ".ini");
        var invocationCount = 0;

        var result = CharacterSnapshotFileCoordinator.ExecuteLocked(
            [path, equivalentPath, path],
            () =>
            {
                invocationCount++;
                return CharacterUid;
            });

        Assert.Equal(CharacterUid, result);
        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public void EmptyPathSetIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CharacterSnapshotFileCoordinator.ExecuteLocked(
            Array.Empty<string>(),
            () => true));
    }

    [Fact]
    public void InvalidMultiPathArgumentsAreRejected()
    {
        var path = Path.Combine(_usersRoot, CharacterUid + ".ini");

        Assert.Throws<ArgumentNullException>(() =>
            CharacterSnapshotFileCoordinator.ExecuteLocked<bool>(
                (IReadOnlyCollection<string>)null!,
                () => true));
        Assert.Throws<ArgumentException>(() =>
            CharacterSnapshotFileCoordinator.ExecuteLocked([path, " "], () => true));
        Assert.Throws<ArgumentNullException>(() =>
            CharacterSnapshotFileCoordinator.ExecuteLocked<bool>([path], null!));
    }

    [Fact]
    public void MultiSectionUpdateAtomicallyUpdatesBothSectionsAndDuplicateKeys()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteWindows1252Snapshot(duplicateMoney: true, includePaidNobility: true);

        Assert.True(CharacterSnapshotFileCoordinator.TryUpdateSections(
            path,
            CharacterUid,
            [
                SectionUpdate(
                    "Character",
                    new Dictionary<string, string>
                    {
                        ["Money"] = "300",
                        ["DonatePoints"] = "75",
                    },
                    createIfMissing: false,
                    "characterSectionMissing"),
                SectionUpdate(
                    "PaidNobility",
                    new Dictionary<string, string>
                    {
                        ["PaidRank"] = "12",
                        ["IsActive"] = bool.TrueString,
                    },
                    createIfMissing: false,
                    "paidNobilitySectionMissing"),
            ],
            detailedIoFailures: false,
            out var reason), reason);

        var lines = ReadWindows1252Lines(path);
        Assert.Equal(2, lines.Count(line => line == "Money=300"));
        Assert.Contains("DonatePoints=75", lines);
        Assert.Equal(2, lines.Count(line => line == "PaidRank=12"));
        Assert.Contains("IsActive=True", lines);
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void MultiSectionUpdateCreatesMissingOptionalSection()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteWindows1252Snapshot();

        Assert.True(CharacterSnapshotFileCoordinator.TryUpdateSections(
            path,
            CharacterUid,
            [
                SectionUpdate(
                    "PaidNobility",
                    new Dictionary<string, string>
                    {
                        ["Identifier"] = CharacterUid.ToString(),
                        ["PaidRank"] = "9",
                    },
                    createIfMissing: true,
                    "paidNobilitySectionMissing"),
            ],
            detailedIoFailures: false,
            out var reason), reason);

        var lines = ReadWindows1252Lines(path);
        Assert.Contains("[PaidNobility]", lines);
        Assert.Contains($"Identifier={CharacterUid}", lines);
        Assert.Contains("PaidRank=9", lines);
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void MissingRequiredSectionLeavesOriginalSnapshotUntouched()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteWindows1252Snapshot();
        var original = File.ReadAllBytes(path);

        Assert.False(CharacterSnapshotFileCoordinator.TryUpdateSections(
            path,
            CharacterUid,
            [
                SectionUpdate(
                    "Character",
                    new Dictionary<string, string> { ["Money"] = "300" },
                    createIfMissing: false,
                    "characterSectionMissing"),
                SectionUpdate(
                    "PaidNobility",
                    new Dictionary<string, string> { ["PaidRank"] = "12" },
                    createIfMissing: false,
                    "paidNobilitySectionMissing"),
            ],
            detailedIoFailures: false,
            out var reason));

        Assert.Equal("paidNobilitySectionMissing", reason);
        Assert.Equal(original, File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void MultiSectionUidMismatchLeavesOriginalSnapshotUntouched()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteWindows1252Snapshot(includePaidNobility: true);
        var original = File.ReadAllBytes(path);

        Assert.False(CharacterSnapshotFileCoordinator.TryUpdateSections(
            path,
            CharacterUid + 1,
            [
                SectionUpdate(
                    "Character",
                    new Dictionary<string, string> { ["Money"] = "300" },
                    createIfMissing: false,
                    "characterSectionMissing"),
                SectionUpdate(
                    "PaidNobility",
                    new Dictionary<string, string> { ["PaidRank"] = "12" },
                    createIfMissing: false,
                    "paidNobilitySectionMissing"),
            ],
            detailedIoFailures: false,
            out var reason));

        Assert.Equal("uidMismatch", reason);
        Assert.Equal(original, File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void MultiSectionUpdatePreservesEncodingAndUnrelatedData()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteWindows1252Snapshot();

        Assert.True(CharacterSnapshotFileCoordinator.TryUpdateSections(
            path,
            CharacterUid,
            [
                SectionUpdate(
                    "Character",
                    new Dictionary<string, string> { ["WHMoney"] = "25" },
                    createIfMissing: false,
                    "characterSectionMissing"),
                SectionUpdate(
                    "PaidNobility",
                    new Dictionary<string, string> { ["PaidRank"] = "12" },
                    createIfMissing: true,
                    "paidNobilitySectionMissing"),
            ],
            detailedIoFailures: false,
            out var reason), reason);

        var bytes = File.ReadAllBytes(path);
        Assert.Contains((byte)0xE9, bytes);
        Assert.Throws<DecoderFallbackException>(() => StrictUtf8().GetString(bytes));
        var text = Windows1252().GetString(bytes);
        var lines = text.Split(Environment.NewLine);
        Assert.Contains("WHMoney=25", lines);
        Assert.Contains("PaidRank=12", lines);
        Assert.Contains("Name=Andre\u00e9", lines);
        Assert.Contains("Note=keep", lines);
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task MultiSectionUpdateSerializesWithConcurrentCharacterUpdate()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteWindows1252Snapshot(includePaidNobility: true);
        using var valuesEnumerationStarted = new ManualResetEventSlim();
        using var releaseValuesEnumeration = new ManualResetEventSlim();
        var blockingPaidNobilityValues = new BlockingReadOnlyDictionary(
            new Dictionary<string, string> { ["PaidRank"] = "12" },
            valuesEnumerationStarted,
            releaseValuesEnumeration);

        var multiSectionUpdate = StartLongRunning(() =>
        {
            var succeeded = CharacterSnapshotFileCoordinator.TryUpdateSections(
                path,
                CharacterUid,
                [
                    SectionUpdate(
                        "Character",
                        new Dictionary<string, string> { ["DonatePoints"] = "75" },
                        createIfMissing: false,
                        "characterSectionMissing"),
                    SectionUpdate(
                        "PaidNobility",
                        blockingPaidNobilityValues,
                        createIfMissing: false,
                        "paidNobilitySectionMissing"),
                ],
                detailedIoFailures: false,
                out var reason);
            return (succeeded, reason);
        });

        Assert.True(valuesEnumerationStarted.Wait(TimeSpan.FromSeconds(5)));
        var characterUpdate = StartLongRunning(() =>
        {
            var succeeded = CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(
                path,
                CharacterUid,
                new Dictionary<string, string> { ["Money"] = "450" },
                detailedIoFailures: false,
                out var reason);
            return (succeeded, reason);
        });

        try
        {
            await Task.Delay(250);
            Assert.False(characterUpdate.IsCompleted);
        }
        finally
        {
            releaseValuesEnumeration.Set();
        }

        var multiSectionResult = await multiSectionUpdate.WaitAsync(TimeSpan.FromSeconds(5));
        var characterResult = await characterUpdate.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(multiSectionResult.succeeded, multiSectionResult.reason);
        Assert.True(characterResult.succeeded, characterResult.reason);

        var lines = ReadWindows1252Lines(path);
        Assert.Contains("DonatePoints=75", lines);
        Assert.Contains("PaidRank=12", lines);
        Assert.Contains("Money=450", lines);
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void UidMismatchLeavesOriginalSnapshotUntouched()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteWindows1252Snapshot();
        var original = File.ReadAllBytes(path);

        Assert.False(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(
            path,
            CharacterUid + 1,
            new Dictionary<string, string> { ["Money"] = "300" },
            detailedIoFailures: false,
            out var reason));
        Assert.Equal("uidMismatch", reason);
        Assert.Equal(original, File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void TransientDestinationContentionRetriesAndCleansArtifacts()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteWindows1252Snapshot();
        var original = File.ReadAllBytes(path);
        using var destinationLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var values = new RetryReleaseReadOnlyDictionary(
            new Dictionary<string, string> { ["Money"] = "301" },
            () =>
            {
                // Values are enumerated once per attempt, after the previous attempt's cleanup.
                Assert.Equal(original, File.ReadAllBytes(path));
                AssertNoTransactionArtifacts();
                destinationLock.Dispose();
            });

        var succeeded = CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(
            path,
            CharacterUid,
            values,
            detailedIoFailures: false,
            out var reason);

        Assert.True(succeeded, reason);
        Assert.Equal(2, values.EnumerationCount);
        Assert.Contains("Money=301", Windows1252().GetString(File.ReadAllBytes(path)), StringComparison.Ordinal);
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void FailedCommitPreservesOriginalAndCleansArtifacts()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteWindows1252Snapshot();
        var original = File.ReadAllBytes(path);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(
                path,
                CharacterUid,
                new Dictionary<string, string> { ["Money"] = "302" },
                detailedIoFailures: false,
                out var reason));
            Assert.Equal("snapshotWriteFailed", reason);
        }

        Assert.Equal(original, File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private string WriteWindows1252Snapshot(
        bool duplicateMoney = false,
        bool includePaidNobility = false)
    {
        var moneyLines = duplicateMoney ? "Money=100\r\nMoney=200" : "Money=100";
        var name = "Andre\u00e9";
        var paidNobilitySection = includePaidNobility
            ? "\r\n\r\n[PaidNobility]\r\nIdentifier=0\r\nPaidRank=0\r\nPaidRank=1\r\nIsActive=False"
            : string.Empty;
        var text = $$"""
            [Character]
            UID={{CharacterUid}}
            Name={{name}}
            {{moneyLines}}
            EpicTrojan=0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/

            [Custom]
            Note=keep{{paidNobilitySection}}
            """;
        var path = Path.Combine(_usersRoot, CharacterUid + ".ini");
        File.WriteAllBytes(path, Windows1252().GetBytes(text));
        return path;
    }

    private static CharacterSnapshotSectionUpdate SectionUpdate(
        string sectionName,
        IReadOnlyDictionary<string, string> values,
        bool createIfMissing,
        string missingReason)
    {
        return new CharacterSnapshotSectionUpdate(
            sectionName,
            values,
            createIfMissing,
            missingReason);
    }

    private static string[] ReadWindows1252Lines(string path)
    {
        return Windows1252().GetString(File.ReadAllBytes(path)).Split(Environment.NewLine);
    }

    private void AssertNoTransactionArtifacts()
    {
        Assert.Empty(Directory.EnumerateFiles(
            _usersRoot,
            "*.character-file-*",
            SearchOption.TopDirectoryOnly));
    }

    private static Task<TResult> StartLongRunning<TResult>(Func<TResult> action)
    {
        return Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private static Encoding Windows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    private static UTF8Encoding StrictUtf8()
    {
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    }

    private sealed class RetryReleaseReadOnlyDictionary : IReadOnlyDictionary<string, string>
    {
        private readonly IReadOnlyDictionary<string, string> _values;
        private readonly Action _releaseOnRetry;

        public RetryReleaseReadOnlyDictionary(
            IReadOnlyDictionary<string, string> values,
            Action releaseOnRetry)
        {
            _values = values;
            _releaseOnRetry = releaseOnRetry;
        }

        public int EnumerationCount { get; private set; }

        public int Count => _values.Count;

        public IEnumerable<string> Keys => _values.Keys;

        public IEnumerable<string> Values => _values.Values;

        public string this[string key] => _values[key];

        public bool ContainsKey(string key)
        {
            return _values.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            EnumerationCount++;
            if (EnumerationCount == 2)
            {
                _releaseOnRetry();
            }

            return _values.GetEnumerator();
        }

        public bool TryGetValue(string key, out string value)
        {
            return _values.TryGetValue(key, out value!);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class BlockingReadOnlyDictionary : IReadOnlyDictionary<string, string>
    {
        private readonly IReadOnlyDictionary<string, string> _values;
        private readonly ManualResetEventSlim _enumerationStarted;
        private readonly ManualResetEventSlim _releaseEnumeration;

        public BlockingReadOnlyDictionary(
            IReadOnlyDictionary<string, string> values,
            ManualResetEventSlim enumerationStarted,
            ManualResetEventSlim releaseEnumeration)
        {
            _values = values;
            _enumerationStarted = enumerationStarted;
            _releaseEnumeration = releaseEnumeration;
        }

        public int Count => _values.Count;

        public IEnumerable<string> Keys => _values.Keys;

        public IEnumerable<string> Values => _values.Values;

        public string this[string key] => _values[key];

        public bool ContainsKey(string key)
        {
            return _values.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            _enumerationStarted.Set();
            if (!_releaseEnumeration.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Timed out waiting to release section value enumeration.");
            }

            return _values.GetEnumerator();
        }

        public bool TryGetValue(string key, out string value)
        {
            return _values.TryGetValue(key, out value!);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
