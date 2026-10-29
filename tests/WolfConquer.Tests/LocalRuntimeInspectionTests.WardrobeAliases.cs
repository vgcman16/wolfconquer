using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LocalRuntimeInspectionTests
{
    [WindowsOperatorPipeFact]
    public Task ActuallyAdmittedGarmentAndMountAliasesHaveBoundedSameObjectWitnesses() => WithShapeOwner(true, async f =>
    {
        var files = Files(f); var sent = f.SentSnapshot(); var water = f.HighWater;
        var garment = f.Native.Equipment!.ClientItems[ShapeRightUid]; var mount = f.Native.Equipment.ClientItems[ShapeOtherUid];
        Assert.Same(garment, f.Native.MyWardrobe!.Items[0][ShapeRightUid]);
        Assert.Same(mount, f.Native.MyWardrobe.Items[1][ShapeOtherUid]);
        Assert.True(f.Native.MyWardrobe.IsOwnedBy(f.Native));
        for (var query = 0; query < 2; query++)
        {
            var result = await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)); Diagnostic(result);
            Assert.Contains(result.Snapshot!.ManagedItems, row => row.Uid == ShapeRightUid && row.WarehouseId == 100 && row.Position == 59);
            var nativeRows = Assert.IsType<LegacyOwnedPlayerItem[]>(result.Snapshot.NativeItems);
            Assert.Contains(nativeRows, row => row.Uid == ShapeOtherUid && row.WarehouseId == 100 && row.Position == 17);
        }
        Assert.Equal(water, f.HighWater); Unchanged(f, files, sent);
    });

    [WindowsOperatorPipeTheory]
    [InlineData("missing-owner")]
    [InlineData("owner-changed")]
    [InlineData("wrong-bucket")]
    [InlineData("wrong-key")]
    [InlineData("different-actual-row")]
    [InlineData("other-bucket-duplicate")]
    [InlineData("native-duplicate-slot")]
    [InlineData("managed-duplicate-slot")]
    [InlineData("alias-position60")]
    public Task MalformedActualSelectedWardrobeAliasesRefuseWithoutPretendingToCollectTheWardrobe(string kind) =>
        WithShapeOwner(true, async f =>
        {
            var owner = f.Native.MyWardrobe!; var garments = owner.Items[0]; var mounts = owner.Items[1];
            var garment = f.Native.Equipment!.ClientItems[ShapeRightUid]; var mount = f.Native.Equipment.ClientItems[ShapeOtherUid];
            var position = mount.Position; var garmentPosition = garment.Position;
            var managed = Assert.IsType<PlayerItemSnapshotItem[]>(f.Current.PlayerItems.EquipmentItems);
            var mountIndex = Array.FindIndex(managed, row => row.Uid == ShapeOtherUid); Assert.True(mountIndex >= 0);
            var original = managed[mountIndex]; var current = f.Current;
            var files = Files(f); var sent = f.SentSnapshot(); var water = f.HighWater;
            try
            {
                switch (kind)
                {
                    case "missing-owner": f.Native.MyWardrobe = null; break;
                    case "owner-changed": owner.Owner = null; break;
                    case "wrong-bucket": Assert.True(garments.TryRemove(ShapeRightUid, out _)); Assert.True(mounts.TryAdd(ShapeRightUid, garment)); break;
                    case "wrong-key": Assert.True(garments.TryRemove(ShapeRightUid, out _)); Assert.True(garments.TryAdd(uint.MaxValue, garment)); break;
                    case "different-actual-row": garments[ShapeRightUid] = Assert.Single(f.Native.Inventory!.ClientItems).Value; break;
                    case "other-bucket-duplicate": Assert.True(mounts.TryAdd(ShapeRightUid, garment)); break;
                    case "native-duplicate-slot": mount.Position = garment.Position; break;
                    case "managed-duplicate-slot": managed[mountIndex] = original.WithPosition(garment.Position); break;
                    case "alias-position60": garment.Position = 60; break;
                    default: throw new ArgumentOutOfRangeException(nameof(kind));
                }
                Rejected(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)));
                Assert.Same(current, f.Current); Assert.False(f.Native.OrdinarySpellBinding!.IsRetired);
                Assert.Equal(water, f.HighWater); Unchanged(f, files, sent);
            }
            finally
            {
                f.Native.MyWardrobe = owner; owner.Owner = f.Native;
                mounts.TryRemove(ShapeRightUid, out _); garments.TryRemove(uint.MaxValue, out _); garments[ShapeRightUid] = garment;
                mount.Position = position; garment.Position = garmentPosition; managed[mountIndex] = original;
            }
            Diagnostic(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)));
        });
}
