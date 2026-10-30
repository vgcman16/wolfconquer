using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedNativeItemJoinTests
{
    [Fact]
    public async Task CentralSetReturnsItsExactCanonicalImageWithoutRetainingTheProposalAsAuthority()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login();
            var source = Assert.Single(f.Current.PlayerItems.InventoryItems).WithBound(1);
            source.OwnerName = "PreparedOwner";
            var proposed = f.Current with { PlayerItems = f.Current.PlayerItems with { InventoryItems = new[] { source } } };

            f.Store.Set(f.Connection, proposed, out var final);

            Assert.Same(final, f.Current);
            Assert.Same(final.PlayerItems, RequireGraph(f).Items.Image.FinalSnapshot);
            Assert.NotSame(proposed.PlayerItems, final.PlayerItems);
            source.OwnerName = "DetachedMutation";
            Assert.Equal("PreparedOwner", Assert.Single(final.PlayerItems.InventoryItems).OwnerName);
            Assert.Equal("PreparedOwner", Assert.Single(f.Native.Inventory!.ClientItems).Value.OwnerName);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepairPublishesCanonicalStateBeforeOutputAndRefusesDriftWithoutCostOrSuccessFrames(bool corruptNative)
    {
        const uint meteorId = 1_088_001;
        var target = new PlayerItemSnapshotItem { Uid = Fixture.ItemUid, ItemId = 725016, Position = 0,
            Durability = 0, MaximumDurability = 10, StackSize = 1 };
        var meteor = new PlayerItemSnapshotItem { Uid = Fixture.ItemUid + 1, ItemId = meteorId, Position = 0,
            Durability = 1, MaximumDurability = 1, StackSize = 5 };
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = meteorId.ToString(System.Globalization.CultureInfo.InvariantCulture); fields[1] = "OwnedRepairMeteor";
        fields[ItemType.DurabilityFieldIndex] = "1"; fields[ItemType.StackSizeFieldIndex] = "99";
        var catalogRow = new ItemType(checked((int)meteorId), fields[1], fields);
        var initial = new PlayerItemSnapshot(new[] { target, meteor }, [], [], [], [], []);
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login(); var before = f.Current; var files = f.Files();
            var binding = RequireGraph(f); var inventory = f.Native.Inventory!; var image = binding.Items.Image;
            var nativeTarget = inventory.ClientItems[target.Uid];
            int first = f.Sent.Count; bool outputObserved = false;
            var nativeMeteor = inventory.ClientItems[meteor.Uid];
            // Raw in-place membership loss leaves the map reference unchanged.
            // The guarded complete proof must still refuse before repair output.
            if (corruptNative) Assert.True(inventory.ClientItems.TryRemove(meteor.Uid, out _));
            f.OnSend = _ =>
            {
                outputObserved = true;
                Assert.False(corruptNative);
                Assert.NotSame(before, f.Current); RequireGraph(f);
                Assert.Equal((ushort)10, inventory.ClientItems[target.Uid].Durability);
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems), File.ReadAllBytes(f.ItemsPath));
            };

            var error = await Record.ExceptionAsync(async () => await f.Send(new MsgItemUsagePacket(
                target.Uid, 0, MsgItemUsageAction.RepairItem, 1234, 0, 0, 0).ToFrameBytes()));
            f.OnSend = null;

            if (corruptNative)
            {
                Assert.Null(error); // Actual Frame handles the rejected preparation without a success reply.
                Assert.False(outputObserved); Assert.Equal(first, f.Sent.Count);
                Assert.Same(before, f.Current); Assert.Same(image, binding.Items.Image);
                Assert.Equal((ushort)0, nativeTarget.Durability); Assert.Equal((byte)0, nativeTarget.Bound);
                Assert.DoesNotContain(meteor.Uid, inventory.ClientItems.Keys);
                Assert.Equal((ushort)5, nativeMeteor.StackSize); f.CheckFiles(files);
            }
            else
            {
                Assert.Null(error); Assert.True(outputObserved, f.Log.ToString());
                Assert.Same(inventory, f.Native.Inventory); Assert.Same(binding, f.Native.OrdinarySpellBinding);
                Assert.DoesNotContain(meteor.Uid, inventory.ClientItems.Keys);
                Assert.Equal((ushort)10, Assert.Single(f.Current.PlayerItems.InventoryItems).Durability);
                Assert.Equal(first + 6, f.Sent.Count);
            }
        }, playerItems: initial, additionalItems: new[] { catalogRow });
    }
}
