using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task HandlerItemUsageEquipVisibleGearRefreshesNearbyPeerSpawnOnly()
    {
        const int UidField = 2;
        const int HeadField = 8;
        const int HeadSoulField = 64;

        var itemsRoot = Path.Combine(Path.GetTempPath(), "wolf-world-equip-appearance-visibility-items-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(_usersRoot);
            Directory.CreateDirectory(itemsRoot);
            WriteSnapshot(1293600, uid: 1293600, mapId: 1015, x: 830, y: 584);
            WriteSnapshot(1293601, uid: 1293601, mapId: 1015, x: 840, y: 590, name: "Nearby");
            WriteSnapshot(1293602, uid: 1293602, mapId: 1015, x: 930, y: 584, name: "Faraway");
            File.WriteAllBytes(
                Path.Combine(itemsRoot, "1293600.bin"),
                PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(
                            uid: 2001,
                            itemId: 111003,
                            position: 0,
                            purificationItemId: 800001),
                    ],
                    []));

            using var log = new StringWriter();
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var actorTicket = handoff.Issue(1293600, "actor", ConquerRuntimeDefaults.WorldServer);
            var nearbyTicket = handoff.Issue(1293601, "nearby", ConquerRuntimeDefaults.WorldServer);
            var farawayTicket = handoff.Issue(1293602, "faraway", ConquerRuntimeDefaults.WorldServer);
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot, itemsRoot), handoff, log);
            var actorSent = new List<byte[]>();
            var nearbySent = new List<byte[]>();
            var farawaySent = new List<byte[]>();
            var actorConnection = CreateSendingConnection(actorSent);
            var nearbyConnection = CreateSendingConnection(nearbySent);
            var farawayConnection = CreateSendingConnection(farawaySent);

            await handler.HandleFrameAsync(
                actorConnection,
                ReadFrame(new InternalWorldEnterRequestPacket(actorTicket.Token).ToFrameBytes()));
            await handler.HandleFrameAsync(
                nearbyConnection,
                ReadFrame(new InternalWorldEnterRequestPacket(nearbyTicket.Token).ToFrameBytes()));
            await handler.HandleFrameAsync(
                farawayConnection,
                ReadFrame(new InternalWorldEnterRequestPacket(farawayTicket.Token).ToFrameBytes()));

            var actorCountBefore = actorSent.Count;
            var nearbyCountBefore = nearbySent.Count;
            var farawayCountBefore = farawaySent.Count;
            await handler.HandleFrameAsync(
                actorConnection,
                ReadFrame(new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 1,
                    Action: MsgItemUsageAction.Equip,
                    Timestamp: 1234,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes()));

            Assert.Equal(actorCountBefore + 13, actorSent.Count);
            Assert.Equal(nearbyCountBefore + 1, nearbySent.Count);
            Assert.Equal(farawayCountBefore, farawaySent.Count);

            var sourceQuery = LegacySelectedEquipmentQueryAssertions.NeutralQuery(
                actorSent, actorCountBefore + 3, 1_293_600U);
            var equipmentRefresh = sourceQuery.Equipment;
            Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipmentRefresh.Action);
            Assert.Equal(0U, equipmentRefresh.Alternante);
            Assert.Equal(2001U, equipmentRefresh.EquipmentSlots.Head);

            Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(nearbySent[^1]), out var refreshedSpawn));
            Assert.Equal(1_293_600UL, refreshedSpawn.GetNumericField(UidField));
            Assert.Equal(111_003UL, refreshedSpawn.GetNumericField(HeadField));
            Assert.Equal(800_001UL, refreshedSpawn.GetNumericField(HeadSoulField));
        }
        finally
        {
            if (Directory.Exists(itemsRoot))
            {
                Directory.Delete(itemsRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerMsgActionQueryEquipmentRefreshesEquipmentAndVisiblePeerSpawn()
    {
        const int UidField = 2;
        const int HeadField = 8;
        const int HeadSoulField = 64;

        var itemsRoot = Path.Combine(Path.GetTempPath(), "wolf-world-query-equipment-appearance-items-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(_usersRoot);
            Directory.CreateDirectory(itemsRoot);
            WriteSnapshot(1293600, uid: 1293600, mapId: 1015, x: 830, y: 584);
            WriteSnapshot(1293601, uid: 1293601, mapId: 1015, x: 840, y: 590, name: "Nearby");
            File.WriteAllBytes(
                Path.Combine(itemsRoot, "1293600.bin"),
                PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(
                            uid: 2001,
                            itemId: 111003,
                            position: 1,
                            purificationItemId: 800001),
                    ],
                    []));

            using var log = new StringWriter();
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var actorTicket = handoff.Issue(1293600, "actor", ConquerRuntimeDefaults.WorldServer);
            var nearbyTicket = handoff.Issue(1293601, "nearby", ConquerRuntimeDefaults.WorldServer);
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot, itemsRoot), handoff, log);
            var actorSent = new List<byte[]>();
            var nearbySent = new List<byte[]>();
            var actorConnection = CreateSendingConnection(actorSent);
            var nearbyConnection = CreateSendingConnection(nearbySent);

            await handler.HandleFrameAsync(
                actorConnection,
                ReadFrame(new InternalWorldEnterRequestPacket(actorTicket.Token).ToFrameBytes()));
            await handler.HandleFrameAsync(
                nearbyConnection,
                ReadFrame(new InternalWorldEnterRequestPacket(nearbyTicket.Token).ToFrameBytes()));

            var actorCountBefore = actorSent.Count;
            var nearbyCountBefore = nearbySent.Count;
            await handler.HandleFrameAsync(
                actorConnection,
                ReadFrame(new MsgActionPacket
                {
                    ObjId = 1_293_600,
                    Type = MsgActionType.QueryEquipment,
                }.ToFrameBytes()));

            Assert.Equal(actorCountBefore + 1, actorSent.Count);
            Assert.Equal(nearbyCountBefore + 1, nearbySent.Count);

            Assert.True(LegacyShowEquipmentPacket.TryParse(ReadFrame(actorSent[^1]), out var equipmentRefresh));
            Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipmentRefresh.Action);
            Assert.Equal(0U, equipmentRefresh.Alternante);
            Assert.Equal(2001U, equipmentRefresh.EquipmentSlots.Head);

            Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(nearbySent[^1]), out var refreshedSpawn));
            Assert.Equal(1_293_600UL, refreshedSpawn.GetNumericField(UidField));
            Assert.Equal(111_003UL, refreshedSpawn.GetNumericField(HeadField));
            Assert.Equal(800_001UL, refreshedSpawn.GetNumericField(HeadSoulField));

            var output = log.ToString();
            Assert.Contains("WorldServer handled source-backed MsgAction QueryEquipment", output);
            Assert.Contains("WorldServer refreshed LegacyPlayerSpawn", output);
            Assert.DoesNotContain("WorldServer source-backed no-op MsgAction QueryEquipment", output);
        }
        finally
        {
            if (Directory.Exists(itemsRoot))
            {
                Directory.Delete(itemsRoot, recursive: true);
            }
        }
    }
}
