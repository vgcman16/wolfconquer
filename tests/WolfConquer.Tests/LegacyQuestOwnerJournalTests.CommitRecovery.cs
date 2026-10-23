using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyQuestOwnerJournalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EveryV3InterruptionRecoversTheActualThreeImagesExactlyOnce(int replacements)
    {
        var changes = Seed();
        var trace = new List<int>();
        Assert.False(LegacyTradeJournal.TryCommitQuestOwner(Users, Items, Quests, changes.Reverse().ToArray(), count =>
        {
            trace.Add(count);
            return count == replacements;
        }, out var reason));
        Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
        Assert.Equal(Enumerable.Range(0, replacements + 1), trace);
        for (var index = 0; index < changes.Length; index++)
            Assert.Equal(replacements > index ? changes[index].After : changes[index].Before, File.ReadAllBytes(Target(changes[index])));
        var record = JsonNode.Parse(File.ReadAllText(Pending))!;
        Assert.Equal(3, record["Version"]!.GetValue<int>());
        Assert.Equal(new[] { 0, 1, 2 }, record["Changes"]!.AsArray().Select(row => row!["Kind"]!.GetValue<int>()));
        Assert.Equal(Users, record["Domain"]!["Users"]!.GetValue<string>());
        Assert.Equal(Items, record["Domain"]!["Items"]!.GetValue<string>());
        Assert.Equal(Quests, record["Domain"]!["Quests"]!.GetValue<string>());

        var restarted = new LegacyTradeRecoveryState(Users, Items, Quests);
        Assert.False(restarted.IsReadyFor(Users, Items, Quests));
        Assert.True(restarted.TryEnsureInitialized(out reason), reason);
        Assert.True(restarted.IsReadyFor(Users, Items, Quests));
        Assert.False(LegacyTradeJournal.HasPending(Users));
        AssertImages(changes, after: true);
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, Quests, out var recovered, out reason), reason);
        Assert.Equal(0, recovered);
        AssertImages(changes, after: true);
    }

    [Theory]
    [InlineData(301L)]
    [InlineData(CharacterUid)]
    public void V3AllowsDistinctOrEqualPositiveAccountAndCharacterIds(long account)
    {
        var changes = Seed(account);
        Assert.True(LegacyTradeJournal.TryCommitQuestOwner(Users, Items, Quests, changes, out var reason), reason);
        Assert.Equal(string.Empty, reason);
        AssertImages(changes, after: true);
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    [Fact]
    public void PairOnlyRecoveryFencesV3WithoutTouchingEvidenceThenFreshTripleRecovers()
    {
        var changes = Seed();
        Assert.False(LegacyTradeJournal.TryCommitQuestOwner(Users, Items, Quests, changes, count => count == 0, out _));
        var files = CaptureFiles();
        Assert.False(LegacyTradeJournal.TryRecover(Users, Items, out var recovered, out var reason));
        Assert.Equal(0, recovered);
        Assert.Equal("tradeJournalRecoveryRequired:questsRootMissing", reason);
        var pair = new LegacyTradeRecoveryState(Users, Items);
        Assert.False(pair.TryEnsureInitialized(out reason));
        Assert.True(pair.IsBlocked);
        Assert.False(pair.IsReadyFor(Users, Items));
        Assert.False(pair.IsReadyFor(Users, Items, Quests));
        AssertFiles(files);
        Assert.True(new LegacyTradeRecoveryState(Users, Items, Quests).TryEnsureInitialized(out reason), reason);
        AssertImages(changes, after: true);
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    [Fact]
    public void UnknownCurrentQuestImagePreventsEveryRecoveryWrite()
    {
        var changes = Seed();
        Assert.False(LegacyTradeJournal.TryCommitQuestOwner(Users, Items, Quests, changes, count => count == 0, out _));
        File.WriteAllBytes(Target(changes[2]), [3, 2, 1]);
        var files = CaptureFiles();
        Assert.False(LegacyTradeJournal.TryRecover(Users, Items, Quests, out var recovered, out var reason));
        Assert.Equal(0, recovered);
        Assert.StartsWith("tradeJournalRecoveryRequired:rollForwardFailed:", reason);
        AssertFiles(files);
    }

    [Fact]
    public void V3UsesTheSamePendingSlotAsBothExistingCommitApis()
    {
        var changes = Seed();
        var second = Seed(302, CharacterUid + 1);
        Assert.False(LegacyTradeJournal.TryCommitQuestOwner(Users, Items, Quests, changes, count => count == 0, out _));
        var files = CaptureFiles();
        var legacyOwner = changes.Take(2).ToArray();
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(Users, Items, legacyOwner, out var reason));
        Assert.Equal("tradeJournalRecoveryRequired:pending", reason);
        Assert.False(LegacyTradeJournal.TryCommit(Users, Items, legacyOwner.Concat(second.Take(2)).ToArray(), out reason));
        Assert.Equal("tradeJournalRecoveryRequired:pending", reason);
        AssertFiles(files);
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, Quests, out _, out reason), reason);
        AssertImages(changes, after: true);
        AssertImages(second, after: false);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void OldVersionsRetainExactRecordShapeOrderBytesAndRecoverWithoutQuestConfiguration(int version)
    {
        var changes = Seed();
        var second = Seed(302, CharacterUid + 1);
        var supplied = version == 1 ? changes.Take(2).Concat(second.Take(2)).Reverse().ToArray()
            : changes.Take(2).Reverse().ToArray();
        Assert.False(version == 1
            ? LegacyTradeJournal.TryCommit(Users, Items, supplied, count => count == 0, out _)
            : LegacyTradeJournal.TryCommitSingleOwner(Users, Items, supplied, count => count == 0, out _));
        var record = JsonNode.Parse(File.ReadAllText(Pending))!.AsObject();
        Assert.Equal(new[] { "Version", "TransactionId", "Changes" }, record.Select(property => property.Key));
        var expectedOrder = version == 1 ? supplied : supplied.OrderBy(change => change.Kind).ToArray();
        var expected = new PriorJournalRecord(version, record["TransactionId"]!.GetValue<string>(), expectedOrder);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(expected), File.ReadAllBytes(Pending));
        // An optional alias would be invalid for v3, but is irrelevant to old
        // records. Do not retrofit new root policy onto their established APIs.
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, questsRoot: Items, out var recovered, out var reason), reason);
        Assert.Equal(1, recovered);
        AssertImages(supplied, after: true);
        Assert.Equal(changes[2].Before, File.ReadAllBytes(Target(changes[2])));
        Assert.Equal(second[2].Before, File.ReadAllBytes(Target(second[2])));
    }

    private sealed record PriorJournalRecord(int Version, string TransactionId, LegacyTradeJournalChange[] Changes);
}
