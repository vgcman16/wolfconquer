using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterEventTests
{
    [Fact]
    public void SaturnConstructorIsRealDeadBirthWithNoSchedulerAssignment()
    {
        using var state = new State(); var saturn = new LegacyNativeMsgSaturn();
        Assert.Equal(LegacyNativeProcesType.Dead, saturn.Mode); Assert.Equal(default, Field<DateTime>(saturn, "FinishTime"));
        Assert.Null(LegacyNativeMsgSchedules.Saturn);
    }
    [Theory]
    [InlineData(2353u, true)] [InlineData(2352u, false)] [InlineData(2354u, false)]
    [InlineData(0u, false)] [InlineData(uint.MaxValue, false)]
    public void SaturnMapQueryKeepsTheSingleSourceId(uint map, bool expected) =>
        Assert.Equal(expected, new LegacyNativeMsgSaturn().IsInSaturn(map));
    [Fact]
    public void SchedulerReferencesAreIndependentPhysicalSlotsNotAutoConstructedEvents()
    {
        using var state = new State(); Assert.Null(LegacyNativeMsgSchedules.Saturn); Assert.Null(LegacyNativeMsgSchedules.DisCity); Assert.Null(LegacyNativeMsgSchedules.PowerArena);
        var saturn = new LegacyNativeMsgSaturn(); var dis = new LegacyNativeMsgDisCity(); var power = new LegacyNativeMsgPowerArena();
        Assert.Null(LegacyNativeMsgSchedules.PowerArena); LegacyNativeMsgSchedules.Saturn = saturn; LegacyNativeMsgSchedules.DisCity = dis;
        Assert.Same(saturn, LegacyNativeMsgSchedules.Saturn); Assert.Same(dis, LegacyNativeMsgSchedules.DisCity);
        Assert.Null(LegacyNativeMsgSchedules.PowerArena); LegacyNativeMsgSchedules.PowerArena = power; Assert.Same(power, LegacyNativeMsgSchedules.PowerArena);
    }
}
