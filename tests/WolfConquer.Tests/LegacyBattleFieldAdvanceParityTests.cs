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

public sealed class LegacyBattleFieldAdvanceParityTests : IDisposable
{
    private const long AccountId = 1_293_601;
    private const uint CharacterUid = 1_293_601;
    private const uint BattleFieldMap1NpcId = 4_026_308;
    private const uint BattleFieldMap2NpcId = 4_026_309;
    private const long ConquerPoints = 100;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyBattleFieldAdvanceParityTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(BattleFieldMap1NpcId)]
    [InlineData(BattleFieldMap2NpcId)]
    public async Task Option1SubtractsExactSourceCostPersistsAndTeleports(uint npcId)
    {
        var route = RouteFor(npcId);

        await AssertSuccessfulAdvanceAsync(route, checked(route.PointCost + 777U), expectedPoints: 777);
    }

    [Theory]
    [InlineData(BattleFieldMap1NpcId)]
    [InlineData(BattleFieldMap2NpcId)]
    public async Task Option1ExactBoundarySucceedsAndLeavesZeroPoints(uint npcId)
    {
        var route = RouteFor(npcId);

        await AssertSuccessfulAdvanceAsync(route, route.PointCost, expectedPoints: 0);
    }

    [Theory]
    [InlineData(BattleFieldMap1NpcId)]
    [InlineData(BattleFieldMap2NpcId)]
    public async Task Option1InsufficientPointsUsesExactSourceDialogWithoutMutation(uint npcId)
    {
        var route = RouteFor(npcId);
        var startingPoints = route.PointCost - 1U;
        using var harness = CreateHarness(route, startingPoints);
        var originalBytes = File.ReadAllBytes(harness.CharacterPath);

        var handled = await harness.InvokeAsync();

        Assert.True(handled);
        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        Assert.Equal(originalBytes, File.ReadAllBytes(harness.CharacterPath));
        Assert.Equal(
            startingPoints.ToString(CultureInfo.InvariantCulture),
            harness.CurrentSession.Character.Attributes["BattleFieldPoints"]);
        Assert.Equal(route.SourceMapId, harness.CurrentSession.Character.MapId);
        Assert.Equal(route.SourceX, harness.CurrentSession.Character.X);
        Assert.Equal(route.SourceY, harness.CurrentSession.Character.Y);
        Assert.Equal(route.NpcId, harness.CurrentSession.ActiveNpcId);
        Assert.Null(harness.PersistedAtCommit);
        Assert.DoesNotContain("commit", harness.ExposureOrder);
        AssertExactInsufficientDialog(harness.Sent, route.InsufficientPointsText);
        AssertNoSuccessFrames(harness.Sent);
    }

    [Theory]
    [InlineData(BattleFieldMap1NpcId)]
    [InlineData(BattleFieldMap2NpcId)]
    public async Task Option1PersistenceFailureRollsBackPointsMapAndSuccessFrames(uint npcId)
    {
        var route = RouteFor(npcId);
        var startingPoints = checked(route.PointCost + 500U);
        using var harness = CreateHarness(route, startingPoints);
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
        Assert.Equal(
            startingPoints.ToString(CultureInfo.InvariantCulture),
            harness.CurrentSession.Character.Attributes["BattleFieldPoints"]);
        Assert.Equal(route.SourceMapId, harness.CurrentSession.Character.MapId);
        Assert.Equal(route.SourceX, harness.CurrentSession.Character.X);
        Assert.Equal(route.SourceY, harness.CurrentSession.Character.Y);
        Assert.Equal(route.NpcId, harness.CurrentSession.ActiveNpcId);
        Assert.Null(harness.PersistedAtCommit);
        Assert.DoesNotContain("commit", harness.ExposureOrder);
        AssertNoSuccessFrames(harness.Sent);
        Assert.Contains("persistence:snapshotWriteFailed", harness.Log.ToString(), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task AssertSuccessfulAdvanceAsync(
        AdvanceRoute route,
        uint startingPoints,
        uint expectedPoints)
    {
        using var harness = CreateHarness(route, startingPoints);

        var handled = await harness.InvokeAsync();

        Assert.True(handled);
        Assert.NotSame(harness.OriginalSession, harness.CurrentSession);
        AssertAdvanceState(harness.CurrentSession.Character, route, expectedPoints);
        Assert.Null(harness.CurrentSession.ActiveNpcId);

        var persistedAtCommit = Assert.IsType<CharacterSnapshot>(harness.PersistedAtCommit);
        AssertAdvanceState(persistedAtCommit, route, expectedPoints);
        AssertAdvanceState(harness.Reload(), route, expectedPoints);
        Assert.Equal("commit", harness.ExposureOrder[0]);
        Assert.Single(harness.ExposureOrder, entry => entry == "commit");

        AssertExactTeleportTransition(harness.Sent, route);
        Assert.DoesNotContain(
            harness.Sent,
            bytes => LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(
            harness.Sent,
            bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        Assert.Contains(
            $"battleFieldPoints={startingPoints}->{expectedPoints} pointCost={route.PointCost} " +
            $"map={route.DestinationMapId} x={route.DestinationX} y={route.DestinationY}",
            harness.Log.ToString(),
            StringComparison.Ordinal);
        Assert.Contains("persistenceBeforeSessionCommit=true", harness.Log.ToString(), StringComparison.Ordinal);
    }

    private AdvanceHarness CreateHarness(AdvanceRoute route, uint battleFieldPoints)
    {
        var usersRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"), "Users");
        Directory.CreateDirectory(usersRoot);
        return new AdvanceHarness(usersRoot, route, battleFieldPoints);
    }

    private static void AssertAdvanceState(
        CharacterSnapshot character,
        AdvanceRoute route,
        uint expectedPoints)
    {
        Assert.Equal(
            expectedPoints.ToString(CultureInfo.InvariantCulture),
            character.Attributes["BattleFieldPoints"]);
        Assert.Equal(ConquerPoints, character.ConquerPoints);
        Assert.Equal(route.DestinationMapId, character.MapId);
        Assert.Equal(route.DestinationX, character.X);
        Assert.Equal(route.DestinationY, character.Y);
        Assert.Equal(route.SourceMapId, character.PreviousMapId);
        Assert.Equal(route.SourceX, character.PreviousMapX);
        Assert.Equal(route.SourceY, character.PreviousMapY);
        Assert.Equal("0", character.Attributes["DynamicID"]);
        Assert.Equal("keep-me", character.Attributes["Sentinel"]);
    }

    private static void AssertExactInsufficientDialog(IReadOnlyList<byte[]> sent, string expectedText)
    {
        Assert.Equal(3, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal(expectedText, dialog.Text);
        var avatar = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal((ushort)63, avatar.InputMaxLength);
        AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Finish);
    }

    private static void AssertNoSuccessFrames(IEnumerable<byte[]> sent)
    {
        Assert.DoesNotContain(
            sent,
            bytes => MsgActionPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(
            sent,
            bytes => LegacyMapStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(
            sent,
            bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(
            sent,
            bytes => LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply) &&
                reply.InteractType == LegacyNpcReplyInteractType.MessageBox);
    }

    private static void AssertExactTeleportTransition(IReadOnlyList<byte[]> sent, AdvanceRoute route)
    {
        Assert.Equal(5, sent.Count);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var teleport));
        Assert.Equal(CharacterUid, teleport.ObjId);
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal((uint)route.DestinationMapId, teleport.DwParam);
        Assert.Equal(route.DestinationMapId, teleport.DwParam3);
        Assert.Equal((uint)route.DestinationX, teleport.PositionX);
        Assert.Equal((uint)route.DestinationY, teleport.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[1]), out var startup));
        Assert.Equal(CharacterUid, startup.ObjId);
        Assert.Equal(MsgActionType.LegacyMerchantStartup, startup.Type);
        Assert.Equal(2U, startup.DwParam);
        Assert.Equal(route.DestinationMapId, startup.DwParam3);
        Assert.Equal((uint)route.DestinationX, startup.PositionX);
        Assert.Equal((uint)route.DestinationY, startup.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[2]), out var stopVending));
        Assert.Equal(CharacterUid, stopVending.ObjId);
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal((uint)route.DestinationMapId, stopVending.DwParam);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var color));
        Assert.Equal(CharacterUid, color.ObjId);
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal((uint)route.DestinationX, color.PositionX);
        Assert.Equal((uint)route.DestinationY, color.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(route.DestinationMapId, mapsById: null), mapStatus);
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

    private static AdvanceRoute RouteFor(uint npcId)
    {
        return npcId switch
        {
            BattleFieldMap1NpcId => new AdvanceRoute(
                BattleFieldMap1NpcId,
                PointCost: 2_000,
                SourceMapId: 1_081,
                SourceX: 196,
                SourceY: 214,
                DestinationMapId: 1_080,
                DestinationX: 195,
                DestinationY: 208,
                InsufficientPointsText: "You not have 2.000 Points."),
            BattleFieldMap2NpcId => new AdvanceRoute(
                BattleFieldMap2NpcId,
                PointCost: 3_500,
                SourceMapId: 1_080,
                SourceX: 195,
                SourceY: 208,
                DestinationMapId: 2_060,
                DestinationX: 183,
                DestinationY: 156,
                InsufficientPointsText: "You not have 3.500 Points."),
            _ => throw new ArgumentOutOfRangeException(nameof(npcId), npcId, "Unknown BattleField map NPC."),
        };
    }

    private sealed class AdvanceHarness : IDisposable
    {
        private readonly AdvanceRoute _route;
        private readonly StringWriter _log = new();

        public AdvanceHarness(string usersRoot, AdvanceRoute route, uint battleFieldPoints)
        {
            _route = route;
            UsersRoot = usersRoot;
            CharacterPath = Path.Combine(
                usersRoot,
                AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            WriteCharacter(CharacterPath, route, battleFieldPoints);
            var character = Assert.IsType<CharacterSnapshot>(
                new CharacterSnapshotLoader(usersRoot).LoadByAccountId(AccountId));
            OriginalSession = new WorldSession(
                AccountId,
                Username: "battlefield-advance-test",
                Character: character,
                PlayerItems: PlayerItemSnapshot.Empty,
                PlayerSpells: PlayerSpellSnapshot.Empty,
                PlayerProficiencies: PlayerProficiencySnapshot.Empty,
                UsersRoot: usersRoot,
                ActiveNpcId: route.NpcId);
            Connection = new ConquerTcpConnection(
                null,
                new IPEndPoint(IPAddress.Loopback, 9_959),
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

        public async Task<bool> InvokeAsync()
        {
            var session = CurrentSession;
            return await WorldPacketHandlers.TryHandleLegacyDisCityBattlefieldServicesNpcAsync(
                new WorldPacketHandlerContext(
                    Connection,
                    _log,
                    Session: session,
                    Sessions: Sessions,
                    ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13),
                session,
                _route.NpcId,
                optionId: 1,
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

        private static void WriteCharacter(string path, AdvanceRoute route, uint battleFieldPoints)
        {
            File.WriteAllText(
                path,
                "[Character]\r\n" +
                $"UID={CharacterUid}\r\n" +
                "Name=BattleFieldHero\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
                "FirstClass=15\r\nSecoundeClass=0\r\nReborn=0\r\nLevel=140\r\nExperience=0\r\n" +
                $"Map={route.SourceMapId}\r\nX={route.SourceX}\r\nY={route.SourceY}\r\n" +
                $"PMap={route.SourceMapId}\r\nPMapX={route.SourceX}\r\nPMapY={route.SourceY}\r\n" +
                "Strength=0\r\nAgility=0\r\nVitaliti=0\r\nSpirit=0\r\nAtributes=0\r\n" +
                "MinHitPoints=1\r\nMinMana=0\r\nMoney=0\r\n" +
                $"ConquerPoints={ConquerPoints}\r\n" +
                "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\n" +
                $"BattleFieldPoints={battleFieldPoints.ToString(CultureInfo.InvariantCulture)}\r\n" +
                "DynamicID=77\r\nSentinel=keep-me\r\n",
                Encoding.Latin1);
        }
    }

    private readonly record struct AdvanceRoute(
        uint NpcId,
        uint PointCost,
        long SourceMapId,
        long SourceX,
        long SourceY,
        long DestinationMapId,
        long DestinationX,
        long DestinationY,
        string InsufficientPointsText);
}
