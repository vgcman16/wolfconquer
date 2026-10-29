using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Exact disposable-file restart simulations; not process-kill/power-loss proof.
public sealed partial class LegacySingleOwnerJournalTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-single-owner-journal-" + Guid.NewGuid().ToString("N"));

    public LegacySingleOwnerJournalTests()
    {
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(ItemsRoot);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SingleOwnerInterruptionPrefixesRecoverExactAfterImagesOnlyOnce(int replacements)
    {
        var changes = SeedOwner(1_299_001);
        // Reverse caller order deliberately: API hook1 must still mean Character.
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes.Reverse().ToArray(),
            LegacyJournalInterruption.StopAfter(replacements), out var reason));
        Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
        Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.Equal(replacements >= 1 ? changes[0].After : changes[0].Before, Read(changes[0]));
        Assert.Equal(replacements >= 2 ? changes[1].After : changes[1].Before, Read(changes[1]));
        var record = JsonNode.Parse(File.ReadAllText(PendingPath))!;
        Assert.Equal(2, record["Version"]!.GetValue<int>());
        Assert.Equal(2, record["Changes"]!.AsArray().Count);
        Assert.Equal(0, record["Changes"]![0]!["Kind"]!.GetValue<int>());

        RecoverOnceAndAssertAfter(changes);
    }

    [Fact]
    public void V1TradeAndV2SingleOwnerApisCannotSubstituteForOneAnothersTargetShape()
    {
        var first = SeedOwner(1_299_011);
        var second = SeedOwner(1_299_012);
        var pair = first.Concat(second).ToArray();
        Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, first, out _));
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, pair, out _));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        AssertBefore(pair);
    }

    [Theory]
    [InlineData("characters")]
    [InlineData("items")]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("kind")]
    [InlineData("character-before")]
    [InlineData("after")]
    [InlineData("oversize")]
    public void InvalidSingleOwnerInputNeverPublishesOrTouchesTargets(string mutation)
    {
        var original = SeedOwner(1_299_021);
        var changes = original.ToArray();
        changes[0] = mutation switch
        {
            "characters" => changes[0],
            "items" => changes[0] with { Kind = LegacyTradeJournalFileKind.Items },
            "zero" => changes[0] with { FileId = 0 },
            "negative" => changes[0] with { FileId = -1 },
            "kind" => changes[0] with { Kind = (LegacyTradeJournalFileKind)17 },
            "character-before" => changes[0] with { Before = null },
            "after" => changes[0] with { After = null! },
            "oversize" => changes[0] with { After = new byte[16 * 1024 * 1024 + 1] },
            _ => throw new InvalidOperationException(),
        };
        if (mutation == "characters") changes[1] = changes[1] with { Kind = LegacyTradeJournalFileKind.Character };

        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, out _));

        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        AssertBefore(original);
    }

    [Theory]
    [InlineData("v1-two-targets")]
    [InlineData("unknown-version")]
    [InlineData("v2-four-targets")]
    [InlineData("v2-one-target")]
    [InlineData("same-kind")]
    [InlineData("nonpositive-id")]
    [InlineData("path")]
    [InlineData("character-before")]
    [InlineData("after")]
    [InlineData("duplicate-property")]
    public void InvalidPublishedV2RecordRetainsEvidenceAndEveryCurrentTarget(string mutation)
    {
        var changes = SeedOwner(1_299_031);
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, LegacyJournalInterruption.StopAfter(0), out _));
        var record = JsonNode.Parse(File.ReadAllText(PendingPath))!.AsObject();
        var entries = record["Changes"]!.AsArray();
        switch (mutation)
        {
            case "v1-two-targets": record["Version"] = 1; break;
            case "unknown-version": record["Version"] = 3; break;
            case "v2-four-targets": entries.Add(entries[0]!.DeepClone()); entries.Add(entries[1]!.DeepClone()); break;
            case "v2-one-target": entries.RemoveAt(1); break;
            case "same-kind": entries[1]!["Kind"] = 0; break;
            case "nonpositive-id": entries[0]!["FileId"] = 0; break;
            case "path": entries[0]!["Path"] = "../outside.ini"; break;
            case "character-before": entries[0]!["Before"] = null; break;
            case "after": entries[1]!["After"] = null; break;
        }
        var text = record.ToJsonString();
        if (mutation == "duplicate-property") text = text.Replace("\"Version\":2", "\"Version\":2,\"Version\":2", StringComparison.Ordinal);
        File.WriteAllText(PendingPath, text);
        var evidence = File.ReadAllBytes(PendingPath);

        Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out var reason));

        Assert.Equal(0, recovered);
        Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
        Assert.Equal(evidence, File.ReadAllBytes(PendingPath));
        Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
        AssertBefore(changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingSlotIsSharedAcrossTradeAndSingleOwnerTransactions(bool pendingSingleOwner)
    {
        var first = SeedOwner(1_299_041);
        var pair = first.Concat(SeedOwner(1_299_042)).ToArray();
        Assert.False(pendingSingleOwner
            ? LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, first, LegacyJournalInterruption.StopAfter(0), out _)
            : LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, pair, LegacyJournalInterruption.StopAfter(0), out _));
        var evidence = File.ReadAllBytes(PendingPath);
        string reason;
        Assert.False(pendingSingleOwner
            ? LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, pair, out reason)
            : LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, first, out reason));
        Assert.StartsWith("tradeJournalRecoveryRequired:pending", reason);
        Assert.Equal(evidence, File.ReadAllBytes(PendingPath));
        AssertBefore(pair);
        RecoverOnceAndAssertAfter(pendingSingleOwner ? first : pair);
        if (pendingSingleOwner) AssertBefore(pair.Skip(2));
    }

    [Fact]
    public void OriginalV1PairRecordStillRecoversThenV2CanUseTheClearedSlot()
    {
        var first = SeedOwner(1_299_051);
        var pair = first.Concat(SeedOwner(1_299_052)).ToArray();
        Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, pair, LegacyJournalInterruption.StopAfter(1), out _));
        var record = JsonNode.Parse(File.ReadAllText(PendingPath))!;
        Assert.Equal(1, record["Version"]!.GetValue<int>());
        Assert.Equal(4, record["Changes"]!.AsArray().Count);
        RecoverOnceAndAssertAfter(pair);
        var sale = first.Select(change => change with { Before = change.After, After = change.After.Concat(new byte[] { 99 }).ToArray() }).ToArray();
        Assert.True(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, sale, out var reason), reason);
        AssertAfter(sale);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    [Fact]
    public void UnknownCurrentItemBytesPreventAnySingleOwnerRecoveryMutation()
    {
        var changes = SeedOwner(1_299_061);
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, LegacyJournalInterruption.StopAfter(0), out _));
        var unknown = new byte[] { 200, 201, 202 };
        File.WriteAllBytes(Target(changes[1]), unknown);
        var evidence = File.ReadAllBytes(PendingPath);
        Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out _));
        Assert.Equal(0, recovered);
        Assert.Equal(changes[0].Before, Read(changes[0]));
        Assert.Equal(unknown, Read(changes[1]));
        Assert.Equal(evidence, File.ReadAllBytes(PendingPath));
    }

    [Fact]
    public void MissingItemPreimageIsAllowedButDoesNotCreateAnotherParticipant()
    {
        var changes = SeedOwner(1_299_071);
        File.Delete(Target(changes[1]));
        changes[1] = changes[1] with { Before = null };
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, LegacyJournalInterruption.StopAfter(1), out _));
        RecoverOnceAndAssertAfter(changes);
        Assert.Single(Directory.GetFiles(UsersRoot, "*.ini"));
        Assert.Single(Directory.GetFiles(ItemsRoot, "*.bin"));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private string UsersRoot => Path.Combine(root, "Users");
    private string ItemsRoot => Path.Combine(root, "PlayersItems");
    private string PendingPath => Path.Combine(UsersRoot, ".managed-trade-journal", "pending.json");
    private string Target(LegacyTradeJournalChange change) => Path.Combine(
        change.Kind == LegacyTradeJournalFileKind.Character ? UsersRoot : ItemsRoot,
        change.FileId.ToString(CultureInfo.InvariantCulture) + (change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin"));
    private byte[] Read(LegacyTradeJournalChange change) => File.ReadAllBytes(Target(change));

    private LegacyTradeJournalChange[] SeedOwner(long uid)
    {
        LegacyTradeJournalChange[] changes =
        [
            new(LegacyTradeJournalFileKind.Character, uid, Encoding.UTF8.GetBytes("Money=1000\r\n"), Encoding.UTF8.GetBytes("Money=1300\r\n")),
            new(LegacyTradeJournalFileKind.Items, uid, [1, 2, 3, 4], [0, 0, 0, 0]),
        ];
        foreach (var change in changes) File.WriteAllBytes(Target(change), change.Before!);
        return changes;
    }

    private void AssertBefore(IEnumerable<LegacyTradeJournalChange> changes)
    {
        foreach (var change in changes) Assert.Equal(change.Before, Read(change));
    }

    private void AssertAfter(IEnumerable<LegacyTradeJournalChange> changes)
    {
        foreach (var change in changes) Assert.Equal(change.After, Read(change));
    }

    private void RecoverOnceAndAssertAfter(LegacyTradeJournalChange[] changes)
    {
        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out var reason), reason);
        Assert.Equal(1, recovered);
        AssertAfter(changes);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out recovered, out reason), reason);
        Assert.Equal(0, recovered);
        AssertAfter(changes);
    }
}
