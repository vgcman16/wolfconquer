using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAttributeCacheTests
{
    [Theory]
    [InlineData("missing-hp")]
    [InlineData("changed-hp")]
    [InlineData("equal-hp-replacement")]
    [InlineData("missing-mana")]
    [InlineData("changed-mana")]
    [InlineData("equal-mana-replacement")]
    public Task AllocationRebuildsAfterOldCacheImageIsMissingChangedOrReplaced(string mutation)
    {
        var (input, catalog) = Inputs();
        return OwnedEquipmentBehaviorFixture.RunAsync(input, catalog, async f =>
        {
            var initial = f.Current();
            var hp = Assert.IsType<LegacyEquipmentHitpointsStatus>(initial.EquipmentHitpointsStatus);
            var overlay = LegacyPhysicalStatusOverlay.ForOwner(initial);
            Assert.Equal(5030U, hp.MaximumHitpoints); Assert.Equal(450U, overlay.MaxMana);
            var changed = mutation switch
            {
                "missing-hp" => initial with { EquipmentHitpointsStatus = null },
                "changed-hp" => initial with { EquipmentHitpointsStatus = hp with { MaximumHitpoints = 5031 } },
                "equal-hp-replacement" => initial with { EquipmentHitpointsStatus = hp with { } },
                "missing-mana" => initial with { PhysicalStatusOverlay = overlay with { MaxMana = null } },
                "changed-mana" => initial with { PhysicalStatusOverlay = overlay with { MaxMana = 451 } },
                "equal-mana-replacement" => initial with { PhysicalStatusOverlay = overlay with { } },
                _ => throw new InvalidOperationException("Unexpected declared mutation."),
            };
            f.Store.Set(f.Connection(Actor), changed);
            await AssertFullRebuildAsync(f);
        });
    }
}
