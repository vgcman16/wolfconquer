using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyHundredWeaponsInfoPacketTests
{
    [Fact]
    public void LegacyHundredWeaponsInfoOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2352, WorldPacketOpcodes.LegacyHundredWeaponsInfo);
        Assert.Equal((ushort)2352, LegacyHundredWeaponsInfoPacket.Opcode);
    }

    [Fact]
    public void LegacyHundredWeaponsInfoRequestRoundTripsWithoutWeapons()
    {
        var packet = LegacyHundredWeaponsInfoPacket.Request(ownerUid: 1_293_600);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyHundredWeaponsInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyHundredWeaponsInfoAction.RequestInfo, parsed.Type);
        Assert.Equal(1_293_600U, parsed.OwnerUid);
        Assert.Empty(parsed.Weapons);
    }

    [Fact]
    public void LegacyHundredWeaponsInfoRoundTripsNestedWeaponRows()
    {
        var packet = new LegacyHundredWeaponsInfoPacket(
            LegacyHundredWeaponsInfoAction.Update,
            OwnerUid: 1_293_600,
            Weapons:
            [
                new LegacyHundredWeaponsInfoWeapon(
                    SubType: 410,
                    AppearancePosition: 1,
                    PositionFlags: 2,
                    Level: 3,
                    Progress: 4,
                    Hitpoints: 10_001,
                    PhysicalAttack: 20_002,
                    PhysicalDefense: 30_003,
                    MagicAttack: 40_004,
                    MagicDefense: 50_005,
                    Score: 6),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyHundredWeaponsInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.Type, parsed.Type);
        Assert.Equal(packet.OwnerUid, parsed.OwnerUid);
        Assert.Equal(packet.Weapons, parsed.Weapons);
    }

    [Fact]
    public async Task HandlerRepliesToSelfRequestWithEmptyLiveHundredWeaponsState()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var session = CreateSession(new Dictionary<string, string>
        {
            ["HundredWeapons"] = "0/",
        });

        await registry.HandleAsync(
            new WorldPacketHandlerContext(CreateConnection(sent), log, session),
            ReadFrame(LegacyHundredWeaponsInfoPacket.Request(ownerUid: 1_293_600).ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyHundredWeaponsInfoPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(LegacyHundredWeaponsInfoAction.RequestInfo, response.Type);
        Assert.Equal(1_293_600U, response.OwnerUid);
        Assert.Empty(response.Weapons);
        Assert.Contains("WorldServer handled LegacyHundredWeaponsInfo", log.ToString());
    }

    [Fact]
    public async Task HandlerRepliesWithSourceEmptyPacketWhenRequestedOwnerIsOffline()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(CreateConnection(sent), log, CreateSession(new Dictionary<string, string>())),
            ReadFrame(LegacyHundredWeaponsInfoPacket.Request(ownerUid: 999).ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyHundredWeaponsInfoPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(LegacyHundredWeaponsInfoAction.RequestInfo, response.Type);
        Assert.Equal(0U, response.OwnerUid);
        Assert.Empty(response.Weapons);
    }

    private static WorldSession CreateSession(IReadOnlyDictionary<string, string> attributes)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "HeroOne",
            CreateSnapshot(attributes),
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateSnapshot(IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
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
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
