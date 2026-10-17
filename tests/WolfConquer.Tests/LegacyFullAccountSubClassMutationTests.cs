using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFullAccountSubClassMutationTests : IDisposable
{
    private const uint NpcId = 465_123;
    private const long AccountId = 1_293_850;
    private const long CharacterUid = 1_293_850;
    private const long Cost = 100_000;
    private const string EmptySubClassState = "0#0#0#";
    private const string FullSubClassState =
        "0#0#7#1#9#9#2#9#9#3#9#9#4#9#9#5#9#9#9#9#9#6#9#9#";

    private static readonly byte[] SourceSubClassOrder = [1, 2, 3, 4, 5, 9, 6];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-full-account-subclasses-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EligibleGrantPersistsMaxSubClassesBeforeCurrentPacketSequence()
    {
        var result = await InvokeAsync(
            conquerPoints: Cost,
            subProfInfo: EmptySubClassState,
            subClassHashPoint: "0",
            observerCount: 1);

        Assert.True(result.Handled);
        Assert.Equal(0, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal("0", result.UpdatedSession.Character.Attributes["ConquerPoints"]);
        Assert.Equal(FullSubClassState, result.UpdatedSession.Character.Attributes["SubProfInfo"]);
        Assert.Equal("900999999", result.UpdatedSession.Character.Attributes["SubClassHashPoint"]);
        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(0, result.PersistedAtCommit!.ConquerPoints);
        Assert.Equal(FullSubClassState, result.PersistedAtCommit.Attributes["SubProfInfo"]);
        Assert.Equal("900999999", result.PersistedAtCommit.Attributes["SubClassHashPoint"]);
        Assert.Equal("keep-me", result.PersistedAtCommit.Attributes["Sentinel"]);
        Assert.Equal("commit", result.ExposureOrder[0]);

        Assert.Equal(9, result.ClaimantSent.Count);
        AssertConquerPointsUpdate(result.ClaimantSent[0], expected: 0);
        var learned = result.ClaimantSent
            .Skip(1)
            .Take(SourceSubClassOrder.Length)
            .Select(ParseSubClassUpdate)
            .ToArray();
        Assert.Equal(SourceSubClassOrder, learned.Select(packet => packet.SubClassId));
        Assert.All(learned, packet =>
        {
            Assert.Equal(LegacySubClassAction.LearnSubClass, packet.Action);
            Assert.Equal((byte)9, packet.Level);
            Assert.Equal((ushort)0, packet.StudyReceive);
        });
        AssertPlayerEffect(result.ClaimantSent[^1]);
        Assert.All(result.ObserverSent, Assert.Empty);

        var persisted = File.ReadAllText(result.CharacterPath, Encoding.Latin1);
        Assert.Contains("ConquerPoints=0", persisted, StringComparison.Ordinal);
        Assert.Contains("SubProfInfo=" + FullSubClassState, persisted, StringComparison.Ordinal);
        Assert.Contains("SubClassHashPoint=900999999", persisted, StringComparison.Ordinal);
        Assert.Contains("level=9 phrase=9", result.Log, StringComparison.Ordinal);
        Assert.Contains("packetOrder=ConquerPoints,LearnSubClass*,Effect", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingEntriesArePreservedAndOnlyMissingSourceCallsSendLearnPackets()
    {
        const string initialState = "7#0#2#2#3#4#5#0#0#";
        const string expectedState = "7#0#7#2#3#4#5#0#0#1#9#9#3#9#9#4#9#9#9#9#9#6#9#9#";
        var result = await InvokeAsync(
            conquerPoints: Cost + 25,
            subProfInfo: initialState,
            subClassHashPoint: "40");

        Assert.True(result.Handled);
        Assert.Equal(25, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(expectedState, result.UpdatedSession.Character.Attributes["SubProfInfo"]);
        Assert.Equal("900909949", result.UpdatedSession.Character.Attributes["SubClassHashPoint"]);
        Assert.Equal(7, result.ClaimantSent.Count);
        AssertConquerPointsUpdate(result.ClaimantSent[0], expected: 25);
        Assert.Equal(
            new byte[] { 1, 3, 4, 9, 6 },
            result.ClaimantSent.Skip(1).Take(5).Select(ParseSubClassUpdate).Select(packet => packet.SubClassId));
        AssertPlayerEffect(result.ClaimantSent[^1]);
        Assert.Contains("learned=1,3,4,9,6", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingApothecarySendsExactSourceDialogBeforeBalanceOrInfrastructureChecks()
    {
        const string state = "0#0#1#5#1#1#";
        var result = await InvokeAsync(
            conquerPoints: -1,
            subProfInfo: state,
            subClassHashPoint: "10000",
            includeSessions: false,
            includeUsersRoot: false,
            observerCount: 1);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Equal(4, result.ClaimantSent.Count);
        AssertNpcReply(
            result.ClaimantSent[0],
            LegacyNpcReplyInteractType.Dialog,
            "Aha,~you~are~already~an~Apothecary,~remember?~I~suggest~you~focus~your~attention~on~upgrading~your~sub-class~level,~now.");
        AssertNpcReply(result.ClaimantSent[1], LegacyNpcReplyInteractType.Option, "Oh.", byte.MaxValue);
        var avatar = AssertNpcReply(result.ClaimantSent[2], LegacyNpcReplyInteractType.Avatar, string.Empty);
        Assert.Equal((ushort)284, avatar.InputMaxLength);
        AssertNpcReply(result.ClaimantSent[3], LegacyNpcReplyInteractType.Finish, string.Empty);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Equal(result.OriginalBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InsufficientCpSendsExactEnglishSourceDialogWithoutMutationInfrastructure()
    {
        var result = await InvokeAsync(
            conquerPoints: Cost - 1,
            subProfInfo: EmptySubClassState,
            subClassHashPoint: "0",
            includeSessions: false,
            includeUsersRoot: false,
            observerCount: 1);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Equal(3, result.ClaimantSent.Count);
        AssertNpcReply(
            result.ClaimantSent[0],
            LegacyNpcReplyInteractType.Dialog,
            "You do not have enough conquer point.");
        AssertNpcReply(result.ClaimantSent[1], LegacyNpcReplyInteractType.Option, "Aww!", byte.MaxValue);
        AssertNpcReply(result.ClaimantSent[2], LegacyNpcReplyInteractType.Finish, string.Empty);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Equal(result.OriginalBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LockedCharacterFileRollsBackWithoutCpSubclassOrPacketExposure()
    {
        var result = await InvokeAsync(
            conquerPoints: Cost,
            subProfInfo: EmptySubClassState,
            subClassHashPoint: "0",
            lockCharacterFile: true,
            observerCount: 1);

        AssertUnchanged(result);
        Assert.Contains("transactionWriteFailed", result.Log, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(result.UsersRoot, "*.reward-points-*"));
    }

    [Theory]
    [InlineData("sessions")]
    [InlineData("users-root")]
    public async Task MissingEligibleMutationInfrastructureFailsClosed(string missing)
    {
        var result = await InvokeAsync(
            conquerPoints: Cost,
            subProfInfo: EmptySubClassState,
            subClassHashPoint: "0",
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
    [InlineData(-1L)]
    [InlineData(4_294_967_296L)]
    public async Task ConquerPointsOutsideSourceUInt32RangeFailClosed(long conquerPoints)
    {
        var result = await InvokeAsync(
            conquerPoints,
            EmptySubClassState,
            subClassHashPoint: "0",
            observerCount: 1);

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
        string subProfInfo,
        string subClassHashPoint,
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
        WriteCharacter(characterPath, conquerPoints, subProfInfo, subClassHashPoint);
        var originalBytes = File.ReadAllBytes(characterPath);

        var claimantSent = new List<byte[]>();
        var observerSent = new List<IReadOnlyList<byte[]>>();
        var exposureOrder = new List<string>();
        var claimant = CreateConnection(claimantSent, exposureOrder, "claimant", 9_978);
        var session = CreateSession(
            conquerPoints,
            subProfInfo,
            subClassHashPoint,
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
        if (includeSessions)
        {
            sessions.Set(claimant, session);
        }

        for (var index = 0; index < observerCount; index++)
        {
            var sent = new List<byte[]>();
            var observer = CreateConnection(
                sent,
                exposureOrder,
                "observer" + index.ToString(CultureInfo.InvariantCulture),
                9_979 + index);
            sessions.Set(
                observer,
                session with
                {
                    AccountId = AccountId + index + 1,
                    Character = session.Character with
                    {
                        Uid = CharacterUid + index + 1,
                        Name = "FullAccountSubClassObserver" + index.ToString(CultureInfo.InvariantCulture),
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
                optionId: 22,
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
        string subProfInfo,
        string subClassHashPoint,
        string? usersRoot)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = conquerPoints.ToString(CultureInfo.InvariantCulture),
            ["SubProfInfo"] = subProfInfo,
            ["SubClassHashPoint"] = subClassHashPoint,
            ["Sentinel"] = "keep-me",
        };
        return new WorldSession(
            AccountId,
            Username: "full-account-subclass-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "FullAccountSubClassHero",
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

    private static void WriteCharacter(
        string path,
        long conquerPoints,
        string subProfInfo,
        string subClassHashPoint)
    {
        File.WriteAllText(
            path,
            "[Character]\r\n" +
            $"UID={CharacterUid}\r\n" +
            "Name=FullAccountSubClassHero\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
            "FirstClass=15\r\nSecoundeClass=0\r\nReborn=2\r\nLevel=140\r\nExperience=0\r\n" +
            "Map=1002\r\nX=430\r\nY=378\r\nPMap=1002\r\nPMapX=430\r\nPMapY=378\r\n" +
            "Strength=0\r\nAgility=0\r\nVitaliti=0\r\nSpirit=0\r\nAtributes=0\r\n" +
            "MinHitPoints=1\r\nMinMana=0\r\nMoney=0\r\n" +
            $"ConquerPoints={conquerPoints.ToString(CultureInfo.InvariantCulture)}\r\n" +
            "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\n" +
            $"SubProfInfo={subProfInfo}\r\n" +
            $"SubClassHashPoint={subClassHashPoint}\r\n" +
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

    private static void AssertConquerPointsUpdate(byte[] bytes, ulong expected)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, update.Type);
        Assert.Equal(expected, update.Value);
    }

    private static LegacySubClassUpdatePacket ParseSubClassUpdate(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.LegacySubClass, frame.Header.Opcode);
        Assert.True(LegacySubClassUpdatePacket.TryParse(frame, out var packet));
        return packet;
    }

    private static void AssertPlayerEffect(byte[] bytes)
    {
        Assert.True(LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal((uint)CharacterUid, packet.Uid);
        Assert.Equal(LegacyStringId.Effect, packet.Id);
        Assert.Equal(["zf2-e300"], packet.Strings);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType type,
        string text,
        byte? optionId = null)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(type, packet.InteractType);
        Assert.Equal(text, packet.Text);
        if (optionId.HasValue)
        {
            Assert.Equal(optionId.Value, packet.OptionId);
        }

        return packet;
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
