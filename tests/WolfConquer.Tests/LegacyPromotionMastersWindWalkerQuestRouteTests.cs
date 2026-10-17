using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersWindWalkerQuestRouteTests
{
    private const uint WindWalkerPromotionNpcId = 19_634;
    private const long CharacterUid = 1_293_744;
    private const byte QuestRouteOption = 56;

    [Fact]
    public async Task Option56ReportsBlockedAutoPatcherWhenNoLocalPacketModelExists()
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
            WindWalkerPromotionNpcId,
            QuestRouteOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(16_002, updated.Character.Class);
        Assert.Equal(987_654, updated.Character.Experience);
        Assert.Equal(321, updated.Character.ConquerPoints);

        var logText = log.ToString();
        Assert.Contains("PARITY-GAP LegacyNpc PromotionWindWalker quest route", logText);
        Assert.Contains("source=NpcHandler.PromotionWindWalker", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:46276-46278", logText);
        Assert.Contains("option=56", logText);
        Assert.Contains("map=1002 x=384 y=251 npcId=7991", logText);
        Assert.Contains("QuestGUI.SendAutoPatcher(1002, 384, 251, 7991)", logText);
        Assert.Contains("no safe local packet model exists", logText);
        Assert.DoesNotContain("PromotionMaster mutation unavailable", logText);
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 100,
            Username: "windwalker-route-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "WindRouteHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 16_002,
                FirstClass: 16_002,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 987_654,
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
                ConquerPoints: 321,
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
