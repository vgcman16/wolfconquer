using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// DATA only. An adopted family is not a registered monster or live range owner.
public sealed class LegacyMonsterPhysicalRangeMetadataTests
{
    [Theory]
    [InlineData("0", "5000", "-128", -128)]
    [InlineData("0", "5000", "127", 127)]
    [InlineData("1", "100000", "50", 3)]
    [InlineData("1", "100001", "50", 12)]
    [InlineData("2", "6999999", "50", 12)]
    [InlineData("1", "7000000", "50", 3)]
    public void ActualLoaderThenCopyRulesKeepTheirWidthsAndOpenBounds(string boss, string hp, string range, int expected)
    {
        var family = Family(boss, hp, range); var catalog = Catalog(family);
        var proof = new LegacyMonsterPhysicalRangeMetadata(catalog, family);
        Assert.Equal(expected, proof.BirthRange); Assert.True(proof.IsCurrentPlain(catalog, family));
        Assert.Equal(int.Parse(hp, CultureInfo.InvariantCulture), proof.MaximumHitpoints);
    }

    [Theory]
    [InlineData("256", "5000", "1")]
    [InlineData("0", "2147483648", "1")]
    [InlineData("0", "0", "1")]
    [InlineData("0", "5000", "128")]
    [InlineData("0", "5000", "-129")]
    [InlineData("0", "5000", "bad")]
    public void RequiredMalformedFamilyNumbersRefuseInsteadOfBecomingDefaultRange(string boss, string hp, string range)
    {
        var family = Family(boss, hp, range);
        Assert.Throws<InvalidDataException>(() => new LegacyMonsterPhysicalRangeMetadata(Catalog(family), family));
    }

    [Fact]
    public void DeclaredAbsentLoaderFieldsUseOnlyTheirObservedBirthDefaults()
    {
        var family = Family("0", "5000", "1");
        var fields = Assert.IsType<Dictionary<string, string>>(family.Attributes);
        fields.Remove("Boss"); fields.Remove("attack_range");
        var proof = new LegacyMonsterPhysicalRangeMetadata(Catalog(family), family);
        Assert.Equal((byte)0, proof.Boss); Assert.Equal((sbyte)0, proof.BirthRange);
    }

    [Fact]
    public void CatalogRowIdentityAndEscapedAttributeChangesCannotRemainCurrent()
    {
        var family = Family("0", "5000", "2"); var catalog = Catalog(family);
        var proof = new LegacyMonsterPhysicalRangeMetadata(catalog, family);
        Assert.Throws<InvalidDataException>(() => new LegacyMonsterPhysicalRangeMetadata(catalog,
            family with { Attributes = new Dictionary<string, string>(family.Attributes) }));
        Assert.IsType<Dictionary<string, string>>(family.Attributes)["attack_range"] = "3";
        Assert.False(proof.IsCurrentPlain(catalog)); Assert.Equal((sbyte)2, proof.BirthRange);
    }

    private static WorldDataCatalog Catalog(Monster family) => LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family });
    private static Monster Family(string boss, string hp, string range) => new(1000, "RangeData",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { ["id"] = "1000", ["life"] = hp, ["Boss"] = boss, ["attack_range"] = range }, []);
}
