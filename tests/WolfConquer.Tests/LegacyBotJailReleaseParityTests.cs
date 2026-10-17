using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBotJailReleaseParityTests : IDisposable
{
    private const uint BotJailNpcId = 9_176;
    private const byte ReleaseOptionId = 3;
    private const long AccountId = 1_293_600;
    private const long CharacterUid = 1_293_600;
    private const long BotJailMapId = 6_000;
    private const long BotJailX = 31;
    private const long BotJailY = 72;
    private const string InsufficientConquerPointsMessage = "sorry you not have enough ConquerPoints  .";
    private const string SuccessMessage = "Please Not Repeat This Again,";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-bot-jail-release-parity-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(0U, 25_000_000L)]
    [InlineData(1U, 25_000_000L)]
    [InlineData(2U, 30_000_000L)]
    [InlineData(3U, 50_000_000L)]
    [InlineData(17U, 50_000_000L)]
    public async Task ExactBoundaryReleasePersistsChargeCountAndTwinCityBeforeOrderedSuccess(
        uint botJailCount,
        long requiredConquerPoints)
    {
        using var fixture = CreateFixture(botJailCount, requiredConquerPoints);

        Assert.True(await fixture.InvokeAsync(fixture.OriginalSession));

        var updated = fixture.StoredSession;
        Assert.Equal(0, updated.Character.ConquerPoints);
        Assert.Equal(
            checked(botJailCount + 1).ToString(CultureInfo.InvariantCulture),
            updated.Character.Attributes["botjail"]);
        Assert.Null(updated.ActiveNpcId);
        AssertTwinCityReleasePosition(updated.Character);

        var persisted = new CharacterSnapshotLoader(fixture.UsersRoot).LoadByAccountId(AccountId);
        Assert.NotNull(persisted);
        Assert.Equal(0, persisted!.ConquerPoints);
        Assert.Equal(
            checked(botJailCount + 1).ToString(CultureInfo.InvariantCulture),
            persisted.Attributes["botjail"]);
        Assert.Equal("keep-me", persisted.Attributes["Sentinel"]);
        AssertTwinCityReleasePosition(persisted);

        Assert.NotEmpty(fixture.PersistedAtCommit);
        Assert.Equal("commit", fixture.ExposureOrder[0]);
        Assert.All(fixture.PersistedAtCommit, AssertTwinCityReleasePosition);

        var conquerUpdate = Assert.Single(
            ParseUpdates(fixture.Sent),
            indexed => indexed.Packet.Updates.Any(IsConquerPointsUpdate));
        var conquerPoints = Assert.Single(conquerUpdate.Packet.Updates, IsConquerPointsUpdate);
        Assert.Equal(0UL, conquerPoints.Value);

        var teleport = Assert.Single(
            ParseActions(fixture.Sent),
            indexed => indexed.Packet.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.Packet.DwParam);
        Assert.Equal(410U, teleport.Packet.PositionX);
        Assert.Equal(354U, teleport.Packet.PositionY);

        var messageBox = Assert.Single(ParseReplies(fixture.Sent));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, messageBox.Packet.InteractType);
        Assert.Equal(SuccessMessage, messageBox.Packet.Text);
        Assert.True(conquerUpdate.Index < teleport.Index);
        Assert.True(teleport.Index < messageBox.Index);

        Assert.Equal(1, fixture.DisconnectCount);
        Assert.Equal("disconnect", fixture.ExposureOrder[^1]);
        Assert.Contains("source=NpcHandler.BotJail", fixture.LogText, StringComparison.Ordinal);
        Assert.Contains("sourceLines=NpcHandler.cs:7198-7266", fixture.LogText, StringComparison.Ordinal);
        Assert.Contains(
            $"cost={requiredConquerPoints.ToString(CultureInfo.InvariantCulture)}",
            fixture.LogText,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0U, 24_999_999L, 25_000_000L)]
    [InlineData(1U, 24_999_999L, 25_000_000L)]
    [InlineData(2U, 29_999_999L, 30_000_000L)]
    [InlineData(3U, 49_999_999L, 50_000_000L)]
    public async Task OneBelowBoundaryUsesExactSourceMessageWithoutMutation(
        uint botJailCount,
        long conquerPoints,
        long requiredConquerPoints)
    {
        using var fixture = CreateFixture(botJailCount, conquerPoints);

        Assert.True(await fixture.InvokeAsync(fixture.OriginalSession));

        AssertUnchanged(fixture);
        Assert.Empty(ParseUpdates(fixture.Sent));
        Assert.DoesNotContain(ParseActions(fixture.Sent), indexed => indexed.Packet.Type == MsgActionType.Teleport);

        var messageBox = Assert.Single(ParseReplies(fixture.Sent));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, messageBox.Packet.InteractType);
        Assert.Equal(InsufficientConquerPointsMessage, messageBox.Packet.Text);
        Assert.Equal(0, fixture.DisconnectCount);
        Assert.Contains(
            $"requiredCp={requiredConquerPoints.ToString(CultureInfo.InvariantCulture)}",
            fixture.LogText,
            StringComparison.Ordinal);
        Assert.Contains("sourceLines=NpcHandler.cs:7198-7266", fixture.LogText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PersistenceFailureRollsBackWithoutSuccessOrDisconnect()
    {
        using var fixture = CreateFixture(botJailCount: 2, conquerPoints: 30_000_000);
        using var lockedCharacter = fixture.LockCharacterFile();

        Assert.True(await fixture.InvokeAsync(fixture.OriginalSession));

        AssertUnchanged(fixture);
        Assert.DoesNotContain(ParseUpdates(fixture.Sent), indexed => indexed.Packet.Updates.Any(IsConquerPointsUpdate));
        Assert.DoesNotContain(ParseActions(fixture.Sent), indexed => indexed.Packet.Type == MsgActionType.Teleport);
        Assert.DoesNotContain(ParseReplies(fixture.Sent), indexed => indexed.Packet.Text == SuccessMessage);
        Assert.Equal(0, fixture.DisconnectCount);
        Assert.Contains("persistence", fixture.LogText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sourceLines=NpcHandler.cs:7198-7266", fixture.LogText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaleInFlightReplayCannotChargeOrExposeReleaseTwice()
    {
        using var fixture = CreateFixture(botJailCount: 2, conquerPoints: 80_000_000);

        Assert.True(await fixture.InvokeAsync(fixture.OriginalSession));
        var afterFirstRelease = fixture.StoredSession;
        Assert.Equal(50_000_000, afterFirstRelease.Character.ConquerPoints);
        Assert.Equal("3", afterFirstRelease.Character.Attributes["botjail"]);
        var sentAfterFirstRelease = fixture.Sent.Count;
        var commitsAfterFirstRelease = fixture.PersistedAtCommit.Count;
        var disconnectsAfterFirstRelease = fixture.DisconnectCount;
        var persistedAfterFirstRelease = File.ReadAllBytes(fixture.CharacterPath);
        Assert.Single(ParseReplies(fixture.Sent), indexed => indexed.Packet.Text == SuccessMessage);

        // Model an option packet that captured the pre-commit session before the first release completed.
        Assert.True(await fixture.InvokeAsync(fixture.OriginalSession));

        Assert.Same(afterFirstRelease, fixture.StoredSession);
        Assert.Equal(50_000_000, fixture.StoredSession.Character.ConquerPoints);
        Assert.Equal("3", fixture.StoredSession.Character.Attributes["botjail"]);
        Assert.Equal(persistedAfterFirstRelease, File.ReadAllBytes(fixture.CharacterPath));
        Assert.Equal(sentAfterFirstRelease, fixture.Sent.Count);
        Assert.Equal(commitsAfterFirstRelease, fixture.PersistedAtCommit.Count);
        Assert.Equal(disconnectsAfterFirstRelease, fixture.DisconnectCount);
        Assert.Single(ParseReplies(fixture.Sent), indexed => indexed.Packet.Text == SuccessMessage);
        Assert.Contains("reason=active-npc-mismatch", fixture.LogText, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private BotJailFixture CreateFixture(uint botJailCount, long conquerPoints)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        return new BotJailFixture(invocationRoot, botJailCount, conquerPoints);
    }

    private static void AssertUnchanged(BotJailFixture fixture)
    {
        Assert.Same(fixture.OriginalSession, fixture.StoredSession);
        Assert.Equal(fixture.InitialConquerPoints, fixture.StoredSession.Character.ConquerPoints);
        Assert.Equal(
            fixture.InitialBotJailCount.ToString(CultureInfo.InvariantCulture),
            fixture.StoredSession.Character.Attributes["botjail"]);
        Assert.Equal(BotJailMapId, fixture.StoredSession.Character.MapId);
        Assert.Equal(BotJailX, fixture.StoredSession.Character.X);
        Assert.Equal(BotJailY, fixture.StoredSession.Character.Y);
        Assert.Equal(fixture.OriginalBytes, File.ReadAllBytes(fixture.CharacterPath));
        Assert.Empty(fixture.PersistedAtCommit);
    }

    private static void AssertTwinCityReleasePosition(CharacterSnapshot character)
    {
        Assert.Equal(1_002, character.MapId);
        Assert.Equal(410, character.X);
        Assert.Equal(354, character.Y);
        Assert.Equal(BotJailMapId, character.PreviousMapId);
        Assert.Equal(BotJailX, character.PreviousMapX);
        Assert.Equal(BotJailY, character.PreviousMapY);
    }

    private static bool IsConquerPointsUpdate(LegacyMsgUpdateEntry update)
    {
        return update.Type == LegacyMsgUpdateDataType.ConquerPoints;
    }

    private static IReadOnlyList<IndexedUpdate> ParseUpdates(IReadOnlyList<byte[]> sent)
    {
        var packets = new List<IndexedUpdate>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[index]), out var packet))
            {
                packets.Add(new IndexedUpdate(index, packet));
            }
        }

        return packets;
    }

    private static IReadOnlyList<IndexedAction> ParseActions(IReadOnlyList<byte[]> sent)
    {
        var packets = new List<IndexedAction>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (MsgActionPacket.TryParse(ReadFrame(sent[index]), out var packet))
            {
                packets.Add(new IndexedAction(index, packet));
            }
        }

        return packets;
    }

    private static IReadOnlyList<IndexedReply> ParseReplies(IReadOnlyList<byte[]> sent)
    {
        var packets = new List<IndexedReply>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(sent[index]), out var packet))
            {
                packets.Add(new IndexedReply(index, packet));
            }
        }

        return packets;
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

    private sealed class BotJailFixture : IDisposable
    {
        private readonly StringWriter _log = new();

        public BotJailFixture(string root, uint botJailCount, long conquerPoints)
        {
            UsersRoot = Path.Combine(root, "Users");
            Directory.CreateDirectory(UsersRoot);
            CharacterPath = Path.Combine(
                UsersRoot,
                AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            WriteCharacter(CharacterPath, botJailCount, conquerPoints);
            OriginalBytes = File.ReadAllBytes(CharacterPath);
            InitialBotJailCount = botJailCount;
            InitialConquerPoints = conquerPoints;
            OriginalSession = CreateSession(CharacterPath, UsersRoot, botJailCount, conquerPoints);

            Connection = new ConquerTcpConnection(
                null,
                new IPEndPoint(IPAddress.Loopback, 9_958),
                (frame, _) =>
                {
                    Sent.Add(frame.ToArray());
                    ExposureOrder.Add("send");
                    return ValueTask.CompletedTask;
                },
                _ =>
                {
                    DisconnectCount++;
                    ExposureOrder.Add("disconnect");
                    return ValueTask.CompletedTask;
                });
            Sessions = new WorldSessionStore((changedConnection, previous, _) =>
            {
                if (!ReferenceEquals(changedConnection, Connection) || previous is null)
                {
                    return;
                }

                var persisted = new CharacterSnapshotLoader(UsersRoot).LoadByAccountId(AccountId);
                PersistedAtCommit.Add(Assert.IsType<CharacterSnapshot>(persisted));
                ExposureOrder.Add("commit");
            });
            Sessions.Set(Connection, OriginalSession);
        }

        public string UsersRoot { get; }

        public string CharacterPath { get; }

        public byte[] OriginalBytes { get; }

        public uint InitialBotJailCount { get; }

        public long InitialConquerPoints { get; }

        public WorldSession OriginalSession { get; }

        public ConquerTcpConnection Connection { get; }

        public WorldSessionStore Sessions { get; }

        public List<byte[]> Sent { get; } = [];

        public List<string> ExposureOrder { get; } = [];

        public List<CharacterSnapshot> PersistedAtCommit { get; } = [];

        public int DisconnectCount { get; private set; }

        public string LogText => _log.ToString();

        public WorldSession StoredSession
        {
            get
            {
                Assert.True(Sessions.TryGet(Connection, out var stored));
                return Assert.IsType<WorldSession>(stored);
            }
        }

        public FileStream LockCharacterFile()
        {
            return new FileStream(CharacterPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public ValueTask<bool> InvokeAsync(WorldSession dispatchedSession)
        {
            return WorldPacketHandlers.TryHandleLegacyMiscWarEventNpcAsync(
                new WorldPacketHandlerContext(
                    Connection,
                    _log,
                    Session: dispatchedSession,
                    Sessions: Sessions),
                dispatchedSession,
                BotJailNpcId,
                ReleaseOptionId,
                CancellationToken.None);
        }

        public void Dispose()
        {
            _log.Dispose();
        }

        private static WorldSession CreateSession(
            string characterPath,
            string usersRoot,
            uint botJailCount,
            long conquerPoints)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DynamicID"] = "0",
                ["ConquerPoints"] = conquerPoints.ToString(CultureInfo.InvariantCulture),
                ["botjail"] = botJailCount.ToString(CultureInfo.InvariantCulture),
                ["Sentinel"] = "keep-me",
            };

            return new WorldSession(
                AccountId,
                Username: "bot-jail-release-parity",
                Character: new CharacterSnapshot(
                    Uid: CharacterUid,
                    Name: "BotJailParity",
                    Body: 1003,
                    Face: 0,
                    Hair: 0,
                    Class: 15,
                    FirstClass: 15,
                    SecondClass: 0,
                    Reborn: 2,
                    Level: 140,
                    Experience: 0,
                    MapId: BotJailMapId,
                    X: BotJailX,
                    Y: BotJailY,
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
                    SourceFile: characterPath,
                    Attributes: attributes,
                    PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
                PlayerItems: PlayerItemSnapshot.Empty,
                PlayerSpells: PlayerSpellSnapshot.Empty,
                PlayerProficiencies: PlayerProficiencySnapshot.Empty,
                UsersRoot: usersRoot,
                ActiveNpcId: BotJailNpcId);
        }

        private static void WriteCharacter(string path, uint botJailCount, long conquerPoints)
        {
            File.WriteAllText(
                path,
                "[Character]\r\n" +
                $"UID={CharacterUid}\r\n" +
                "Name=BotJailParity\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
                "FirstClass=15\r\nSecoundeClass=0\r\nReborn=2\r\nLevel=140\r\nExperience=0\r\n" +
                $"Map={BotJailMapId}\r\nX={BotJailX}\r\nY={BotJailY}\r\n" +
                "PMap=1002\r\nPMapX=430\r\nPMapY=378\r\n" +
                "Strength=0\r\nAgility=0\r\nVitaliti=0\r\nSpirit=0\r\nAtributes=0\r\n" +
                "MinHitPoints=1\r\nMinMana=0\r\nMoney=0\r\n" +
                $"ConquerPoints={conquerPoints.ToString(CultureInfo.InvariantCulture)}\r\n" +
                "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\n" +
                $"botjail={botJailCount.ToString(CultureInfo.InvariantCulture)}\r\n" +
                "DynamicID=0\r\nSentinel=keep-me\r\n",
                Encoding.Latin1);
        }
    }

    private sealed record IndexedUpdate(int Index, LegacyMsgUpdatePacket Packet);

    private sealed record IndexedAction(int Index, MsgActionPacket Packet);

    private sealed record IndexedReply(int Index, LegacyNpcReplyPacket Packet);
}
