using System.Reflection;
using WolfConquer.Core.World;
using Gem = WolfConquer.Core.World.LegacyNativeGameItemFlags.Gem;

namespace WolfConquer.Tests;

public sealed class LegacyNativeClientGemTests
{
    [Fact]
    public void NativeConstructionOwnsFreshMutableThirteenSlotGemsAndStatus()
    {
        var first = new LegacyNativeGameClientRuntime();
        var second = new LegacyNativeGameClientRuntime();
        Assert.Equal(13, first.Gems.Length);
        Assert.All(first.Gems, value => Assert.Equal((ushort)0, value));
        Assert.Equal((byte)0, first.TortoiseGems);
        Assert.NotSame(first.Gems, second.Gems);
        Assert.NotSame(first.Status, second.Status);
        var replacement = new ushort[] { 99 };
        first.Gems = replacement;
        Assert.Same(replacement, first.Gems);
        Assert.Equal(99u, first.GemValues(Gem.NoSocket));
        var status = new LegacyNativeMsgStatus { PhysicalPercent = 7 };
        first.Status = status;
        Assert.Same(status, first.Status);
    }

    [Fact]
    public void ExactFiveTenFifteenValuesMutatePhysicalOnlyForTheDragonTrio()
    {
        foreach (var value in new ushort[] { 5, 10, 15 })
        foreach (var gem in new[] { Gem.NormalDragonGem, Gem.RefinedDragonGem, Gem.SuperDragonGem,
                     Gem.NormalVioletGem, Gem.NormalPhoenixGem, Gem.NoSocket })
        {
            var client = new LegacyNativeGameClientRuntime();
            client.AddGem(gem, value);
            bool dragon = (byte)gem is 11 or 12 or 13;
            Assert.Equal(dragon ? (uint)value : 0u, client.Status.PhysicalPercent);
            Assert.Equal(dragon ? 0u : value, client.Status.MagicPercent);
            Assert.Equal((uint)value, client.GemValues(gem));
        }
    }

    [Fact]
    public void NonstandardValuesSkipPercentAndNativeUnsignedAccumulationsWrap()
    {
        var client = new LegacyNativeGameClientRuntime();
        client.AddGem(Gem.NormalDragonGem, 7);
        client.AddGem(Gem.NormalVioletGem, 17);
        Assert.Equal(0u, client.Status.PhysicalPercent);
        Assert.Equal(0u, client.Status.MagicPercent);
        Assert.Equal(7u, client.GemValues(Gem.SuperDragonGem));
        client.Gems[5] = 65530;
        client.Status.MagicPercent = uint.MaxValue - 2;
        client.AddGem(Gem.RefinedVioletGem, 5);
        Assert.Equal(65535u, client.GemValues(Gem.NormalVioletGem));
        Assert.Equal(2u, client.Status.MagicPercent);
        client.AddGem(Gem.SuperVioletGem, 10);
        Assert.Equal(9u, client.GemValues(Gem.NormalVioletGem));
        Assert.Equal(12u, client.Status.MagicPercent);
    }

    [Fact]
    public void NinthTortoiseBlocksEveryGemArrayAdditionButNotEarlierPercentEffects()
    {
        var client = new LegacyNativeGameClientRuntime();
        for (int i = 0; i < 8; i++) client.AddGem(Gem.NormalTortoiseGem, 2);
        Assert.Equal((byte)8, client.TortoiseGems);
        Assert.Equal(16u, client.GemValues(Gem.SuperTortoiseGem));
        client.AddGem(Gem.RefinedTortoiseGem, 5);
        Assert.Equal((byte)9, client.TortoiseGems);
        Assert.Equal(16u, client.GemValues(Gem.SuperTortoiseGem));
        Assert.Equal(5u, client.Status.MagicPercent);
        client.Gems = null!;
        client.AddGem(Gem.SuperDragonGem, 15); // No array dereference behind count9.
        Assert.Equal(15u, client.Status.PhysicalPercent);
        Assert.Equal((byte)9, client.TortoiseGems);
    }

    [Fact]
    public void TortoiseCounterWrapsAtByteWidthBeforeTheArrayGate()
    {
        var client = new LegacyNativeGameClientRuntime { TortoiseGems = byte.MaxValue };
        client.AddGem(Gem.SuperTortoiseGem, 10);
        Assert.Equal((byte)0, client.TortoiseGems);
        Assert.Equal(10u, client.GemValues(Gem.NormalTortoiseGem));
        Assert.Equal(10u, client.Status.MagicPercent);
    }

