using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeJobFamilyTests
{
    [Theory]
    [InlineData("Trojan", 1000u, 1051u)]
    [InlineData("Warrior", 2000u, 2051u)]
    [InlineData("Archer", 4000u, 4051u)]
    [InlineData("Ninja", 5000u, 5051u)]
    [InlineData("Monk", 6000u, 6051u)]
    [InlineData("Pirate", 7000u, 7051u)]
    [InlineData("Lee", 8000u, 8051u)]
    [InlineData("ThunderStriker", 9000u, 9051u)]
    [InlineData("Water", 13002u, 13051u)]
    [InlineData("Fire", 14002u, 14051u)]
    [InlineData("Taoist", 10000u, 14051u)]
    [InlineData("WindWalker", 16000u, 16051u)]
    public void AllTwelvePredicatesKeepLiteralInclusiveNativeBounds(string family, uint first, uint last)
    {
        Func<uint, bool> predicate = family switch
        {
            "Trojan" => LegacyNativeAtributesStatus.IsTrojan,
            "Warrior" => LegacyNativeAtributesStatus.IsWarrior,
            "Archer" => LegacyNativeAtributesStatus.IsArcher,
            "Ninja" => LegacyNativeAtributesStatus.IsNinja,
            "Monk" => LegacyNativeAtributesStatus.IsMonk,
            "Pirate" => LegacyNativeAtributesStatus.IsPirate,
            "Lee" => LegacyNativeAtributesStatus.IsLee,
            "ThunderStriker" => LegacyNativeAtributesStatus.IsThunderStriker,
            "Water" => LegacyNativeAtributesStatus.IsWater,
            "Fire" => LegacyNativeAtributesStatus.IsFire,
            "Taoist" => LegacyNativeAtributesStatus.IsTaoist,
            "WindWalker" => LegacyNativeAtributesStatus.IsWindWalker,
            _ => throw new InvalidOperationException("Unknown test family.")
        };
        Assert.False(predicate(0));
        Assert.False(predicate(first - 1));
        Assert.True(predicate(first));
        Assert.True(predicate(first + 1));
        Assert.True(predicate((first + last) / 2));
        Assert.True(predicate(last - 1));
        Assert.True(predicate(last));
        Assert.False(predicate(last + 1));
        Assert.False(predicate(uint.MaxValue));
    }

    [Fact]
    public void WaterFireStartAtPromotionTwoWhileTaoistIncludesTheWholeNativeGap()
    {
        foreach (uint job in new uint[] { 10000, 10051, 11000, 12000, 13000, 13001, 13002, 13051, 13500, 14000, 14001, 14002, 14051 })
            Assert.True(LegacyNativeAtributesStatus.IsTaoist(job));
        Assert.False(LegacyNativeAtributesStatus.IsWater(13000));
        Assert.False(LegacyNativeAtributesStatus.IsWater(13001));
        Assert.True(LegacyNativeAtributesStatus.IsWater(13002));
        Assert.False(LegacyNativeAtributesStatus.IsFire(14000));
        Assert.False(LegacyNativeAtributesStatus.IsFire(14001));
        Assert.True(LegacyNativeAtributesStatus.IsFire(14002));
        Assert.False(LegacyNativeAtributesStatus.IsTaoist(14052));
        Assert.False(LegacyNativeAtributesStatus.IsWater(13500));
        Assert.False(LegacyNativeAtributesStatus.IsFire(13500));
    }

    [Fact]
    public void LeeJobFamilyExtendsTo8051WithoutChangingTheDistinctItemRequirement8049Limit()
    {
        var user = new LegacyNativeGameClientRuntime();
        var item = new LegacyNativeItemRuneMetadata { Class = 8000 };
        foreach (uint job in new uint[] { 8049, 8050, 8051 })
        {
            typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(user.Player, job); // Explicit getter prestate, not a partial setter.
            Assert.True(LegacyNativeAtributesStatus.IsLee(job));
            Assert.Equal(job == 8049, LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(item, user));
        }
        Assert.False(LegacyNativeAtributesStatus.IsLee(8052));
        Assert.Equal(8000u, item.Class);
    }
}
