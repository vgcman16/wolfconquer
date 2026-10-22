using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task NativeRiseEquipmentNeutral251PublishesRawMagicAttackAndHitpoints()
    {
        await using var fixture = new NativeRiseEquipmentFixture();
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, LegacyRiseOfTaoismFlag));
        var start = await fixture.QueryOwnerAsync();
        fixture.AssertQuery(start, expectedMagicAttack: 301, expectedMaximum: 5_000,
            expectedCurrentHitpoints: 5_000,
            expectedPairs: [(5_000U, 5_000UL), (5_000U, 5_000UL), (5_000U, 5_000UL)]);
        await fixture.AssertThunderHitAsync(expectedDamage: 421, expectedPeerHitpoints: 19_579);
    }

    [Fact]
    public async Task NativeRise15440AcquisitionAndRepeated251RebuildAddMagicOnce()
    {
        await using var fixture = new NativeRiseEquipmentFixture();
        var initial = await fixture.QueryOwnerAsync();
        fixture.AssertQuery(initial, 301, 5_000, 5_000,
            [(5_000U, 5_000UL), (5_000U, 5_000UL), (5_000U, 5_000UL)]);

        var acquired = await fixture.AcquireRiseAsync();
        // AttachStatus15440 must itself call QueryEquipment, not just appearance.
        fixture.AssertQuery(acquired, 13_301, 55_000, 55_000,
            [(5_000U, 5_000UL), (55_000U, 55_000UL),
             (55_000U, 55_000UL), (55_000U, 55_000UL)]);

        var repeated = await fixture.QueryOwnerAsync();
        // UpdateStats629/633 resets MA from current raw Spirit before AddRune1817.
        // HP is a separate native setter: another +50000 is observable before clamp.
        fixture.AssertQuery(repeated, 13_301, 55_000, 55_000,
            [(5_000U, 55_000UL), (55_000U, 105_000UL),
             (55_000U, 55_000UL), (55_000U, 55_000UL)]);
    }

    [Fact]
    public async Task NativeRise15440ActualExpiryRebuildsAfterRemovedVectorRetainedAttack()
    {
        await using var fixture = new NativeRiseEquipmentFixture();
        var initial = await fixture.QueryOwnerAsync();
        fixture.AssertQuery(initial, 301, 5_000, 5_000,
            [(5_000U, 5_000UL), (5_000U, 5_000UL), (5_000U, 5_000UL)]);
        var acquired = await fixture.AcquireRiseAsync();
        fixture.AssertQuery(acquired, 13_301, 55_000, 55_000,
            [(5_000U, 5_000UL), (55_000U, 55_000UL),
             (55_000U, 55_000UL), (55_000U, 55_000UL)]);

        var expired = await fixture.ExpireProducedRiseAsync();
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, LegacyRiseOfTaoismFlag));
        // An entered removed-vector send already proved cached13301 ->6921 before
        // ThreadPool3010-3011's following real query rebuilt neutral MA and HP.
        fixture.AssertQuery(expired, 301, 5_000, 5_000,
            [(5_000U, 55_000UL), (5_000U, 5_000UL), (5_000U, 5_000UL)]);
        Assert.Equal("27", fixture.Owner.Character.Attributes["RiseofTaoismExtraMDamage"]);
        // Callback1000 earned the native500ms root stamp before this second manual cast.
        await fixture.StopRuntimeAndAdvanceManualCastCadenceAsync();
        await fixture.AssertThunderHitAsync(expectedDamage: 421, expectedPeerHitpoints: 12_658);
    }
}
