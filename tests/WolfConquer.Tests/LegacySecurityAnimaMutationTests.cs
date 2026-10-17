using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySecurityAnimaMutationTests : IDisposable
{
    private const uint NpcId = 489_123;
    private const long AccountId = 1_293_820;
    private const long CharacterUid = 1_293_820;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-security-anima-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ConfirmedPinPersistsLowercaseBeforeExposureAndUsesCurrentNpcReplyFrames()
    {
        var usersRoot = Path.Combine(_root, "Users");
        Directory.CreateDirectory(usersRoot);
        var characterPath = Path.Combine(
            usersRoot,
            AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        WriteCharacter(characterPath);

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
                !current.Character.Attributes.ContainsKey("PinCodeAnima"))
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
            optionId: 2,
            input: "AbC123",
            CancellationToken.None));

        Assert.True(sessions.TryGet(connection, out var staged));
        var stagedSession = Assert.IsType<WorldSession>(staged);
        Assert.Equal("AbC123", stagedSession.LegacyAnimaSecurityPendingPin);
        Assert.False(stagedSession.Character.Attributes.ContainsKey("PinCodeAnima"));
        sent.Clear();
        exposureOrder.Clear();

        Assert.True(await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            context with { Session = stagedSession },
            stagedSession,
            NpcId,
            optionId: 3,
            input: string.Empty,
            CancellationToken.None));

        Assert.True(sessions.TryGet(connection, out var stored));
        var updatedSession = Assert.IsType<WorldSession>(stored);
        Assert.Equal("abc123", updatedSession.Character.Attributes["PinCodeAnima"]);
        var committed = Assert.IsType<CharacterSnapshot>(persistedAtCommit);
        Assert.Equal("abc123", committed.Attributes["PinCodeAnima"]);
        var reloaded = Assert.IsType<CharacterSnapshot>(
            new CharacterSnapshotLoader(usersRoot).LoadByAccountId(AccountId));
        Assert.Equal("abc123", reloaded.Attributes["PinCodeAnima"]);
        Assert.Equal("AbC123", updatedSession.LegacyAnimaSecurityPendingPin);
        Assert.Equal("commit", exposureOrder[0]);

        Assert.Equal(3, sent.Count);
        var replies = sent.Select(ParseReply).ToArray();
        Assert.Equal(
            [
                LegacyNpcReplyInteractType.MessageBox,
                LegacyNpcReplyInteractType.Avatar,
                LegacyNpcReplyInteractType.Finish,
            ],
            replies.Select(reply => reply.InteractType).ToArray());
        Assert.Equal("Completed AbC123", replies[0].Text);
        Assert.Equal((ushort)286, replies[1].InputMaxLength);
        Assert.All(sent, bytes => Assert.Equal(LegacyNpcReplyPacket.Opcode, ReadFrame(bytes).Header.Opcode));
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("wire=7899", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SixCharacterInputIsStagedOnlyInSessionWithoutInventingValidation()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot: null);
        var latestAttributes = new Dictionary<string, string>(
            session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["ConcurrentSentinel"] = "keep-latest",
        };
        sessions.Set(connection, session with
        {
            Character = session.Character with { Attributes = latestAttributes },
        });
        using var log = new StringWriter();

        Assert.True(await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            NpcId,
            optionId: 2,
            input: "A!C 23",
            CancellationToken.None));

        Assert.True(sessions.TryGet(connection, out var stored));
        var staged = Assert.IsType<WorldSession>(stored);
        Assert.Equal("A!C 23", staged.LegacyAnimaSecurityPendingPin);
        Assert.False(staged.Character.Attributes.ContainsKey("PinCodeAnima"));
        Assert.Equal("keep-latest", staged.Character.Attributes["ConcurrentSentinel"]);
        Assert.Equal("Is this your PIN code? *** 23", ParseReply(sent[0]).Text);
        Assert.Equal("Yes, use this PIN code.", ParseReply(sent[1]).Text);
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingPinMakesConfirmationReturnToRootWithoutMutation()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            usersRoot: null,
            pin: "existing",
            pendingPin: "ABC123");
        sessions.Set(connection, session);
        using var log = new StringWriter();

        Assert.True(await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            NpcId,
            optionId: 3,
            input: string.Empty,
            CancellationToken.None));

        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(session, stored);
        Assert.Contains(
            sent.Select(ParseReply),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text.Contains("protect your Anima", StringComparison.Ordinal));
        Assert.DoesNotContain(
            sent.Select(ParseReply),
            reply => reply.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Equal("existing", session.Character.Attributes["PinCodeAnima"]);
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sessions", "session-store-missing")]
    [InlineData("users-root", "usersRootMissing")]
    [InlineData("snapshot", "snapshotMissing")]
    public async Task MissingCommitInfrastructureFailsClosed(string missing, string expectedReason)
    {
        var usersRoot = Path.Combine(_root, missing, "Users");
        Directory.CreateDirectory(usersRoot);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            usersRoot: missing == "users-root" ? null : usersRoot,
            pendingPin: "ABC123");
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
            optionId: 3,
            input: string.Empty,
            CancellationToken.None));

        Assert.Empty(sent);
        Assert.False(session.Character.Attributes.ContainsKey("PinCodeAnima"));
        Assert.Contains(expectedReason, log.ToString(), StringComparison.Ordinal);
        Assert.Contains("PARITY-GAP", log.ToString(), StringComparison.Ordinal);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(session, stored);
    }

    [Fact]
    public async Task LockedCharacterFileFailsClosedWithoutExposingPin()
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
        var session = CreateSession(usersRoot, pendingPin: "ABC123");
        sessions.Set(connection, session);
        using var log = new StringWriter();

        using (new FileStream(characterPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.True(await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
                new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
                session,
                NpcId,
                optionId: 3,
                input: string.Empty,
                CancellationToken.None));
        }

        Assert.Empty(sent);
        Assert.Equal(originalBytes, File.ReadAllBytes(characterPath));
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(session, stored);
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
        string pin = "",
        string pendingPin = "")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sentinel"] = "keep-me",
        };
        if (pin.Length != 0)
        {
            attributes["PinCodeAnima"] = pin;
        }

        return new WorldSession(
            AccountId,
            Username: "security-anima-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "SecurityAnima",
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
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot,
            LegacyAnimaSecurityPendingPin: pendingPin);
    }

    private static void WriteCharacter(string path)
    {
        File.WriteAllText(
            path,
            "[Character]\r\n" +
            $"UID={CharacterUid}\r\n" +
            "Name=SecurityAnima\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
            "FirstClass=15\r\nSecoundeClass=0\r\nReborn=2\r\nLevel=140\r\nExperience=0\r\n" +
            "Map=1002\r\nX=430\r\nY=378\r\nPMap=1002\r\nPMapX=430\r\nPMapY=378\r\n" +
            "Strength=0\r\nAgility=0\r\nVitaliti=0\r\nSpirit=0\r\nAtributes=0\r\n" +
            "MinHitPoints=1\r\nMinMana=0\r\nMoney=0\r\nConquerPoints=0\r\n" +
            "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\nSentinel=keep-me\r\n",
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
}
