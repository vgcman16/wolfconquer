using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGameMapTerrainMovementTests : IDisposable
{
    private readonly string _usersRoot = Path.Combine(
        Path.GetTempPath(),
        "wolf-terrain-movement-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MovementIntoBlockedDMapCellAllowsThroughStaticTerrain()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 1, y: 1);
        using var log = new StringWriter();
        var terrain = CreateTerrain(
            1_015,
            width: 4,
            height: 4,
            [
                Cell(0), Cell(0), Cell(0), Cell(0),
                Cell(0), Cell(0), Cell(1), Cell(0),
                Cell(0), Cell(0), Cell(0), Cell(0),
                Cell(0), Cell(0), Cell(0), Cell(0),
            ]);
        var (handler, connection, sent) = await LoginAsync(terrain, worldData: null, log);

        var countBeforeMove = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgMovementPacket
            {
                Direction = 6,
                Uid = 0,
                Running = MsgMovementMode.Walk,
                Timestamp = 456,
            }.ToFrameBytes()));

        Assert.Equal(countBeforeMove + 1, sent.Count);
        AssertMovementEcho(sent[countBeforeMove], mapId: 1_015, direction: 6, running: MsgMovementMode.Walk);

        var session = GetSession(handler, connection);
        Assert.Equal(1_015, session.Character.MapId);
        Assert.Equal(2, session.Character.X);
        Assert.Equal(1, session.Character.Y);
        Assert.DoesNotContain("WorldServer rejected MsgMovement static terrain", log.ToString());
        Assert.Contains("WorldServer updated MsgMovement", log.ToString());
    }

    [Fact]
    public async Task JumpAcrossBlockedDMapCellAllowsThroughStaticTerrain()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 1, y: 1);
        using var log = new StringWriter();
        var terrain = CreateTerrain(
            1_015,
            width: 4,
            height: 4,
            [
                Cell(0), Cell(0), Cell(0), Cell(0),
                Cell(0), Cell(0), Cell(1), Cell(0),
                Cell(0), Cell(0), Cell(0), Cell(0),
                Cell(0), Cell(0), Cell(0), Cell(0),
            ]);
        var (handler, connection, sent) = await LoginAsync(terrain, worldData: null, log);

        var countBeforeJump = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_293_600,
                TargetPositionX = 3,
                TargetPositionY = 1,
                Timestamp = 789,
                Type = MsgActionType.Jump,
                Facing = 6,
            }.ToFrameBytes()));

        Assert.Equal(countBeforeJump + 1, sent.Count);
        AssertJumpEcho(sent[countBeforeJump], mapId: 1_015, x: 3, y: 1);

        var session = GetSession(handler, connection);
        Assert.Equal(1_015, session.Character.MapId);
        Assert.Equal(3, session.Character.X);
        Assert.Equal(1, session.Character.Y);
        Assert.DoesNotContain("WorldServer rejected MsgAction Jump static terrain", log.ToString());
        Assert.Contains("WorldServer handled MsgAction Jump", log.ToString());
    }

    [Theory]
    [InlineData(18, 1)]
    [InlineData(1, 18)]
    public async Task JumpWithDeltaAtLeastSeventeenSendsSetLocationPullbackAndDoesNotMove(uint targetX, uint targetY)
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 1, y: 1);
        using var log = new StringWriter();
        var terrain = CreateFlatTerrain(1_015, width: 20, height: 20);
        var (handler, connection, sent) = await LoginAsync(terrain, worldData: null, log);

        var countBeforeJump = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_293_600,
                TargetPositionX = targetX,
                TargetPositionY = targetY,
                Timestamp = 789,
                Type = MsgActionType.Jump,
                Facing = 6,
            }.ToFrameBytes()));

        Assert.Equal(countBeforeJump + 1, sent.Count);
        AssertSetLocationPullback(sent[countBeforeJump], mapId: 1_015, x: 1, y: 1);

        var session = GetSession(handler, connection);
        Assert.Equal(1_015, session.Character.MapId);
        Assert.Equal(1, session.Character.X);
        Assert.Equal(1, session.Character.Y);
        Assert.Contains("WorldServer rejected MsgAction Jump", log.ToString());
        Assert.Contains("legacy jump delta must be less than 17 on both axes", log.ToString());
        Assert.DoesNotContain("WorldServer handled MsgAction Jump", log.ToString());
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(65_535, 1, 6)]
    [InlineData(1, 0, 4)]
    [InlineData(1, 65_535, 0)]
    public async Task MovementOutsideLegacyCoordinateRangeSkipsStateUpdateAndEcho(long startX, long startY, uint direction)
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: startX, y: startY);
        using var log = new StringWriter();
        var terrain = CreateFlatTerrain(1_015, width: 1, height: 1);
        var (handler, connection, sent) = await LoginAsync(terrain, worldData: null, log);

        var countBeforeMove = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgMovementPacket
            {
                Direction = direction,
                Uid = 0,
                Running = MsgMovementMode.Walk,
                Timestamp = 456,
            }.ToFrameBytes()));

        Assert.Equal(countBeforeMove, sent.Count);

        var session = GetSession(handler, connection);
        Assert.Equal(1_015, session.Character.MapId);
        Assert.Equal(startX, session.Character.X);
        Assert.Equal(startY, session.Character.Y);
        Assert.Contains("WorldServer handled MsgMovement", log.ToString());
        Assert.Contains("movement target is outside legacy coordinate range", log.ToString());
        Assert.DoesNotContain("WorldServer updated MsgMovement", log.ToString());
    }

    [Fact]
    public async Task ChangeMapIntoBlockedDMapCellTeleportsThroughLegacyPortal()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_000, x: 52, y: 50);
        using var log = new StringWriter();
        var terrain = CreateTerrain(
            9_999,
            width: 4,
            height: 4,
            [
                Cell(0), Cell(0), Cell(0), Cell(0),
                Cell(0), Cell(0), Cell(1), Cell(0),
                Cell(0), Cell(0), Cell(0), Cell(0),
                Cell(0), Cell(0), Cell(0), Cell(0),
            ]);
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            portals: [new Portal(1_000, 50, 50, 9_999, 2, 1)],
            maps:
            [
                CreateMapMetadata(1_000),
                CreateMapMetadata(9_999),
            ]);
        var (handler, connection, sent) = await LoginAsync(terrain, worldData, log);

        var countBeforeChangeMap = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_293_600,
                Timestamp = 4321,
                Type = MsgActionType.ChangeMap,
            }.ToFrameBytes()));

        Assert.Equal(countBeforeChangeMap + 6, sent.Count);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[countBeforeChangeMap]), out var teleport));
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal(1_293_600U, teleport.ObjId);
        Assert.Equal(9_999U, teleport.DwParam);
        Assert.Equal(9_999U, teleport.DwParam3);
        Assert.Equal(4_321U, teleport.Timestamp);
        Assert.Equal(2U, teleport.PositionX);
        Assert.Equal(1U, teleport.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[countBeforeChangeMap + 1]), out var merchantStartup));
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal(9_999U, merchantStartup.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[countBeforeChangeMap + 2]), out var stopVending));
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(9_999U, stopVending.DwParam);
        Assert.Equal(9_999U, stopVending.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[countBeforeChangeMap + 3]), out var color));
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(0U, color.DwParam);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[countBeforeChangeMap + 4]), out var mapStatus));
        Assert.Equal(new LegacyMapStatusPacket(9_999, 9_999, Status: 0), mapStatus);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[countBeforeChangeMap + 5]), out var changeMapAck));
        Assert.Equal(MsgActionType.ChangeMap, changeMapAck.Type);
        Assert.Equal(1_293_600U, changeMapAck.ObjId);

        var session = GetSession(handler, connection);
        Assert.Equal(9_999, session.Character.MapId);
        Assert.Equal(2, session.Character.X);
        Assert.Equal(1, session.Character.Y);
        Assert.Equal(1_000, session.Character.PreviousMapId);
        Assert.Equal(52, session.Character.PreviousMapX);
        Assert.Equal(50, session.Character.PreviousMapY);
        Assert.Contains("WorldServer handled MsgAction ChangeMap", log.ToString());
        Assert.DoesNotContain("WorldServer rejected MsgAction ChangeMap static terrain", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)> LoginAsync(
        LegacyGameMapTerrainQuery terrain,
        WorldDataCatalog? worldData,
        TextWriter log)
    {
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            arenaStatistics: null,
            teamArenaStatistics: null,
            worldData: worldData,
            runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled,
            log: log,
            terrain: terrain);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        return (handler, connection, sent);
    }

    private static void AssertSetLocationPullback(byte[] bytes, uint mapId, uint x, uint y)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(bytes), out var action));
        Assert.Equal(MsgActionType.SetLocation, action.Type);
        Assert.Equal(1_293_600U, action.ObjId);
        Assert.Equal(mapId, action.DwParam);
        Assert.Equal(0U, action.Timestamp);
        Assert.Equal(x, action.PositionX);
        Assert.Equal(y, action.PositionY);
    }

    private static void AssertMovementEcho(byte[] bytes, uint mapId, uint direction, MsgMovementMode running)
    {
        Assert.True(MsgMovementPacket.TryParse(ReadFrame(bytes), out var movement));
        Assert.Equal(direction, movement.Direction);
        Assert.Equal(1_293_600U, movement.Uid);
        Assert.Equal(running, movement.Running);
        Assert.Equal(mapId, movement.MapId);
    }

    private static void AssertJumpEcho(byte[] bytes, uint mapId, uint x, uint y)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(bytes), out var action));
        Assert.Equal(MsgActionType.Jump, action.Type);
        Assert.Equal(1_293_600U, action.ObjId);
        Assert.Equal(x, action.PositionX);
        Assert.Equal(y, action.PositionY);
        Assert.Equal(x, action.TargetPositionX);
        Assert.Equal(y, action.TargetPositionY);
        Assert.Equal(mapId, action.MapId);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static WorldSession GetSession(WorldServerFrameHandler handler, ConquerTcpConnection connection)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var sessions = Assert.IsType<WorldSessionStore>(field.GetValue(handler));
        Assert.True(sessions.TryGet(connection, out var session));
        Assert.NotNull(session);
        return session;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static LegacyGameMapTerrainQuery CreateTerrain(
        int mapId,
        int width,
        int height,
        IReadOnlyList<TestDMapCell> cells)
    {
        var terrain = LegacyGameMapParser.ParseDMap(
            BuildDMap(width, height, cells),
            mapId,
            $"map{mapId}.dmap");

        return new LegacyGameMapTerrainQuery([terrain]);
    }

    private static LegacyGameMapTerrainQuery CreateFlatTerrain(int mapId, int width, int height)
    {
        return CreateTerrain(mapId, width, height, Enumerable.Repeat(Cell(0), width * height).ToArray());
    }

    private static TestDMapCell Cell(short terrainFlag)
    {
        return new TestDMapCell(terrainFlag, 0, 0);
    }

    private static byte[] BuildDMap(int width, int height, IReadOnlyList<TestDMapCell> cells)
    {
        Assert.Equal(width * height, cells.Count);

        var bytes = new byte[LegacyGameMapParser.DMapHeaderLength + (2 * sizeof(int)) + (height * ((width * 6) + sizeof(int)))];
        var offset = LegacyGameMapParser.DMapHeaderLength;
        WriteInt32(bytes, ref offset, width);
        WriteInt32(bytes, ref offset, height);

        var cellIndex = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cell = cells[cellIndex++];
                WriteInt16(bytes, ref offset, cell.TerrainFlag);
                WriteUInt16(bytes, ref offset, cell.Surface);
                WriteInt16(bytes, ref offset, cell.Altitude);
            }

            WriteInt32(bytes, ref offset, y + 1);
        }

        return bytes;
    }

    private static void WriteInt32(byte[] bytes, ref int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)), value);
        offset += sizeof(int);
    }

    private static void WriteInt16(byte[] bytes, ref int offset, short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset, sizeof(short)), value);
        offset += sizeof(short);
    }

    private static void WriteUInt16(byte[] bytes, ref int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)), value);
        offset += sizeof(ushort);
    }

    private static MapMetadata CreateMapMetadata(int mapId)
    {
        return new MapMetadata(
            mapId,
            mapId.ToString(CultureInfo.InvariantCulture) + ".ini",
            Name: null,
            MapDoc: mapId,
            TypeRaw: "0",
            Type: 0,
            Portal0X: null,
            Portal0Y: null,
            RebornMap: mapId,
            RebornPortal: 0,
            ResLev: 0,
            LinkMap: 0,
            LinkX: 0,
            LinkY: 0,
            ColorRaw: "0",
            Color: 0,
            Attributes: new Dictionary<string, string>());
    }

    private void WriteSnapshot(
        long accountId,
        long uid,
        long mapId,
        long x,
        long y)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map={{mapId}}
            X={{x}}
            Y={{y}}
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            RacePoints=0
            ChampionPoints=0
            PkPoints=0
            Online=7
            Day={{DateTime.Now.DayOfYear}}
            Achivement=0/171966688/262144/523784/939949824/524288/1342381056/3/576/0/0/524288/25167856/6817280/0/
            Activeness=0/80/2/1/10/1/22/0/0/
            Dragon=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Pheonix=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Tiger=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Turtle=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            ChiPoints=153850

            [PaidNobility]
            Rank=King
            Identifier={{uid}}
            """);
    }

    private sealed record TestDMapCell(short TerrainFlag, ushort Surface, short Altitude);
}
