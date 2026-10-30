using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedNativeItemJoinTests
{
    [Fact]
    public async Task NonWardrobeItemInWh100RefusesActualAdmissionBeforeAnyDurableOrNativePrefix()
    {
        const uint meteorId = 1_088_001;
        var item = new PlayerItemSnapshotItem
        {
            Uid = Fixture.ItemUid, ItemId = meteorId, WarehouseId = 100,
            Position = 0, Durability = 5, MaximumDurability = 10, StackSize = 1,
        };
        var saved = PlayerItemSnapshot.Empty with { WardrobeItems = new[] { item } };
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = meteorId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "OwnedMeteorOutsideWardrobe";
        fields[ItemType.DurabilityFieldIndex] = "10";
        fields[ItemType.StackSizeFieldIndex] = "99";
        var row = new ItemType(checked((int)meteorId), fields[1], fields);

        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            var before = f.Files();

            await f.Login();

            Assert.False(f.Store.TryGet(f.Connection, out _));
            Assert.Null(f.Native.OrdinarySpellBinding);
            Assert.Equal(0u, f.Native.Player.UID);
            Assert.Null(f.Native.Inventory); Assert.Null(f.Native.Equipment);
            Assert.Null(f.Native.Warehouse); Assert.Null(f.Native.MyWardrobe);
            Assert.Null(f.Native.Rune); Assert.Null(f.Native.RelicFuse); Assert.Null(f.Native.GearMythsoulBag);
            byte[] success = LegacyMsgMessagePacket.AnswerOk().ToFrameBytes();
            Assert.DoesNotContain(f.Sent, frame => frame.SequenceEqual(success));
            f.CheckFiles(before);
            Assert.Equal(meteorId, item.ItemId);
            Assert.Equal(100u, item.WarehouseId);
            Assert.Equal((ushort)0, item.Position);
        }, playerItems: saved, additionalItems: new[] { row });
    }
}
