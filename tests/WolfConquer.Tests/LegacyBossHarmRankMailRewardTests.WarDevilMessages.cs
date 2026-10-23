using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Fact]
    public async Task HandlerDeityLandWarDevilUndefinedLuckyRewardPreservesPointsAndSuppressesReceipt()
    {
        var monsterFamily = CreateBossFamily(id: 10_260, name: "300-StarWarDevil", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(3_316_793)],
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var peerSent = new List<byte[]>();
        var peerConnection = CreateConnection(peerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "0"),
                ("DeityLandLuckyPoints", "12"),
                ("LegacyDeityLandWarDevilLuckyRewardRoll", "22"),
                ("LegacyMonsterDropRoll", "40")));
        sessions.Set(killerConnection, killerSession);
        sessions.Set(peerConnection, CreateSession(8_888, "Watcher"));

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("12", updatedSession!.Character.Attributes["DeityLandLuckyPoints"]);
        Assert.Empty(updatedSession.PlayerItems.InventoryItems);

        var expectedBroadcast =
            "LastHit defeated a devil in the Deityland and received a rare treasure Item3316793!";
        Assert.DoesNotContain(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content.Contains("You've consumed 10 Luck Points", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content == expectedBroadcast));
        Assert.DoesNotContain(
            ParseMessages(peerSent),
            packet => packet.Content.Any(content => content == expectedBroadcast));
        Assert.DoesNotContain(
            ParseStringPackets(killerSent),
            packet => packet.Uid == KillerUid &&
                packet.Id == LegacyStringId.Effect &&
                packet.Strings.SequenceEqual(["angelwing"]));
        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 3_316_793U);

        var output = log.ToString();
        Assert.Contains("WarDevil", output);
        Assert.Contains("itemRewardDefinitionUnavailable", output);
        Assert.DoesNotContain("WarDevil reward notice", output);
    }

    [Fact]
    public async Task HandlerDeityLandWarDevilUndefinedBaseRewardDoesNotBroadcastTreasure()
    {
        var monsterFamily = CreateBossFamily(id: 10_261, name: "300-StarWarDevil", boss: false);
        var spawn = CreateBossSpawn(monsterFamily, mapId: 10_250);
        var worldData = CreateWorldDataCatalog(
            monsterFamily,
            spawn,
            items: [CreateItemType(3_008_735)],
            questMissions: [CreateQuestMission(3_944, intentAmount: 1)]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var killerSent = new List<byte[]>();
        var killerConnection = CreateConnection(killerSent);
        var killerSession = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 10_250,
            attributes: DeityLandInventoryRewardsDisabledAttributes(
                ("LegacyDeityLandRandomDropRate", "0"),
                ("DeityLandLuckyPoints", "9"),
                ("LegacyDeityLandWarDevilBaseRewardRoll", "2"),
                ("LegacyMonsterDropRoll", "40")));
        sessions.Set(killerConnection, killerSession);

        await KillBossAsync(
            killerConnection,
            killerSession,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log);

        Assert.True(sessions.TryGet(killerConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("9", updatedSession!.Character.Attributes["DeityLandLuckyPoints"]);
        Assert.Empty(updatedSession.PlayerItems.InventoryItems);

        Assert.DoesNotContain(
            ParseMessages(killerSent),
            packet => packet.Content.Any(content => content.Contains("You've consumed 10 Luck Points", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            ParseMessages(killerSent),
            packet => packet.Content.Any(
                content => content == "LastHit defeated a devil in the Deityland and received a rare treasure Item3008735!"));
        Assert.DoesNotContain(
            ParseStringPackets(killerSent),
            packet => packet.Uid == KillerUid &&
                packet.Id == LegacyStringId.Effect &&
                packet.Strings.SequenceEqual(["angelwing"]));
        Assert.DoesNotContain(ParseGameItems(killerSent), packet => packet.ItemId == 3_008_735U);

        var output = log.ToString();
        Assert.Contains("WarDevil", output);
        Assert.Contains("itemRewardDefinitionUnavailable", output);
        Assert.DoesNotContain("WarDevil reward notice", output);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("9")]
    [InlineData("10")]
    [InlineData("18446744073709551615")]
    public async Task RewardMessageUndefinedWarDevilPayoutNeverSpendsEntitlementOrAnnouncesTreasure(string points)
    {
        var scene = await KillRewardMessageMonsterAsync("war",
            luckyPoints: ulong.Parse(points, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Null(scene.Failure);
        Assert.NotNull(scene.Current);
        Assert.Equal(points, scene.Current.Character.Attributes["DeityLandLuckyPoints"]);
        Assert.Same(scene.Original.Character, scene.Current.Character);
        Assert.Equal(scene.OriginalRows, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Current.PlayerItems));
        Assert.Equal(scene.ItemBefore, scene.ItemAfter);
        Assert.Equal(scene.UserBefore, scene.UserAfter);
        Assert.DoesNotContain(ParseMessages(scene.Sent.Concat(scene.PeerSent)), packet =>
            packet.Content.Any(text => text.Contains("received a rare treasure", StringComparison.Ordinal) ||
                text.Contains("You've consumed 10 Luck Points", StringComparison.Ordinal)));
        Assert.DoesNotContain(ParseStringPackets(scene.Sent), packet => packet.Strings.Contains("angelwing"));
        Assert.Empty(ParseGameItems(scene.Sent));
        Assert.Contains("itemRewardDefinitionUnavailable", scene.Log);
    }
}
