using System.Globalization;
using WolfConquer.Core.World;
using MagicType = WolfConquer.Core.Data.LegacyNativeMagicType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMagicTypeTests
{
    [Fact]
    public void AttackInFlayUsesTheExactIdRangesRegardlessOfTheRowFlagOrSort()
    {
        var magic = new MagicType();
        var forbidden = new ushort[]
        {
            1045, 1046, 1250, 1260, 1290, 6000, 6001, 10315, 10381, 10415, 10490,
            11000, 11005, 11040, 11070, 11110, 11140, 11170, 11180, 11190, 11230, 6010
        }.Concat(Enumerable.Range(5010, 41).Select(id => (ushort)id))
            .Concat(Enumerable.Range(7000, 41).Select(id => (ushort)id));
        foreach (ushort id in forbidden)
            Assert.False(magic.AttackInFlay(new() { ID = id, AttackInFly = true, Type = MagicType.MagicSort.Attack }));
        foreach (ushort id in new ushort[] { 0, 1044, 1047, 5009, 5051, 5999, 6002, 6009, 6011, 6999, 7041, ushort.MaxValue })
            Assert.True(magic.AttackInFlay(new() { ID = id, AttackInFly = false, Type = MagicType.MagicSort.AttachStatus }));
        Assert.Throws<NullReferenceException>(() => magic.AttackInFlay(null!));
    }

    [Fact]
    public void GetSpellUsesActualRowIdsCaseSensitiveSubstringsAndTheLastEnumeratedMatch()
    {
        var first = new MagicType.Magic { ID = 17, Name = "Needle first" };
        var second = new MagicType.Magic { ID = 18, Name = "prefixNeedleSuffix" };
        var last = new MagicType.Magic { ID = 12, Name = "Last Needle" };
        var magic = new MagicType
        {
            [9999] = new() { [4] = first, [1] = second },
            [1] = new() { [0] = last },
            [2] = new() { [0] = new() { ID = 99, Name = null! } }
        };
        Assert.Equal(12u, magic.GetSpell("Needle"));
        Assert.Equal(18u, magic.GetSpell("prefixNeedle"));
        Assert.Equal(0u, magic.GetSpell("needle"));
        Assert.Equal(12u, magic.GetSpell(""));
        Assert.Equal(0u, magic.GetSpell("not found"));
        last.Name = "No match now";
        Assert.Equal(18u, magic.GetSpell("Needle"));
        magic[1][0] = new() { ID = 7, Name = "Needle replacement" };
        Assert.Equal(7u, magic.GetSpell("Needle"));
        Assert.Same(first, magic[9999][4]);
        Assert.Equal(17u, first.ID);
    }

    [Fact]
    public void GetSpellNullSearchAndNullOwnersFollowNativeLazyDereferenceOrder()
    {
        var magic = new MagicType();
        Assert.Equal(0u, magic.GetSpell(null!)); // No prevalidation on an empty owner.
        magic[1] = new() { [0] = new() { Name = null! } };
        Assert.Equal(0u, magic.GetSpell(null!));
        magic[1][0].Name = "Actual name";
        Assert.Throws<ArgumentNullException>(() => magic.GetSpell(null!));
        magic[1][0] = null!;
        Assert.Throws<NullReferenceException>(() => magic.GetSpell("x"));
        magic[1] = null!;
        Assert.Throws<NullReferenceException>(() => magic.GetSpell("x"));
    }

    [Fact]
    public void OwnedFixtureRestoresExactCacheConfigurationAndCultureReferences()
    {
        var magic = LegacyNativePoolMagic.Magic;
        var weapons = LegacyNativePoolMagic.WeaponSpells;
        var extra = MagicType.Weaponextra;
        var location = LegacyNativeServerConfig.DbLocation;
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        string root;
        using (var fixture = new NativeMagicTypeTestFixture())
        {
            root = fixture.Root;
            Assert.NotSame(magic, LegacyNativePoolMagic.Magic);
            Assert.NotSame(weapons, LegacyNativePoolMagic.WeaponSpells);
            Assert.NotSame(extra, MagicType.Weaponextra);
            fixture.WriteRows(Row(6010));
            fixture.Effect(6010, "IsRuneSkill", "1");
            fixture.Magic.Load();
        }
        Assert.Same(magic, LegacyNativePoolMagic.Magic);
        Assert.Same(weapons, LegacyNativePoolMagic.WeaponSpells);
        Assert.Same(extra, MagicType.Weaponextra);
        Assert.Same(location, LegacyNativeServerConfig.DbLocation);
        Assert.Same(culture, CultureInfo.CurrentCulture);
        Assert.Same(uiCulture, CultureInfo.CurrentUICulture);
        Assert.False(Directory.Exists(root));
    }
}
