using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPkResetMutationTests : IDisposable
{
    private const uint NpcId = 52_003;
    private const long AccountId = 1_293_800;
    private const long CharacterUid = 1_293_800;
    private const int RedNameFlag = 14;
    private const int BlackNameFlag = 15;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-pk-reset-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, long, long, bool, string> SuccessCases => new()
    {
        { 1, 2_000_000, 100, false, "....#55" },
        { 2, 5_000_000, 500, true, "You have deducted 500 PK points." },
        { 3, 5_000, 500, true, "You have deducted 500 PK points." },
    };

    [Theory]
    [MemberData(nameof(SuccessCases))]
    public async Task ExactThresholdPersistsBeforeExposureAndPreservesSourcePacketOrder(
        byte optionId,
        long conquerPointCost,
        long pkPointReduction,
        bool messageBeforeEffect,
        string successMessage)
    {
        var result = await InvokeAsync(
            optionId,
            conquerPointCost,
            pkPointReduction,
            statusFlags: StatusFlags(BlackNameFlag),
            observerCount: 1);

        Assert.True(result.Handled);
        Assert.Equal(0, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(0, result.UpdatedSession.Character.PkPoints);
        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(0, result.PersistedAtCommit!.ConquerPoints);
        Assert.Equal(0, result.PersistedAtCommit.PkPoints);
        Assert.Equal("commit", result.ExposureOrder[0]);

        var reloaded = Assert.IsType<CharacterSnapshot>(
            new CharacterSnapshotLoader(result.UsersRoot).LoadByAccountId(AccountId));
        Assert.Equal(0, reloaded.ConquerPoints);
        Assert.Equal(0, reloaded.PkPoints);
        Assert.False(HasStatusFlag(reloaded, RedNameFlag));
        Assert.False(HasStatusFlag(reloaded, BlackNameFlag));
        Assert.Equal("keep-me", reloaded.Attributes["Sentinel"]);

        Assert.Equal(5, result.ClaimantSent.Count);
        AssertStatusVector(result.ClaimantSent[0], RedNameFlag, expected: false);
        AssertStatusVector(result.ClaimantSent[0], BlackNameFlag, expected: false);
        AssertScalarUpdate(
            result.ClaimantSent[1],
            LegacyMsgUpdateDataType.PKPoints,
            expectedValue: 0);
        AssertScalarUpdate(
            result.ClaimantSent[2],
            LegacyMsgUpdateDataType.ConquerPoints,
            expectedValue: 0);

        var messageIndex = messageBeforeEffect ? 3 : 4;
        var effectIndex = messageBeforeEffect ? 4 : 3;
        AssertMessageBox(result.ClaimantSent[messageIndex], successMessage);
        AssertEffect(result.ClaimantSent[effectIndex]);

        var observerFrames = Assert.Single(result.ObserverSent);
        Assert.Equal(2, observerFrames.Count);
        AssertStatusVector(observerFrames[0], RedNameFlag, expected: false);
        AssertStatusVector(observerFrames[0], BlackNameFlag, expected: false);
        AssertEffect(observerFrames[1]);

        var expectedOrder = messageBeforeEffect
            ? "StatusFlags,PKPoints,ConquerPoints,MessageBox,Effect"
            : "StatusFlags,PKPoints,ConquerPoints,Effect,MessageBox";
        Assert.Contains("packetOrder=" + expectedOrder, result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemainingFiftyPkPointsSwitchesBlackNameToRedNameAndPersistsOtherFlags()
    {
        const int unrelatedFlag = 20;
        var result = await InvokeAsync(
            optionId: 1,
            conquerPoints: 2_000_100,
            pkPoints: 150,
            statusFlags: StatusFlags(BlackNameFlag, unrelatedFlag));

        Assert.Equal(100, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(50, result.UpdatedSession.Character.PkPoints);
        Assert.True(HasStatusFlag(result.UpdatedSession.Character, RedNameFlag));
        Assert.False(HasStatusFlag(result.UpdatedSession.Character, BlackNameFlag));
        Assert.True(HasStatusFlag(result.UpdatedSession.Character, unrelatedFlag));

        var reloaded = Assert.IsType<CharacterSnapshot>(
            new CharacterSnapshotLoader(result.UsersRoot).LoadByAccountId(AccountId));
        Assert.Equal(100, reloaded.ConquerPoints);
        Assert.Equal(50, reloaded.PkPoints);
        Assert.True(HasStatusFlag(reloaded, RedNameFlag));
        Assert.False(HasStatusFlag(reloaded, BlackNameFlag));
        Assert.True(HasStatusFlag(reloaded, unrelatedFlag));
        AssertStatusVector(result.ClaimantSent[0], RedNameFlag, expected: true);
        AssertStatusVector(result.ClaimantSent[0], BlackNameFlag, expected: false);
        AssertStatusVector(result.ClaimantSent[0], unrelatedFlag, expected: true);
    }

    [Theory]
    [InlineData(1, 2_000_000, 100)]
    [InlineData(2, 5_000_000, 500)]
    [InlineData(3, 5_000, 500)]
    public async Task EligibleTradeActiveBranchIsSourceSilent(
        byte optionId,
        long conquerPoints,
        long pkPoints)
    {
        var result = await InvokeAsync(
            optionId,
            conquerPoints,
            pkPoints,
            statusFlags: StatusFlags(BlackNameFlag),
            tradeActive: true,
            observerCount: 1);

        AssertUnchanged(result);
        Assert.Contains("reason=trade-active", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LockedCharacterFileRollsBackWithoutSessionOrPacketExposure()
    {
        var result = await InvokeAsync(
            optionId: 2,
            conquerPoints: 5_000_000,
            pkPoints: 500,
            statusFlags: StatusFlags(BlackNameFlag),
            lockCharacterFile: true,
            observerCount: 1);

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
            optionId: 1,
            conquerPoints: 2_000_000,
            pkPoints: 100,
            statusFlags: StatusFlags(BlackNameFlag),
            includeSessions: missing != "sessions",
            includeUsersRoot: missing != "users-root",
            observerCount: 1);

        AssertUnchanged(result);
        Assert.Contains(
            missing == "sessions" ? "session-store-missing" : "usersRootMissing",
            result.Log,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(4_294_967_296L, 500L)]
    [InlineData(5_000_000L, 65_536L)]
    public async Task OutOfSourceScalarRangeFailsClosed(long conquerPoints, long pkPoints)
    {
        var result = await InvokeAsync(
            optionId: 2,
            conquerPoints,
            pkPoints,
            statusFlags: StatusFlags(BlackNameFlag),
            observerCount: 1);

        AssertUnchanged(result);
        Assert.Contains("character-scalar-out-of-range", result.Log, StringComparison.Ordinal);
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
        long pkPoints,
        string statusFlags,
        bool tradeActive = false,
        bool includeSessions = true,
        bool includeUsersRoot = true,
        bool lockCharacterFile = false,
        int observerCount = 0)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        var characterPath = Path.Combine(
            usersRoot,
            AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        WriteCharacter(characterPath, conquerPoints, pkPoints, statusFlags);
        var originalBytes = File.ReadAllBytes(characterPath);

        var claimantSent = new List<byte[]>();
        var observerSent = new List<IReadOnlyList<byte[]>>();
        var exposureOrder = new List<string>();
        var claimant = CreateConnection(claimantSent, exposureOrder, "claimant", 9_958);
        var session = CreateSession(
            conquerPoints,
            pkPoints,
            statusFlags,
            includeUsersRoot ? usersRoot : null,
            tradeActive);
        CharacterSnapshot? persistedAtCommit = null;
        var sessions = new WorldSessionStore((connection, previous, _) =>
        {
            if (!ReferenceEquals(connection, claimant) || previous is null)
            {
                return;
            }

            persistedAtCommit = new CharacterSnapshotLoader(usersRoot).LoadByAccountId(AccountId);
            exposureOrder.Add("commit");
        });
        sessions.Set(claimant, session);

        for (var index = 0; index < observerCount; index++)
        {
            var sent = new List<byte[]>();
            var observerConnection = CreateConnection(
                sent,
                exposureOrder,
                "observer" + index.ToString(CultureInfo.InvariantCulture),
                9_959 + index);
            sessions.Set(
                observerConnection,
                session with
                {
                    AccountId = AccountId + index + 1,
                    Character = session.Character with
                    {
                        Uid = CharacterUid + index + 1,
                        Name = "PkResetObserver" + index.ToString(CultureInfo.InvariantCulture),
                    },
                    UsersRoot = null,
                    LegacyTradeActive = false,
                });
            observerSent.Add(sent);
        }

        FileStream? locked = lockCharacterFile
            ? new FileStream(characterPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
        bool handled;
        using var log = new StringWriter();
        try
        {
            handled = await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
                new WorldPacketHandlerContext(
                    claimant,
                    log,
                    Session: session,
                    Sessions: includeSessions ? sessions : null),
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

        var updatedSession = session;
        if (includeSessions)
        {
            Assert.True(sessions.TryGet(claimant, out var stored));
            updatedSession = Assert.IsType<WorldSession>(stored);
        }

        return new InvokeResult(
            handled,
            session,
            updatedSession,
            persistedAtCommit,
            claimantSent,
            observerSent,
            exposureOrder,
            log.ToString(),
            usersRoot,
            characterPath,
            originalBytes);
    }

    private static WorldSession CreateSession(
        long conquerPoints,
        long pkPoints,
        string statusFlags,
        string? usersRoot,
        bool tradeActive)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = conquerPoints.ToString(CultureInfo.InvariantCulture),
            ["PKPoints"] = pkPoints.ToString(CultureInfo.InvariantCulture),
            ["StatusFlags"] = statusFlags,
            ["Sentinel"] = "keep-me",
        };
        return new WorldSession(
            AccountId,
            Username: "pk-reset-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PkResetHero",
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
                PkPoints: pkPoints,
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

    private static void WriteCharacter(
        string path,
        long conquerPoints,
        long pkPoints,
        string statusFlags)
    {
        File.WriteAllText(
            path,
            "[Character]\r\n" +
            $"UID={CharacterUid}\r\n" +
            "Name=PkResetHero\r\n" +
            "Body=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\nFirstClass=15\r\nSecoundeClass=0\r\n" +
            "Reborn=2\r\nLevel=140\r\nExperience=0\r\nMap=1002\r\nX=430\r\nY=378\r\n" +
            "PMap=1002\r\nPMapX=430\r\nPMapY=378\r\nStrength=0\r\nAgility=0\r\nVitaliti=0\r\n" +
            "Spirit=0\r\nAtributes=0\r\nMinHitPoints=1\r\nMinMana=0\r\nMoney=0\r\n" +
            $"ConquerPoints={conquerPoints.ToString(CultureInfo.InvariantCulture)}\r\n" +
            "BoundConquerPoints=0\r\nBankCps=0\r\n" +
            $"PkPoints={pkPoints.ToString(CultureInfo.InvariantCulture)}\r\n" +
            "Online=0\r\n" +
            $"StatusFlags={statusFlags}\r\n" +
            "Sentinel=keep-me\r\n",
            Encoding.Latin1);
    }

    private static ConquerTcpConnection CreateConnection(
        List<byte[]> sent,
        List<string> exposureOrder,
        string label,
        int port)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, port), (frame, _) =>
        {
            var bytes = frame.ToArray();
            sent.Add(bytes);
            exposureOrder.Add(
                label + ":" + ReadFrame(bytes).Header.Opcode.ToString(CultureInfo.InvariantCulture));
            return ValueTask.CompletedTask;
        });
    }

    private static string StatusFlags(params int[] flags)
    {
        var words = new ulong[10];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words) + "/";
    }

    private static bool HasStatusFlag(CharacterSnapshot character, int flag)
    {
        var raw = character.Attributes.TryGetValue("StatusFlags", out var statusFlags)
            ? statusFlags
            : character.Attributes.GetValueOrDefault("BitVector", string.Empty);
        var words = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return flag / 32 < words.Length &&
            ulong.TryParse(words[flag / 32], NumberStyles.Integer, CultureInfo.InvariantCulture, out var word) &&
            (word & (1UL << (flag % 32))) != 0;
    }

    private static void AssertStatusVector(byte[] bytes, int flag, bool expected)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, update.Type);
        Assert.NotNull(update.Values);
        var values = update.Values!;
        var actual = (values[flag / 32] & (1UL << (flag % 32))) != 0;
        Assert.Equal(expected, actual);
    }

    private static void AssertScalarUpdate(
        byte[] bytes,
        LegacyMsgUpdateDataType type,
        ulong expectedValue)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(LegacyMsgUpdatePacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(type, update.Type);
        Assert.Equal(expectedValue, update.Value);
    }

    private static void AssertEffect(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(LegacyStringPacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyStringPacket.TryParse(frame, out var effect));
        Assert.Equal((uint)CharacterUid, effect.Uid);
        Assert.Equal(LegacyStringId.Effect, effect.Id);
        Assert.Equal(["eidolon"], effect.Strings);
    }

    private static void AssertMessageBox(byte[] bytes, string expectedText)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(LegacyNpcReplyPacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal(expectedText, reply.Text);
    }

    private static void AssertUnchanged(InvokeResult result)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Empty(result.ClaimantSent);
        Assert.All(result.ObserverSent, Assert.Empty);
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
        IReadOnlyList<byte[]> ClaimantSent,
        IReadOnlyList<IReadOnlyList<byte[]>> ObserverSent,
        IReadOnlyList<string> ExposureOrder,
        string Log,
        string UsersRoot,
        string CharacterPath,
        byte[] OriginalBytes);
}
