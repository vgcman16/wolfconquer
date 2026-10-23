using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Theory]
    [InlineData(65_536U)]
    [InlineData(uint.MaxValue)]
    public async Task PlumeInvalidDurabilityDoesNotAdvanceCollection(uint durability)
    {
        var result = await KillMap1002Family2TurtledoveQuest1007MonsterAsync(
            objective0: 2, plumeDurability: durability);
        AssertPlumeRefusal(result.UpdatedSession, result.Sent, result.Log, [2U]);
        Assert.Contains("itemRewardMetadataInvalid", result.Log);
    }

    [Fact]
    public async Task PlumeObjectiveOverflowDoesNotPublishEitherHalfOfReward()
    {
        var result = await KillMap1002Family2TurtledoveQuest1007MonsterAsync(objective0: uint.MaxValue);
        AssertPlumeRefusal(result.UpdatedSession, result.Sent, result.Log, [uint.MaxValue]);
        Assert.Contains("questRewardPreparationInvalid", result.Log);
    }

    [Fact]
    public async Task PlumeOversizedRealObjectiveFrameDoesNotPublishReward()
    {
        var intentions = new uint[PacketHeader.DefaultMaxFrameSize / sizeof(uint)];
        intentions[0] = 2;
        var result = await KillMap1002Family2TurtledoveQuest1007MonsterAsync(intentions: intentions);
        AssertPlumeRefusal(result.UpdatedSession, result.Sent, result.Log, intentions);
        Assert.Equal(2U, intentions[0]);
        Assert.Contains("questRewardPreparationInvalid", result.Log);
    }

    [Fact]
    public async Task PlumeEmptyObjectiveRetainsOriginalSingleCounterBehavior()
    {
        var result = await KillMap1002Family2TurtledoveQuest1007MonsterAsync(intentions: []);
        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests);
        Assert.Equal([1U], quest.Intentions);
        Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_236U);
        var frame = Assert.Single(result.Sent, bytes =>
            MsgQuestDataPacket.TryParseObjectiveFrame(ReadFrame(bytes), out var parsed) && parsed.Uid == 1_007U);
        Assert.Equal(MsgQuestDataPacket.CreateObjectiveFrame(1_007U, [1U]), frame);
    }

    [Fact]
    public async Task PlumeCheckedPlanPreservesUnrelatedCountersAndExistingPhysicalItem()
    {
        uint[] intentions = [2, 17, 29];
        var result = await KillMap1002Family2TurtledoveQuest1007MonsterAsync(
            intentions: intentions, occupiedUid: 41, plumeDurability: 123);
        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests);
        Assert.Equal([3U, 17U, 29U], quest.Intentions);
        Assert.Equal([2U, 17U, 29U], intentions);
        var priorItem = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 999_001U);
        Assert.Equal(41U, priorItem.Uid);
        Assert.Equal((ushort)1, priorItem.StackSize);
        var plume = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_236U);
        Assert.Equal((ushort)123, plume.Durability);
        Assert.Equal((ushort)123, plume.MaximumDurability);
        Assert.Equal((ushort)1, plume.StackSize);
        var frame = Assert.Single(result.Sent, bytes =>
            MsgQuestDataPacket.TryParseObjectiveFrame(ReadFrame(bytes), out var parsed) && parsed.Uid == 1_007U);
        Assert.Equal(MsgQuestDataPacket.CreateObjectiveFrame(1_007U, [3U, 17U, 29U]), frame);
    }

    [Fact]
    public async Task PlumeQuestSaveFailureIsCommittedRewardWithTruthfulPersistenceGap()
    {
        using var file = new PlumeQuestFile(rootIsFile: true);
        var original = File.ReadAllBytes(file.Root);
        var result = await KillMap1002Family2TurtledoveQuest1007MonsterAsync(
            objective0: 2, playerQuestsRoot: file.Root);
        Assert.Equal([3U], Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests).Intentions);
        Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_236U);
        Assert.Contains(ParseQuestObjectiveFrames(result.Sent), frame => frame.Uid == 1_007U && frame.Intentions[0] == 3);
        Assert.Contains(ParseGameItems(result.Sent), item => item.ItemId == 711_236U);
        Assert.Equal(original, File.ReadAllBytes(file.Root));
        Assert.Contains("PARITY-GAP LegacyPlayerQuests persistence", result.Log);
        Assert.Contains("reason=", result.Log);
        Assert.Contains("Turtledove Plume reward", result.Log);
        Assert.DoesNotContain("WorldServer refused legacy Turtledove Plume reward", result.Log);
    }

    [Fact]
    public async Task PlumeValidRewardRetainsRealQuestPersistence()
    {
        using var file = new PlumeQuestFile(rootIsFile: false);
        var result = await KillMap1002Family2TurtledoveQuest1007MonsterAsync(
            objective0: 2, playerQuestsRoot: file.Root);
        var saved = new PlayerQuestSnapshotLoader(file.Root).LoadByCharacterUid(KillerUid);
        Assert.Equal([3U], Assert.Single(saved.Quests).Intentions);
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(result.UpdatedSession.Character.PlayerQuests),
            File.ReadAllBytes(file.QuestPath));
        Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_236U);
        Assert.Contains("WorldServer persisted LegacyPlayerQuests", result.Log);
    }

    private static void AssertPlumeRefusal(WorldSession session, List<byte[]> sent, string log,
        IReadOnlyList<uint> expectedIntentions)
    {
        var quest = Assert.Single(session.Character.PlayerQuests.Quests, entry => entry.Uid == 1_007U);
        Assert.Equal(expectedIntentions, quest.Intentions);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.ItemId == 711_236U);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(sent), frame => frame.Uid == 1_007U);
        Assert.DoesNotContain(ParseGameItems(sent), item => item.ItemId == 711_236U);
        Assert.DoesNotContain("WorldServer applied legacy map 1002 Turtledove Plume reward", log);
        Assert.DoesNotContain("map1002-turtledove-plume-autopatcher", log);
        Assert.DoesNotContain("WorldServer persisted LegacyPlayerQuests", log);
    }

    private sealed class PlumeQuestFile : IDisposable
    {
        internal PlumeQuestFile(bool rootIsFile)
        {
            Root = Path.Combine(Path.GetTempPath(), "WolfConquerPlumeQuest-" + Guid.NewGuid().ToString("N"));
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                Path.GetDirectoryName(Path.GetFullPath(Root)));
            RequireOrdinaryAncestors(Root);
            if (rootIsFile) File.WriteAllBytes(Root, [11, 22, 33]);
            else Directory.CreateDirectory(Root);
        }

        internal string Root { get; }
        internal string QuestPath => Path.Combine(Root, KillerUid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin");

        public void Dispose()
        {
            RequireOrdinaryAncestors(Root);
            if (File.Exists(Root)) File.Delete(Root);
            else if (Directory.Exists(Root))
            {
                RequireOrdinaryAncestors(QuestPath);
                if (File.Exists(QuestPath)) File.Delete(QuestPath);
                Directory.Delete(Root, recursive: false);
            }
        }

        private static void RequireOrdinaryAncestors(string path)
        {
            for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
                if (File.Exists(current) || Directory.Exists(current))
                    Assert.Equal((FileAttributes)0, File.GetAttributes(current) & FileAttributes.ReparsePoint);
        }
    }
}
