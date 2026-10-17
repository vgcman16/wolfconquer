using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAddStgmaMutationTests : IDisposable
{
    private const uint NpcId = 22_155;
    private const long AccountId = 1_293_820;
    private const long CharacterUid = 1_293_820;
    private const int MagicShieldFlag = 0x08;
    private const int StigmaFlag = 0x09;
    private const int UnrelatedFlag = 0x15;
    private const int DurationSeconds = 6_000;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-add-stgma-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(500_001L, 1L)]
    [InlineData(600_000L, 100_000L)]
    public async Task EligiblePurchasePersistsCpBeforeTwoSourceOrderedStatusUpdates(
        long conquerPoints,
        long expectedConquerPoints)
    {
        var before = DateTimeOffset.UtcNow;
        var result = await InvokeAsync(
            conquerPoints,
            StatusFlags(UnrelatedFlag),
            observerCount: 1);
        var after = DateTimeOffset.UtcNow;

        Assert.True(result.Handled);
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        AssertStatus(result.UpdatedSession.Character, StigmaFlag, expected: true);
        AssertStatus(result.UpdatedSession.Character, MagicShieldFlag, expected: true);
        AssertStatus(result.UpdatedSession.Character, UnrelatedFlag, expected: true);
        AssertExpiration(
            result.UpdatedSession.Character,
            "LegacyStigmaExpiresAtUtc",
            before,
            after);
        AssertExpiration(
            result.UpdatedSession.Character,
            "LegacyMagicShieldExpiresAtUtc",
            before,
            after);
        Assert.Equal(
            bool.TrueString,
            result.UpdatedSession.Character.Attributes["LegacyStigmaRemoveOnDead"]);
        Assert.Equal(
            bool.TrueString,
            result.UpdatedSession.Character.Attributes["LegacyMagicShieldRemoveOnDead"]);

        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(expectedConquerPoints, result.PersistedAtCommit!.ConquerPoints);
        AssertStatus(result.PersistedAtCommit, StigmaFlag, expected: false);
        AssertStatus(result.PersistedAtCommit, MagicShieldFlag, expected: false);
        AssertStatus(result.PersistedAtCommit, UnrelatedFlag, expected: true);
        Assert.False(result.PersistedAtCommit.Attributes.ContainsKey("LegacyStigmaExpiresAtUtc"));
        Assert.False(result.PersistedAtCommit.Attributes.ContainsKey("LegacyMagicShieldExpiresAtUtc"));
        Assert.Equal("keep-me", result.PersistedAtCommit.Attributes["Sentinel"]);

        Assert.Equal("commit", result.ExposureOrder[0]);
        Assert.Equal(3, result.ClaimantSent.Count);
        AssertScalarUpdate(result.ClaimantSent[0], expectedConquerPoints);
        AssertStatusVector(result.ClaimantSent[1], StigmaFlag, expected: true);
        AssertStatusVector(result.ClaimantSent[1], MagicShieldFlag, expected: false);
        AssertStatusVector(result.ClaimantSent[1], UnrelatedFlag, expected: true);
        AssertStatusVector(result.ClaimantSent[2], StigmaFlag, expected: true);
        AssertStatusVector(result.ClaimantSent[2], MagicShieldFlag, expected: true);
        Assert.DoesNotContain(
            result.ClaimantSent,
            frame => LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out _));

        var observerFrames = Assert.Single(result.ObserverSent);
        Assert.Equal(2, observerFrames.Count);
        AssertStatusVector(observerFrames[0], StigmaFlag, expected: true);
        AssertStatusVector(observerFrames[0], MagicShieldFlag, expected: false);
        AssertStatusVector(observerFrames[1], StigmaFlag, expected: true);
        AssertStatusVector(observerFrames[1], MagicShieldFlag, expected: true);

        Assert.Contains("durationSeconds=6000", result.Log, StringComparison.Ordinal);
        Assert.Contains(
            "packetOrder=ConquerPoints,StigmaStatusFlags,MagicShieldStatusFlags",
            result.Log,
            StringComparison.Ordinal);
        Assert.Contains("dialog advertises 600000", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingFlagsAreRefreshedWithoutAVisibleClearBetweenSourceCalls()
    {
        var result = await InvokeAsync(
            conquerPoints: 500_001,
            statusFlags: StatusFlags(StigmaFlag, MagicShieldFlag));

        Assert.Equal(3, result.ClaimantSent.Count);
        AssertStatusVector(result.ClaimantSent[1], StigmaFlag, expected: true);
        AssertStatusVector(result.ClaimantSent[1], MagicShieldFlag, expected: true);
        AssertStatusVector(result.ClaimantSent[2], StigmaFlag, expected: true);
        AssertStatusVector(result.ClaimantSent[2], MagicShieldFlag, expected: true);
    }

    [Fact]
    public async Task SourceStrictBoundaryRejectsExactlyFiveHundredThousandCp()
    {
        var result = await InvokeAsync(
            conquerPoints: 500_000,
            statusFlags: StatusFlags(UnrelatedFlag));

        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Equal(3, result.ClaimantSent.Count);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(result.ClaimantSent[0]), out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, reply.InteractType);
        Assert.Equal("Sorry, you do not have enough CPs.", reply.Text);
        Assert.Equal(result.OriginalBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LockedCharacterFileRollsBackWithoutStatusOrPacketExposure()
    {
        var result = await InvokeAsync(
            conquerPoints: 500_001,
            statusFlags: StatusFlags(UnrelatedFlag),
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
            conquerPoints: 500_001,
            statusFlags: StatusFlags(UnrelatedFlag),
            includeSessions: missing != "sessions",
            includeUsersRoot: missing != "users-root",
            observerCount: 1);

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
            statusFlags: StatusFlags(UnrelatedFlag));

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
        string statusFlags,
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
        WriteCharacter(characterPath, conquerPoints, statusFlags);
        var originalBytes = File.ReadAllBytes(characterPath);

        var claimantSent = new List<byte[]>();
        var observerSent = new List<IReadOnlyList<byte[]>>();
        var exposureOrder = new List<string>();
        var claimant = CreateConnection(claimantSent, exposureOrder, "claimant", 9_958);
        var session = CreateSession(
            conquerPoints,
            statusFlags,
            includeUsersRoot ? usersRoot : null);
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
            var observer = CreateConnection(
                sent,
                exposureOrder,
                "observer" + index.ToString(CultureInfo.InvariantCulture),
                9_959 + index);
            sessions.Set(
                observer,
                session with
                {
                    AccountId = AccountId + index + 1,
                    Character = session.Character with
                    {
                        Uid = CharacterUid + index + 1,
                        Name = "AddStgmaObserver" + index.ToString(CultureInfo.InvariantCulture),
                    },
                    UsersRoot = null,
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
                optionId: 1,
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
        string statusFlags,
        string? usersRoot)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = conquerPoints.ToString(CultureInfo.InvariantCulture),
            ["StatusFlags"] = statusFlags,
            ["Sentinel"] = "keep-me",
        };
        return new WorldSession(
            AccountId,
            Username: "add-stgma-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "AddStgmaHero",
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
            UsersRoot: usersRoot);
    }

    private static void WriteCharacter(string path, long conquerPoints, string statusFlags)
    {
        File.WriteAllText(
            path,
            "[Character]\r\n" +
            $"UID={CharacterUid}\r\n" +
            "Name=AddStgmaHero\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
            "FirstClass=15\r\nSecoundeClass=0\r\nReborn=2\r\nLevel=140\r\nExperience=0\r\n" +
            "Map=1002\r\nX=430\r\nY=378\r\nPMap=1002\r\nPMapX=430\r\nPMapY=378\r\n" +
            "Strength=0\r\nAgility=0\r\nVitaliti=0\r\nSpirit=0\r\nAtributes=0\r\n" +
            "MinHitPoints=1\r\nMinMana=0\r\nMoney=0\r\n" +
            $"ConquerPoints={conquerPoints.ToString(CultureInfo.InvariantCulture)}\r\n" +
            "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\n" +
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

    private static void AssertStatus(CharacterSnapshot character, int flag, bool expected)
    {
        var raw = character.Attributes.TryGetValue("StatusFlags", out var statusFlags)
            ? statusFlags
            : character.Attributes.GetValueOrDefault("BitVector", string.Empty);
        var words = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var actual = flag / 32 < words.Length &&
            ulong.TryParse(words[flag / 32], NumberStyles.Integer, CultureInfo.InvariantCulture, out var word) &&
            (word & (1UL << (flag % 32))) != 0;
        Assert.Equal(expected, actual);
    }

    private static void AssertExpiration(
        CharacterSnapshot character,
        string key,
        DateTimeOffset before,
        DateTimeOffset after)
    {
        Assert.True(DateTimeOffset.TryParse(
            character.Attributes[key],
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var expiresAt));
        Assert.InRange(
            expiresAt,
            before.AddSeconds(DurationSeconds),
            after.AddSeconds(DurationSeconds));
    }

    private static void AssertStatusVector(byte[] bytes, int flag, bool expected)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, update.Type);
        Assert.NotNull(update.Values);
        var actual = (update.Values![flag / 32] & (1UL << (flag % 32))) != 0;
        Assert.Equal(expected, actual);
    }

    private static void AssertScalarUpdate(byte[] bytes, long expectedValue)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, update.Type);
        Assert.Equal(checked((ulong)expectedValue), update.Value);
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
