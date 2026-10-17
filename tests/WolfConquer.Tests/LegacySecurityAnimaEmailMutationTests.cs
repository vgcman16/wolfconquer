using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySecurityAnimaEmailMutationTests : IDisposable
{
    private const uint NpcId = 489_123;
    private const long AccountId = 1_293_824;
    private const long CharacterUid = 1_293_824;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-security-anima-email-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RecoveryMatchAppendsYahooAndRevealsPinOnCurrentReplyFrames()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot: null);
        var verifier = new RecordingEmailVerifier(matches: true);
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(
            connection,
            sessions,
            session,
            verifier,
            optionId: 6,
            input: "player",
            log));

        Assert.Equal([(AccountId, "player@yahoo.com")], verifier.Calls);
        var replies = sent.Select(ParseReply).ToArray();
        Assert.Equal(
            [
                LegacyNpcReplyInteractType.Dialog,
                LegacyNpcReplyInteractType.Option,
                LegacyNpcReplyInteractType.Avatar,
                LegacyNpcReplyInteractType.Finish,
            ],
            replies.Select(reply => reply.InteractType).ToArray());
        Assert.Equal("Your PIN code [ AbC123 ]", replies[0].Text);
        Assert.Equal("Just passing by.", replies[1].Text);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, replies[1].OptionId);
        Assert.Equal((ushort)86, replies[2].InputMaxLength);
        Assert.All(sent, bytes => Assert.Equal(LegacyNpcReplyPacket.Opcode, ReadFrame(bytes).Header.Opcode));
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("wire=7899", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("player@yahoo.com", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("AbC123", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecoveryMismatchPreservesExplicitDomainAndDoesNotRevealPin()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot: null);
        var verifier = new RecordingEmailVerifier(matches: false);
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(
            connection,
            sessions,
            session,
            verifier,
            optionId: 6,
            input: "player@example.com",
            log));

        Assert.Equal([(AccountId, "player@example.com")], verifier.Calls);
        var replies = sent.Select(ParseReply).ToArray();
        Assert.Equal("Please re-enter your data.", replies[0].Text);
        Assert.Equal("Okay.", replies[1].Text);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, replies[1].OptionId);
        Assert.Equal((ushort)86, replies[2].InputMaxLength);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, replies[3].InteractType);
        Assert.DoesNotContain(replies, reply => reply.Text.Contains("AbC123", StringComparison.Ordinal));
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(60)]
    public async Task EmptyEmailInputPreservesSourceNoOp(byte optionId)
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot: null);
        var verifier = new RecordingEmailVerifier(matches: true);
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(
            connection,
            sessions,
            session,
            verifier,
            optionId,
            input: string.Empty,
            log));

        Assert.Empty(verifier.Calls);
        Assert.Empty(sent);
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrFailingEmailAuthorityFailsClosed(bool throws)
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot: null);
        var verifier = throws
            ? new RecordingEmailVerifier(matches: false, new IOException("database unavailable"))
            : null;
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(
            connection,
            sessions,
            session,
            verifier,
            optionId: 6,
            input: "private@example.com",
            log));

        Assert.Contains(
            sent.Select(ParseReply),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            sent.Select(ParseReply),
            reply => reply.Text.Contains("AbC123", StringComparison.Ordinal));
        Assert.Contains("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private@example.com", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("AbC123", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemovalMatchPersistsEmptyPinAndClearsActivationBeforeReplyFrames()
    {
        var usersRoot = CreateUsersRootWithCharacter();
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
        var sessions = new WorldSessionStore((changedConnection, previous, current) =>
        {
            if (!ReferenceEquals(changedConnection, connection) || previous is null)
            {
                return;
            }

            var persisted = Assert.IsType<CharacterSnapshot>(
                new CharacterSnapshotLoader(usersRoot).LoadByAccountId(AccountId));
            Assert.Equal(string.Empty, persisted.Attributes["PinCodeAnima"]);
            Assert.Equal("false", current.Character.Attributes["TREPIN2"]);
            exposureOrder.Add("session");
        });
        var session = CreateSession(usersRoot);
        var verifier = new RecordingEmailVerifier(matches: true);
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(
            connection,
            sessions,
            session,
            verifier,
            optionId: 60,
            input: "player",
            log));

        Assert.Equal([(AccountId, "player@yahoo.com")], verifier.Calls);
        Assert.True(sessions.TryGet(connection, out var stored));
        var updated = Assert.IsType<WorldSession>(stored);
        Assert.Equal(string.Empty, updated.Character.Attributes["PinCodeAnima"]);
        Assert.Equal("false", updated.Character.Attributes["TREPIN2"]);
        Assert.Equal("session", exposureOrder[0]);

        var reloaded = Assert.IsType<CharacterSnapshot>(
            new CharacterSnapshotLoader(usersRoot).LoadByAccountId(AccountId));
        Assert.Equal(string.Empty, reloaded.Attributes["PinCodeAnima"]);
        Assert.False(reloaded.Attributes.ContainsKey("TREPIN2"));

        var replies = sent.Select(ParseReply).ToArray();
        Assert.Equal("Successfully removed PIN code.", replies[0].Text);
        Assert.Equal("Just looking.", replies[1].Text);
        Assert.Equal((byte)20, replies[1].OptionId);
        Assert.Equal((ushort)86, replies[2].InputMaxLength);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, replies[3].InteractType);
        Assert.All(sent, bytes => Assert.Equal(LegacyNpcReplyPacket.Opcode, ReadFrame(bytes).Header.Opcode));
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("wire=7899", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemovalMismatchKeepsPinAndOffersSourceRetry()
    {
        var usersRoot = CreateUsersRootWithCharacter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot);
        var verifier = new RecordingEmailVerifier(matches: false);
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(
            connection,
            sessions,
            session,
            verifier,
            optionId: 60,
            input: "wrong@example.com",
            log));

        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(session, stored);
        Assert.Equal("AbC123", session.Character.Attributes["PinCodeAnima"]);
        Assert.Equal("true", session.Character.Attributes["TREPIN2"]);
        var persisted = Assert.IsType<CharacterSnapshot>(
            new CharacterSnapshotLoader(usersRoot).LoadByAccountId(AccountId));
        Assert.Equal("AbC123", persisted.Attributes["PinCodeAnima"]);

        var replies = sent.Select(ParseReply).ToArray();
        Assert.Equal("Please re-enter your data.", replies[0].Text);
        Assert.Equal("Okay.", replies[1].Text);
        Assert.Equal((byte)5, replies[1].OptionId);
        Assert.Equal((ushort)86, replies[2].InputMaxLength);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, replies[3].InteractType);
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sessions", "session-store-missing")]
    [InlineData("users-root", "usersRootMissing")]
    [InlineData("snapshot", "snapshotMissing")]
    public async Task RemovalMissingCommitInfrastructureFailsClosed(string missing, string expectedReason)
    {
        var usersRoot = Path.Combine(_root, missing, "Users");
        Directory.CreateDirectory(usersRoot);
        if (missing != "snapshot")
        {
            WriteCharacter(Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"));
        }

        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(missing == "users-root" ? null : usersRoot);
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await InvokeAsync(
            connection,
            missing == "sessions" ? null : sessions,
            session,
            new RecordingEmailVerifier(matches: true),
            optionId: 60,
            input: "player@example.com",
            log));

        Assert.Empty(sent);
        Assert.Equal("AbC123", session.Character.Attributes["PinCodeAnima"]);
        Assert.Equal("true", session.Character.Attributes["TREPIN2"]);
        Assert.Contains(expectedReason, log.ToString(), StringComparison.Ordinal);
        Assert.Contains("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemovalLockedCharacterFileRollsBackWithoutPackets()
    {
        var usersRoot = CreateUsersRootWithCharacter();
        var path = Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot);
        sessions.Set(connection, session);
        using var log = new StringWriter();
        using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.True(await InvokeAsync(
            connection,
            sessions,
            session,
            new RecordingEmailVerifier(matches: true),
            optionId: 60,
            input: "player@example.com",
            log));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(session, stored);
        Assert.Equal("AbC123", session.Character.Attributes["PinCodeAnima"]);
        Assert.Contains("transactionWriteFailed", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateUsersRootWithCharacter()
    {
        var usersRoot = Path.Combine(_root, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteCharacter(Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"));
        return usersRoot;
    }

    private static async ValueTask<bool> InvokeAsync(
        ConquerTcpConnection connection,
        WorldSessionStore? sessions,
        WorldSession session,
        IAccountEmailVerifier? verifier,
        byte optionId,
        string input,
        TextWriter log)
    {
        return await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                AccountEmailVerifier: verifier),
            session,
            NpcId,
            optionId,
            input,
            CancellationToken.None);
    }

    private static WorldSession CreateSession(string? usersRoot)
    {
        return new WorldSession(
            AccountId,
            "security-anima-email-test",
            new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "SecurityEmail",
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
                    : Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PinCodeAnima"] = "AbC123",
                    ["TREPIN2"] = "true",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static void WriteCharacter(string path)
    {
        File.WriteAllText(
            path,
            "[Character]\r\n" +
            $"UID={CharacterUid}\r\n" +
            "Name=SecurityEmail\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
            "FirstClass=15\r\nSecoundeClass=0\r\nReborn=2\r\nLevel=140\r\nExperience=0\r\n" +
            "Map=1002\r\nX=430\r\nY=378\r\nPMap=1002\r\nPMapX=430\r\nPMapY=378\r\n" +
            "Strength=0\r\nAgility=0\r\nVitaliti=0\r\nSpirit=0\r\nAtributes=0\r\n" +
            "MinHitPoints=1\r\nMinMana=0\r\nMoney=0\r\nConquerPoints=0\r\n" +
            "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\n" +
            "PinCodeAnima=AbC123\r\nSentinel=keep-me\r\n",
            Encoding.Latin1);
    }

    private static LegacyNpcReplyPacket ParseReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply));
        return reply;
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }

    private sealed class RecordingEmailVerifier(bool matches, Exception? exception = null) : IAccountEmailVerifier
    {
        public List<(long AccountId, string Email)> Calls { get; } = [];

        public ValueTask<bool> MatchesEmailAsync(
            long accountId,
            string email,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add((accountId, email));
            if (exception is not null)
            {
                throw exception;
            }

            return ValueTask.FromResult(matches);
        }
    }
}
