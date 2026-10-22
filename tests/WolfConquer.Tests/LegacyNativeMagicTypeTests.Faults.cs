using WolfConquer.Core.World;
using MagicType = WolfConquer.Core.Data.LegacyNativeMagicType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMagicTypeTests
{
    [Theory]
    [InlineData("id", typeof(OverflowException))]
    [InlineData("sort", typeof(OverflowException))]
    [InlineData("max-targets", typeof(OverflowException))]
    [InlineData("cps", typeof(OverflowException))]
    [InlineData("decimal-gdamage", typeof(FormatException))]
    [InlineData("cooldown-override", typeof(FormatException))]
    [InlineData("empty-name", typeof(IndexOutOfRangeException))]
    [InlineData("blank", typeof(IndexOutOfRangeException))]
    public void ParseFaultRetainsEarlierRowsAndWeaponIndexButPublishesNeitherBadRowNorAlias(string kind, Type errorType)
    {
        using var fixture = new NativeMagicTypeTestFixture();
        var bad = kind switch
        {
            "id" => Row(9200, (1, "65536")),
            "sort" => Row(9200, (2, "256")),
            "max-targets" => Row(9200, (14, "65536")),
            "cps" => Row(9200, (48, "65536")),
            "decimal-gdamage" => Row(9200, (10, "1.5")), // Double branch succeeds, later raw Int32 parse fails.
            "cooldown-override" => Row(10415, (33, "bad")), // Override800 does not bypass the original parse.
            "empty-name" => Row(9200, (3, "")), // RemoveEmptyEntries shifts fields; no empty-column repair.
            "blank" => "",
            _ => throw new InvalidOperationException("Unknown owned magic fault fixture.")
        };
        fixture.WriteRows(Row(1250, (3, "Published first"), (22, "420")), bad, Row(6010));

        var error = Record.Exception(fixture.Magic.Load);

        Assert.NotNull(error);
        Assert.Equal(errorType, error.GetType());
        Assert.Single(fixture.Magic);
        Assert.Equal("Published first", fixture.Magic[1250][0].Name);
        Assert.Equal(new ushort[] { 1250 }, fixture.Weapons[420]);
        Assert.False(fixture.Magic.ContainsKey(9200));
        Assert.False(fixture.Magic.ContainsKey(10415));
        Assert.False(fixture.Magic.ContainsKey(6010));
        Assert.False(fixture.Magic.ContainsKey(6009));
    }

    [Fact]
    public void RepeatedLoadKeepsFirstRowsAndExtendsTheLiveAliasBeforeDuplicate6009FinallyThrows()
    {
        using var fixture = new NativeMagicTypeTestFixture();
        fixture.WriteRows(Row(1250, (3, "First"), (22, "420")), Row(6010));
        fixture.Magic.Load();
        var first = fixture.Magic[1250][0];
        var shared = fixture.Magic[6010];
        var original6010 = shared[0];
        var primary = fixture.Magic;
        var weapons = fixture.Weapons;
        fixture.WriteRows(Row(1250, (3, "Ignored replacement"), (22, "561530")),
            Row(6010, (8, "1"), (3, "New aliased level")), Row(9201, (3, "Published before final fault")));

        Assert.Throws<ArgumentException>(fixture.Magic.Load);

        Assert.Same(primary, LegacyNativePoolMagic.Magic);
        Assert.Same(weapons, LegacyNativePoolMagic.WeaponSpells);
        Assert.Same(first, fixture.Magic[1250][0]);
        Assert.Equal("First", first.Name);
        Assert.Equal(420u, first.WeaponType);
        Assert.Same(shared, fixture.Magic[6009]);
        Assert.Same(shared, fixture.Magic[6010]);
        Assert.Same(original6010, shared[0]);
        Assert.Equal("New aliased level", fixture.Magic[6009][1].Name);
        Assert.Same(fixture.Magic[6010][1], fixture.Magic[6009][1]);
        Assert.Equal("Published before final fault", fixture.Magic[9201][0].Name);
        Assert.Equal(new ushort[] { 1250 }, fixture.Weapons[420]);
        Assert.Equal(new ushort[] { 1250 }, fixture.Weapons[530]);
        Assert.Equal(new ushort[] { 1250 }, fixture.Weapons[561]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingTextFileStillAttemptsTheUnconditional6010To6009Alias(bool has6010)
    {
        using var fixture = new NativeMagicTypeTestFixture();
        var levels = new Dictionary<ushort, MagicType.Magic> { [7] = new() { ID = 6010, Level = 7 } };
        if (has6010) fixture.Magic[6010] = levels;
        Assert.False(File.Exists(fixture.DataPath));

        if (has6010)
        {
            fixture.Magic.Load();
            Assert.Equal(2, fixture.Magic.Count);
            Assert.Same(levels, fixture.Magic[6009]);
            Assert.Same(levels[7], fixture.Magic[6009][7]);
        }
        else
        {
            Assert.Throws<KeyNotFoundException>(fixture.Magic.Load);
            Assert.Empty(fixture.Magic);
        }
        Assert.Empty(fixture.Weapons);
        Assert.False(File.Exists(fixture.DataPath));
    }

    [Fact]
    public void FileWithout6010KeepsCompletedRowsBeforeTheFinalAliasLookupFault()
    {
        using var fixture = new NativeMagicTypeTestFixture();
        fixture.WriteRows(Row(1250, (22, "420")));

        Assert.Throws<KeyNotFoundException>(fixture.Magic.Load);

        Assert.Single(fixture.Magic);
        Assert.Equal((ushort)1250, fixture.Magic[1250][0].ID);
        Assert.Equal(new ushort[] { 1250 }, fixture.Weapons[420]);
        Assert.False(fixture.Magic.ContainsKey(6009));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingSecondaryCacheOrListFaultsAfterPrimaryPublicationAndBeforeTheNextRow(bool missingList)
    {
        using var fixture = new NativeMagicTypeTestFixture();
        fixture.WriteRows(Row(1250, (22, "420")), Row(6010));
        if (missingList) fixture.Weapons[420] = null!;
        else LegacyNativePoolMagic.WeaponSpells = null!;

        Assert.Throws<NullReferenceException>(fixture.Magic.Load);

        Assert.Single(fixture.Magic);
        Assert.Equal((ushort)1250, fixture.Magic[1250][0].ID);
        Assert.False(fixture.Magic.ContainsKey(6010));
        Assert.False(fixture.Magic.ContainsKey(6009));
        if (missingList)
        {
            Assert.Same(fixture.Weapons, LegacyNativePoolMagic.WeaponSpells);
            Assert.Null(fixture.Weapons[420]);
        }
        else Assert.Null(LegacyNativePoolMagic.WeaponSpells);
    }
}
