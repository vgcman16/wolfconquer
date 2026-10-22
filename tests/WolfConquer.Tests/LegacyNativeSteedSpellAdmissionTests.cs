using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed class LegacyNativeSteedSpellAdmissionTests
{
    [Fact]
    public void UnregisteredRideDoesNotReadEquipmentEvenIfTheBitmapIsSet()
    {
        var client = new LegacyNativeGameClientRuntime();
        client.Player.BitVector.Add((int)Flag.Ride);
        Assert.False(client.Player.ContainFlag(Flag.Ride));
        Assert.Null(client.Equipment);
        Assert.True(client.AllowUseSpellOnSteed(0));
        Assert.True(client.AllowUseSpellOnSteed(ushort.MaxValue));
        Assert.True(client.Player.BitVector.Contain((int)Flag.Ride));
        Assert.Equal(0, client.Player.CompletedRateDraws);
    }

    [Theory]
    [InlineData((ushort)0, false)]
    [InlineData((ushort)7000, false)]
    [InlineData((ushort)7001, true)]
    [InlineData((ushort)7002, true)]
    [InlineData((ushort)7003, true)]
    [InlineData((ushort)7004, false)]
    [InlineData(ushort.MaxValue, false)]
    public void RegisteredRideWithoutCropAllowsOnlyTheThreeSourceSpellIds(ushort spell, bool expected)
    {
        var client = new LegacyNativeGameClientRuntime();
        client.Player.BitVector.TryAdd((int)Flag.Ride, 7, false, 0);
        var row = client.Player.BitVector.ArrayFlags[(int)Flag.Ride];
        client.Player.BitVector.Remove((int)Flag.Ride); // Predicate uses dictionary containment, not visibility.
        client.Equipment = new LegacyNativeEquipItemStorage(null);
        Assert.Equal(expected, client.AllowUseSpellOnSteed(spell));
        Assert.Same(row, client.Player.BitVector.ArrayFlags[(int)Flag.Ride]);
        Assert.False(client.Player.BitVector.Contain((int)Flag.Ride));
        Assert.Equal(0, client.Player.CompletedRateDraws);
    }

    [Fact]
    public void EquipmentFaultPrecedesEvenTheAllowedSpellIdCheck()
    {
        var client = new LegacyNativeGameClientRuntime();
        client.Player.BitVector.TryAdd((int)Flag.Ride, 7, false, 0);
        Assert.Null(client.Equipment);
        Assert.Throws<NullReferenceException>(() => client.AllowUseSpellOnSteed(7001));
        Assert.True(client.Player.ContainFlag(Flag.Ride));
        Assert.True(client.Player.BitVector.Contain((int)Flag.Ride));
        Assert.Null(client.Equipment);
    }

    [Fact]
    public void CurrentPlayerReferenceAndCurrentEquipmentAreReadWithoutRebindingTheirOwners()
    {
        var client = new LegacyNativeGameClientRuntime();
        var foreign = new LegacyNativeGameClientRuntime();
        foreign.Player.BitVector.TryAdd((int)Flag.Ride, 7, false, 0);
        client.Player = foreign.Player;
        foreign.Equipment = new LegacyNativeEquipItemStorage(foreign) { RidingCrop = 999 };
        client.Equipment = new LegacyNativeEquipItemStorage(foreign) { RidingCrop = 0 };
        Assert.False(client.AllowUseSpellOnSteed(1)); // Not the physical Player.Owner's Equipment.
        client.Equipment.RidingCrop = uint.MaxValue;
        Assert.True(client.AllowUseSpellOnSteed(1));
        Assert.Same(foreign, client.Player.Owner);
        Assert.True(foreign.Player.ContainFlag(Flag.Ride));
        Assert.Null(client.Socket);
    }
}
