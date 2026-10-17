using System.Buffers;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterHitInterruptionTests
{
    private const uint PlayerUid = 1_293_600;

    [Fact]
    public async Task NonlethalMonsterHitInterruptsSitAndSubtractsCappedStamina()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Action"] = "250",
            ["Stamina"] = "15",
        });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, session, Sessions: sessions);

        var updated = await InvokeSitInterruptionAsync(
            context,
            session,
            damage: 500,
            killedPlayer: false);

        Assert.Equal("0", updated.Character.Attributes["Action"]);
        Assert.Equal("0", updated.Character.Attributes["Stamina"]);

        var update = Assert.Single(ParseMsgUpdates(sent));
        Assert.Equal(PlayerUid, update.Uid);
        var stamina = Assert.Single(update.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Stamina);
        Assert.Equal(0UL, stamina.Value);
        Assert.Contains("WorldServer applied legacy monster hit sit interruption", log.ToString());
        Assert.Contains("source=MsgMonster.ActionHandler.CheckForOponnentDead Sit", log.ToString());
    }

    [Fact]
    public async Task LethalMonsterHitRemovesActivePickButSkipsSitInterruption()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ActivePick"] = "1",
            ["Action"] = "250",
            ["Stamina"] = "15",
        });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, session, Sessions: sessions);

        var afterActivePick = await InvokeActivePickRemovalAsync(context, session, damage: 500);
        var updated = await InvokeSitInterruptionAsync(
            context with { Session = afterActivePick },
            afterActivePick,
            damage: 500,
            killedPlayer: true);

        var action = Assert.Single(ParseMsgActions(sent));
        Assert.Equal(PlayerUid, action.ObjId);
        Assert.Equal(220U, action.DwParam);
        Assert.Equal(MsgActionType.ActionPick, action.Type);
        Assert.Equal(2, action.Facing);
        Assert.Equal(0, action.RemainTime);
        Assert.Equal(["Interrupted"], action.Strings);

        Assert.False(updated.Character.Attributes.ContainsKey("ActivePick"));
        Assert.Equal("250", updated.Character.Attributes["Action"]);
        Assert.Equal("15", updated.Character.Attributes["Stamina"]);
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Contains("WorldServer sent legacy monster hit ActivePick interruption", log.ToString());
        Assert.DoesNotContain("WorldServer applied legacy monster hit sit interruption", log.ToString());
    }

    private static async ValueTask<WorldSession> InvokeSitInterruptionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint damage,
        bool killedPlayer)
    {
        var method = GetPrivateHandlerMethod(
            "TryApplyLegacyMonsterHitSitInterruptionAsync",
            typeof(WorldPacketHandlerContext),
            typeof(WorldSession),
            typeof(uint),
            typeof(bool),
            typeof(CancellationToken));
        var invoked = method.Invoke(null, [context, session, damage, killedPlayer, CancellationToken.None]);
        var task = Assert.IsType<ValueTask<WorldSession>>(invoked);
        return await task.ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> InvokeActivePickRemovalAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint damage)
    {
        var method = GetPrivateHandlerMethod(
            "TryApplyLegacyMonsterHitActivePickRemovalAsync",
            typeof(WorldPacketHandlerContext),
            typeof(WorldSession),
            typeof(uint),
            typeof(CancellationToken));
        var invoked = method.Invoke(null, [context, session, damage, CancellationToken.None]);
        var task = Assert.IsType<ValueTask<WorldSession>>(invoked);
        return await task.ConfigureAwait(false);
    }

    private static MethodInfo GetPrivateHandlerMethod(string name, params Type[] types)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            name,
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types,
            modifiers: null);
        Assert.NotNull(method);
        return method;
    }

    private static WorldSession CreateSession(IReadOnlyDictionary<string, string> attributes)
    {
        return new WorldSession(
            AccountId: 1,
            Username: "monster-hit-interruption",
            new CharacterSnapshot(
                Uid: PlayerUid,
                Name: "MonsterHitInterruption",
                Body: 1_003,
                Face: 0,
                Hair: 0,
                Class: 142,
                FirstClass: 142,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: 1_015,
                X: 830,
                Y: 584,
                PreviousMapId: 1_015,
                PreviousMapX: 830,
                PreviousMapY: 584,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 500,
                MinMana: 500,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture://monster-hit-interruption",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty);
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

    private static IReadOnlyList<MsgActionPacket> ParseMsgActions(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in frames)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
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
}
