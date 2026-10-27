using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    public static IEnumerable<object[]> InvalidPrestigeLoadRows()
    {
        var row = CompletePrestigeLoadRow(99, total: 1);
        yield return ["short", "99/Short/4/"];
        yield return ["missing-terminal-delimiter", row[..^1]];
        yield return ["extra-terminal-delimiter", row + "/"];
        yield return ["missing-component", ChangePrestigeLoadField(row, 21, "3")];
        yield return ["extra-component", ChangePrestigeLoadField(row, 21, "1")];
        yield return ["negative-component-count", ChangePrestigeLoadField(row, 21, "-1")];
        yield return ["component-count-overflow", ChangePrestigeLoadField(row, 21, "2147483648")];
        yield return ["nul-terminated-component-count", ChangePrestigeLoadField(row, 21, "2\0")];
        yield return ["zero-uid", ChangePrestigeLoadField(row, 0, "0")];
        yield return ["negative-uid", ChangePrestigeLoadField(row, 0, "-1")];
        yield return ["uid-overflow", ChangePrestigeLoadField(row, 0, "4294967296")];
        yield return ["nul-terminated-uid", ChangePrestigeLoadField(row, 0, "99\0")];
        yield return ["world-is-not-a-persisted-class", ChangePrestigeLoadField(row, 2, "0")];
        yield return ["category-outside-range", ChangePrestigeLoadField(row, 2, "12")];
        yield return ["category-cannot-byte-wrap", ChangePrestigeLoadField(row, 2, "256")];
        yield return ["level-overflow", ChangePrestigeLoadField(row, 4, "256")];
        yield return ["level-negative", ChangePrestigeLoadField(row, 4, "-1")];
        yield return ["unsigned-negative", ChangePrestigeLoadField(row, 5, "-1")];
        yield return ["unsigned-overflow", ChangePrestigeLoadField(row, 5, "4294967296")];
        yield return ["unsigned-leading-plus", ChangePrestigeLoadField(row, 5, "+7")];
        yield return ["unsigned-surrounding-space", ChangePrestigeLoadField(row, 5, " 7 ")];
        yield return ["nul-terminated-unsigned-field", ChangePrestigeLoadField(row, 5, "707\0")];
        yield return ["point-negative", ChangePrestigeLoadField(row, 22, "-1")];
        yield return ["point-overflow", ChangePrestigeLoadField(row, 23, "4294967296")];
        yield return ["nul-terminated-point", ChangePrestigeLoadField(row, 23, "202\0")];
        yield return ["empty-name", ChangePrestigeLoadField(row, 1, "")];
        yield return ["blank-name", ChangePrestigeLoadField(row, 1, " ")];
        yield return ["name-control", ChangePrestigeLoadField(row, 1, "Bad\0Name")];
        yield return ["guild-control", ChangePrestigeLoadField(row, 20, "Bad\tGuild")];
        yield return ["name-delimiter", ChangePrestigeLoadField(row, 1, "Bad/Name")];
        yield return ["guild-delimiter", ChangePrestigeLoadField(row, 20, "Bad/Guild")];
    }

    [Theory]
    [MemberData(nameof(InvalidPrestigeLoadRows))]
    public void LoadSafetyInvalidLowScoreTailCannotUseDefaultsOrEscapeCompleteValidation(string _, string row) =>
        RunOwnedPrestigeLoad(files => RequireRejectedPrestigeText(files, Text(FullRow, row)));

    public static IEnumerable<object[]> PrestigeLoadNumericColumns()
    {
        foreach (var index in Enumerable.Range(0, 24).Where(index => index is not 1 and not 20))
            yield return [index];
    }

    [Theory]
    [MemberData(nameof(PrestigeLoadNumericColumns))]
    public void LoadSafetyEverySavedNumericColumnMustBeParsedRatherThanSkippedOrDefaulted(int index) =>
        RunOwnedPrestigeLoad(files => RequireRejectedPrestigeText(files,
            Text(FullRow, ChangePrestigeLoadField(CompletePrestigeLoadRow(99, 1), index, "not-a-number"))));

    [Fact]
    public void LoadSafetyDuplicateUidInAnotherClassBelowTheThresholdStillRefusesTheCompleteFile() =>
        RunOwnedPrestigeLoad(files => RequireRejectedPrestigeText(files,
            Text(FullRow, CompletePrestigeLoadRow(41, total: 1, category: 3, name: "Duplicate"))));

    [Fact]
    public void LoadSafetyCorruptPrestigeTailBeyondClassAndWorldLimitsCannotBeIgnored() =>
        RunOwnedPrestigeLoad(files =>
        {
            var rows = Enumerable.Range(0, 110)
                .Select(index => CompletePrestigeLoadRow((uint)(1000 + index), (uint)(20_000 + index)))
                .Append(ChangePrestigeLoadField(CompletePrestigeLoadRow(9999, 1), 23, "4294967296"))
                .ToArray();
            RequireRejectedPrestigeText(files, Text(rows));
        });

    [Fact]
    public void LoadSafetyPrimitiveWidthsAndValidUnicodeSurviveWithoutSignedOrByteNarrowing() =>
        RunOwnedPrestigeLoad(files =>
        {
            const string name = "H\u00E9ro\U0001F409";
            var row = CompletePrestigeLoadRow(uint.MaxValue, uint.MaxValue, category: 11,
                name: name, guild: "", points: [0, uint.MaxValue]);
            foreach (var index in new[] { 3, 5 }.Concat(Enumerable.Range(7, 13)))
                row = ChangePrestigeLoadField(row, index, "4294967295");
            row = ChangePrestigeLoadField(row, 4, "255");
            var text = Text(row);
            files.Seed(text);
            Ranking.Load();
            var actual = Assert.Single(Ranking.Ranks[Ranking.Type.World].Items).Value;
            Assert.Equal(uint.MaxValue, actual.UID);
            Assert.Equal(name, actual.Name);
            Assert.Equal((Ranking.Type)11, actual.type);
            Assert.Equal(byte.MaxValue, actual.Level);
            Assert.Equal("", actual.GuildName);
            Assert.All(new[] { actual.Class, actual.Mesh, actual.TotalPoints, actual.HairStyle,
                actual.Head, actual.Garment, actual.LeftWeapon, actual.LefttWeaponAccessory,
                actual.RightWeapon, actual.RightWeaponAccessory, actual.MountArmor, actual.Armor,
                actual.Wing, actual.WingPlus, actual.Title, actual.Flag },
                value => Assert.Equal(uint.MaxValue, value));
            Assert.Equal(new uint[] { 0, uint.MaxValue }, actual.Points);
            Assert.Same(actual, Ranking.Ranks[(Ranking.Type)11].Items[uint.MaxValue]);
            files.Expect(text);
        });
}
