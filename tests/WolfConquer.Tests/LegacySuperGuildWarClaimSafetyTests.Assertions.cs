using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySuperGuildWarClaimSafetyTests
{
    private static void AssertNoPreparationEffects(Attempt result)
    {
        AssertUnawardedStateAndFiles(result);
        Assert.Empty(result.OwnerFrames);
        Assert.All(result.OtherFrames, frames => Assert.Empty(frames));
    }

    private static void AssertDialogRefusal(Attempt result, string text)
    {
        Assert.Null(result.Failure);
        AssertUnawardedStateAndFiles(result);
        AssertFrames(Dialog(text), result.OwnerFrames);
        Assert.All(result.OtherFrames, frames => Assert.Empty(frames));
    }

    private static void AssertUnawardedStateAndFiles(Attempt result)
    {
        Assert.True(result.After.ActiveNpcId == result.Before.ActiveNpcId || result.After.ActiveNpcId == NpcId);
        Assert.Equal(result.Before with { ActiveNpcId = result.After.ActiveNpcId }, result.After);
        Assert.Same(result.Before.Character, result.After.Character);
        Assert.Same(result.Before.PlayerItems, result.After.PlayerItems);
        Assert.Equal(result.BeforeAttributes, result.Before.Character.Attributes.ToArray());
        Assert.Equal(result.BeforeItems, PlayerItemSnapshotLoader.ToLegacyBytes(result.Before.PlayerItems));
        for (var index = 0; index < result.BeforeFiles.Length; index++)
            Assert.Equal(result.BeforeFiles[index], result.AfterFiles[index]);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(result.Before.PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(result.SavedItems));
    }

    private static void AssertAward(Attempt result, ulong rank)
    {
        Assert.Null(result.Failure);
        Assert.Contains("WorldServer handled LegacyNpcReply", result.Log);
        Assert.Contains("source=NpcHandler.SuperGuildWar", result.Log);
        Assert.Contains("WorldServer handled LegacyNpc SuperGuildWar reward", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
        var leader = rank == 1;
        var before = result.Before;
        var after = result.After;
        Assert.Equal(result.BeforeAttributes, before.Character.Attributes.ToArray());
        Assert.Equal(result.BeforeItems, PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems));
        Assert.NotSame(before, after);
        Assert.Equal(before with { Character = after.Character, PlayerItems = after.PlayerItems,
            ActiveNpcId = after.ActiveNpcId }, after);
        Assert.Equal(before.Character with
        {
            Money = before.Character.Money + (leader ? 500_000 : 0),
            ConquerPoints = before.Character.ConquerPoints + (leader ? 300_000 : 0),
            Attributes = after.Character.Attributes,
        }, after.Character);
        Assert.Equal(after.Character.ConquerPoints, result.Reloaded.ConquerPoints);
        Assert.Equal(after.Character.Money, result.Reloaded.Money);
        Assert.Equal(after.Character.Name, result.Reloaded.Name);
        Assert.Equal(after.Character.Uid, result.Reloaded.Uid);
        Assert.Equal(before.Character.Level, result.Reloaded.Level);
        Assert.Equal("keep", result.Reloaded.Attributes["Sentinel"]);
        var kind = leader ? "Leader" : "DeputyLeader";
        var flag = leader ? 151 : 152;
        Assert.Equal("true", after.Character.Attributes["LegacySuperGuildWarRewardClaimed"]);
        Assert.Equal("true", after.Character.Attributes[leader
            ? "LegacySuperGuildWarLeaderRewardClaimed" : "LegacySuperGuildWarDeputyRewardClaimed"]);
        Assert.Equal(kind, after.Character.Attributes["LegacySuperGuildWarRewardKind"]);
        var words = new ulong[18]; words[flag / 32] = 1UL << (flag % 32);
        var savedFlags = string.Join('/', words.Take(10)) + "/";
        Assert.Equal(savedFlags, after.Character.Attributes["StatusFlags"]);
        Assert.Equal(savedFlags, result.Reloaded.Attributes["StatusFlags"]);
        var frames = new List<byte[]>();
        PlayerItemSnapshotItem? granted = null;
        if (leader)
        {
            frames.Add(Scalar(after.Character.Uid, LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)after.Character.ConquerPoints)));
            frames.Add(Scalar(after.Character.Uid, LegacyMsgUpdateDataType.Money,
                checked((ulong)after.Character.Money)));
            granted = Assert.Single(after.PlayerItems.InventoryItems, row => row.ItemId == RewardId);
            Assert.True(granted.Uid != 0);
            Assert.DoesNotContain(before.PlayerItems.InventoryItems, row => row.Uid == granted.Uid);
            var expected = new PlayerItemSnapshotItem
            {
                Uid = granted.Uid, ItemId = RewardId, Durability = RewardDurability,
                MaximumDurability = RewardDurability, Position = 0, StackSize = 1,
            };
            Assert.Equal(ItemBytes(expected), ItemBytes(granted));
            frames.Add(LegacyGameItemPacket.FromSnapshotItem(expected).ToFrameBytes());
        }
        else Assert.Same(before.PlayerItems, after.PlayerItems);
        Assert.Equal(before.PlayerItems.InventoryItems.Count + (leader ? 1 : 0), after.PlayerItems.InventoryItems.Count);
        for (var index = 0; index < before.PlayerItems.InventoryItems.Count; index++)
            Assert.Same(before.PlayerItems.InventoryItems[index], after.PlayerItems.InventoryItems[index]);
        var expectedItems = leader ? before.PlayerItems with
        { InventoryItems = before.PlayerItems.InventoryItems.Concat([granted!]).ToArray() } : before.PlayerItems;
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(after.PlayerItems));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(result.SavedItems));
        frames.Add(LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
            checked((uint)after.Character.Uid), words).ToFrameBytes());
        frames.AddRange(Dialog(leader
            ? "You claimed 300000 CPs, 500000 silver, and item 2100065 for winning Super Guild War."
            : "You claimed the Deputy Leader Super Guild War title flag."));
        var role = leader ? "GuildLeader" : "DeputyLeader";
        var text = leader
            ? $"{after.Character.Name} , {role} from WinnerGuild was rewarded with 300000 Cps for the domination the SuperGuildWar."
            : $"{after.Character.Name} , {role} from WinnerGuild claimed the SuperGuildWar title reward.";
        var broadcast = new LegacyMsgMessagePacket(LegacyMsgMessagePacket.RedColor, 2010,
            0, 0, 0, 0, 0, 1, 0, 0, ["SYSTEM", "ALL", "", text, "", "", ""]).ToFrameBytes();
        frames.Add(broadcast);
        AssertFrames(frames, result.OwnerFrames);
        Assert.All(result.OtherFrames, other => Assert.Equal(broadcast, Assert.Single(other)));
        var ownerIndex = checked((int)after.AccountId - 100);
        for (var index = 0; index < result.BeforeFiles.Length; index++)
            if (index / 2 != ownerIndex) Assert.Equal(result.BeforeFiles[index], result.AfterFiles[index]);
    }

    private static byte[] ItemBytes(PlayerItemSnapshotItem item) =>
        PlayerItemSnapshotLoader.ToLegacyBytes(new([item], [], [], [], []));

    private static byte[] Scalar(long uid, LegacyMsgUpdateDataType type, ulong value) =>
        new LegacyMsgUpdatePacket(checked((uint)uid), [new LegacyMsgUpdateEntry(type, value)]).ToFrameBytes();

    private static void AssertFrames(IReadOnlyList<byte[]> expected, IReadOnlyList<byte[]> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++) Assert.Equal(expected[index], actual[index]);
    }

    private static byte[][] Dialog(string text) =>
    [
        LegacyNpcReplyPacket.ForDialog(text).ToFrameBytes(),
        LegacyNpcReplyPacket.ForClosedOption("Okay.").ToFrameBytes(),
        LegacyNpcReplyPacket.ForAvatar(110).ToFrameBytes(),
        LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
    ];
}
