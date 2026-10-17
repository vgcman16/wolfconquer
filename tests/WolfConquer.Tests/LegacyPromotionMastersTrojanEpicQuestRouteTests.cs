using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTrojanEpicQuestRouteTests
{
    private const uint TrojanPromotionNpcId = 10_022;
    private const long CharacterUid = 1_293_736;
    private const byte EpicQuestRouteOption = 101;

    [Fact]
    public async Task Option101ReportsBlockedAutoPatcherWhenNoLocalPacketModelExists()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            TrojanPromotionNpcId,
            EpicQuestRouteOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_001, updated.Character.Class);

        var logText = log.ToString();
        Assert.Contains("PARITY-GAP LegacyNpc PromotionTrojan epic quest route", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:47782-47784", logText);
        Assert.Contains("option=101", logText);
        Assert.Contains("map=1002 x=267 y=194 npcId=10579", logText);
        Assert.Contains("QuestGUI.SendAutoPatcher(1002, 267, 194, 10579)", logText);
        Assert.Contains("no safe local packet model exists", logText);
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 100,
            Username: "trojan-epic-route-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "TrojanRouteHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 1_001,
                FirstClass: 1_001,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: null);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }
}
