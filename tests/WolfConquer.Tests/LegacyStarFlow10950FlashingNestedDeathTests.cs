using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task StarFlow10950NestedRealDeathDuringFlashVectorCannotResurrectTransientRegistration()
    {
        var armed = false;
        var callbacks = 0;
        LegacyCanAttackFlashingRegistration? enteredRegistration = null;
        await using var fixture = Flash0LifetimeFixture(async (current, recipient, bytes) =>
        {
            if (!armed || recipient != PlayerUid || !Flash0Vector(bytes, active: true)) return;
            armed = false;
            callbacks++;
            enteredRegistration = Flash0Read(current.Owner); // Real AddFlag already committed before entered self frame.
            await current.KillOwnerAsync(); // Real peer1000 receive enters normaldeath/ClearFlags; no0/timer seed.
            Assert.Equal(0, current.Owner.Character.MinHitPoints);
            Flash0AssertRetired(current.Owner);
        });
        await ArrowThirdWarmAsync(fixture);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0));
        Assert.Null(fixture.Owner.CanAttackFlashingRegistration);
        armed = true;
        await fixture.OrdinaryMagicAsync(); // W2 actual third336 callback ->10950 ->entered Flash self vector.
        Assert.Equal(1, callbacks);
        Assert.NotNull(enteredRegistration);
        Assert.Equal(ArrowLifeOrigin.AddSeconds(2).DateTime, enteredRegistration.Timer);
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Flash0AssertRetired(fixture.Owner); // Outer continuation must use current sameUID, not restore its preawait record.
        Assert.Equal((long)PlayerUid, fixture.Owner.Character.Uid);
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }
}
