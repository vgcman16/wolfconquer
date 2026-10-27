using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.World;
using ItemType = WolfConquer.Core.World.LegacyNativeNinja.ItemType;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeNinjaTests
{
    [Fact]
    public void CompleteThreeUInt64EnumsKeepNativeNamesValuesAndLackOfFlagsAttribute()
    {
        EnumDeclaration<LegacyNativeNinja.ItemType>(
            "WildSigil WildSigilBurning WildSigilRapid FlameSigil FlameSigilRapid FlameSigilScorch " +
            "PrisonSigil PrisonSigilFatigue PrisonSigilFutility DragonSigil DragonSigilBillow DragonSigilFading " +
            "MudSigil MudSigilImpeccable MudSigilSolidity DustSigil DustSigilExtinction DustSigilStunn " +
            "SickleSigil SickleSigilFlurry SickleSigilGallop WhirlSigil WhirlSigilSage WhirlSigilAura " +
            "KylinSigil KylinSigilBurial KylinSigilSeal SlashSeal SlashSealFlash SlashSealProwess " +
            "PaperDance HellVortex InfiniteMist HeavenSWonder BonePulse",
            [100, 101, 102, 103, 104, 105, 200, 201, 202, 203, 204, 205, 300, 301, 302, 303, 304, 305,
                400, 401, 402, 403, 404, 405, 500, 501, 502, 503, 504, 505, 600, 601, 602, 603, 604]);
        EnumDeclaration<LegacyNativeNinja.Flags>(
            "Gate_of_Dawn Gate_of_Rest Gate_of_Life Gate_of_Pain Gate_of_Limlt Gate_of_View Gate_of_Shock Gate_of_Death " +
            "Gate_of_Dawn_2 Gate_of_Rest_2 Gate_of_Life_2 Gate_of_Pain_2 Gate_of_Limlt_2 Gate_of_View_2 Gate_of_Shock_2 Gate_of_Death_2 Full",
            [1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65535]);
        EnumDeclaration<LegacyNativeNinja.UnlockMasterys>("FireMastery WaterMastery EarthMastery WindMastery LightningMastery", [1, 2, 4, 8, 16]);
    }

    [Fact]
    public void BothConstructorsAllocateIndependentDictionariesButDoNotPublishClientSlotOrTables()
    {
        using var tables = new Tables();
        var types = LegacyNativeNinjaFile.gouyu_type;
        var immortals = LegacyNativeNinjaFile.gouyu_immortals;
        var user = Client();
        Assert.Null(user.MyNinja);
        var first = new LegacyNativeNinja();
        var second = new LegacyNativeNinja(user);
        var nullUser = new LegacyNativeNinja(null!);
        Assert.Null(first.user);
        Assert.Null(nullUser.user);
        Assert.Same(user, second.user);
        Assert.Null(user.MyNinja);
        Assert.NotSame(first.Items, second.Items);
        Assert.NotSame(first.Items, nullUser.Items);
        Assert.NotSame(second.Items, nullUser.Items);
        foreach (var ninja in new[] { first, second, nullUser })
        {
            Assert.IsType<ConcurrentDictionary<uint, LegacyNativeNinja.Item>>(ninja.Items);
            Assert.Empty(ninja.Items);
            var scalarProperties = typeof(LegacyNativeNinja).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanWrite && property.PropertyType is { } type && (type == typeof(uint) || type == typeof(ulong)))
                .ToArray();
            var expectedScalars = new[] { "Flag", "Exp", "Fire", "Water", "Earth", "Wind", "Lightning", "tmp_Fire", "tmp_Water",
                "tmp_Earth", "tmp_Wind", "tmp_Lightning", "FireMastery", "WaterMastery", "EarthMastery", "WindMastery",
                "LightningMastery", "UnlockMastery" };
            Assert.Equal(expectedScalars.Order(), scalarProperties.Select(property => property.Name).Order());
            foreach (var property in scalarProperties)
            {
                Assert.Equal(property.Name == "Flag" ? typeof(ulong) : typeof(uint), property.PropertyType);
                Assert.Equal(0UL, Convert.ToUInt64(property.GetValue(ninja)));
                Assert.True(property.GetMethod!.IsPublic);
                Assert.True(property.SetMethod!.IsPublic);
            }
        }
        second.Flag = ulong.MaxValue;
        second.Exp = 77;
        second.user = null!;
        var replacement = new ConcurrentDictionary<uint, LegacyNativeNinja.Item>();
        second.Items = replacement;
        Assert.Same(replacement, second.Items);
        Assert.Equal(ulong.MaxValue, second.Flag);
        Assert.Equal(77u, second.Exp);
        Assert.Null(second.user);
        Assert.Same(types, LegacyNativeNinjaFile.gouyu_type);
        Assert.Same(immortals, LegacyNativeNinjaFile.gouyu_immortals);
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(4999u, false)]
    [InlineData(5000u, true)]
    [InlineData(5051u, true)]
    [InlineData(5058u, false)]
    [InlineData(uint.MaxValue, false)]
    public void NinjaClassGateClearsOutBeforeMissingEquipmentOrItems(uint job, bool accepted)
    {
        var user = Client(job);
        user.Equipment = null;
        var ninja = new LegacyNativeNinja(user) { Items = null! };
        LegacyNativeNinja.Item result = new();
        if (accepted)
            Assert.Throws<NullReferenceException>(() => ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        else
            Assert.False(ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Null(result);
        Assert.Null(user.Equipment);
        Assert.Null(ninja.Items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EverySlotIsScannedAndTheLastMatchingSlotWinsRatherThanFirst(bool alternate)
    {
        var ninja = new LegacyNativeNinja(Client(alternate: alternate));
        Put(ninja, 1000, 0);
        Put(ninja, 1001, 17);
        Assert.False(ninja.TryGetValueEquip(ItemType.WildSigil, out var absent));
        Assert.Null(absent);
        for (uint slot = 1; slot <= 8; slot++)
        {
            var current = Put(ninja, slot, alternate ? slot + 8 : slot);
            Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out var result));
            Assert.Same(current, result);
        }
    }

    [Fact]
    public void AlternatePrefersWithinEachPairButALaterFallbackOverridesAnEarlierPrimary()
    {
        var user = Client(alternate: true);
        var ninja = new LegacyNativeNinja(user);
        Put(ninja, 101, 1);
        var primary1 = Put(ninja, 109, 9);
        Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out var result));
        Assert.Same(primary1, result);
        var fallback2 = Put(ninja, 102, 2);
        Put(ninja, 210, 10, 200); // Wrong ItemID does not suppress this pair's fallback.
        Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Same(fallback2, result);
        var primary2 = Put(ninja, 110, 10);
        Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Same(primary2, result);
        var fallback8 = Put(ninja, 108, 8);
        Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Same(fallback8, result);
        var primary8 = Put(ninja, 116, 16);
        Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Same(primary8, result);
        primary8.ItemID = 200;
        Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Same(fallback8, result);
        fallback8.Position = 17;
        Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Same(primary2, result);
        user.Equipment!.Alternante = false;
        Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Same(fallback2, result);
    }

    [Theory]
    [InlineData(100UL)]
    [InlineData(0x100000064UL)]
    [InlineData(ulong.MaxValue)]
    public void EquippedQueryUsesRawUInt32CastRatherThanEnumValidation(ulong raw)
    {
        var ninja = new LegacyNativeNinja(Client());
        var item = Put(ninja, 99, 1, unchecked((uint)raw));
        Assert.True(ninja.TryGetValueEquip((ItemType)raw, out var result));
        Assert.Same(item, result);
    }

    [Fact]
    public void QueriesReadCurrentUserPlayerEquipmentAndDictionaryReferencesWithoutCaching()
    {
        var original = Client();
        var ninja = new LegacyNativeNinja(original);
        var normal = Put(ninja, 1, 1);
        var alternate = Put(ninja, 9, 9);
        Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out var result));
        Assert.Same(normal, result);
        original.Equipment = new(original) { Alternante = true };
        Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Same(alternate, result);
        original.Player = new LegacyNativePlayerRuntime(original);
        Assert.False(ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Null(result);
        var foreign = Client(5051);
        ninja.user = foreign;
        Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Same(normal, result);
        var priorItems = ninja.Items;
        ninja.Items = new();
        var replacement = Put(ninja, 77, 8);
        Assert.True(ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Same(replacement, result);
        Assert.Same(normal, priorItems[1]);
        Assert.Same(foreign, ninja.user);
    }

    private static void EnumDeclaration<T>(string names, ulong[] values) where T : struct, Enum
    {
        Assert.Equal(typeof(ulong), Enum.GetUnderlyingType(typeof(T)));
        Assert.Null(typeof(T).GetCustomAttribute<FlagsAttribute>());
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(names.Split(' '), fields.Select(field => field.Name));
        Assert.Equal(values, fields.Select(field => (ulong)field.GetRawConstantValue()!));
        Assert.Equal(values.Length, values.Distinct().Count());
    }
}
