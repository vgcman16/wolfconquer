using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData("crop", 65_536U, 1_000U, 500U)]
    [InlineData("crop", 2_147_483_647U, 1_000U, 500U)]
    [InlineData("crop", 2_147_483_649U, 1U, 500U)]
    [InlineData("crop", 4_294_967_295U, 65_535U, 500U)]
    [InlineData("crop-then-steed", 65_536U, 50U, 600U)]
    [InlineData("steed-then-crop", 65_536U, 1_050U, 600U)]
    [InlineData("crop-then-unplussed-steed", 65_536U, 1_000U, 500U)]
    [InlineData("broken-crop", 500U, 0U, 500U)]
    public async Task DirectQueryVigorUsesSourceSignedIntClampAndOrderedActualCropSteedContributions(
        string scenario, uint incomingVigor, uint expectedVigor, uint expectedMaximum)
    {
        using var fixture = new DirectQueryVigorFixture(scenario, incomingVigor);
        Assert.Equal(incomingVigor.ToString(CultureInfo.InvariantCulture),
            fixture.Owner.Character.Attributes["Vigor"]);
        Assert.Null(fixture.Owner.EquipmentHitpointsStatus);
        Assert.Null(fixture.Owner.PhysicalStatusOverlay);
        await fixture.StartupAsync();
        var frames = fixture.Frames.ToArray();
        Assert.Equal(12, frames.Length); // Startup talk, response, then exact neutral query.
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(frames[0]), out _));
        Assert.True(LegacyRuneStoragePacket.TryParse(ReadFrame(frames[1]), out var response));
        Assert.Equal(LegacyRuneStoragePacket.StartupType, response.Type);
        Assert.Equal(0U, response.ItemUID);
        Assert.Equal(0U, response.HPAdd);
        var query = LegacySelectedEquipmentQueryAssertions.NeutralQuery(frames, 2, PlayerUid);
        Assert.Equal(expectedMaximum, query.Status.MaxHP);
        Assert.True(LegacyServerInfoPacket.TryParse(ReadFrame(frames[3]), out var serverInfo));
        Assert.Equal(expectedVigor, serverInfo.Vigor);
        Assert.Equal(new[] { expectedVigor }, fixture.VigorAtPublication.ToArray());
        Assert.Equal(expectedVigor.ToString(CultureInfo.InvariantCulture),
            fixture.Owner.Character.Attributes["Vigor"]);
        Assert.Equal(300, fixture.Owner.Character.MinHitPoints);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, expectedMaximum);
        Assert.Equal(0, fixture.Clock.Reads);
        Assert.Equal(0, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRain1StatusFlag));
        Assert.DoesNotContain(frames, bytes => LegacyPlayerSpawnPacket.TryParse(ReadFrame(bytes), out _));
    }

    private sealed class DirectQueryVigorFixture : IDisposable
    {
        private const uint Helmet = 111_098, Crop = 203_003, Steed = 300_000;
        private readonly WorldSessionStore _sessions = new();
        private readonly StringWriter _log = new();
        private readonly WorldPacketHandlerContext _context;
        private readonly ConquerTcpConnection _connection;

        public DirectQueryVigorFixture(string scenario, uint vigor)
        {
            var helmet = DirectOverlayItem(Helmet, "QueryVigorHelmet");
            var fields = helmet.Fields.ToArray();
            fields[ItemType.ItemHPFieldIndex] = "500";
            helmet = helmet with { Fields = fields };
            // Literal supplied DBServer/ItemAdd.ini5041, not a query-calculator oracle:
            // 300000 1 100 20 20 0 20 0 50 5. ITPlus.Agility is Values[6]=50.
            var steedPlus = new ItemAddBonus(300_000, 1,
                [100, 20, 20, 0, 20, 0, 50, 5], SourceLine: 5_041,
                RawLine: "300000 1 100 20 20 0 20 0 50 5");
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], [],
                items: [helmet, DirectOverlayItem(Crop, "RidingCrop"), DirectOverlayItem(Steed, "Steed")],
                itemAddBonuses: [steedPlus]);
            PlayerItemSnapshotItem Item(uint uid, uint id, ushort position, byte plus = 0, ushort durability = 100) => new()
            {
                Uid = uid, ItemId = id, Position = position, Plus = plus,
                Durability = durability, MaximumDurability = 100,
            };
            var crop = Item(97_802, Crop, 18, durability: scenario == "broken-crop" ? (ushort)0 : (ushort)100);
            var steed = Item(97_803, Steed, 12, plus: scenario == "crop-then-unplussed-steed" ? (byte)0 : (byte)1);
            PlayerItemSnapshotItem[] equipment = scenario switch
            {
                "crop-then-steed" or "crop-then-unplussed-steed" => [Item(97_801, Helmet, 1), crop, steed],
                "steed-then-crop" => [Item(97_801, Helmet, 1), steed, crop],
                _ => [Item(97_801, Helmet, 1), crop],
            };
            var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2,
                strength: 0, spirit: 0, minHitPoints: 300, spellIds: [],
                playerItems: PlayerItemSnapshot.Empty with { EquipmentItems = equipment },
                attributes: new Dictionary<string, string> { ["Vigor"] = vigor.ToString(CultureInfo.InvariantCulture) });
            Assert.Equal("500", world.ItemsById[(int)Helmet][0].Fields[18]);
            Assert.Equal(new[] { 100, 20, 20, 0, 20, 0, 50, 5 },
                world.ItemAddBonusByItemTypeIdAndLevel[(300_000, 1)].Values);
            Assert.Equal((ushort)18, crop.Position);
            Assert.Equal((ushort)12, steed.Position);
            Assert.Equal((uint)300_000, steed.ItemId);
            Assert.False(owner.Character.Attributes.ContainsKey("MaxHitpoints"));
            _connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (memory, token) =>
                {
                    var bytes = memory.ToArray();
                    Frames.Enqueue(bytes);
                    if (LegacyServerInfoPacket.TryParse(ReadFrame(bytes), out var packet))
                    {
                        Assert.Equal(packet.Vigor.ToString(CultureInfo.InvariantCulture), Owner.Character.Attributes["Vigor"]);
                        Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(Owner, out var maximum));
                        Assert.Equal(scenario is "crop-then-steed" or "steed-then-crop" ? 600U : 500U, maximum);
                        Assert.Equal(300, Owner.Character.MinHitPoints); // Prefix, before neutral AddRune/clamp pairs.
                        VigorAtPublication.Enqueue(packet.Vigor);
                    }
                    return ValueTask.CompletedTask;
                });
            _sessions.Set(_connection, owner);
            _context = new WorldPacketHandlerContext(_connection, TextWriter.Synchronized(_log), owner,
                WorldData: world, Sessions: _sessions, LegacyClock: Clock,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public ConcurrentQueue<byte[]> Frames { get; } = new();
        public ConcurrentQueue<uint> VigorAtPublication { get; } = new();
        public DirectQueryVigorClock Clock { get; } = new();
        public WorldSession Owner
        {
            get
            {
                Assert.True(_sessions.TryGet(_connection, out var session));
                return Assert.IsType<WorldSession>(session);
            }
        }
        public async Task StartupAsync() => await PacketHandlers.HandleAsync(_context with { Session = Owner },
            ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));
        public void Dispose() => _log.Dispose();
    }

    private sealed class DirectQueryVigorClock : ILegacySignedMillisecondClock
    {
        public int Reads { get; private set; }
        public int GetMilliseconds() { Reads++; return 10_000; }
    }
}
