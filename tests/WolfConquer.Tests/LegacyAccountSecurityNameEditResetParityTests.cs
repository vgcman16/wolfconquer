using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAccountSecurityNameEditResetParityTests : IDisposable
{
    private const uint NpcId = 7_935;
    private const byte ResetOptionId = 11;
    private const long AccountId = 1_293_811;
    private const long CharacterUid = 1_293_811;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-account-security-name-edit-reset-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)4)]
    [InlineData((ushort)5)]
    [InlineData(ushort.MaxValue)]
    public async Task SuccessChargesExactlyOnceAndResetsEverySourceCount(ushort nameEditCount)
    {
        var result = await InvokeAsync(
            optionId: ResetOptionId,
            conquerPoints: 20_025,
            nameEditCount);

        Assert.True(result.Handled);
        Assert.Equal(10_025, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal("0", result.UpdatedSession.Character.Attributes["NameEditCount"]);
        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(10_025, result.PersistedAtCommit!.ConquerPoints);
        Assert.Equal("0", result.PersistedAtCommit.Attributes["NameEditCount"]);
        Assert.Equal("keep-me", result.PersistedAtCommit.Attributes["Sentinel"]);
        Assert.Equal("commit", result.ExposureOrder[0]);

        var updateBytes = Assert.Single(
            result.Sent,
            bytes => ReadFrame(bytes).Header.Opcode == LegacyMsgUpdatePacket.Opcode);
        AssertConquerPointsUpdate(updateBytes, expectedConquerPoints: 10_025);

        var replies = result.Sent
            .Where(bytes => ReadFrame(bytes).Header.Opcode == LegacyNpcReplyPacket.Opcode)
            .Select(ParseReply)
            .ToArray();
        Assert.Equal(4, replies.Length);
        Assert.Equal("Your name reset was finished succesfully!", replies[0].Text);
        Assert.Equal("thank you", replies[1].Text);
        Assert.Equal(byte.MaxValue, replies[1].OptionId);
        Assert.Contains("stateKey=NameEditCount nameEditCount=0", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InsufficientConquerPointsDoNotMutateOrEmitWalletUpdate()
    {
        var result = await InvokeAsync(
            optionId: ResetOptionId,
            conquerPoints: 9_999,
            nameEditCount: 5);

        AssertUnchanged(result);
        var dialog = Assert.Single(
            result.Sent.Select(ParseReply),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("Sorry, you do not have enough CPs.", dialog.Text);
        Assert.DoesNotContain(
            result.Sent,
            bytes => ReadFrame(bytes).Header.Opcode == LegacyMsgUpdatePacket.Opcode);
    }

    [Fact]
    public async Task LockedCharacterFileRollsBackBeforeSessionOrPacketExposure()
    {
        var result = await InvokeAsync(
            optionId: ResetOptionId,
            conquerPoints: 10_000,
            nameEditCount: 5,
            lockCharacterFile: true);

        AssertUnchanged(result, expectPackets: false);
        Assert.Contains("transactionWriteFailed", result.Log, StringComparison.Ordinal);
        Assert.Contains("no CP, name-edit count, session state, or packets were committed", result.Log, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(result.UsersRoot, "*.reward-points-*"));
    }

    [Fact]
    public async Task EligibleTradeBranchIsSourceSilent()
    {
        var result = await InvokeAsync(
            optionId: ResetOptionId,
            conquerPoints: 10_000,
            nameEditCount: 5,
            tradeActive: true);

        AssertUnchanged(result, expectPackets: false);
        Assert.Contains("reason=trade-active", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NameChangeEntryRemainsFencedWithoutAnEarly810CpCharge()
    {
        var result = await InvokeAsync(
            optionId: 1,
            conquerPoints: 810,
            nameEditCount: 4);

        AssertUnchanged(result);
        Assert.Contains("PARITY-GAP LegacyNpc AccountSecurity", result.Log, StringComparison.Ordinal);
        Assert.Contains("optionId=1", result.Log, StringComparison.Ordinal);
        Assert.Contains("source opens name-change UI and mutates the name registry", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain(
            result.Sent,
            bytes => ReadFrame(bytes).Header.Opcode == LegacyMsgUpdatePacket.Opcode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        byte optionId,
        long conquerPoints,
        ushort nameEditCount,
        bool tradeActive = false,
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
                    "packet:" + ReadFrame(bytes).Header.Opcode.ToString(CultureInfo.InvariantCulture));
                return ValueTask.CompletedTask;
            });
        var session = CreateSession(conquerPoints, nameEditCount, usersRoot, tradeActive);
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
                    Sessions: sessions),
                session,
                NpcId,
                optionId,
                input: string.Empty,
                CancellationToken.None);
        }
        finally
        {
            locked?.Dispose();
        }

        Assert.True(sessions.TryGet(connection, out var stored));
        return new InvokeResult(
            handled,
            session,
            Assert.IsType<WorldSession>(stored),
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
        string usersRoot,
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
            Username: "name-edit-reset-parity",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "NameResetParity",
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
                SourceFile: Path.Combine(
                    usersRoot,
                    AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
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
            "Name=NameResetParity\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
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

    private static void AssertConquerPointsUpdate(byte[] bytes, long expectedConquerPoints)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }

    private static void AssertUnchanged(InvokeResult result, bool expectPackets = true)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Equal(result.OriginalBytes, File.ReadAllBytes(result.CharacterPath));
        if (!expectPackets)
        {
            Assert.Empty(result.Sent);
            Assert.Empty(result.ExposureOrder);
        }
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
