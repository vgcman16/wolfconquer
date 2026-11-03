using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedTradeEligibilityTests
{
    private const uint OfferedUid = 2002, ChildUid = 2010, Sash = 1_100_009, ChildType = 1_088_001;

    private static Task Run(PlayerItemSnapshotItem offered, WorldDataCatalog catalog, Func<Fixture, Task> body)
    {
        var actor = Fixture.Character(minHitPoints: 1000, reborn: 0) with { Money = 1000, ConquerPoints = 100 };
        var peer = Fixture.Character(Fixture.Peer, "EligibilityPeer", x: 831, minHitPoints: 1000, reborn: 0)
            with { Money = 2000, ConquerPoints = 200 };
        return Fixture.RunAsync(new(actor, new([offered], [], [], [], [])), catalog, body,
            new OwnedEquipmentBehaviorInput(peer, PlayerItemSnapshot.Empty));
    }

    private static PlayerItemSnapshotItem Root(ushort stack = 1, byte suspicious = 0,
        params PlayerItemSnapshotItem[] children) => new()
        { Uid = OfferedUid, ItemId = Sash, StackSize = stack, Suspicious = suspicious,
          Durability = 1, MaximumDurability = 1, DepositedItems = children };

    private static PlayerItemSnapshotItem Child(ushort stack = 1, byte suspicious = 0) => new()
        { Uid = ChildUid, ItemId = ChildType, StackSize = stack, Suspicious = suspicious,
          Durability = 1, MaximumDurability = 1 };

    private static WorldDataCatalog Catalog(string rootCap = "1", string childCap = "1") => Fixture.Catalog(
        Fixture.Item(Sash, fields: new Dictionary<int, string> { [ItemType.StackSizeFieldIndex] = rootCap }),
        Fixture.Item(ChildType, fields: new Dictionary<int, string> { [ItemType.StackSizeFieldIndex] = childCap }));

    private static async Task Open(Fixture f)
    {
        await Trade(f, Fixture.Actor, new(0) { UID = Fixture.Peer });
        await Trade(f, Fixture.Peer, new(1) { UID = Fixture.Actor });
        Assert.True(f.Current().LegacyTradeActive); Assert.True(f.Current(Fixture.Peer).LegacyTradeActive);
    }

    private static LegacyTradeParty Party(Fixture f)
    {
        var trades = Assert.IsType<LegacyTradeStateStore>(typeof(WorldServerFrameHandler)
            .GetField("_tradeStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
        lock (trades.SyncRoot)
        { Assert.True(trades.TryGet(f.Connection(Fixture.Actor), out var party)); return party; }
    }

    private static Task Trade(Fixture f, uint uid, LegacyTradePacket packet) => f.SendAsync(uid, packet.ToNativeFrameBytes());
    private static bool IsCompletion(byte[] bytes) => LegacyTradePacket.TryParse(Fixture.ReadFrame(bytes), out var packet) && packet.Action == 4;
}
