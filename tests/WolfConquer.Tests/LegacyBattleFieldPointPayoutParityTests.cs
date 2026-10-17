using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBattleFieldPointPayoutParityTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint CharacterUid = 1_293_600;
    private const uint BattleFieldMap1NpcId = 4_026_308;
    private const uint BattleFieldMap2NpcId = 4_026_309;
    private const uint BattleFieldMap3NpcId = 4_026_310;
    private const long SourceMapId = 1_081;
    private const long SourceX = 196;
    private const long SourceY = 214;
    private const long TwinCityMapId = 1_002;
    private const long TwinCityX = 410;
    private const long TwinCityY = 354;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyBattleFieldPointPayoutParityTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(BattleFieldMap1NpcId, 5U, 1U)]
    [InlineData(BattleFieldMap1NpcId, 2_504U, 500U)]
    [InlineData(BattleFieldMap2NpcId, 5U, 1U)]
    [InlineData(BattleFieldMap2NpcId, 2_504U, 500U)]
    [InlineData(BattleFieldMap3NpcId, 5U, 1U)]
    [InlineData(BattleFieldMap3NpcId, 2_504U, 500U)]
    public async Task PositivePayoutCommitsExactQuotientResetAndD13Frames(
        uint npcId,
        uint battleFieldPoints,
        uint expectedReward)
    {
        using var harness = CreateHarness(battleFieldPoints, conquerPoints: 100, npcId: npcId);

        var handled = await harness.InvokeAsync();

        Assert.True(handled);
        var updated = harness.CurrentSession;
        Assert.Equal(100L + expectedReward, updated.Character.ConquerPoints);
        Assert.Equal("0", updated.Character.Attributes["BattleFieldPoints"]);
        Assert.Equal(TwinCityMapId, updated.Character.MapId);
        Assert.Equal(TwinCityX, updated.Character.X);
        Assert.Equal(TwinCityY, updated.Character.Y);
        Assert.Equal(SourceMapId, updated.Character.PreviousMapId);
        Assert.Equal(SourceX, updated.Character.PreviousMapX);
        Assert.Equal(SourceY, updated.Character.PreviousMapY);
        Assert.Equal("0", updated.Character.Attributes["DynamicID"]);
        Assert.Null(updated.ActiveNpcId);

        var persistedAtCommit = Assert.IsType<CharacterSnapshot>(harness.PersistedAtCommit);
        AssertPersistedPayout(persistedAtCommit, 100L + expectedReward);
        AssertPersistedPayout(harness.Reload(), 100L + expectedReward);
        Assert.Equal("commit", harness.ExposureOrder[0]);

        AssertExactPayoutFrames(harness.Sent, expectedReward, 100UL + expectedReward);
        Assert.Contains(
            "packetOrder=NpcReply.MessageBox,MsgUpdate.ConquerPoints,GameClient.Teleport",
            harness.Log.ToString(),
            StringComparison.Ordinal);
        Assert.Contains(
            $"battleFieldPoints={battleFieldPoints}->0 pointsPerConquerPoint=5 rewardConquerPoints={expectedReward}",
            harness.Log.ToString(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(BattleFieldMap1NpcId)]
    [InlineData(BattleFieldMap2NpcId)]
    [InlineData(BattleFieldMap3NpcId)]
    public async Task SubThresholdPointsCommitSourceZeroRewardResetAndD13Frames(uint npcId)
    {
        using var harness = CreateHarness(battleFieldPoints: 4, conquerPoints: 100, npcId: npcId);

        var handled = await harness.InvokeAsync();

        Assert.True(handled);
        Assert.False(
            ReferenceEquals(harness.OriginalSession, harness.CurrentSession),
            harness.Log.ToString());
        Assert.Equal(100, harness.CurrentSession.Character.ConquerPoints);
        AssertPersistedPayout(
            Assert.IsType<CharacterSnapshot>(harness.PersistedAtCommit),
            expectedConquerPoints: 100);
        AssertPersistedPayout(harness.Reload(), expectedConquerPoints: 100);
        Assert.Equal("commit", harness.ExposureOrder[0]);
        AssertExactPayoutFrames(harness.Sent, expectedReward: 0, expectedWallet: 100);
        Assert.Contains(
            "battleFieldPoints=4->0 pointsPerConquerPoint=5 rewardConquerPoints=0",
            harness.Log.ToString(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(BattleFieldMap1NpcId)]
    [InlineData(BattleFieldMap2NpcId)]
    [InlineData(BattleFieldMap3NpcId)]
    public async Task PersistenceFailureRollsBackWalletPointsMapAndSuccessFrames(uint npcId)
    {
        using var harness = CreateHarness(battleFieldPoints: 2_500, conquerPoints: 100, npcId: npcId);
        var originalBytes = File.ReadAllBytes(harness.CharacterPath);
        using var locked = new FileStream(
            harness.CharacterPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var handled = await harness.InvokeAsync();

        Assert.True(handled);
        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        Assert.Equal(originalBytes, File.ReadAllBytes(harness.CharacterPath));
        Assert.Null(harness.PersistedAtCommit);
        AssertExactUnavailableDialog(harness.Sent);
        AssertNoSuccessFrames(harness.Sent);
        Assert.Contains("persistence:snapshotWriteFailed", harness.Log.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(BattleFieldMap1NpcId)]
    [InlineData(BattleFieldMap2NpcId)]
    [InlineData(BattleFieldMap3NpcId)]
    public async Task DuplicateReplyUsingStaleSessionCannotClaimResetPointsTwice(uint npcId)
    {
        using var harness = CreateHarness(battleFieldPoints: 2_500, conquerPoints: 100, npcId: npcId);

        Assert.True(await harness.InvokeAsync());
        Assert.Equal(600, harness.CurrentSession.Character.ConquerPoints);
        var firstClaimFrameCount = harness.Sent.Count;
        var firstCommitCount = harness.ExposureOrder.Count(entry => entry == "commit");

        Assert.True(await harness.InvokeAsync(harness.OriginalSession));

        Assert.Equal(600, harness.CurrentSession.Character.ConquerPoints);
        Assert.Equal("0", harness.CurrentSession.Character.Attributes["BattleFieldPoints"]);
        AssertPersistedPayout(
            harness.Reload(),
            expectedConquerPoints: 600);
        Assert.Equal(firstCommitCount + 2, harness.ExposureOrder.Count(entry => entry == "commit"));
        var duplicateFrames = harness.Sent.Skip(firstClaimFrameCount).ToArray();
        AssertExactPayoutFrames(duplicateFrames, expectedReward: 0, expectedWallet: 600);
        Assert.Contains(
            "battleFieldPoints=0->0 pointsPerConquerPoint=5 rewardConquerPoints=0",
            harness.Log.ToString(),
            StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private PayoutHarness CreateHarness(uint battleFieldPoints, long conquerPoints, uint npcId)
    {
        var usersRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"), "Users");
        Directory.CreateDirectory(usersRoot);
        return new PayoutHarness(usersRoot, battleFieldPoints, conquerPoints, npcId);
    }

    private static void AssertPersistedPayout(
        CharacterSnapshot character,
        long expectedConquerPoints,
        long expectedPreviousMapId = SourceMapId,
        long expectedPreviousX = SourceX,
        long expectedPreviousY = SourceY)
    {
        Assert.Equal(expectedConquerPoints, character.ConquerPoints);
        Assert.Equal("0", character.Attributes["BattleFieldPoints"]);
        Assert.Equal(TwinCityMapId, character.MapId);
        Assert.Equal(TwinCityX, character.X);
        Assert.Equal(TwinCityY, character.Y);
        Assert.Equal(expectedPreviousMapId, character.PreviousMapId);
        Assert.Equal(expectedPreviousX, character.PreviousMapX);
        Assert.Equal(expectedPreviousY, character.PreviousMapY);
        Assert.Equal("0", character.Attributes["DynamicID"]);
        Assert.Equal("keep-me", character.Attributes["Sentinel"]);
    }

    private static void AssertExactUnavailableDialog(IReadOnlyList<byte[]> sent)
    {
        Assert.Equal(4, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("BattleField point payout is not available from this runtime yet.", dialog.Text);
        var option = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal("I understand.", option.Text);
        Assert.Equal(byte.MaxValue, option.OptionId);
        var avatar = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal((ushort)63, avatar.InputMaxLength);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);
    }

    private static void AssertNoSuccessFrames(IEnumerable<byte[]> sent)
    {
        Assert.DoesNotContain(
            sent,
            bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(
            sent,
            bytes => MsgActionPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(
            sent,
            bytes => LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply) &&
                reply.InteractType == LegacyNpcReplyInteractType.MessageBox);
    }

    private static void AssertExactPayoutFrames(
        IReadOnlyList<byte[]> sent,
        uint expectedReward,
        ulong expectedWallet)
    {
        Assert.Equal(7, sent.Count);
        var message = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.MessageBox);
        Assert.Equal($"You received {expectedReward} ConquerPoints.", message.Text);
        Assert.Equal((byte)0, message.OptionId);
        Assert.Equal(ushort.MaxValue, message.InputMaxLength);
        AssertSingleConquerPointsUpdate(sent[1], expectedWallet);
        AssertLegacyTwinCityTransition(sent, startIndex: 2);
        Assert.Single(sent, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        Assert.Single(sent, bytes => LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out _));
    }

    private static void AssertSingleConquerPointsUpdate(byte[] bytes, ulong expectedValue)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(CharacterUid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, update.Type);
        Assert.Equal(expectedValue, update.Value);
    }

    private static void AssertLegacyTwinCityTransition(IReadOnlyList<byte[]> sent, int startIndex)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex]), out var teleport));
        Assert.Equal(CharacterUid, teleport.ObjId);
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal((uint)TwinCityMapId, teleport.DwParam);
        Assert.Equal(TwinCityMapId, teleport.DwParam3);
        Assert.Equal((uint)TwinCityX, teleport.PositionX);
        Assert.Equal((uint)TwinCityY, teleport.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 1]), out var startup));
        Assert.Equal(MsgActionType.LegacyMerchantStartup, startup.Type);
        Assert.Equal(2U, startup.DwParam);
        Assert.Equal(TwinCityMapId, startup.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 2]), out var stopVending));
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal((uint)TwinCityMapId, stopVending.DwParam);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 3]), out var color));
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal((uint)TwinCityX, color.PositionX);
        Assert.Equal((uint)TwinCityY, color.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[startIndex + 4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(TwinCityMapId, mapsById: null), mapStatus);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply));
        Assert.Equal(expectedType, reply.InteractType);
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

    private sealed class PayoutHarness : IDisposable
    {
        private readonly StringWriter _log = new();
        private readonly uint _npcId;

        public PayoutHarness(string usersRoot, uint battleFieldPoints, long conquerPoints, uint npcId)
        {
            _npcId = npcId;
            UsersRoot = usersRoot;
            CharacterPath = Path.Combine(
                usersRoot,
                AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            WriteCharacter(CharacterPath, battleFieldPoints, conquerPoints);
            OriginalSession = CreateSession(usersRoot, battleFieldPoints, conquerPoints, npcId);
            Connection = new ConquerTcpConnection(
                null,
                new IPEndPoint(IPAddress.Loopback, 9_958),
                (frame, _) =>
                {
                    var bytes = frame.ToArray();
                    Sent.Add(bytes);
                    ExposureOrder.Add(
                        "claimant:" + ReadFrame(bytes).Header.Opcode.ToString(CultureInfo.InvariantCulture));
                    return ValueTask.CompletedTask;
                });
            Sessions = new WorldSessionStore((connection, previous, _) =>
            {
                if (!ReferenceEquals(connection, Connection) || previous is null)
                {
                    return;
                }

                PersistedAtCommit = Reload();
                ExposureOrder.Add("commit");
            });
            Sessions.Set(Connection, OriginalSession);
        }

        public string UsersRoot { get; }

        public string CharacterPath { get; }

        public WorldSession OriginalSession { get; }

        public ConquerTcpConnection Connection { get; }

        public WorldSessionStore Sessions { get; }

        public List<byte[]> Sent { get; } = [];

        public List<string> ExposureOrder { get; } = [];

        public CharacterSnapshot? PersistedAtCommit { get; private set; }

        public StringWriter Log => _log;

        public WorldSession CurrentSession
        {
            get
            {
                Assert.True(Sessions.TryGet(Connection, out var session));
                return Assert.IsType<WorldSession>(session);
            }
        }

        public async Task<bool> InvokeAsync(WorldSession? suppliedSession = null)
        {
            var session = suppliedSession ?? CurrentSession;
            return await WorldPacketHandlers.TryHandleLegacyDisCityBattlefieldServicesNpcAsync(
                new WorldPacketHandlerContext(
                    Connection,
                    _log,
                    Session: session,
                    Sessions: Sessions,
                    ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13),
                session,
                _npcId,
                optionId: 2,
                CancellationToken.None);
        }

        public CharacterSnapshot Reload()
        {
            return Assert.IsType<CharacterSnapshot>(
                new CharacterSnapshotLoader(UsersRoot).LoadByAccountId(AccountId));
        }

        public void Dispose()
        {
            _log.Dispose();
        }

        private static WorldSession CreateSession(
            string usersRoot,
            uint battleFieldPoints,
            long conquerPoints,
            uint npcId)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["BattleFieldPoints"] = battleFieldPoints.ToString(CultureInfo.InvariantCulture),
                ["ConquerPoints"] = conquerPoints.ToString(CultureInfo.InvariantCulture),
                ["DynamicID"] = "77",
                ["Sentinel"] = "keep-me",
            };
            return new WorldSession(
                AccountId,
                Username: "battlefield-payout-test",
                Character: new CharacterSnapshot(
                    Uid: CharacterUid,
                    Name: "BattleFieldHero",
                    Body: 1003,
                    Face: 0,
                    Hair: 0,
                    Class: 15,
                    FirstClass: 15,
                    SecondClass: 0,
                    Reborn: 0,
                    Level: 140,
                    Experience: 0,
                    MapId: SourceMapId,
                    X: SourceX,
                    Y: SourceY,
                    PreviousMapId: SourceMapId,
                    PreviousMapX: SourceX,
                    PreviousMapY: SourceY,
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
                ActiveNpcId: npcId);
        }

        private static void WriteCharacter(string path, uint battleFieldPoints, long conquerPoints)
        {
            File.WriteAllText(
                path,
                "[Character]\r\n" +
                $"UID={CharacterUid}\r\n" +
                "Name=BattleFieldHero\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
                "FirstClass=15\r\nSecoundeClass=0\r\nReborn=0\r\nLevel=140\r\nExperience=0\r\n" +
                $"Map={SourceMapId}\r\nX={SourceX}\r\nY={SourceY}\r\n" +
                $"PMap={SourceMapId}\r\nPMapX={SourceX}\r\nPMapY={SourceY}\r\n" +
                "Strength=0\r\nAgility=0\r\nVitaliti=0\r\nSpirit=0\r\nAtributes=0\r\n" +
                "MinHitPoints=1\r\nMinMana=0\r\nMoney=0\r\n" +
                $"ConquerPoints={conquerPoints.ToString(CultureInfo.InvariantCulture)}\r\n" +
                "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\n" +
                $"BattleFieldPoints={battleFieldPoints.ToString(CultureInfo.InvariantCulture)}\r\n" +
                "DynamicID=77\r\nSentinel=keep-me\r\n",
                Encoding.Latin1);
        }
    }
}
