using System.Text;
using WolfConquer.Core.World;
using Metadata = WolfConquer.Core.World.LegacyNativeNinjaFile;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMetadataLoadTests
{
    [Theory]
    [InlineData("utf8")]
    [InlineData("utf16")]
    [InlineData("utf32")]
    public void ValidBomInputsRetainOpaqueChineseColumnsAndCompleteKnownNumericRows(string encodingName) => RunOwnedMetadata(files =>
    {
        Encoding encoding = encodingName switch
        { "utf8" => new UTF8Encoding(true), "utf16" => Encoding.Unicode, "utf32" => Encoding.UTF32, _ => throw new ArgumentOutOfRangeException() };
        var first = TypeTokens(); first[10] = "秘术";
        var second = ImmortalTokens(); second[9] = "忍术"; second[10] = "属性"; second[11] = "说明";
        byte[] Bytes(string text) => encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        files.SeedBytes(false, Bytes(string.Join("@@", first)));
        files.SeedBytes(true, Bytes(string.Join("@@", second)));
        Metadata.Load();
        AssertType(17); AssertImmortal(21);
        Assert.Empty(files.Output.ToString());
    });

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void SourceNewlineFormsRetainFileOrderAndBothCompleteTables(string newline) => RunOwnedMetadata(files =>
    {
        files.Seed(false, Row(false) + newline + Row(false, 19) + newline);
        files.Seed(true, Row(true, 21) + newline + Row(true, 23) + newline);
        Metadata.Load();
        Assert.Equal(new uint[] { 17, 19 }, Metadata.gouyu_type.Keys);
        Assert.Equal(new uint[] { 21, 23 }, Metadata.gouyu_immortals.Keys);
        AssertType(17); AssertType(19); AssertImmortal(21); AssertImmortal(23);
        Assert.Empty(files.Output.ToString());
    });

    [Fact]
    public void EqualItemAndLevelRowsWithDistinctIdsRetainFirstFileMatchForActualNativeConsumer() => RunOwnedMetadata(files =>
    {
        var later = TypeTokens(19); later[11] = "99";
        files.Seed(false, Row(false) + "\n" + string.Join("@@", later));
        files.Seed(true, Row(true, 21));
        Metadata.Load();
        var item = new LegacyNativeNinja.Item { ItemID = 100, Level = 3 };
        Assert.Same(Metadata.gouyu_type[17], item.DBItem);
        Assert.Equal(12U, item.DBItem.Score); Assert.Equal(-9, item.DBItem.Power);
        var ninja = new LegacyNativeNinja();
        ninja.Items.TryAdd(100, item);
        Assert.Equal(12U, ninja.Score);
        Metadata.gouyu_type[17].Score = 40;
        Assert.Equal(40U, ninja.Score); // Public live metadata behavior is retained; no immutable-authority claim.
        Assert.Equal(99U, Metadata.gouyu_type[19].Score);
        Assert.Empty(files.Output.ToString());
    });
}
