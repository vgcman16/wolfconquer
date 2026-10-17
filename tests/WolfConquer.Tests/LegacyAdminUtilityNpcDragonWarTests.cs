using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAdminUtilityNpcDragonWarTests
{
    private const uint StuffNpcId = 77_839;
    private const byte OpenDragonWarOptionId = 201;
    private const string DragonWarInvitationText = "DragonWar[SS/FB]";
    private const string DragonWarInvitationCenterText = " " + DragonWarInvitationText;

    [Fact]
    public async Task AdminStuffRootDialogExposesOpenDragonWarOptionForPrivilegedGm()
    {
        var result = await InvokeAsync(StuffNpcId, optionId: 0, gmName: true);

        Assert.True(result.Handled);
        Assert.Contains(
            ParseNpcReplies(result.GmSent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
                reply.OptionId == OpenDragonWarOptionId &&
                reply.Text == "Open DragonWar.");
        AssertAsciiReplies(result.GmSent);
    }

    [Fact]
    public async Task AdminOption201OpensDragonWarAndInvitesEveryActiveSession()
    {
        using var log = new StringWriter();
        var gmSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var gmConnection = CreateSendingConnection(gmSent, 9958);
        var peerConnection = CreateSendingConnection(peerSent, 9959);
        var sessions = new WorldSessionStore();
        var gmSession = CreateSession(uid: 1_293_600, name: "DragonWarAdmin[GM]");
        var peerSession = CreateSession(uid: 1_293_601, name: "DragonWarGuest");
        sessions.Set(gmConnection, gmSession);
        sessions.Set(peerConnection, peerSession);
        var dragonWarStates = new LegacyDragonWarRuntimeStateStore();

        var handled = await WorldPacketHandlers.TryHandleLegacyAdminUtilityNpcAsync(
            new WorldPacketHandlerContext(
                gmConnection,
                log,
                Session: gmSession,
                Sessions: sessions,
                DragonWarStates: dragonWarStates),
            gmSession,
            StuffNpcId,
            OpenDragonWarOptionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(LegacyDragonWarProcess.Idle, dragonWarStates.Process);
        AssertDragonWarInvitationSent(gmSent);
        AssertDragonWarInvitationSent(peerSent);
        AssertPendingDragonWarInvitation(sessions, gmConnection);
        AssertPendingDragonWarInvitation(sessions, peerConnection);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc AdminUtility", log.ToString());
    }

    [Fact]
    public async Task AdminOption201WhileDragonWarOpenDoesNotDuplicateInvitation()
    {
        using var log = new StringWriter();
        var gmSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var gmConnection = CreateSendingConnection(gmSent, 9960);
        var peerConnection = CreateSendingConnection(peerSent, 9961);
        var sessions = new WorldSessionStore();
        var gmSession = CreateSession(uid: 1_293_600, name: "DragonWarAdmin[GM]");
        var peerSession = CreateSession(uid: 1_293_601, name: "DragonWarGuest");
        sessions.Set(gmConnection, gmSession);
        sessions.Set(peerConnection, peerSession);
        var dragonWarStates = new LegacyDragonWarRuntimeStateStore();
        dragonWarStates.Open(new DateTimeOffset(2026, 6, 28, 19, 0, 0, TimeSpan.Zero));

        var handled = await WorldPacketHandlers.TryHandleLegacyAdminUtilityNpcAsync(
            new WorldPacketHandlerContext(
                gmConnection,
                log,
                Session: gmSession,
                Sessions: sessions,
                DragonWarStates: dragonWarStates),
            gmSession,
            StuffNpcId,
            OpenDragonWarOptionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(LegacyDragonWarProcess.Idle, dragonWarStates.Process);
        Assert.DoesNotContain(gmSent, IsDragonWarInvitationMessageBox);
        Assert.DoesNotContain(peerSent, IsDragonWarInvitationMessageBox);
        Assert.Empty(peerSent);

        var combinedText = string.Join(
            " ",
            ParseNpcReplies(gmSent).Select(reply => reply.Text).Append(log.ToString()));
        Assert.Contains("DragonWar", combinedText, StringComparison.Ordinal);
        Assert.Contains("already open", combinedText, StringComparison.OrdinalIgnoreCase);
        AssertAsciiReplies(gmSent);
    }

    [Fact]
    public async Task NormalPlayersCannotOpenDragonWarFromAdminUtility()
    {
        var result = await InvokeAsync(
            StuffNpcId,
            OpenDragonWarOptionId,
            gmName: false,
            dragonWarStates: new LegacyDragonWarRuntimeStateStore());

        Assert.True(result.Handled);
        Assert.Empty(result.GmSent);
        Assert.Equal(LegacyDragonWarProcess.Dead, result.DragonWarStates!.Process);
        Assert.Contains("ignored LegacyNpc admin utility for non-privileged character", result.Log);
    }

    private static async Task<AdminDragonWarInvokeResult> InvokeAsync(
        uint npcId,
        byte optionId,
        bool gmName,
        LegacyDragonWarRuntimeStateStore? dragonWarStates = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent, 9958);
        var session = CreateSession(uid: 1_293_600, name: gmName ? "AdminUtilityHero[GM]" : "AdminUtilityHero");
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyAdminUtilityNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                DragonWarStates: dragonWarStates),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        return new AdminDragonWarInvokeResult(handled, sent, log.ToString(), dragonWarStates);
    }

    private static void AssertDragonWarInvitationSent(IReadOnlyList<byte[]> sent)
    {
        Assert.Contains(sent, IsDragonWarSystemCenter);
        Assert.Contains(sent, IsDragonWarInvitationMessageBox);
    }

    private static bool IsDragonWarSystemCenter(byte[] bytes)
    {
        return LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
            message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
            message.Content.Contains(DragonWarInvitationCenterText);
    }

    private static bool IsDragonWarInvitationMessageBox(byte[] bytes)
    {
        return LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply) &&
            reply.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            reply.Text == DragonWarInvitationText;
    }

    private static void AssertPendingDragonWarInvitation(
        WorldSessionStore sessions,
        ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(
            LegacyRuntimeMessageBoxPrompt.DragonWarInvitationKey,
            updated.Character.Attributes[LegacyRuntimeMessageBoxPrompt.PendingAttribute]);
        Assert.Equal("1002", updated.Character.Attributes[LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute]);
        Assert.Equal("319", updated.Character.Attributes[LegacyRuntimeMessageBoxPrompt.DestinationXAttribute]);
        Assert.Equal("256", updated.Character.Attributes[LegacyRuntimeMessageBoxPrompt.DestinationYAttribute]);
        Assert.Equal("0", updated.Character.Attributes[LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute]);
        Assert.Equal("60", updated.Character.Attributes[LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute]);
    }

    private static WorldSession CreateSession(long uid, string name)
    {
        return new WorldSession(
            AccountId: uid,
            Username: name,
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 100,
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
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent, int port)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, port), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertAsciiReplies(IEnumerable<byte[]> sent)
    {
        foreach (var reply in ParseNpcReplies(sent))
        {
            if (!string.IsNullOrEmpty(reply.Text))
            {
                Assert.True(reply.Text.All(static character => character <= 127), reply.Text);
            }
        }
    }

    private sealed record AdminDragonWarInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> GmSent,
        string Log,
        LegacyDragonWarRuntimeStateStore? DragonWarStates);
}