    [Fact]
    public void NativeFaultOrderKeepsEarlierPercentAndCountMutationsWithoutRepairs()
    {
        var client = new LegacyNativeGameClientRuntime { Gems = null! };
        Assert.Throws<NullReferenceException>(() => client.AddGem(Gem.NormalTortoiseGem, 5));
        Assert.Equal(5u, client.Status.MagicPercent);
        Assert.Equal((byte)1, client.TortoiseGems);
        client.Gems = new ushort[13];
        Assert.Throws<IndexOutOfRangeException>(() => client.AddGem(Gem.NormalInfinityGem, 15));
        Assert.Equal(20u, client.Status.MagicPercent);
        Assert.Equal((byte)1, client.TortoiseGems);
        client.Status = null!;
        Assert.Throws<NullReferenceException>(() => client.AddGem(Gem.SuperTortoiseGem, 10));
        Assert.Equal((byte)1, client.TortoiseGems);
        client.AddGem(Gem.NormalVioletGem, 17); // Status is not read for this value.
        Assert.Equal(17u, client.GemValues(Gem.NormalVioletGem));
    }

    [Fact]
    public void QueriesUseTheLivePhysicalArrayAndRawByteTensGroupIncludingNativeBoundsFaults()
    {
        var client = new LegacyNativeGameClientRuntime();
        var replacement = Enumerable.Range(0, 13).Select(index => (ushort)(index * 3)).ToArray();
        client.Gems = replacement;
        Assert.Equal(15u, client.GemValues(Gem.SuperVioletGem));
        replacement[5] = ushort.MaxValue;
        Assert.Equal(65535u, client.GemValues(Gem.NormalVioletGem));
        Assert.Equal(36u, client.GemValues(Gem.SuperGloryGem));
        Assert.Equal(0u, client.GemValues((Gem)9));
        Assert.Throws<IndexOutOfRangeException>(() => client.GemValues(Gem.NormalInfinityGem));
        Assert.Throws<IndexOutOfRangeException>(() => client.GemValues(Gem.EmptySocket));
        client.Gems = null!;
        Assert.Throws<NullReferenceException>(() => client.GemValues(Gem.NoSocket));
    }

    [Fact]
    public void StatusRetainsAllNativeMutableFieldNamesWidthsAndImplicitZeros()
    {
        string[] names = ["Stamp", "UID", "MaxHitpoints", "MaxMana", "MaxAttack", "MinAttack", "Defence",
            "MagicAttack", "MagicDefence", "Dodge", "AgilityAtack", "Accuracy", "PhysicalPercent", "MagicPercent",
            "Damage", "ItemBless", "CriticalStrike", "SkillCStrike", "Immunity", "Penetration", "Block",
            "Breakthrough", "Counteraction", "Detoxication", "PhysicalDamageIncrease", "MagicDamageIncrease",
            "PhysicalDamageDecrease", "MagicDamageDecrease", "MetalResistance", "WoodResistance", "WaterResistance",
            "FireResistance", "EarthResistance", "MDefence", "MaxVigor", "PrestigeLevel", "TotalAnimaLevel",
            "RuneScore", "DashRate", "LukyStrike", "Parry", "DodgeRate", "BloodthirstLevel", "EtherealLevel",
            "ElanLevel", "SolidLevel", "VenomLevel"];
        var fields = typeof(LegacyNativeMsgStatus).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(names, fields.Select(field => field.Name));
        var value = new LegacyNativeMsgStatus();
        foreach (var field in fields)
        {
            var expectedType = field.Name switch
            {
                "Stamp" => typeof(int),
                "MaxVigor" or "TotalAnimaLevel" => typeof(ushort),
                "LukyStrike" or "Parry" => typeof(double),
                "BloodthirstLevel" or "EtherealLevel" or "ElanLevel" or "SolidLevel" or "VenomLevel" => typeof(byte),
                _ => typeof(uint)
            };
            Assert.Equal(expectedType, field.FieldType);
            Assert.False(field.IsInitOnly);
            Assert.Equal(Activator.CreateInstance(expectedType), field.GetValue(value));
        }
    }
}
