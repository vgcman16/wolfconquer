using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyInstancePacketTests
{
    private const uint FirstSourceMonsterUid = 400_000;
    private const uint LastFallbackMonsterUid = 899_999;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public void LegacyInstanceOpcodeMatchesSourceCMsgInstance()
    {
        Assert.Equal((ushort)2299, WorldPacketOpcodes.LegacyInstance);
        Assert.Equal(WorldPacketOpcodes.LegacyInstance, LegacyInstancePacket.Opcode);
    }

    [Theory]
    [InlineData(LegacyInstanceAction.Info, "0a00fb08080a18002000")]
    [InlineData(LegacyInstanceAction.Quit, "0a00fb08080b18002000")]
    public void LegacyInstanceActionPacketRoundTripsSourceShape(
        LegacyInstanceAction action,
        string expectedHex)
    {
        var packet = new LegacyInstancePacket(action);

        Assert.Equal(expectedHex, Convert.ToHexString(packet.ToFrameBytes()).ToLowerInvariant());
        Assert.True(LegacyInstancePacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));
        Assert.Equal(packet, parsed);
        Assert.Equal(action, parsed.Action);
    }

    [Fact]
    public void LegacyInstancePacketPreservesWeatherAndUserId()
    {
        var packet = new LegacyInstancePacket(LegacyInstanceAction.Info, weather: 4, userId: 1_293_600);

        Assert.Equal(
            "0c00fb08080a180420a0fa4e",
            Convert.ToHexString(packet.ToFrameBytes()).ToLowerInvariant());

        Assert.True(LegacyInstancePacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));
        Assert.Equal(LegacyInstanceAction.Info, parsed.Action);
        Assert.Equal(4U, parsed.Weather);
        Assert.Equal(1_293_600U, parsed.UserId);
    }

    [Fact]
    public void LegacyInstancePacketRejectsMissingType()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyInstance, payloadLength: 4);
        writer.Payload[0] = 0x18;
        writer.Payload[1] = 0x01;
        writer.Payload[2] = 0x20;
        writer.Payload[3] = 0x02;

        Assert.False(LegacyInstancePacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public async Task HandlerDispatchesLegacyInstanceInfoUsingD13ClientMapAndSpawnsSourceMonster()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(mapId: 1002, x: 425, y: 404);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
                    maps: [CreateMapMetadata(10_428, type: 18_685_966)]),
                Sessions: sessions),
            ReadFrame(new LegacyInstancePacket(LegacyInstanceAction.Info, weather: 7, userId: 1_293_600).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(10_429, updated!.Character.MapId);
        Assert.Equal(61, updated.Character.X);
        Assert.Equal(66, updated.Character.Y);
        Assert.Equal(1002, updated.Character.PreviousMapId);
        Assert.Equal(425, updated.Character.PreviousMapX);
        Assert.Equal(404, updated.Character.PreviousMapY);
        var dynamicId = long.Parse(updated.Character.Attributes["DynamicID"]);
        Assert.True(dynamicId > 10_428_000);
        var expectedMonsterUid = ResolveFallbackMonsterUid(dynamicId);

        Assert.Equal(9, sent.Count);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var teleport));
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal(1_293_600U, teleport.ObjId);
        Assert.Equal(10_428U, teleport.DwParam);
        Assert.Equal(10_428, teleport.DwParam3);
        Assert.Equal(61U, teleport.PositionX);
        Assert.Equal(66U, teleport.PositionY);
        Assert.Equal(0U, teleport.Timestamp);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[1]), out var merchantStartup));
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal(10_429, merchantStartup.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[2]), out var stopVending));
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(10_429U, stopVending.DwParam);
        Assert.Equal(10_429, stopVending.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var color));
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(0U, color.DwParam);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[4]), out var mapStatus));
        Assert.Equal(new LegacyMapStatusPacket(10_428, 10_428, 18_685_966), mapStatus);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[5]), out var monsterEffect));
        Assert.Equal(MsgActionType.MonsterRespawnEffect, monsterEffect.Type);
        Assert.InRange(monsterEffect.ObjId, FirstSourceMonsterUid, LastFallbackMonsterUid);
        Assert.Equal(expectedMonsterUid, monsterEffect.ObjId);
        Assert.Equal(61U, monsterEffect.PositionX);
        Assert.Equal(66U, monsterEffect.PositionY);
        Assert.True(LegacyMonsterSpawnPacket.TryParse(ReadFrame(sent[6]), out var monsterSpawn));
        Assert.Equal(expectedMonsterUid, monsterSpawn.Uid);
        Assert.Equal(3_546U, monsterSpawn.MonsterId);
        Assert.Equal("FlameDevil", monsterSpawn.Names[0]);
        Assert.Equal(171U, monsterSpawn.Mesh);
        Assert.Equal(140, monsterSpawn.Level);
        Assert.Equal(40_000_000U, monsterSpawn.Hitpoints);
        Assert.Equal(40_000_000U, monsterSpawn.MaxLife);
        Assert.Equal(61, monsterSpawn.X);
        Assert.Equal(66, monsterSpawn.Y);
        Assert.False(monsterSpawn.Boss);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[7]), out var maxLife));
        Assert.Equal(expectedMonsterUid, maxLife.Uid);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maxLife.Updates[0].Type);
        Assert.Equal(40_000_000UL, maxLife.Updates[0].Value);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[8]), out var hitpoints));
        Assert.Equal(expectedMonsterUid, hitpoints.Uid);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, hitpoints.Updates[0].Type);
        Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, hitpoints.Updates[1].Type);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyInstance", output);
        Assert.Contains("type=Info", output);
        Assert.Contains("WorldServer sent legacy GameClient.Teleport transition", output);
        Assert.Contains("map=10429 clientMap=10428", output);
        Assert.Contains("WorldServer applied LegacyInstance Info teleport", output);
        Assert.Contains("source CMsgInstance.Info random landing coordinate modeled with deterministic safe fallback", output);
        Assert.Contains("WorldServer sent LegacyInstance Info monster spawn", output);
        Assert.Contains("source AddMapMonster monsterId=3546 count=1 modeled with deterministic safe fallback coordinate", output);
        Assert.Contains("WorldServer skipped normal LegacyNpcSpawn visibility", output);
        Assert.Contains("WorldServer skipped normal LegacySobNpcSpawn visibility", output);
        Assert.Contains("WorldServer skipped normal LegacyMonsterSpawn visibility", output);
        Assert.Contains("source LegacyInstance.Info sends its custom AddMapMonster spawn separately", output);
        Assert.DoesNotContain("PARITY-GAP no LegacyNpcSpawn rows loaded", output);
        Assert.DoesNotContain("PARITY-GAP no LegacySobNpcSpawn rows loaded", output);
        Assert.DoesNotContain("PARITY-GAP no LegacyMonsterSpawn rows loaded", output);
        Assert.DoesNotContain("PARITY-GAP no visible LegacyNpcSpawn", output);
        Assert.DoesNotContain("PARITY-GAP no visible LegacySobNpcSpawn", output);
        Assert.DoesNotContain("PARITY-GAP no visible LegacyMonsterSpawn", output);
        Assert.DoesNotContain("PARITY-GAP LegacyInstance Info random coordinate and monster spawn are not modeled", output);
        Assert.DoesNotContain("PARITY-GAP LegacyInstance Info monster spawn is not modeled", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyInstance", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
    }

    [Fact]
    public async Task HandlerReplaysDuplicateLegacyInstanceInfoWithoutAllocatingAnotherDynamicInstance()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            maps: [CreateMapMetadata(10_428, type: 18_685_966)]);
        var session = CreateSession(mapId: 1002, x: 425, y: 404);
        var request = new LegacyInstancePacket(
            LegacyInstanceAction.Info,
            weather: 476,
            userId: 1_293_600).ToFrameBytes();
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(request));

        Assert.True(sessions.TryGet(connection, out var firstUpdated));
        Assert.NotNull(firstUpdated);
        var dynamicId = long.Parse(firstUpdated!.Character.Attributes["DynamicID"]);
        var firstResponse = sent.Select(frame => frame.ToArray()).ToArray();
        Assert.Equal(9, firstResponse.Length);
        sent.Clear();

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                firstUpdated,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(request));

        Assert.True(sessions.TryGet(connection, out var replayed));
        Assert.NotNull(replayed);
        Assert.Equal(dynamicId.ToString(), replayed!.Character.Attributes["DynamicID"]);
        Assert.Equal(10_429, replayed.Character.MapId);
        Assert.Equal(1002, replayed.Character.PreviousMapId);
        Assert.Equal(firstResponse.Length, sent.Count);
        for (var i = 0; i < firstResponse.Length; i++)
        {
            Assert.Equal(firstResponse[i], sent[i]);
        }

        Assert.True(LegacyMonsterSpawnPacket.TryParse(ReadFrame(sent[6]), out var replayedMonster));
        Assert.InRange(replayedMonster.Uid, FirstSourceMonsterUid, LastFallbackMonsterUid);
        Assert.Equal(ResolveFallbackMonsterUid(dynamicId), replayedMonster.Uid);
        Assert.Contains(
            $"WorldServer replayed LegacyInstance Info response session=accountId=1293600 username=HeroOne " +
            $"characterUid=1293600 characterName=HeroOne map=10429 x=61 y=66 hp=61258 money=0 cp=0 " +
            $"dynamicId={dynamicId} clientMap=10428 monsterUid={replayedMonster.Uid}",
            log.ToString());
    }

    private static uint ResolveFallbackMonsterUid(long dynamicId)
    {
        const uint fallbackMonsterUidSpan = 500_000;
        return FirstSourceMonsterUid +
            checked((uint)(checked((ulong)dynamicId) % fallbackMonsterUidSpan));
    }

    [Fact]
    public async Task SetLocationFromPersistedLogicalInstanceUsesD13ClientMap()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 10_429,
            x: 61,
            y: 66,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DynamicID"] = "10428001",
            });
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            maps: [CreateMapMetadata(10_428, type: 18_685_966)]);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, WorldData: worldData),
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_293_600,
                Type = MsgActionType.SetLocation,
            }.ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var setLocation));
        Assert.Equal(MsgActionType.SetLocation, setLocation.Type);
        Assert.Equal(10_428U, setLocation.DwParam);
        Assert.Equal(61U, setLocation.PositionX);
        Assert.Equal(66U, setLocation.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[1]), out var mapStatus));
        Assert.Equal(new LegacyMapStatusPacket(10_428, 10_428, 18_685_966), mapStatus);
        Assert.Contains("WorldServer sent LegacyMapStatus", log.ToString());
    }

    [Fact]
    public async Task HandlerLogsUnsupportedLegacyInstanceType()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(connection, log),
            ReadFrame(new LegacyInstancePacket(12).ToFrameBytes()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyInstance session=none type=12 typeId=12", output);
        Assert.Contains("PARITY-GAP unmodeled LegacyInstance session=none type=12; source logs unknown CMsgInstance type; source no-op fallback in this lane.", output);
        Assert.DoesNotContain("PARITY-GAP LegacyInstance Info random coordinate and monster spawn are not modeled", output);
        Assert.DoesNotContain("PARITY-GAP LegacyInstance Info monster spawn is not modeled", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyInstanceQuitAndReturnsToTwinCity()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            mapId: 10_429,
            x: 61,
            y: 66,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DynamicID"] = "10428001",
            });
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, Sessions: sessions),
            ReadFrame(new LegacyInstancePacket(LegacyInstanceAction.Quit).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_002, updated!.Character.MapId);
        Assert.Equal(410, updated.Character.X);
        Assert.Equal(354, updated.Character.Y);
        Assert.Equal(10_429, updated.Character.PreviousMapId);
        Assert.Equal(61, updated.Character.PreviousMapX);
        Assert.Equal(66, updated.Character.PreviousMapY);
        Assert.False(updated.Character.Attributes.ContainsKey("DynamicID"));

        Assert.Equal(5, sent.Count);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var teleport));
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal(1_293_600U, teleport.ObjId);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(1_002, teleport.DwParam3);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(354U, teleport.PositionY);
        Assert.Equal(0U, teleport.Timestamp);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[1]), out var merchantStartup));
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal(1_002, merchantStartup.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[2]), out var stopVending));
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(1_002U, stopVending.DwParam);
        Assert.Equal(1_002, stopVending.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var color));
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(0U, color.DwParam);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[4]), out var mapStatus));
        Assert.Equal(1_002U, mapStatus.Id);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyInstance", output);
        Assert.Contains("type=Quit", output);
        Assert.Contains("WorldServer sent legacy GameClient.Teleport transition", output);
        Assert.Contains("WorldServer applied LegacyInstance Quit teleport", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyInstanceWithoutSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(connection, log),
            ReadFrame(new LegacyInstancePacket(LegacyInstanceAction.Info).ToFrameBytes()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyInstance session=none type=Info", output);
        Assert.Contains("WorldServer skipped LegacyInstance session=none type=Info; no character session is available.", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyInstance()
    {
        using var log = new StringWriter();
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyInstance, payloadLength: 0);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(CreateSendingConnection([]), log),
            ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyInstance packet session=none.", output);
        Assert.Contains("opcode=2299 payloadLength=0", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
    }

    private static WorldSession CreateSession(
        long mapId,
        long x,
        long y,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "HeroOne",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HeroOne",
                Body: 1008,
                Face: 154,
                Hair: 27,
                Class: 7051,
                FirstClass: 0,
                SecondClass: 7050,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: x,
                Y: y,
                PreviousMapId: 1000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 61_258,
                MinMana: 5_000,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 7,
                SourceFile: "",
                Attributes: attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static MapMetadata CreateMapMetadata(int id, long type)
    {
        return new MapMetadata(
            id,
            $"{id}.ini",
            "Anima",
            MapDoc: id,
            TypeRaw: type.ToString(),
            Type: type,
            Portal0X: null,
            Portal0Y: null,
            RebornMap: 1002,
            RebornPortal: 0,
            ResLev: 0,
            LinkMap: 0,
            LinkX: 0,
            LinkY: 0,
            ColorRaw: "0",
            Color: 0,
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
