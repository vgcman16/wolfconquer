using System.Collections.Concurrent;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task DirectRuneStorageInventoryFoundButFullBagStillQueriesBeforeResponseAndAgainAfterIt()
    {
        const uint inventoryUid = 97_001, inventoryRune = 4_020_101, bagRune = 4_036_006, helmetId = 111_097;
        var helmet = DirectOverlayItem(helmetId, "RuneStorageFailureHelmet");
        var fields = helmet.Fields.ToArray();
        fields[ItemType.ItemHPFieldIndex] = "500";
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], [],
            items: [helmet with { Fields = fields }, DirectOverlayItem(inventoryRune, "FuryStrike(+1)"),
                DirectOverlayItem(bagRune, "Circle of Life (+6)")]);
        PlayerItemSnapshotItem Item(uint uid, uint id, ushort position) => new()
        {
            Uid = uid, ItemId = id, Position = position, Durability = 100, MaximumDurability = 100,
        };
        // Rune.Add source capacity counts Position211 only. No duplicate UID or
        // same-base Position212 is needed to make this real Add attempt fail.
        var items = PlayerItemSnapshot.Empty with
        {
            InventoryItems = [Item(inventoryUid, inventoryRune, 0)],
            RuneItems = Enumerable.Range(0, 100).Select(index => Item((uint)(96_000 + index), bagRune, 211)).ToArray(),
            EquipmentItems = [Item(98_001, helmetId, 1)],
        };
        var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2,
            strength: 0, spirit: 0, minHitPoints: 300, spellIds: [], playerItems: items);
        Assert.Equal(100, owner.PlayerItems.RuneItems.Count(item => item.Position == 211));
        Assert.Equal(100, owner.PlayerItems.RuneItems.Select(item => item.Uid).Distinct().Count());
        Assert.DoesNotContain(owner.PlayerItems.RuneItems, item => item.Uid == inventoryUid || item.Position == 212);
        Assert.False(owner.Character.Attributes.ContainsKey("MaxHitpoints"));
        Assert.Null(owner.EquipmentHitpointsStatus);
        Assert.Null(owner.PhysicalStatusOverlay);
        var sessions = new WorldSessionStore();
        var frames = new ConcurrentQueue<byte[]>();
        var responseObserved = 0;
        ConquerTcpConnection? connection = null;
        WorldSession Current()
        {
            Assert.True(sessions.TryGet(connection!, out var current));
            return Assert.IsType<WorldSession>(current);
        }
        connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (memory, token) =>
            {
                var bytes = memory.ToArray();
                frames.Enqueue(bytes);
                if (LegacyRuneStoragePacket.TryParse(ReadFrame(bytes), out var response) && response.Type == 3)
                {
                    Assert.Equal(1, Interlocked.Increment(ref responseObserved));
                    Assert.Equal(11, frames.Count); // Entire first ten-frame query completed BEFORE response.
                    Assert.Equal(inventoryUid, response.ItemUID);
                    Assert.Equal(0U, response.HPAdd);
                    Assert.Same(items, Current().PlayerItems); // Failed Add never accepted a new item snapshot.
                    DirectFineRainHpAssertOwnedMaximum(Current(), 500);
                    Assert.Equal(300, Current().Character.MinHitPoints);
                }
                return ValueTask.CompletedTask;
            });
        sessions.Set(connection, owner);
        using var log = new StringWriter();
        var clock = new DirectOverlayFrozenClock();
        var context = new WorldPacketHandlerContext(connection, TextWriter.Synchronized(log), owner,
            WorldData: world, Sessions: sessions, TimeProvider: clock,
            LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new LegacyRuneStoragePacket(Type: 3, ItemUID: inventoryUid, HPAdd: 0).ToFrameBytes()));
        var sent = frames.ToArray();
        Assert.Equal(21, sent.Length);
        var inner = LegacySelectedEquipmentQueryAssertions.NeutralQuery(sent, 0, PlayerUid);
        Assert.True(LegacyRuneStoragePacket.TryParse(ReadFrame(sent[10]), out var packet));
        Assert.Equal((byte)3, packet.Type);
        Assert.Equal(inventoryUid, packet.ItemUID);
        Assert.Equal(0U, packet.HPAdd);
        var tail = LegacySelectedEquipmentQueryAssertions.NeutralQuery(sent, 11, PlayerUid);
        Assert.Equal(500U, inner.Status.MaxHP);
        Assert.Equal(500U, tail.Status.MaxHP);
        Assert.Equal(0U, inner.Status.RuneScore); // Bag211 is excluded from source Rune.Score.
        Assert.Equal(0U, tail.Status.RuneScore);
        Assert.Equal(1, responseObserved);
        Assert.Equal(2, sent.Count(bytes => LegacyServerInfoPacket.TryParse(ReadFrame(bytes), out _)));
        Assert.Same(items, Current().PlayerItems);
        Assert.Equal(100, Current().PlayerItems.RuneItems.Count);
        Assert.Equal(inventoryUid, Assert.Single(Current().PlayerItems.InventoryItems).Uid);
        Assert.DoesNotContain(Current().PlayerItems.RuneItems, item => item.Position == 212);
        Assert.Equal(300, Current().Character.MinHitPoints);
        DirectFineRainHpAssertOwnedMaximum(Current(), 500);
        Assert.Equal(0, LegacyFineRainEquipmentAdmission.ForOwner(Current()).StampMilliseconds);
        Assert.False(HasLegacyStatusFlag(Current().Character.Attributes, FineRain1StatusFlag));
    }
}
