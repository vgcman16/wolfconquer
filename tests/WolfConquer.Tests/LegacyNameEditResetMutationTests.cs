using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNameEditResetMutationTests : IDisposable
{
    private const uint NpcId = 7_935;
    private const long AccountId = 1_293_810;
    private const long CharacterUid = 1_293_810;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-name-edit-reset-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(10_000L, 0L)]
    [InlineData(10_025L, 25L)]
    public async Task EligibleResetPersistsBeforeCpUpdateAndSendsExactSourceDialog(
        long conquerPoints,
        long expectedConquerPoints)
    {
        var result = await InvokeAsync(conquerPoints, nameEditCount: 7);

        Assert.True(result.Handled);
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal("0", result.UpdatedSession.Character.Attributes["NameEditCount"]);
        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(expectedConquerPoints, result.PersistedAtCommit!.ConquerPoints);
        Assert.Equal("0", result.PersistedAtCommit.Attributes["NameEditCount"]);
        Assert.Equal("keep-me", result.PersistedAtCommit.Attributes["Sentinel"]);
        Assert.Equal("commit", result.ExposureOrder[0]);
        Assert.Equal("claimant:" + LegacyMsgUpdatePacket.Opcode, result.ExposureOrder[1]);

        Assert.Equal(5, result.Sent.Count);
        AssertScalarUpdate(result.Sent[0], expectedConquerPoints);
        var replies = result.Sent.Skip(1).Select(ParseReply).ToArray();
        Assert.Equal(
            [
                LegacyNpcReplyInteractType.Dialog,
                LegacyNpcReplyInteractType.Option,
                LegacyNpcReplyInteractType.Avatar,
                LegacyNpcReplyInteractType.Finish,
            ],
            replies.Select(reply => reply.InteractType).ToArray());
        Assert.Equal("Your name reset was finished succesfully!", replies[0].Text);
        Assert.Equal("thank you", replies[1].Text);
        Assert.Equal(byte.MaxValue, replies[1].OptionId);
        Assert.Equal((ushort)31, replies[2].InputMaxLength);
        Assert.All(replies, AssertAscii);
        Assert.Contains(
            "packetOrder=ConquerPoints,NpcDialog,NpcOption,NpcAvatar,NpcFinish",
            result.Log,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task InsufficientCpIsReportedBeforeTradeGate()
    {
        var result = await InvokeAsync(
            conquerPoints: 9_999,
            nameEditCount: 7,
            tradeActive: true);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Equal(4, result.Sent.Count);
        var replies = result.Sent.Select(ParseReply).ToArray();
        Assert.Equal("Sorry, you do not have enough CPs.", replies[0].Text);
        Assert.DoesNotContain("trade-active", result.Log, StringComparison.Ordinal);
        Assert.Equal(result.OriginalBytes, File.ReadAllBytes(result.CharacterPath));
    }

    [Fact]
    public async Task EligibleTradeActiveBranchIsSourceSilent()
    {
        var result = await InvokeAsync(
            conquerPoints: 10_000,
            nameEditCount: 7,
            tradeActive: true);

        AssertUnchanged(result);
        Assert.Contains("reason=trade-active", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LockedCharacterFileRollsBackWithoutExposure()
    {
        var result = await InvokeAsync(
            conquerPoints: 10_000,
            nameEditCount: 7,
            lockCharacterFile: true);

        AssertUnchanged(result);
        Assert.Contains("transactionWriteFailed", result.Log, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(result.UsersRoot, "*.reward-points-*"));
    }

    [Theory]
    [InlineData("sessions")]
    [InlineData("users-root")]
    public async Task MissingSuccessInfrastructureFailsClosed(string missing)
    {
        var result = await InvokeAsync(
            conquerPoints: 10_000,
            nameEditCount: 7,
            includeSessions: missing != "sessions",
            includeUsersRoot: missing != "users-root");

        AssertUnchanged(result);
        Assert.Contains(
            missing == "sessions" ? "session-store-missing" : "usersRootMissing",
            result.Log,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConquerPointsOutsideSourceUInt32RangeFailClosed()
    {
        var result = await InvokeAsync(
            conquerPoints: 4_294_967_296,
            nameEditCount: 7);

        AssertUnchanged(result);
        Assert.Contains("conquer-points-out-of-range", result.Log, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        long conquerPoints,
        ushort nameEditCount,
        bool tradeActive = false,
        bool includeSessions = true,
        bool includeUsersRoot = true,
        bool lockCharacterFile = false)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        var characterPath = Path.Combine(
            usersRoot,
            AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        WriteCharacter(characterPath, conquerPoints, nameEditCount);
        var originalBytes = File.ReadAllBytes(characterPath);

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
        var session = CreateSession(
            conquerPoints,
            nameEditCount,
            includeUsersRoot ? usersRoot : null,
            tradeActive);
        CharacterSnapshot? persistedAtCommit = null;
        var sessions = new WorldSessionStore((changedConnection, previous, _) =>
        {
            if (!ReferenceEquals(changedConnection, connection) || previous is null)
            {
                return;
            }

            persistedAtCommit = new CharacterSnapshotLoader(usersRoot).LoadByAccountId(AccountId);
            exposureOrder.Add("commit");
        });
        sessions.Set(connection, session);

        FileStream? locked = lockCharacterFile
            ? new FileStream(characterPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
        bool handled;
        using var log = new StringWriter();
        try
        {
            handled = await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
                new WorldPacketHandlerContext(
                    connection,
                    log,
                    Session: session,
                    Sessions: includeSessions ? sessions : null),
                session,
                NpcId,
                optionId: 11,
                input: string.Empty,
                CancellationToken.None);
        }
        finally
        {
            locked?.Dispose();
        }

        var updatedSession = session;
        if (includeSessions)
        {
            Assert.True(sessions.TryGet(connection, out var stored));
            updatedSession = Assert.IsType<WorldSession>(stored);
        }

        return new InvokeResult(
            handled,
            session,
            updatedSession,
            persistedAtCommit,
            sent,
            exposureOrder,
            log.ToString(),
            usersRoot,
            characterPath,
            originalBytes);
    }

    private static WorldSession CreateSession(
        long conquerPoints,
        ushort nameEditCount,
        string? usersRoot,
        bool tradeActive)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = conquerPoints.ToString(CultureInfo.InvariantCulture),
            ["NameEditCount"] = nameEditCount.ToString(CultureInfo.InvariantCulture),
            ["Sentinel"] = "keep-me",
        };
        return new WorldSession(
            AccountId,
            Username: "name-edit-reset-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "NameResetHero",
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
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: usersRoot is null
                    ? "fixture"
                    : Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot,
            LegacyTradeActive: tradeActive);
    }

    private static void WriteCharacter(string path, long conquerPoints, ushort nameEditCount)
    {
        File.WriteAllText(
            path,
            "[Character]\r\n" +
            $"UID={CharacterUid}\r\n" +
            "Name=NameResetHero\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
            "FirstClass=15\r\nSecoundeClass=0\r\nReborn=2\r\nLevel=140\r\nExperience=0\r\n" +
            "Map=1002\r\nX=430\r\nY=378\r\nPMap=1002\r\nPMapX=430\r\nPMapY=378\r\n" +
            "Strength=0\r\nAgility=0\r\nVitaliti=0\r\nSpirit=0\r\nAtributes=0\r\n" +
            "MinHitPoints=1\r\nMinMana=0\r\nMoney=0\r\n" +
            $"ConquerPoints={conquerPoints.ToString(CultureInfo.InvariantCulture)}\r\n" +
            "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\n" +
            $"NameEditCount={nameEditCount.ToString(CultureInfo.InvariantCulture)}\r\n" +
            "Sentinel=keep-me\r\n",
            Encoding.Latin1);
    }

    private static void AssertScalarUpdate(byte[] bytes, long expectedConquerPoints)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(LegacyMsgUpdatePacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, update.Type);
        Assert.Equal(checked((ulong)expectedConquerPoints), update.Value);
    }

    private static LegacyNpcReplyPacket ParseReply(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(LegacyNpcReplyPacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var reply));
        return reply;
    }

    private static void AssertAscii(LegacyNpcReplyPacket reply)
    {
        Assert.All(reply.Text, character => Assert.InRange(character, (char)0x20, (char)0x7E));
    }

    private static void AssertUnchanged(InvokeResult result)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Empty(result.Sent);
        Assert.Empty(result.ExposureOrder);
        Assert.Equal(result.OriginalBytes, File.ReadAllBytes(result.CharacterPath));
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

    private sealed record InvokeResult(
        bool Handled,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        CharacterSnapshot? PersistedAtCommit,
        IReadOnlyList<byte[]> Sent,
        IReadOnlyList<string> ExposureOrder,
        string Log,
        string UsersRoot,
        string CharacterPath,
        byte[] OriginalBytes);
}
