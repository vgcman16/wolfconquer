using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static WorldDataCatalog NativeRowCatalog(params MagicType[] rows) =>
        CreateMagicCatalogCore(CreateMonsterFamily(), [], [], rows);

    private static MagicType NativeInputRow(ushort id, byte sort, string raw)
    {
        var row = CreateMagicRow(id, sort, damage: 1, coolDown: 0);
        var fields = row.Fields.ToArray();
        fields[10] = raw;
        return row with { Fields = fields };
    }

    [Theory]
    [InlineData((ushort)1_000, (byte)1, "-16777217", -16777216f, -16777217)]
    [InlineData((ushort)14_500, (byte)6, "16777217", 16777216f, 16777217)]
    [InlineData((ushort)6_001, (byte)48, "30338", 38f, 30338)]
    [InlineData((ushort)1_195, (byte)20, "30038", 38f, 30038)]
    [InlineData((ushort)12_580, (byte)6, "1", 100f, 1)]
    [InlineData((ushort)12_070, (byte)48, "1", 104f, 1)]
    [InlineData((ushort)3_306, (byte)6, "400", 1f, 400)]
    [InlineData((ushort)10_490, (byte)6, "400", 150f, 400)]
    public void NativeMagicMutableRowUsesSignedLoadDomainAndFloatPrecision(
        ushort id, byte sort, string raw, float expectedDamage, int expectedGDamage)
    {
        var world = NativeRowCatalog(NativeInputRow(id, sort, raw));
        Assert.True(LegacyMagicRuntime.TryResolveNativeLoadedSpell(world, id, 0, out var descriptor, out var row));
        Assert.Equal(expectedDamage, row.Damage);
        Assert.Equal(expectedGDamage, row.GDamage);
        Assert.Equal(expectedDamage, LegacyMagicRuntime.ReadNativeDamage(world, descriptor));
        Assert.Equal(unchecked((uint)expectedGDamage), descriptor.GDamage);
    }

    [Fact]
    public void NativeMagicMutableRowRejectsFractionAfterPreliminaryDoubleDamageParsing()
    {
        var world = NativeRowCatalog(NativeInputRow(1_000, 5, "30338.5"));
        Assert.Throws<FormatException>(() => world.FindNativeMagicRow(1_000, 0));
    }

    [Fact]
    public void NativeMagicMutableRowRejectsUnsignedValueOutsideSignedGDamageDomain()
    {
        var world = NativeRowCatalog(NativeInputRow(1_000, 5, "4294967295"));
        Assert.Throws<OverflowException>(() => world.FindNativeMagicRow(1_000, 0));
    }

    [Fact]
    public void NativeMagicDescriptorReadsRateSeparatelyAndKeepsNativeDurationAndByteCast()
    {
        var input = NativeInputRow(6_001, 48, "30338");
        var fields = input.Fields.ToArray();
        fields[12] = "-7"; // Native Rate; this is not native GDamage.
        fields[13] = "unused-for-6001";
        fields[14] = "300"; // Native ushort parse followed by unchecked byte cast.
        var world = NativeRowCatalog(input with { Fields = fields });
        Assert.True(LegacyMagicRuntime.TryResolveNativeLoadedSpell(world, 6_001, 0, out var descriptor, out var row));
        Assert.Equal(30338, row.GDamage);
        Assert.Equal(30338U, descriptor.GDamage);
        Assert.Equal(20U, descriptor.Duration);
        Assert.Equal((byte)44, descriptor.MaxTargets);
    }

    [Fact]
    public void NativeMagicSharedRowMutationSurvivesHeldDescriptorButNotAnotherCatalog()
    {
        var raw = NativeInputRow(1_000, 1, "1");
        var first = NativeRowCatalog(raw);
        var second = NativeRowCatalog(raw); // Intentionally the SAME raw object in two catalogs.
        Assert.True(LegacyMagicRuntime.TryResolveNativeLoadedSpell(first, 1_000, 0, out var held, out var firstRow));
        Assert.True(LegacyMagicRuntime.TryResolveNativeLoadedSpell(second, 1_000, 0, out var other, out var secondRow));
        var dtoJson = System.Text.Json.JsonSerializer.Serialize(held);
        LegacyMagicRuntime.ClampNativePhysicalMonsterDamage(first, held);
        LegacyMagicRuntime.NormalizeNativeRootCoolDown(first, held, 500);
        Assert.Equal(1U, held.Damage); // Public DTO shape/value is unchanged; it is not scalar authority.
        Assert.Equal(10f, firstRow.Damage);
        Assert.Equal(10f, LegacyMagicRuntime.ReadNativeDamage(first, held));
        Assert.Equal((ushort)500, LegacyMagicRuntime.ReadNativeCoolDown(first, held));
        Assert.Equal(1f, LegacyMagicRuntime.ReadNativeDamage(second, other));
        Assert.Equal((ushort)0, secondRow.CoolDown);
        Assert.NotSame(firstRow, secondRow);
        Assert.Equal(dtoJson, System.Text.Json.JsonSerializer.Serialize(held));
        Assert.True(LegacyMagicRuntime.TryResolveNativeLoadedSpell(first, 1_000, 0, out var later, out var retained));
        Assert.Same(firstRow, retained);
        Assert.Equal(10U, later.Damage);
    }

    [Fact]
    public void NativeMagic6009AliasKeepsTheSame6010ObjectWhileLearnedBookRemainsRequired()
    {
        var world = NativeRowCatalog(NativeInputRow(6_010, 48, "100"));
        Assert.Same(world.FindNativeMagicRow(6_010, 0), world.FindNativeMagicRow(6_009, 0));
        Assert.False(LegacyMagicRuntime.TryResolveNativeLearnedSpell(
            PlayerSpellSnapshot.Empty, world, 6_009, out _, out _, out _));
        var entry = new PlayerSpellSnapshotEntry(6_009, 0, 0, 0, 0, 0);
        Assert.True(LegacyMagicRuntime.TryResolveNativeLearnedSpell(
            new PlayerSpellSnapshot([entry]), world, 6_009, out var learned, out var descriptor, out var row));
        Assert.Same(entry, learned);
        Assert.Equal((ushort)6_010, descriptor.Id);
        Assert.Same(world.FindNativeMagicRow(6_010, 0), row);
    }
}
