using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClanWarCurrentRewardPayoutTests
{
    private const long TunedClanWarCurrentConquerPointsReward = 300_000;
    private const long SourceClanWarCurrentConquerPointsReward = 15_000_000;
    private const ushort SourceClanWarCurrentStudyPointsReward = 10_000;
    private const int SourceClanWarCurrentInnerPowerPotencyReward = 10_000;
    private const ulong WinningClanId = 501;
    private const ulong RivalClanId = 502;
    private const ulong EventDynamicId = 77;
    private const long BaseConquerPoints = 100;
    private const uint TwinCityRewardItemId = 722_454;

    private static readonly (uint ItemId, ushort StackSize)[] ExpectedTwinCityRewardItems =
    [
        (3_009_003, 3),
        (3_324_226, 10),
        (4_050_001, 500),
        (4_040_001, 500),
        (TwinCityRewardItemId, 1),
    ];

    [Fact]
    public async Task CompleteWarPaysTunedSourceBundleOnlyToWinningClanLeader()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        using var log = new StringWriter();
        var sessions = new WorldSessionStore();

        var eligible = AddSession(
            sessions,
            CreateClanWarSession(
                playerUid: 1_293_600,
                name: "WinningLeader",
                clanId: WinningClanId,
                clanRank: 100,
                conquerPoints: BaseConquerPoints));
        var nonleader = AddSession(
            sessions,
            CreateClanWarSession(
                playerUid: 1_293_601,
                name: "WinningMember",
                clanId: WinningClanId,
                clanRank: 10,
                conquerPoints: BaseConquerPoints + 1));
        var rivalLeader = AddSession(
            sessions,
            CreateClanWarSession(
                playerUid: 1_293_602,
                name: "RivalLeader",
                clanId: RivalClanId,
                clanName: "RivalClan",
                clanRank: 100,
                conquerPoints: BaseConquerPoints + 2));

        await CompleteTwinClanWarAsync(log, sessions, eligible);

        AssertClanWarCurrentRewardGranted(
            GetUpdatedSession(sessions, eligible),
            eligible.Sent,
            expectedConquerPoints: BaseConquerPoints + TunedClanWarCurrentConquerPointsReward);
        AssertClanWarCurrentRewardNotGranted(
            GetUpdatedSession(sessions, nonleader),
            nonleader.Sent,
            expectedConquerPoints: BaseConquerPoints + 1);
        AssertClanWarCurrentRewardNotGranted(
            GetUpdatedSession(sessions, rivalLeader),
            rivalLeader.Sent,
            expectedConquerPoints: BaseConquerPoints + 2);

        var output = log.ToString();
        Assert.Contains($"sourceConquerPoints={SourceClanWarCurrentConquerPointsReward.ToString(CultureInfo.InvariantCulture)}", output);
        Assert.Contains($"tunedConquerPoints={TunedClanWarCurrentConquerPointsReward.ToString(CultureInfo.InvariantCulture)}", output);
        Assert.Contains("items=3009003x3,3324226x10,4050001x500,4040001x500,722454x1", output);
        Assert.Contains($"studyPoints={SourceClanWarCurrentStudyPointsReward.ToString(CultureInfo.InvariantCulture)}", output);
        Assert.Contains($"innerPowerPotency={SourceClanWarCurrentInnerPowerPotencyReward.ToString(CultureInfo.InvariantCulture)}", output);
        Assert.Contains("source=MsgClanWar.CityWar.GetRewards", output);
    }

    [Fact]
    public async Task CompleteWarRejectsWinningClanLeaderWhoIsNotAlive()
    {
        await AssertWinningLeaderIneligibleForCurrentRewardAsync(
            CreateClanWarSession(
                playerUid: 1_293_603,
                name: "DeadLeader",
                clanId: WinningClanId,
                clanRank: 100,
                minHitPoints: 0,
                conquerPoints: BaseConquerPoints + 3),
            expectedConquerPoints: BaseConquerPoints + 3);
    }

    [Fact]
    public async Task CompleteWarRejectsWinningClanLeaderOutsideEventDynamicId()
    {
        await AssertWinningLeaderIneligibleForCurrentRewardAsync(
            CreateClanWarSession(
                playerUid: 1_293_604,
                name: "WrongDynamicLeader",
                clanId: WinningClanId,
                clanRank: 100,
                dynamicId: 88,
                conquerPoints: BaseConquerPoints + 4),
            expectedConquerPoints: BaseConquerPoints + 4);
    }

    [Fact]
    public async Task CompleteWarRejectsWinningClanLeaderOutsideEventMap()
    {
        await AssertWinningLeaderIneligibleForCurrentRewardAsync(
            CreateClanWarSession(
                playerUid: 1_293_605,
                name: "WrongMapLeader",
                clanId: WinningClanId,
                clanRank: 100,
                mapId: 1_002,
                conquerPoints: BaseConquerPoints + 5),
            expectedConquerPoints: BaseConquerPoints + 5);
    }

    [Fact]
    public async Task CompleteWarRecordsClaimSoSecondCompletionDoesNotDoublePay()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        using var log = new StringWriter();
        var sessions = new WorldSessionStore();
        var eligible = AddSession(
            sessions,
            CreateClanWarSession(
                playerUid: 1_293_610,
                name: "WinningLeader",
                clanId: WinningClanId,
                clanRank: 100,
                conquerPoints: BaseConquerPoints));

        await CompleteTwinClanWarAsync(log, sessions, eligible);

        var firstUpdated = GetUpdatedSession(sessions, eligible);
        var expectedConquerPoints = BaseConquerPoints + TunedClanWarCurrentConquerPointsReward;
        AssertClanWarCurrentRewardGranted(firstUpdated, eligible.Sent, expectedConquerPoints);
        var firstRewardItemCount = firstUpdated.PlayerItems.InventoryItems.Count(item =>
            ExpectedTwinCityRewardItems.Any(expected => expected.ItemId == item.ItemId));

        eligible.Sent.Clear();
        await CompleteTwinClanWarAsync(log, sessions, eligible, firstUpdated);

        var secondUpdated = GetUpdatedSession(sessions, eligible);
        Assert.Equal(expectedConquerPoints, secondUpdated.Character.ConquerPoints);
        Assert.Equal(
            firstRewardItemCount,
            secondUpdated.PlayerItems.InventoryItems.Count(item =>
                ExpectedTwinCityRewardItems.Any(expected => expected.ItemId == item.ItemId)));
        Assert.DoesNotContain(ParseGameItems(eligible.Sent), item =>
            ExpectedTwinCityRewardItems.Any(expected => expected.ItemId == item.ItemId));
        Assert.DoesNotContain(
            ParseMsgUpdates(eligible.Sent),
            packet => packet.Updates.Any(IsConquerPointUpdate));
    }

    [Fact]
    public async Task CompleteWarWithoutWinnerRejectsCurrentRewardWithoutPaying()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        using var log = new StringWriter();
        var sessions = new WorldSessionStore();
        var leader = AddSession(
            sessions,
            CreateClanWarSession(
                playerUid: 1_293_620,
                name: "LeaderWithoutWinner",
                clanId: WinningClanId,
                clanRank: 100,
                winnerClanId: 0,
                winnerClanName: string.Empty,
                conquerPoints: BaseConquerPoints));

        await CompleteTwinClanWarAsync(log, sessions, leader);

        AssertClanWarCurrentRewardNotGranted(
            GetUpdatedSession(sessions, leader),
            leader.Sent,
            expectedConquerPoints: BaseConquerPoints);
        var output = log.ToString();
        Assert.Contains("WorldServer skipped legacy ClanWar current-war reward", output);
        Assert.Contains("missing winner", output, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CompleteTwinClanWarAsync(
        StringWriter log,
        WorldSessionStore sessions,
        TrackedSession tracked,
        WorldSession? sessionOverride = null)
    {
        var session = sessionOverride ?? tracked.InitialSession;
        await WorldPacketHandlers.SendDueLegacyClanWarCurrentCompleteWarFramesAsync(
                new WorldPacketHandlerContext(
                    tracked.Connection,
                    log,
                    session,
                    Sessions: sessions),
                new DateTimeOffset(2026, 6, 30, 12, 10, 0, TimeSpan.Zero),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static async Task AssertWinningLeaderIneligibleForCurrentRewardAsync(
        WorldSession ineligibleSession,
        long expectedConquerPoints)
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        using var log = new StringWriter();
        var sessions = new WorldSessionStore();
        var eligible = AddSession(
            sessions,
            CreateClanWarSession(
                playerUid: 1_293_630,
                name: "WinningLeader",
                clanId: WinningClanId,
                clanRank: 100,
                conquerPoints: BaseConquerPoints));
        var ineligible = AddSession(sessions, ineligibleSession);

        await CompleteTwinClanWarAsync(log, sessions, eligible);

        AssertClanWarCurrentRewardGranted(
            GetUpdatedSession(sessions, eligible),
            eligible.Sent,
            expectedConquerPoints: BaseConquerPoints + TunedClanWarCurrentConquerPointsReward);
        AssertClanWarCurrentRewardNotGranted(
            GetUpdatedSession(sessions, ineligible),
            ineligible.Sent,
            expectedConquerPoints);
    }

    private static WorldSession CreateClanWarSession(
        long playerUid,
        string name,
        ulong clanId,
        int clanRank,
        string clanName = "SourceClan",
        ulong winnerClanId = WinningClanId,
        string winnerClanName = "SourceClan",
        long mapId = LegacySobNpcEventRules.TwinCityClanWarMapId,
        ulong eventDynamicId = EventDynamicId,
        ulong dynamicId = EventDynamicId,
        int minHitPoints = 5_000,
        long conquerPoints = 0)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ClanID"] = clanId.ToString(CultureInfo.InvariantCulture),
            ["ClanUID"] = clanId.ToString(CultureInfo.InvariantCulture),
            ["ClanName"] = clanName,
            ["LegacyClanName"] = clanName,
            ["ClanRank"] = clanRank.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarProcess"] = "Alive",
            ["ClanWarProcess"] = "Alive",
            ["RuntimeClanWarProcess"] = "Alive",
            ["LegacyClanWarProces"] = "Alive",
            ["ClanWarProces"] = "Alive",
            ["RuntimeClanWarProces"] = "Alive",
            ["LegacyClanWarCurrentProcess"] = "Alive",
            ["ClanWarCurrentProcess"] = "Alive",
            ["RuntimeClanWarCurrentProcess"] = "Alive",
            ["LegacyClanWarCurrentProces"] = "Alive",
            ["ClanWarCurrentProces"] = "Alive",
            ["RuntimeClanWarCurrentProces"] = "Alive",
            ["LegacyClanWarCityProcess"] = "Alive",
            ["ClanWarCityProcess"] = "Alive",
            ["RuntimeClanWarCityProcess"] = "Alive",
            ["LegacyClanWarCityProces"] = "Alive",
            ["ClanWarCityProces"] = "Alive",
            ["RuntimeClanWarCityProces"] = "Alive",
            ["LegacyClanWarInWar"] = "true",
            ["ClanWarInWar"] = "true",
            ["RuntimeClanWarInWar"] = "true",
            ["LegacyClanWarCurrentCity"] = "Twin",
            ["ClanWarCurrentCity"] = "Twin",
            ["RuntimeClanWarCurrentCity"] = "Twin",
            ["LegacyClanWarCity"] = "Twin",
            ["ClanWarCity"] = "Twin",
            ["RuntimeClanWarCity"] = "Twin",
            ["LegacyClanWarCurrentDynamicID"] = eventDynamicId.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentDynamicID"] = eventDynamicId.ToString(CultureInfo.InvariantCulture),
            ["RuntimeClanWarCurrentDynamicID"] = eventDynamicId.ToString(CultureInfo.InvariantCulture),
            ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture),
            ["DynamicId"] = dynamicId.ToString(CultureInfo.InvariantCulture),
            ["ConquerPoints"] = conquerPoints.ToString(CultureInfo.InvariantCulture),
            ["ClassExperience"] = "0",
            ["SubProfInfo"] = "0#0#0#",
        };

        attributes["LegacyClanWarCurrentWinnerClanID"] = winnerClanId.ToString(CultureInfo.InvariantCulture);
        attributes["ClanWarCurrentWinnerClanID"] = winnerClanId.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyClanWarWinnerClanID"] = winnerClanId.ToString(CultureInfo.InvariantCulture);
        attributes["ClanWarWinnerClanID"] = winnerClanId.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyClanWarCurrentWinnerClanName"] = winnerClanName;
        attributes["ClanWarCurrentWinnerClanName"] = winnerClanName;
        attributes["LegacyClanWarWinnerClanName"] = winnerClanName;
        attributes["ClanWarWinnerClanName"] = winnerClanName;

        return new WorldSession(
            AccountId: playerUid,
            Username: name.ToLowerInvariant(),
            Character: new CharacterSnapshot(
                Uid: playerUid,
                Name: name,
                Body: 1_003,
                Face: 0,
                Hair: 0,
                Class: 142,
                FirstClass: 142,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: mapId,
                X: 100,
                Y: 100,
                PreviousMapId: mapId,
                PreviousMapX: 100,
                PreviousMapY: 100,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 300,
                AttributePoints: 0,
                MinHitPoints: minHitPoints,
                MinMana: 500,
                Money: 0,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture://legacy-clan-war-current-reward",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static TrackedSession AddSession(WorldSessionStore sessions, WorldSession session)
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        return new TrackedSession(connection, sent, session);
    }

    private static WorldSession GetUpdatedSession(WorldSessionStore sessions, TrackedSession tracked)
    {
        Assert.True(sessions.TryGet(tracked.Connection, out var updated));
        Assert.NotNull(updated);
        return updated;
    }

    private static void AssertClanWarCurrentRewardGranted(
        WorldSession updatedSession,
        IEnumerable<byte[]> sent,
        long expectedConquerPoints)
    {
        Assert.Equal(expectedConquerPoints, updatedSession.Character.ConquerPoints);
        Assert.Equal("10000#0#0#", updatedSession.Character.Attributes["SubProfInfo"]);
        Assert.Contains(
            ParseMsgUpdates(sent),
            packet => packet.Uid == checked((uint)updatedSession.Character.Uid) &&
                packet.Updates.Any(entry =>
                    IsConquerPointUpdate(entry) &&
                    entry.Value == checked((ulong)expectedConquerPoints)));

        foreach (var expected in ExpectedTwinCityRewardItems)
        {
            Assert.Contains(
                updatedSession.PlayerItems.InventoryItems,
                item => item.ItemId == expected.ItemId && item.StackSize == expected.StackSize);
            Assert.Contains(
                ParseGameItems(sent),
                item => item.ItemId == expected.ItemId && item.StackSize == expected.StackSize);
        }

        Assert.Contains(updatedSession.Character.Attributes, attribute =>
            IsClanWarCurrentRewardClaimAttribute(attribute.Key) &&
            bool.TryParse(attribute.Value, out var claimed) &&
            claimed);
    }

    private static void AssertClanWarCurrentRewardNotGranted(
        WorldSession updatedSession,
        IEnumerable<byte[]> sent,
        long expectedConquerPoints)
    {
        Assert.Equal(expectedConquerPoints, updatedSession.Character.ConquerPoints);
        Assert.Equal("0#0#0#", updatedSession.Character.Attributes["SubProfInfo"]);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(IsConquerPointUpdate));
        Assert.DoesNotContain(
            ParseGameItems(sent),
            item => ExpectedTwinCityRewardItems.Any(expected => expected.ItemId == item.ItemId));
        Assert.DoesNotContain(
            updatedSession.PlayerItems.InventoryItems,
            item => ExpectedTwinCityRewardItems.Any(expected => expected.ItemId == item.ItemId));
        Assert.DoesNotContain(updatedSession.Character.Attributes, attribute =>
            IsClanWarCurrentRewardClaimAttribute(attribute.Key) &&
            bool.TryParse(attribute.Value, out var claimed) &&
            claimed);
    }

    private static bool IsClanWarCurrentRewardClaimAttribute(string key)
    {
        return key.Contains("ClanWar", StringComparison.OrdinalIgnoreCase) &&
            key.Contains("Reward", StringComparison.OrdinalIgnoreCase) &&
            key.Contains("Claim", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConquerPointUpdate(LegacyMsgUpdateEntry entry)
    {
        return entry.Type is LegacyMsgUpdateDataType.Conquer or LegacyMsgUpdateDataType.ConquerPoints;
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyGameItemPacket> ParseGameItems(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyGameItemPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private sealed record TrackedSession(
        ConquerTcpConnection Connection,
        List<byte[]> Sent,
        WorldSession InitialSession);
}
