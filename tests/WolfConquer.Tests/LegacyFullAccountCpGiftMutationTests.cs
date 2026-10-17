using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFullAccountCpGiftMutationTests : IDisposable
{
    private const uint NpcId = 465_123;
    private const long AccountId = 1_293_830;
    private const long CharacterUid = 1_293_830;
    private const long GiftAmount = 20_000_000;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-full-account-cp-gift-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(0L, 20_000_000L)]
    [InlineData(179_999_999L, 199_999_999L)]
    [InlineData(199_999_998L, 219_999_998L)]
    public async Task EligibleGiftPersistsBeforeCurrentCpUpdate(
        long conquerPoints,
        long expectedConquerPoints)
    {
        var result = await InvokeAsync(conquerPoints, observerCount: 1);

        Assert.True(result.Handled);
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(
            expectedConquerPoints.ToString(CultureInfo.InvariantCulture),
            result.UpdatedSession.Character.Attributes["ConquerPoints"]);
        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(expectedConquerPoints, result.PersistedAtCommit!.ConquerPoints);
        Assert.Equal("keep-me", result.PersistedAtCommit.Attributes["Sentinel"]);
        Assert.Equal("commit", result.ExposureOrder[0]);

        var updateBytes = Assert.Single(result.ClaimantSent);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(updateBytes), out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, update.Type);
        Assert.Equal(checked((ulong)expectedConquerPoints), update.Value);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Contains($"gift={GiftAmount}", result.Log, StringComparison.Ordinal);
        Assert.Contains("packetOrder=ConquerPoints", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(199_999_999L)]
    [InlineData(200_000_000L)]
    [InlineData(4_294_967_295L)]
    public async Task SourceCapShowsExactErrorWithoutNeedingMutationInfrastructure(long conquerPoints)
    {
        var result = await InvokeAsync(
            conquerPoints,
            includeSessions: false,
            includeUsersRoot: false,
            observerCount: 1);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        var messageBytes = Assert.Single(result.ClaimantSent);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(messageBytes), out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal("Error...", reply.Text);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Equal(result.OriginalBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Contains("threshold=199999999", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LockedCharacterFileRollsBackWithoutCpOrPacketExposure()
    {
        var result = await InvokeAsync(
            conquerPoints: 0,
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
            conquerPoints: 0,
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
        var result = await InvokeAsync(conquerPoints);

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
        WriteCharacter(characterPath, conquerPoints);
        var originalBytes = File.ReadAllBytes(characterPath);

        var claimantSent = new List<byte[]>();
        var observerSent = new List<IReadOnlyList<byte[]>>();
        var exposureOrder = new List<string>();
        var claimant = CreateConnection(claimantSent, exposureOrder, "claimant", 9_958);
        var session = CreateSession(conquerPoints, includeUsersRoot ? usersRoot : null);
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
                        Name = "FullAccountObserver" + index.ToString(CultureInfo.InvariantCulture),
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
                optionId: 18,
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

    private static WorldSession CreateSession(long conquerPoints, string? usersRoot)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = conquerPoints.ToString(CultureInfo.InvariantCulture),
            ["Sentinel"] = "keep-me",
        };
        return new WorldSession(
            AccountId,
            Username: "full-account-cp-gift-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "FullAccountHero",
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

    private static void WriteCharacter(string path, long conquerPoints)
    {
        File.WriteAllText(
            path,
            "[Character]\r\n" +
            $"UID={CharacterUid}\r\n" +
            "Name=FullAccountHero\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
            "FirstClass=15\r\nSecoundeClass=0\r\nReborn=2\r\nLevel=140\r\nExperience=0\r\n" +
            "Map=1002\r\nX=430\r\nY=378\r\nPMap=1002\r\nPMapX=430\r\nPMapY=378\r\n" +
            "Strength=0\r\nAgility=0\r\nVitaliti=0\r\nSpirit=0\r\nAtributes=0\r\n" +
            "MinHitPoints=1\r\nMinMana=0\r\nMoney=0\r\n" +
            $"ConquerPoints={conquerPoints.ToString(CultureInfo.InvariantCulture)}\r\n" +
            "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\n" +
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
