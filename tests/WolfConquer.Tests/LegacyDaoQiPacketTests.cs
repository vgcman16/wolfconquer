using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDaoQiPacketTests
{
    [Fact]
    public void LegacyDaoQiOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2451, WorldPacketOpcodes.LegacyDaoQi);
        Assert.Equal(WorldPacketOpcodes.LegacyDaoQi, LegacyDaoQiPacket.Opcode);
    }

    [Fact]
    public void LegacyDaoQiInfoRoundTripsLegacyProtobufShape()
    {
        var packet = LegacyDaoQiPacket.Info(
            taoistReiki: 12_345,
            lottery: 2,
            masteryPoints: 777,
            pirateExp: 5_555);

        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(LegacyDaoQiPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0x00,
            0x10, 0x00,
            0x18, 0xB9, 0x60,
            0x20, 0x02,
            0x28, 0x06,
            0x30, 0x09,
            0x38, 0x89, 0x06,
            0x40, 0x01,
            0x48, 0xB3, 0x2B,
            0x50, 0x03,
            0x58, 0x00,
            0x60, 0x00,
        ], frame.Payload.ToArray());
        Assert.True(LegacyDaoQiPacket.TryParse(frame, out var parsed));
        AssertPacketEqual(packet, parsed);
    }

    [Fact]
    public void LegacyDaoQiLoadItemRoundTripsItems()
    {
        var packet = new LegacyDaoQiPacket(
            LegacyDaoQiType.LoadItem,
            ItemId: 0,
            TaoistReiki: 0,
            Lottery: 0,
            Data5: 0,
            Data6: 0,
            MasteryPoints: 0,
            Data8: 0,
            PirateExp: 0,
            Data10: 0,
            Position: 0,
            Done: 0,
            Items:
            [
                new LegacyDaoQiItem(113),
                new LegacyDaoQiItem(16_907),
            ],
            Positions: []);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyDaoQiPacket.TryParse(frame, out var parsed));
        AssertPacketEqual(packet, parsed);
    }

    [Fact]
    public void LegacyDaoQiTaoistReikiUpdateRoundTripsSourceShape()
    {
        var packet = LegacyDaoQiPacket.TaoistReikiUpdate(12_345);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyDaoQiPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyDaoQiType.TaoistReiki, parsed.Type);
        Assert.Equal(12_345U, parsed.TaoistReiki);
        Assert.Equal(0U, parsed.ItemId);
        Assert.Equal(0U, parsed.Done);
        Assert.Empty(parsed.Items);
        Assert.Empty(parsed.Positions);
    }

    [Fact]
    public void LegacyDaoQiPiratePointsUpdateRoundTripsSourceShape()
    {
        var packet = LegacyDaoQiPacket.PiratePointsUpdate(678);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyDaoQiPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyDaoQiType.PiratePoints, parsed.Type);
        Assert.Equal(678U, parsed.PirateExp);
        Assert.Equal(0U, parsed.ItemId);
        Assert.Equal(0U, parsed.Done);
        Assert.Empty(parsed.Items);
        Assert.Empty(parsed.Positions);
    }

    [Fact]
    public void LegacyDaoQiRejectsMalformedUnknownAndInvalidFields()
    {
        var missingType = new PacketWriter(LegacyDaoQiPacket.Opcode, payloadLength: 2);
        missingType.Payload[0] = 0x10;
        missingType.Payload[1] = 0x00;
        Assert.False(LegacyDaoQiPacket.TryParse(ReadFrame(missingType.ToArray()), out _));

        var malformed = new PacketWriter(LegacyDaoQiPacket.Opcode, payloadLength: 1);
        malformed.Payload[0] = 0x80;
        Assert.False(LegacyDaoQiPacket.TryParse(ReadFrame(malformed.ToArray()), out _));

        var unknown = new PacketWriter(LegacyDaoQiPacket.Opcode, payloadLength: 4);
        unknown.Payload[0] = 0x08;
        unknown.Payload[1] = 0x00;
        unknown.Payload[2] = 0x78;
        unknown.Payload[3] = 0x01;
        Assert.False(LegacyDaoQiPacket.TryParse(ReadFrame(unknown.ToArray()), out _));

        var invalidType = new PacketWriter(LegacyDaoQiPacket.Opcode, payloadLength: 2);
        invalidType.Payload[0] = 0x08;
        invalidType.Payload[1] = 0x63;
        Assert.False(LegacyDaoQiPacket.TryParse(ReadFrame(invalidType.ToArray()), out _));
    }

    [Fact]
    public void LegacyDaoQiRejectsMalformedNestedItem()
    {
        var writer = new PacketWriter(LegacyDaoQiPacket.Opcode, payloadLength: 4);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x01;
        writer.Payload[2] = 0x6A;
        writer.Payload[3] = 0x00;

        Assert.False(LegacyDaoQiPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public async Task HandlerTaoistReikiSourceNoOpsWithoutDeferredLog()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(CreateCombatGear(
            BlackJades: [16_907, 1_907, 19_907, 14_907, 3_907, 2_907],
            Bag: [18_907, 8_907],
            TaoistReiki: 45,
            MasteryPoints: 7,
            PirateExp: 90));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var request = new LegacyDaoQiPacket(
            LegacyDaoQiType.TaoistReiki,
            ItemId: 0,
            TaoistReiki: 999,
            Lottery: 0,
            Data5: 0,
            Data6: 0,
            MasteryPoints: 0,
            Data8: 0,
            PirateExp: 0,
            Data10: 0,
            Position: 3,
            Done: 0,
            Items: [],
            Positions: []);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var storedSession));
        Assert.NotNull(storedSession);
        Assert.Equal(45U, storedSession!.CombatGear!.TaoistReiki);
        Assert.Equal(7U, storedSession.CombatGear.MasteryPoints);
        Assert.Equal(90U, storedSession.CombatGear.PirateExp);
        Assert.Contains("WorldServer handled LegacyDaoQi action", log.ToString());
        Assert.Contains("type=TaoistReiki", log.ToString());
        Assert.Contains("responses=0", log.ToString());
        Assert.DoesNotContain("deferred LegacyDaoQi action", log.ToString());
    }

    [Fact]
    public async Task HandlerStillDefersUnsupportedPirateItemAction()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(CreateCombatGear(
            BlackJades: [16_907, 1_907, 19_907, 14_907, 3_907, 2_907],
            Bag: [18_907, 8_907],
            TaoistReiki: 45));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var request = new LegacyDaoQiPacket(
            LegacyDaoQiType.PirateItem,
            ItemId: 18_907,
            TaoistReiki: 0,
            Lottery: 0,
            Data5: 0,
            Data6: 0,
            MasteryPoints: 0,
            Data8: 0,
            PirateExp: 0,
            Data10: 0,
            Position: 0,
            Done: 0,
            Items: [],
            Positions: []);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        Assert.Empty(sent);
        Assert.Contains("WorldServer deferred LegacyDaoQi action", log.ToString());
        Assert.Contains("type=PirateItem", log.ToString());
    }

    [Fact]
    public async Task HandlerBuyAddsTaoistReikiAndEchoesDone()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(CreateCombatGear(
            BlackJades: [16_907, 1_907, 19_907, 14_907, 3_907, 2_907],
            Bag: [18_907, 8_907],
            TaoistReiki: 45));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var request = new LegacyDaoQiPacket(
            LegacyDaoQiType.Buy,
            ItemId: 0,
            TaoistReiki: 0,
            Lottery: 0,
            Data5: 0,
            Data6: 0,
            MasteryPoints: 0,
            Data8: 0,
            PirateExp: 0,
            Data10: 0,
            Position: 2,
            Done: 0,
            Items: [],
            Positions: []);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        Assert.True(LegacyDaoQiPacket.TryParse(ReadFrame(sent[0]), out var reikiResponse));
        Assert.Equal(LegacyDaoQiType.TaoistReiki, reikiResponse.Type);
        Assert.Equal(75U, reikiResponse.TaoistReiki);

        Assert.True(LegacyDaoQiPacket.TryParse(ReadFrame(sent[1]), out var buyResponse));
        Assert.Equal(LegacyDaoQiType.Buy, buyResponse.Type);
        Assert.Equal(2U, buyResponse.Position);
        Assert.Equal(1U, buyResponse.Done);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.NotNull(updatedSession!.CombatGear);
        Assert.Equal(75U, updatedSession.CombatGear!.TaoistReiki);
        Assert.Contains("WorldServer handled LegacyDaoQi action", log.ToString());
        Assert.Contains("type=Buy", log.ToString());
        Assert.DoesNotContain("deferred LegacyDaoQi action", log.ToString());
    }

    [Fact]
    public async Task HandlerDrawsFirstFreeSeedJadeUpdatesLotteryReikiAndBag()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(CreateCombatGear(
            BlackJades: [16_907, 1_907, 19_907, 14_907, 3_907, 2_907],
            Bag: [],
            TaoistReiki: 0,
            Lottery: 12));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var request = new LegacyDaoQiPacket(
            LegacyDaoQiType.Draws,
            ItemId: 0,
            TaoistReiki: 0,
            Lottery: 0,
            Data5: 0,
            Data6: 0,
            MasteryPoints: 0,
            Data8: 0,
            PirateExp: 0,
            Data10: 0,
            Position: 0,
            Done: 0,
            Items: [],
            Positions: []);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        Assert.Equal(3, sent.Count);
        Assert.True(LegacyDaoQiPacket.TryParse(ReadFrame(sent[0]), out var lotteryResponse));
        Assert.Equal(LegacyDaoQiType.Lottery, lotteryResponse.Type);
        Assert.Equal(14U, lotteryResponse.Lottery);

        Assert.True(LegacyDaoQiPacket.TryParse(ReadFrame(sent[1]), out var reikiResponse));
        Assert.Equal(LegacyDaoQiType.TaoistReiki, reikiResponse.Type);
        Assert.Equal(0U, reikiResponse.TaoistReiki);

        Assert.True(LegacyDaoQiPacket.TryParse(ReadFrame(sent[2]), out var drawsResponse));
        Assert.Equal(LegacyDaoQiType.Draws, drawsResponse.Type);
        Assert.Equal(0U, drawsResponse.Position);
        Assert.Equal([113U], drawsResponse.Items.Select(item => item.Id));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.NotNull(updatedSession!.CombatGear);
        Assert.Equal(14U, updatedSession.CombatGear!.Lottery);
        Assert.Equal(0U, updatedSession.CombatGear.TaoistReiki);
        Assert.Equal([113U], updatedSession.CombatGear.BlackJadeBag.BlackJades);
        Assert.Contains("WorldServer handled LegacyDaoQi action", log.ToString());
        Assert.Contains("type=Draws", log.ToString());
        Assert.DoesNotContain("deferred LegacyDaoQi action", log.ToString());
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerPirateUpgradeConsumesPirateExpAndUpgradesBag()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        Assert.True(worldData.DaoqiDictTypesById.TryGetValue(1_007, out var sourceJade));
        Assert.Equal(500, sourceJade.Values[8]);

        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(CreateCombatGear(
            BlackJades: [16_907, 1_907, 19_907, 14_907, 3_907, 2_907],
            Bag: [1_007],
            PirateExp: 600));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var request = new LegacyDaoQiPacket(
            LegacyDaoQiType.PirateUpgrade,
            ItemId: 1_007,
            TaoistReiki: 0,
            Lottery: 0,
            Data5: 0,
            Data6: 0,
            MasteryPoints: 0,
            Data8: 0,
            PirateExp: 0,
            Data10: 0,
            Position: 0,
            Done: 0,
            Items: [],
            Positions: []);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, WorldData: worldData, Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        Assert.True(LegacyDaoQiPacket.TryParse(ReadFrame(sent[0]), out var piratePoints));
        Assert.Equal(LegacyDaoQiType.PiratePoints, piratePoints.Type);
        Assert.Equal(100U, piratePoints.PirateExp);

        Assert.True(LegacyDaoQiPacket.TryParse(ReadFrame(sent[1]), out var upgradeResponse));
        Assert.Equal(LegacyDaoQiType.PirateUpgrade, upgradeResponse.Type);
        Assert.Equal(1_007U, upgradeResponse.ItemId);
        Assert.Equal(1_107U, upgradeResponse.PirateExp);
        Assert.Equal(1U, upgradeResponse.Position);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.NotNull(updatedSession!.CombatGear);
        Assert.Equal(100U, updatedSession.CombatGear!.PirateExp);
        Assert.Equal([1_107U], updatedSession.CombatGear.BlackJadeBag.BlackJades);
        Assert.Contains("WorldServer handled LegacyDaoQi action", log.ToString());
        Assert.Contains("type=PirateUpgrade", log.ToString());
        Assert.DoesNotContain("deferred LegacyDaoQi action", log.ToString());
    }

    [Theory]
    [InlineData(5U, 18_907U)]
    [InlineData(6U, 8_907U)]
    public async Task HandlerEquipBlackJadeEchoesDoneAndUpdatesCombatGearSession(uint position, uint blackJadeId)
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(CreateCombatGear(
            BlackJades: [16_907, 1_907, 19_907, 14_907, 3_907, 2_907],
            Bag: [18_907, 8_907]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var request = new LegacyDaoQiPacket(
            LegacyDaoQiType.Equip,
            ItemId: 701,
            TaoistReiki: 0,
            Lottery: 0,
            Data5: 0,
            Data6: 0,
            MasteryPoints: 0,
            Data8: 0,
            PirateExp: 0,
            Data10: 0,
            Position: position,
            Done: 0,
            Items: [new LegacyDaoQiItem(blackJadeId)],
            Positions: []);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyDaoQiPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(LegacyDaoQiType.Equip, response.Type);
        Assert.Equal(701U, response.ItemId);
        Assert.Equal(position, response.Position);
        Assert.Equal(1U, response.Done);
        Assert.Equal([blackJadeId], response.Items.Select(item => item.Id));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.NotNull(updatedSession!.CombatGear);
        var updatedGear = updatedSession.CombatGear!;
        var updatedItem = Assert.Single(updatedGear.Items);
        Assert.Equal(blackJadeId, updatedItem.BlackJades[(int)position - 1]);
        Assert.Contains("WorldServer handled LegacyDaoQi action", log.ToString());
        Assert.Contains("type=Equip", log.ToString());
        Assert.DoesNotContain("deferred LegacyDaoQi action", log.ToString());
    }

    [Fact]
    public async Task HandlerUnEquipAndReplaceBlackJadesFollowSourceDoneEchoes()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(CreateCombatGear(
            BlackJades: [16_907, 1_907, 19_907, 14_907, 3_907, 2_907],
            Bag: [18_907, 8_907]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, Sessions: sessions),
            ReadFrame(new LegacyDaoQiPacket(
                LegacyDaoQiType.UnEquip,
                ItemId: 701,
                TaoistReiki: 0,
                Lottery: 0,
                Data5: 0,
                Data6: 0,
                MasteryPoints: 0,
                Data8: 0,
                PirateExp: 0,
                Data10: 0,
                Position: 5,
                Done: 0,
                Items: [],
                Positions: []).ToFrameBytes()));

        var unEquipResponseBytes = Assert.Single(sent);
        Assert.True(LegacyDaoQiPacket.TryParse(ReadFrame(unEquipResponseBytes), out var unEquipResponse));
        Assert.Equal(LegacyDaoQiType.UnEquip, unEquipResponse.Type);
        Assert.Equal(1U, unEquipResponse.Done);
        Assert.True(sessions.TryGet(connection, out var afterUnEquip));
        Assert.NotNull(afterUnEquip);
        Assert.Equal(0U, Assert.Single(afterUnEquip!.CombatGear!.Items).BlackJades[4]);

        sent.Clear();
        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, afterUnEquip, Sessions: sessions),
            ReadFrame(new LegacyDaoQiPacket(
                LegacyDaoQiType.Replace,
                ItemId: 701,
                TaoistReiki: 0,
                Lottery: 0,
                Data5: 0,
                Data6: 0,
                MasteryPoints: 0,
                Data8: 0,
                PirateExp: 0,
                Data10: 0,
                Position: 0,
                Done: 0,
                Items:
                [
                    new LegacyDaoQiItem(18_907),
                    new LegacyDaoQiItem(8_907),
                ],
                Positions: []).ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        foreach (var responseBytes in sent)
        {
            Assert.True(LegacyDaoQiPacket.TryParse(ReadFrame(responseBytes), out var replaceResponse));
            Assert.Equal(LegacyDaoQiType.Replace, replaceResponse.Type);
            Assert.Equal(1U, replaceResponse.Done);
        }

        Assert.True(sessions.TryGet(connection, out var afterReplace));
        Assert.NotNull(afterReplace);
        var replaced = Assert.Single(afterReplace!.CombatGear!.Items);
        Assert.Equal(18_907U, replaced.BlackJades[0]);
        Assert.Equal(8_907U, replaced.BlackJades[1]);
        Assert.DoesNotContain("deferred LegacyDaoQi action", log.ToString());
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertPacketEqual(LegacyDaoQiPacket expected, LegacyDaoQiPacket actual)
    {
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.ItemId, actual.ItemId);
        Assert.Equal(expected.TaoistReiki, actual.TaoistReiki);
        Assert.Equal(expected.Lottery, actual.Lottery);
        Assert.Equal(expected.Data5, actual.Data5);
        Assert.Equal(expected.Data6, actual.Data6);
        Assert.Equal(expected.MasteryPoints, actual.MasteryPoints);
        Assert.Equal(expected.Data8, actual.Data8);
        Assert.Equal(expected.PirateExp, actual.PirateExp);
        Assert.Equal(expected.Data10, actual.Data10);
        Assert.Equal(expected.Position, actual.Position);
        Assert.Equal(expected.Done, actual.Done);
        Assert.Equal(expected.Items, actual.Items);
        Assert.Equal(expected.Positions, actual.Positions);
    }

    private static WorldSession CreateSession(CombatGearSnapshot combatGear)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "HeroOne",
            new CharacterSnapshot(
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
                MapId: 1015,
                X: 830,
                Y: 584,
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
                Money: 98_602_439_956,
                ConquerPoints: 639_778_904,
                BoundConquerPoints: 456,
                BankConquerPoints: 123,
                PkPoints: 0,
                Online: 7,
                SourceFile: "1293600.ini",
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            CombatGear: combatGear);
    }

    private static CombatGearSnapshot CreateCombatGear(
        IReadOnlyList<uint> BlackJades,
        IReadOnlyList<uint> Bag,
        uint TaoistReiki = 0,
        uint Lottery = 12,
        uint MasteryPoints = 0,
        uint PirateExp = 0)
    {
        return new CombatGearSnapshot(
            MasteryPoints,
            TaoistReiki,
            PirateExp,
            Lottery,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            Items:
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 4,
                    Exp: 0,
                    Equip: 0,
                    Transform: 0,
                    MasteryScore: 928_000,
                    Animas: [new uint[7], new uint[7]],
                    BlackJades),
            ],
            new CombatGearBlackJadeBagSnapshot(Bag.Count, Bag));
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate WolfConquer.sln from the test base directory.");
    }
}
