using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeEquipmentCallerStateTests
{
    [Fact]
    public void NativeCallerFieldsKeepNullAndZeroDefaultsWithoutInventingTransformOrWeaponBirth()
    {
        var owner = new LegacyNativeGameClientRuntime();
        Assert.Null(owner.Weapons);
        Assert.Null(owner.Player.TransformInfo);
        Assert.False(owner.Player.IsAsasin);
        Assert.Equal(default, owner.Player.FineRaneSave);
        Assert.False(owner.Player.OnTransform);
    }

    [Fact]
    public void TransformConstructorStoresTheExactPlayerReferenceWithAllFiveNativeDefaults()
    {
        var player = new LegacyNativeGameClientRuntime().Player;
        var transform = new LegacyNativeClientTransformRuntime(player);
        Assert.Same(player, typeof(LegacyNativeClientTransformRuntime)
            .GetField("Owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(transform));
        Assert.Equal(0u, transform.HitPoints);
        Assert.Equal((ushort)0, transform.ID);
        Assert.Equal(default, transform.Stamp);
        Assert.Equal((ushort)0, transform.SpellID);
        Assert.Equal((ushort)0, transform.Level);
        var nullOwner = new LegacyNativeClientTransformRuntime(null!);
        Assert.Null(typeof(LegacyNativeClientTransformRuntime)
            .GetField("Owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(nullOwner));
    }

    [Theory]
    [InlineData((ushort)0, false)] [InlineData((ushort)1, true)] [InlineData(ushort.MaxValue, true)]
    public void OnTransformReadsTheExistingNativeTransformationSlotNotTheOptionalTransformOwner(ushort id, bool expected)
    {
        var player = new LegacyNativeGameClientRuntime().Player;
        player.TransformationID = id; // Cold owner preserves actual complete setter gate.
        Assert.Null(player.TransformInfo);
        Assert.Equal(expected, player.OnTransform);
        player.TransformInfo = new(player) { ID = 123 };
        Assert.Equal(expected, player.OnTransform);
    }
}
