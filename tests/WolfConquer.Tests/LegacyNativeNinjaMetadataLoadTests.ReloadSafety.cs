using Metadata = WolfConquer.Core.World.LegacyNativeNinjaFile;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMetadataLoadTests
{
    [Theory]
    [InlineData(false, "format")]
    [InlineData(true, "format")]
    [InlineData(false, "short")]
    [InlineData(true, "short")]
    [InlineData(false, "duplicate")]
    [InlineData(true, "duplicate")]
    [InlineData(false, "oversized-line")]
    [InlineData(true, "oversized-line")]
    [InlineData(false, "blank-tail")]
    [InlineData(true, "blank-tail")]
    [InlineData(false, "locked")]
    [InlineData(true, "locked")]
    public void FailedReloadRetainsEveryPriorTableAndRowIdentityAndValue(bool second, string fault) => RunOwnedMetadata(files =>
    {
        files.Seed(false, Row(false)); files.Seed(true, Row(true, 21));
        Metadata.Load();
        AssertType(17); AssertImmortal(21);
        var before = new NinjaMetadataSnapshot();
        if (fault == "locked")
        {
            using var held = new FileStream(files.PathName(second), FileMode.Open, FileAccess.Read, FileShare.None);
            Assert.Throws<IOException>(() => Metadata.Load());
        }
        else
        {
            var tokens = second ? ImmortalTokens(23) : TypeTokens(19);
            var valid = Row(second, second ? 21U : 17U);
            var exception = typeof(InvalidDataException);
            string broken;
            switch (fault)
            {
                case "format": tokens[^1] = "bad"; broken = valid + "\n" + string.Join("@@", tokens); exception = typeof(FormatException); break;
                case "short": broken = valid + "\n" + string.Join("@@", tokens[..^1]); break;
                case "duplicate": broken = valid + "\n" + valid; exception = typeof(ArgumentException); break;
                case "oversized-line": broken = valid + "\n" + new string('1', 1024 * 1024 + 1); break;
                case "blank-tail": broken = valid + "\n\n"; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            files.Rewrite(second, broken);
            Assert.Throws(exception, () => Metadata.Load());
        }
        before.RequireUnchanged();
        Assert.Empty(files.Output.ToString());
    });

    [Fact]
    public void CompleteChangedReloadRemovesObsoleteKeysButPreservesAllEscapedPriorRows() => RunOwnedMetadata(files =>
    {
        files.Seed(false, Row(false)); files.Seed(true, Row(true, 21));
        Metadata.Load();
        var oldTypes = Metadata.gouyu_type; var oldImmortals = Metadata.gouyu_immortals;
        var oldType = oldTypes[17]; var oldImmortal = oldImmortals[21];
        files.Rewrite(false, Row(false, 19)); files.Rewrite(true, Row(true, 23));
        Metadata.Load();
        AssertType(19); AssertImmortal(23);
        Assert.False(Metadata.gouyu_type.ContainsKey(17)); Assert.False(Metadata.gouyu_immortals.ContainsKey(21));
        Assert.Same(oldType, Assert.Single(oldTypes).Value); Assert.Same(oldImmortal, Assert.Single(oldImmortals).Value);
        Assert.Equal(12U, oldType.Score); Assert.Equal(23U, oldImmortal.MaxHP);
        Assert.NotSame(oldTypes, Metadata.gouyu_type); Assert.NotSame(oldImmortals, Metadata.gouyu_immortals);
        Assert.Empty(files.Output.ToString());
    });

    [Fact]
    public void MissingBothInputsPreservesNaturalColdNullOwners() => RunOwnedMetadata(files =>
    {
        Metadata.gouyu_type = null!; Metadata.gouyu_immortals = null!;
        Metadata.Load();
        Assert.Null(Metadata.gouyu_type); Assert.Null(Metadata.gouyu_immortals);
        Assert.Equal("!File.Exists: " + files.PathName(false) + Environment.NewLine, files.Output.ToString());
    });

    [Fact]
    public void CompleteEmptyFilesReplaceBothPriorOwnersTogether() => RunOwnedMetadata(files =>
    {
        files.Seed(false, ""); files.Seed(true, "");
        Metadata.Load();
        Assert.Empty(Metadata.gouyu_type); Assert.Empty(Metadata.gouyu_immortals);
        Assert.NotSame(files.OldTypes, Metadata.gouyu_type); Assert.NotSame(files.OldImmortals, Metadata.gouyu_immortals);
        Assert.Equal(123U, files.OldTypeRow.Score); Assert.Equal(456U, files.OldImmortalRow.MaxHP);
        Assert.Empty(files.Output.ToString());
    });
}
