using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Disposable-file interrupted-write simulation, not filesystem power-loss or SQL proof.
public sealed class LegacyTradeJournalTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-trade-journal-" + Guid.NewGuid().ToString("N"));

    public LegacyTradeJournalTests()
    {
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(ItemsRoot);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryInterruptedReplacementPrefixRollsForwardExactlyOnce(int replacements)
    {
        var changes = SeedChanges();

        Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes,
            count => count == replacements, out var reason));
        Assert.Contains("tradeJournalRecoveryRequired", reason);
        Assert.True(LegacyTradeJournal.HasPending(UsersRoot));

        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out reason), reason);
        Assert.Equal(1, recovered);
        AssertAllAfter(changes);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));

        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out recovered, out reason), reason);
        Assert.Equal(0, recovered);
        AssertAllAfter(changes);
    }

    [Fact]
    public void UnknownCurrentBytesRefuseRecoveryBeforeChangingAnyParticipant()
    {
        var changes = SeedChanges();
        Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, count => count == 0, out _));
        File.WriteAllBytes(Target(changes[3]), Encoding.UTF8.GetBytes("unrelated-new-owner-state"));
        var beforeRecovery = ReadTargets(changes);

        Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out _));

        Assert.Equal(0, recovered);
        AssertTargetsUnchanged(changes, beforeRecovery);
        Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"Version\":2,\"TransactionId\":\"2d680b20ef8d434d8c2a31c7b867b054\",\"Changes\":[]}")]
    [InlineData("{\"Version\":1,\"TransactionId\":\"not-a-guid\",\"Changes\":[]}")]
    [InlineData("{\"Version\":1,\"TransactionId\":\"2d680b20ef8d434d8c2a31c7b867b054\",\"Changes\":[{\"Kind\":0,\"FileId\":-1,\"Before\":\"YQ==\",\"After\":\"Yg==\"}]}")]
    [InlineData("{\"Version\":1,\"TransactionId\":\"2d680b20ef8d434d8c2a31c7b867b054\",\"Changes\":[{\"Kind\":0,\"FileId\":1,\"Path\":\"../outside.ini\",\"Before\":\"YQ==\",\"After\":\"Yg==\"}]}")]
    public void CorruptVersionIdentityAndPathBearingRecordsFailClosed(string record)
    {
        var changes = SeedChanges();
        var beforeRecovery = ReadTargets(changes);
        Directory.CreateDirectory(Path.GetDirectoryName(PendingPath)!);
        File.WriteAllText(PendingPath, record);

        Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out _));

        Assert.Equal(0, recovered);
        AssertTargetsUnchanged(changes, beforeRecovery);
        Assert.Equal(record, File.ReadAllText(PendingPath));
    }

    [Theory]
    [InlineData("Version")]
    [InlineData("Path")]
    [InlineData("FileId")]
    public void OtherwiseValidPublishedRecordRejectsUnsupportedVersionPathOrEscapingIdentity(string mutation)
    {
        var changes = SeedChanges();
        Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, count => count == 0, out _));
        var record = JsonNode.Parse(File.ReadAllText(PendingPath))!.AsObject();
        if (mutation == "Version") record["Version"] = 2;
        else if (mutation == "Path") record["Changes"]![0]!["Path"] = "../outside.ini";
        else record["Changes"]![0]!["FileId"] = -1;
        File.WriteAllText(PendingPath, record.ToJsonString());
        var beforeRecovery = ReadTargets(changes);
        var journalBytes = File.ReadAllBytes(PendingPath);

        Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out _));

        Assert.Equal(0, recovered);
        AssertTargetsUnchanged(changes, beforeRecovery);
        Assert.Equal(journalBytes, File.ReadAllBytes(PendingPath));
    }

    [Fact]
    public void LockedTargetBeforePublicationLeavesAllBytesAndJournalUntouched()
    {
        var changes = SeedChanges();
        var beforeCommit = ReadTargets(changes);
        using (File.Open(Target(changes[3]), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, out _));
            Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        }
        AssertTargetsUnchanged(changes, beforeCommit);
    }

    [Fact]
    public void DuplicateTargetCannotPublishAnAmbiguousTransaction()
    {
        var original = SeedChanges();
        var changes = original.ToArray();
        changes[3] = changes[3] with { FileId = changes[1].FileId };
        var beforeCommit = ReadTargets(original);

        Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, out _));

        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        AssertTargetsUnchanged(original, beforeCommit);
    }

    [Fact]
    public void MissingItemBaselineCanRecoverFromPublishedJournalWithoutInventingAnotherTarget()
    {
        var original = SeedChanges();
        File.Delete(Target(original[3]));
        var changes = original.ToArray();
        changes[3] = changes[3] with { Before = null };
        Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, count => count == 0, out _));

        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out var reason), reason);

        Assert.Equal(1, recovered);
        AssertAllAfter(changes);
        Assert.Equal(2, Directory.GetFiles(ItemsRoot, "*.bin").Length);
        Assert.Equal(2, Directory.GetFiles(UsersRoot, "*.ini").Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private string UsersRoot => Path.Combine(root, "Users");
    private string ItemsRoot => Path.Combine(root, "PlayersItems");
    private string PendingPath => Path.Combine(UsersRoot, ".managed-trade-journal", "pending.json");

    private LegacyTradeJournalChange[] SeedChanges()
    {
        LegacyTradeJournalChange[] changes =
        [
            new(LegacyTradeJournalFileKind.Character, 1_295_001, Encoding.UTF8.GetBytes("left-wallet-before"), Encoding.UTF8.GetBytes("left-wallet-after")),
            new(LegacyTradeJournalFileKind.Items, 1_295_001, [1, 2, 3], [4, 5, 6]),
            new(LegacyTradeJournalFileKind.Character, 1_295_002, Encoding.UTF8.GetBytes("right-wallet-before"), Encoding.UTF8.GetBytes("right-wallet-after")),
            new(LegacyTradeJournalFileKind.Items, 1_295_002, [7, 8, 9], [10, 11, 12]),
        ];
        foreach (var change in changes) File.WriteAllBytes(Target(change), change.Before!);
        return changes;
    }

    private string Target(LegacyTradeJournalChange change) => Path.Combine(
        change.Kind == LegacyTradeJournalFileKind.Character ? UsersRoot : ItemsRoot,
        change.FileId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
        (change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin"));

    private byte[][] ReadTargets(IEnumerable<LegacyTradeJournalChange> changes) =>
        changes.Select(change => File.ReadAllBytes(Target(change))).ToArray();

    private void AssertAllAfter(IEnumerable<LegacyTradeJournalChange> changes)
    {
        foreach (var change in changes) Assert.Equal(change.After, File.ReadAllBytes(Target(change)));
    }

    private void AssertTargetsUnchanged(IReadOnlyList<LegacyTradeJournalChange> changes, IReadOnlyList<byte[]> before)
    {
        for (var index = 0; index < changes.Count; index++)
            Assert.Equal(before[index], File.ReadAllBytes(Target(changes[index])));
    }
}
