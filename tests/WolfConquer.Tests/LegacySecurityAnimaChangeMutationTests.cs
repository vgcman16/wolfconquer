using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySecurityAnimaChangeMutationTests : IDisposable
{
    private const uint NpcId = 489_123;
    private const long AccountId = 1_293_821;
    private const long CharacterUid = 1_293_821;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-security-anima-change-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CorrectOldPinStagesMaskedProofThenPersistsNewPinBeforeCurrentReplyFrames()
    {
        var usersRoot = Path.Combine(_root, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteCharacter(Path.Combine(
            usersRoot,
            AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"));

        var sent = new List<byte[]>();
        var exposureOrder = new List<string>();
        var connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9_958),
            (frame, _) =>
            {
                var bytes = frame.ToArray();
                sent.Add(bytes);
                exposureOrder.Add(
                    "claimant:" + ReadFrame(bytes).Header.Opcode.ToString(CultureInfo.InvariantCulture));
                return ValueTask.CompletedTask;
            });
        CharacterSnapshot? persistedAtCommit = null;
        var sessions = new WorldSessionStore((changedConnection, previous, current) =>
        {
            if (!ReferenceEquals(changedConnection, connection) ||
                previous is null ||
                !current.Character.Attributes.TryGetValue("PinCodeAnima", out var pin) ||
                pin != "new456")
            {
                return;
            }

            persistedAtCommit = new CharacterSnapshotLoader(usersRoot).LoadByAccountId(AccountId);
            exposureOrder.Add("commit");
        });
        var session = CreateSession(usersRoot);
        sessions.Set(connection, session);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);

        Assert.True(await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            context,
            session,
            NpcId,
            optionId: 11,
            input: "OLD123",
            CancellationToken.None));

        Assert.True(sessions.TryGet(connection, out var verified));
        var verifiedSession = Assert.IsType<WorldSession>(verified);
        Assert.Equal("***123", verifiedSession.LegacyAnimaSecurityPendingPin);
        Assert.True(verifiedSession.LegacyAnimaSecurityOldPinVerified);
        var promptReplies = sent.Select(ParseReply).ToArray();
        Assert.Equal(
            [
                LegacyNpcReplyInteractType.Dialog,
                LegacyNpcReplyInteractType.Dialog,
                LegacyNpcReplyInteractType.Input,
                LegacyNpcReplyInteractType.Avatar,
                LegacyNpcReplyInteractType.Finish,
            ],
            promptReplies.Select(reply => reply.InteractType).ToArray());
        Assert.Equal("Is this the old PIN code? ***123", promptReplies[0].Text);
        Assert.Equal("If correct, please enter the new PIN code.", promptReplies[1].Text);
        Assert.Equal("New PIN:", promptReplies[2].Text);
        Assert.Equal((byte)13, promptReplies[2].OptionId);
        Assert.Equal((ushort)6, promptReplies[2].InputMaxLength);
        Assert.Equal((ushort)286, promptReplies[3].InputMaxLength);

        sent.Clear();
        exposureOrder.Clear();
        Assert.True(await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            context with { Session = verifiedSession },
            verifiedSession,
            NpcId,
            optionId: 13,
            input: "NeW456",
            CancellationToken.None));

        Assert.True(sessions.TryGet(connection, out var changed));
        var changedSession = Assert.IsType<WorldSession>(changed);
        Assert.Equal("new456", changedSession.Character.Attributes["PinCodeAnima"]);
        Assert.False(changedSession.LegacyAnimaSecurityOldPinVerified);
        Assert.Equal("commit", exposureOrder[0]);
        var committed = Assert.IsType<CharacterSnapshot>(persistedAtCommit);
        Assert.Equal("new456", committed.Attributes["PinCodeAnima"]);
        var reloaded = Assert.IsType<CharacterSnapshot>(
            new CharacterSnapshotLoader(usersRoot).LoadByAccountId(AccountId));
        Assert.Equal("new456", reloaded.Attributes["PinCodeAnima"]);

        var changeReplies = sent.Select(ParseReply).ToArray();
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, changeReplies[0].InteractType);
        Assert.Equal("Change completed", changeReplies[0].Text);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, changeReplies[1].InteractType);
        Assert.Contains(
            changeReplies.Skip(2),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text.Contains("protect your Anima", StringComparison.Ordinal));
        Assert.All(sent, bytes => Assert.Equal(LegacyNpcReplyPacket.Opcode, ReadFrame(bytes).Header.Opcode));
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("wire=7899", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongOldPinStoresMaskedValueWithoutVerificationAndReturnsToRoot()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot: null);
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(connection, sessions, session, optionId: 11, input: "BAD999", log));

        Assert.True(sessions.TryGet(connection, out var stored));
        var rejected = Assert.IsType<WorldSession>(stored);
        Assert.Equal("***999", rejected.LegacyAnimaSecurityPendingPin);
        Assert.False(rejected.LegacyAnimaSecurityOldPinVerified);
        Assert.Equal("old123", rejected.Character.Attributes["PinCodeAnima"]);
        var replies = sent.Select(ParseReply).ToArray();
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, replies[0].InteractType);
        Assert.Equal("The old PIN code is not correct.", replies[0].Text);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, replies[1].InteractType);
        AssertRootReturned(replies.Skip(2));
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShortOldPinReturnsLengthMessageThenRepeatsOldPinPrompt()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot: null, pendingPin: "stale", oldPinVerified: true);
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(connection, sessions, session, optionId: 11, input: "short", log));

        Assert.True(sessions.TryGet(connection, out var stored));
        var rejected = Assert.IsType<WorldSession>(stored);
        Assert.Equal("stale", rejected.LegacyAnimaSecurityPendingPin);
        Assert.False(rejected.LegacyAnimaSecurityOldPinVerified);
        var replies = sent.Select(ParseReply).ToArray();
        Assert.Equal("Please enter exactly six letters or digits.", replies[0].Text);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, replies[1].InteractType);
        Assert.Contains(
            replies.Skip(2),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Input &&
                reply.OptionId == 11 &&
                reply.Text == "Old PIN:");
    }

    [Fact]
    public async Task ForgedDirectChangeIsSafetyFencedWithoutDiskOrSessionMutation()
    {
        var usersRoot = Path.Combine(_root, "forged", "Users");
        Directory.CreateDirectory(usersRoot);
        var characterPath = Path.Combine(
            usersRoot,
            AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        WriteCharacter(characterPath);
        var originalBytes = File.ReadAllBytes(characterPath);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot);
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(connection, sessions, session, optionId: 13, input: "new456", log));

        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(session, stored);
        Assert.Equal(originalBytes, File.ReadAllBytes(characterPath));
        Assert.Equal("old123", session.Character.Attributes["PinCodeAnima"]);
        AssertRootReturned(sent.Select(ParseReply));
        Assert.Contains("old-pin-not-verified", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShortNewPinKeepsVerifiedStateAndOldPinForRetry()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            usersRoot: null,
            pendingPin: "***123",
            oldPinVerified: true);
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(connection, sessions, session, optionId: 13, input: "short", log));

        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(session, stored);
        Assert.True(session.LegacyAnimaSecurityOldPinVerified);
        Assert.Equal("old123", session.Character.Attributes["PinCodeAnima"]);
        var replies = sent.Select(ParseReply).ToArray();
        Assert.Equal("Please enter exactly six letters or digits.", replies[0].Text);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, replies[1].InteractType);
        AssertRootReturned(replies.Skip(2));
    }

    [Theory]
    [InlineData("sessions", "session-store-missing")]
    [InlineData("users-root", "usersRootMissing")]
    [InlineData("snapshot", "snapshotMissing")]
    public async Task MissingChangeInfrastructureFailsClosed(string missing, string expectedReason)
    {
        var usersRoot = Path.Combine(_root, missing, "Users");
        Directory.CreateDirectory(usersRoot);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            missing == "users-root" ? null : usersRoot,
            pendingPin: "***123",
            oldPinVerified: true);
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: missing == "sessions" ? null : sessions),
            session,
            NpcId,
            optionId: 13,
            input: "new456",
            CancellationToken.None));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(session, stored);
        Assert.Equal("old123", session.Character.Attributes["PinCodeAnima"]);
        Assert.Contains(expectedReason, log.ToString(), StringComparison.Ordinal);
        Assert.Contains("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LockedCharacterFileRollsBackChangeWithoutExposure()
    {
        var usersRoot = Path.Combine(_root, "locked", "Users");
        Directory.CreateDirectory(usersRoot);
        var characterPath = Path.Combine(
            usersRoot,
            AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        WriteCharacter(characterPath);
        var originalBytes = File.ReadAllBytes(characterPath);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot, pendingPin: "***123", oldPinVerified: true);
        sessions.Set(connection, session);
        using var log = new StringWriter();

        using (new FileStream(characterPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.True(await InvokeAsync(connection, sessions, session, optionId: 13, input: "new456", log));
        }

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(session, stored);
        Assert.Equal(originalBytes, File.ReadAllBytes(characterPath));
        Assert.Contains("transactionWriteFailed", log.ToString(), StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(usersRoot, "*.reward-points-*"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        string? usersRoot,
        string pendingPin = "",
        bool oldPinVerified = false)
    {
        return new WorldSession(
            AccountId,
            Username: "security-anima-change-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "SecurityChange",
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
                SourceFile: usersRoot is null
                    ? "fixture"
                    : Path.Combine(
                        usersRoot,
                        AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PinCodeAnima"] = "old123",
                    ["Sentinel"] = "keep-me",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot,
            LegacyAnimaSecurityPendingPin: pendingPin,
            LegacyAnimaSecurityOldPinVerified: oldPinVerified);
    }

    private static void WriteCharacter(string path)
    {
        File.WriteAllText(
            path,
            "[Character]\r\n" +
            $"UID={CharacterUid}\r\n" +
            "Name=SecurityChange\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
            "FirstClass=15\r\nSecoundeClass=0\r\nReborn=2\r\nLevel=140\r\nExperience=0\r\n" +
            "Map=1002\r\nX=430\r\nY=378\r\nPMap=1002\r\nPMapX=430\r\nPMapY=378\r\n" +
            "Strength=0\r\nAgility=0\r\nVitaliti=0\r\nSpirit=0\r\nAtributes=0\r\n" +
            "MinHitPoints=1\r\nMinMana=0\r\nMoney=0\r\nConquerPoints=0\r\n" +
            "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\n" +
            "PinCodeAnima=old123\r\nSentinel=keep-me\r\n",
            Encoding.Latin1);
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
