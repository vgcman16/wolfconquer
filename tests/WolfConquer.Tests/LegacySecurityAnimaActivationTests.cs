using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySecurityAnimaActivationTests
{
    private const uint NpcId = 489_123;

    [Fact]
    public async Task CorrectPinActivatesOnlyLiveSessionBeforeCurrentReplyFrames()
    {
        var sent = new List<byte[]>();
        var exposureOrder = new List<string>();
        var connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9_958),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                exposureOrder.Add("claimant");
                return ValueTask.CompletedTask;
            });
        var session = CreateSession();
        var sessions = new WorldSessionStore((changedConnection, previous, _) =>
        {
            if (ReferenceEquals(changedConnection, connection) && previous is not null)
            {
                exposureOrder.Add("session");
            }
        });
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            NpcId,
            optionId: 80,
            input: "ABC123",
            CancellationToken.None));

        Assert.True(sessions.TryGet(connection, out var stored));
        var activated = Assert.IsType<WorldSession>(stored);
        Assert.Equal("true", activated.Character.Attributes["TREPIN2"]);
        Assert.Equal("abc123", activated.Character.Attributes["PinCodeAnima"]);
        Assert.Equal("session", exposureOrder[0]);
        Assert.False(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(session.Character, activated.Character));

        var replies = sent.Select(ParseReply).ToArray();
        Assert.Equal(
            [
                LegacyNpcReplyInteractType.MessageBox,
                LegacyNpcReplyInteractType.Finish,
            ],
            replies.Select(reply => reply.InteractType).ToArray());
        Assert.Equal("Account activated", replies[0].Text);
        Assert.All(sent, bytes => Assert.Equal(LegacyNpcReplyPacket.Opcode, ReadFrame(bytes).Header.Opcode));
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("wire=7899", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("persistence=transient-source", log.ToString(), StringComparison.Ordinal);

        sent.Clear();
        Assert.True(await InvokeAsync(connection, sessions, activated, optionId: 0, input: string.Empty, log));
        Assert.Contains(
            sent.Select(ParseReply),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "Activated = true");
    }

    [Fact]
    public async Task ShortPinSendsSourceErrorThenReturnsToRootWithoutActivation()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession();
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(connection, sessions, session, optionId: 80, input: "short", log));

        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(session, stored);
        Assert.False(session.Character.Attributes.ContainsKey("TREPIN2"));
        var replies = sent.Select(ParseReply).ToArray();
        Assert.Equal("The old PIN code is not correct.", replies[0].Text);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, replies[1].InteractType);
        AssertRootReturned(replies.Skip(2));
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongSixCharacterPinReturnsSilentlyToRoot()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession();
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(connection, sessions, session, optionId: 80, input: "BAD999", log));

        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(session, stored);
        Assert.DoesNotContain(
            sent.Select(ParseReply),
            reply => reply.InteractType == LegacyNpcReplyInteractType.MessageBox);
        AssertRootReturned(sent.Select(ParseReply));
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CorrectPinWithoutSessionStoreFailsClosed()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        using var log = new StringWriter();

        Assert.True(await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: null),
            session,
            NpcId,
            optionId: 80,
            input: "ABC123",
            CancellationToken.None));

        Assert.Empty(sent);
        Assert.False(session.Character.Attributes.ContainsKey("TREPIN2"));
        Assert.Contains("session-store-missing", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 1_293_822,
            Username: "security-anima-activation-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_822,
                Name: "SecurityActivate",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 15,
                FirstClass: 15,
                SecondClass: 0,
                Reborn: 2,
                Level: 140,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PinCodeAnima"] = "abc123",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static LegacyNpcReplyPacket ParseReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply));
        return reply;
    }

    private static async ValueTask<bool> InvokeAsync(
        ConquerTcpConnection connection,
        WorldSessionStore sessions,
        WorldSession session,
        byte optionId,
        string input,
        TextWriter log)
    {
        return await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            NpcId,
            optionId,
            input,
            CancellationToken.None);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9_958),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static void AssertRootReturned(IEnumerable<LegacyNpcReplyPacket> replies)
    {
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text.Contains("protect your Anima", StringComparison.Ordinal));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }
}
