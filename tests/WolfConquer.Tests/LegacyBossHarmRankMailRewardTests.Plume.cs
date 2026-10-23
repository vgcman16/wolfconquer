using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Fact]
    public async Task HandlerMap1002Family2Quest1007IncrementsAndAddsTurtledovePlume()
    {
        var result = await KillMap1002Family2TurtledoveQuest1007MonsterAsync(objective0: 2);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_007U);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal([3U], quest.Intentions);
        var objectiveFrame = Assert.Single(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_007U);
        Assert.Equal([3U], objectiveFrame.Intentions);

        var plume = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_236U);
        Assert.Equal((ushort)1, plume.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            packet => packet.ItemId == 711_236U &&
                packet.StackSize == 1 &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);
        var objectiveIndex = result.Sent.FindIndex(bytes => MsgQuestDataPacket.TryParseObjectiveFrame(ReadFrame(bytes), out var packet) && packet.Uid == 1_007U);
        var itemIndex = result.Sent.FindIndex(bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) && packet.ItemId == 711_236U);
        Assert.True(objectiveIndex >= 0 && itemIndex > objectiveIndex);
        Assert.Contains("Turtledove Plume reward", result.Log);
        Assert.Contains("map1002-turtledove-plume-autopatcher", result.Log);
        Assert.Contains("finishNpcId=7991", result.Log);
        Assert.Contains("finishX=525", result.Log);
        Assert.Contains("finishY=318", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family2Quest1007SkipsWhenPlumeAlreadyOwned()
    {
        var result = await KillMap1002Family2TurtledoveQuest1007MonsterAsync(
            objective0: 2,
            existingPlume: true);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_007U);
        Assert.Equal([2U], quest.Intentions);
        var plume = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_236U);
        Assert.Equal((ushort)1, plume.StackSize);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_007U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_236U);
        Assert.DoesNotContain("Turtledove Plume reward", result.Log);
        Assert.DoesNotContain("map1002-turtledove-plume-autopatcher", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family2Quest1007SkipsWhenQuestNotAccepted()
    {
        var result = await KillMap1002Family2TurtledoveQuest1007MonsterAsync(
            objective0: 2,
            status: PlayerQuestStatus.Finished);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_007U);
        Assert.Equal(PlayerQuestStatus.Finished, quest.Status);
        Assert.Equal([2U], quest.Intentions);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_236U);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_007U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_236U);
        Assert.DoesNotContain("Turtledove Plume reward", result.Log);
    }

    [Fact]
    public async Task HandlerMap1002Family2Quest1007RefusesProgressWhenPlumeItemTypeMissing()
    {
        var result = await KillMap1002Family2TurtledoveQuest1007MonsterAsync(
            objective0: 2,
            includePlumeItemType: false);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 1_007U);
        Assert.Equal([2U], quest.Intentions);
        Assert.DoesNotContain(ParseQuestObjectiveFrames(result.Sent), packet => packet.Uid == 1_007U);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 711_236U);
        Assert.DoesNotContain(ParseGameItems(result.Sent), packet => packet.ItemId == 711_236U);
        Assert.DoesNotContain("map1002-turtledove-plume-autopatcher", result.Log);
        Assert.DoesNotContain("Turtledove Plume reward", result.Log);
        Assert.Contains("map1002-turtledove-plume", result.Log);
    }

}
