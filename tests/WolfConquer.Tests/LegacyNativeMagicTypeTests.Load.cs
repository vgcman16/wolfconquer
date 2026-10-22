using System.Globalization;
using WolfConquer.Core.World;
using MagicType = WolfConquer.Core.Data.LegacyNativeMagicType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMagicTypeTests
{
    [Fact]
    public void LoadPopulatesEverySourceRowFieldAndPublishesTheExact6009DictionaryAlias()
    {
        using var fixture = new NativeMagicTypeTestFixture();
        fixture.Effect(9200, "IsRuneSkill", "1");
        fixture.Effect(9200, "DescEx", "A Passive effect");
        fixture.WriteRows(Row(9200, (2, "87"), (3, "Full row"), (8, "255"), (9, "65535"),
            (10, "12345"), (12, "-7"), (13, "4294967295"), (14, "65535"), (15, "255"),
            (16, "-2147483648"), (18, "4294967295"), (20, "65535"), (22, "4294967295"),
            (29, "255"), (30, "255"), (33, "65535"), (35, "-2147483648"), (36, "2147483647"),
            (37, "-99"), (43, "-3"), (44, "123"), (47, "500"), (48, "65535")), Row(6010));

        fixture.Magic.Load();

        var row = fixture.Magic[9200][255];
        Assert.Equal((ushort)9200, row.ID);
        Assert.Equal("Full row", row.Name);
        Assert.Equal(MagicType.MagicSort.Rectangle, row.Type);
        Assert.Equal(byte.MaxValue, row.Level);
        Assert.Equal(ushort.MaxValue, row.UseMana);
        Assert.Equal(345f, row.Damage);
        Assert.Equal(-7, row.Rate);
        Assert.Equal(uint.MaxValue, row.Experience);
        Assert.Equal(byte.MaxValue, row.Range);
        Assert.Equal((ushort)5100, row.Sector);
        Assert.Equal(uint.MaxValue, row.Duration);
        Assert.Equal(uint.MaxValue, row.WeaponType);
        Assert.Equal(byte.MaxValue, row.UseStamina);
        Assert.Equal(0u, row.GiveHitPoints);
        Assert.True(row.AttackInFly);
        Assert.False(row.isTrojanArchiveSkill);
        Assert.Equal(int.MinValue, row.Status);
        Assert.Equal(ushort.MaxValue, row.NeedLevel);
        Assert.Equal(65535u, row.CpsCost);
        Assert.Equal(byte.MaxValue, row.MaxTargets);
        Assert.Equal((byte)0, row.IncreaseStamin);
        Assert.Equal(ushort.MaxValue, row.CoolDown);
        Assert.Equal(byte.MaxValue, row.AutoLearn);
        Assert.Equal(500, row.ColdTime);
        Assert.True(row.IsSpellWithColdTime);
        Assert.Equal(int.MaxValue, row.Damage2);
        Assert.Equal(-99, row.Damage3);
        Assert.Equal(int.MinValue, row.DamageOnHuman);
        Assert.Equal(123, row.DamageOnMonster);
        Assert.True(row.Passive);
        Assert.True(row.isRuneSkill);
        Assert.Equal(-3, row.width);
        Assert.Equal(12345, row.GDamage);
        Assert.Equal(3, fixture.Magic.Count);
        Assert.Same(fixture.Magic[6010], fixture.Magic[6009]);
        Assert.Same(fixture.Magic[6010][0], fixture.Magic[6009][0]);
        Assert.False(fixture.Magic[6010][0].AttackInFly);
        Assert.Empty(fixture.Weapons); // 9200 is not in the native weapon-index switch.
        Assert.Same(fixture.Extra, MagicType.Weaponextra);
        Assert.Equal(new ushort[] { 99 }, fixture.Extra);
    }

    [Fact]
    public void NativeIniRuneByteAndPassiveSubstringRulesKeepTruncationCaseAndArchiveBoundaries()
    {
        using var fixture = new NativeMagicTypeTestFixture();
        fixture.Effect(9200, "IsRuneSkill", "00001junk"); // ReadByte size6 retains only five chars.
        fixture.Effect(9200, "DescEx", "prefixPassiveSuffix");
        fixture.Effect(9201, "IsRuneSkill", "256"); // Parse overflow falls back to0.
        fixture.Effect(9201, "DescEx", "passive");
        fixture.Effect(9202, "IsRuneSkill", "000001");
        fixture.Effect(9202, "DescEx", new string('X', 247) + "Passive"); // Exactly254 chars.
        fixture.Effect(9203, "IsRuneSkill", "bad");
        fixture.Effect(9203, "DescEx", new string('X', 248) + "Passive"); // ReadString255 cuts the final e.
        fixture.Effect(9204, "IsRuneSkill", "2");
        fixture.WriteRows(Row(9200, (8, "3")), Row(9201), Row(9202), Row(9203), Row(9204),
            Row(15859), Row(15860), Row(15940), Row(15941), Row(6010));

        fixture.Magic.Load();

        Assert.True(fixture.Magic[9200][3].isRuneSkill); // Section is ID+00, not ID+level.
        Assert.True(fixture.Magic[9200][3].Passive);
        Assert.False(fixture.Magic[9201][0].isRuneSkill);
        Assert.False(fixture.Magic[9201][0].Passive);
        Assert.False(fixture.Magic[9202][0].isRuneSkill);
        Assert.True(fixture.Magic[9202][0].Passive);
        Assert.False(fixture.Magic[9203][0].isRuneSkill);
        Assert.False(fixture.Magic[9203][0].Passive);
        Assert.True(fixture.Magic[9204][0].isRuneSkill);
        foreach (ushort id in new ushort[] { 15860, 15940 })
        {
            Assert.True(fixture.Magic[id][0].isTrojanArchiveSkill);
            Assert.True(fixture.Magic[id][0].Passive);
        }
        foreach (ushort id in new ushort[] { 15859, 15941 })
        {
            Assert.False(fixture.Magic[id][0].isTrojanArchiveSkill);
            Assert.False(fixture.Magic[id][0].Passive);
        }
    }

    [Fact]
    public void DamageBranchesAndEveryIdOverrideRetainIndependentRawGDamage()
    {
        using var fixture = new NativeMagicTypeTestFixture();
        var cases = new List<(ushort Id, byte Type, int Raw, float Expected)>();
        foreach (ushort id in new ushort[] { 13030, 15560, 14320, 12200, 15440, 15470, 14500 })
            cases.Add((id, 4, 20050, 20050));
        cases.AddRange(new (ushort, byte, int, float)[]
        {
            (9200, 4, 20050, 50), (9201, 4, 2800, 500), (9202, 4, -1250, -250),
            (9203, 1, 9999, 9999), (9204, 1, 10001, 1), (9205, 2, 9999, 9999),
            (9206, 20, 9999, 9999), (9207, 51, 20050, 20050), (9208, 4, 0, 100),
            (6001, 4, 12345, 45), (12580, 4, 800, 100), (12590, 4, 800, 100), (12600, 4, 800, 100),
            (11140, 4, 0, 150), (10490, 4, 0, 150), (3306, 4, 0, 1), (12380, 4, 0, 5000),
            (12070, 4, 0, 104), (10415, 4, 101, 101), (7030, 4, 101, 101)
        });
        var rows = cases.Select(value => Row(value.Id,
            (2, value.Type.ToString(CultureInfo.InvariantCulture)), (10, value.Raw.ToString(CultureInfo.InvariantCulture)),
            (12, "-9"), (13, value.Id == 6001 ? "not-a-uint" : "37"), (33, "999"))).ToList();
        rows.Add(Row(6010));
        fixture.WriteRows(rows.ToArray());

        fixture.Magic.Load();

        foreach (var expected in cases)
        {
            var row = fixture.Magic[expected.Id][0];
            Assert.True(row.Damage == expected.Expected, "Native damage branch ID=" + expected.Id);
            Assert.Equal(expected.Raw, row.GDamage);
            Assert.Equal(expected.Id == 6001 ? 20u : 37u, row.Duration);
            Assert.Equal(expected.Id == 10415 ? (ushort)800 : (ushort)999, row.CoolDown);
            Assert.Equal(expected.Id == 7030 ? 21 : -9, row.Rate);
        }
    }

    [Fact]
    public void FirstPrimaryRowWinsButDuplicateRowsStillExtendTheLiveWeaponIndexInSourceOrder()
    {
        using var fixture = new NativeMagicTypeTestFixture();
        fixture.WriteRows(Row(1250, (3, "First"), (22, "410420")),
            Row(1250, (3, "Ignored"), (22, "530561")), Row(1260, (22, "410420")),
            Row(1250, (8, "1"), (22, "410420")), Row(9200, (22, "777666")),
            Row(1290, (22, "50000")), Row(1300, (22, "0")), Row(5010, (22, "60000")),
            Row(5020, (22, "410410")), Row(6010));

        fixture.Magic.Load();

        Assert.Equal("First", fixture.Magic[1250][0].Name);
        Assert.Equal(410420u, fixture.Magic[1250][0].WeaponType);
        Assert.Equal(2, fixture.Magic[1250].Count);
        Assert.Equal(new ushort[] { 1250, 1260 }, fixture.Weapons[420]);
        Assert.Equal(new ushort[] { 1250, 1260, 5020 }, fixture.Weapons[410]);
        Assert.Equal(new ushort[] { 1250 }, fixture.Weapons[561]);
        Assert.Equal(new ushort[] { 1250 }, fixture.Weapons[530]);
        Assert.Equal(new ushort[] { 5010 }, fixture.Weapons[60]);
        Assert.Equal(new ushort[] { 60, 410, 420, 530, 561 }, fixture.Weapons.Keys.OrderBy(id => id));
        Assert.False(fixture.Weapons.ContainsKey(614)); // Source's num==60000 branch cannot follow %1000.
        Assert.Equal(new ushort[] { 99 }, fixture.Extra);
    }

    [Fact]
    public void LoadingAnIndependentReceiverDoesNotReplaceTheGlobalMagicOwner()
    {
        using var fixture = new NativeMagicTypeTestFixture();
        var receiver = new MagicType();
        fixture.WriteRows(Row(1250, (22, "420")), Row(6010));

        receiver.Load();

        Assert.Same(fixture.Magic, LegacyNativePoolMagic.Magic);
        Assert.Empty(fixture.Magic);
        Assert.Equal(3, receiver.Count);
        Assert.Same(receiver[6010], receiver[6009]);
        Assert.Equal(new ushort[] { 1250 }, fixture.Weapons[420]);
    }

    [Fact]
    public void LoadUsesCurrentCultureForRowParsersAndTheGenuineIniByteCallback()
    {
        using var fixture = new NativeMagicTypeTestFixture();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        culture.NumberFormat.NegativeSign = "m";
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        fixture.Effect(9200, "IsRuneSkill", "p1");
        fixture.WriteRows(Row(9200, (1, "p9200"), (10, "p123"), (12, "m7"), (35, "m23"), (48, "p321")), Row(6010));

        fixture.Magic.Load();

        var row = fixture.Magic[9200][0];
        Assert.Equal(123f, row.Damage);
        Assert.Equal(123, row.GDamage);
        Assert.Equal(-7, row.Rate);
        Assert.Equal(-23, row.DamageOnHuman);
        Assert.Equal(321u, row.CpsCost);
        Assert.True(row.isRuneSkill);
        Assert.Same(culture, CultureInfo.CurrentCulture);
    }

    [Fact]
    public void RemoveEmptyEntriesKeepsLeadingTrailingSeparatorsAndExtraUnusedFieldsAccepted()
    {
        using var fixture = new NativeMagicTypeTestFixture();
        fixture.WriteRows("@@@@" + Row(9200, (3, "Untrimmed name"), (2, "255")) + "@@unused@@",
            Row(9201, (5, "2")), Row(6010));

        fixture.Magic.Load();

        Assert.Equal("Untrimmed name", fixture.Magic[9200][0].Name);
        Assert.Equal((MagicType.MagicSort)255, fixture.Magic[9200][0].Type);
        Assert.True(fixture.Magic[9200][0].AttackInFly);
        Assert.False(fixture.Magic[9201][0].AttackInFly);
        Assert.False(fixture.Magic[6010][0].AttackInFly);
    }
}
