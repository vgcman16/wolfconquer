namespace WolfConquer.Tests;

public sealed partial class VendorListingIncarnationTests
{
    [Theory]
    [InlineData(false, "different-type")] [InlineData(true, "different-type")]
    [InlineData(false, "equal-root")] [InlineData(true, "equal-root")]
    [InlineData(false, "root-value")] [InlineData(true, "root-value")]
    [InlineData(false, "child-value")] [InlineData(true, "child-value")]
    [InlineData(false, "equal-child")] [InlineData(true, "equal-child")]
    [InlineData(false, "child-order")] [InlineData(true, "child-order")]
    [InlineData(false, "child-added")] [InlineData(true, "child-added")]
    public async Task RegisteredDetachedStaleListingCannotShowOrSellADifferentItemIncarnation(bool cp, string mutation)
    {
        using var f = new ComponentFixture(cp);
        await f.StartAndListAsync();
        f.ChangeCurrentItem(mutation);
        var before = Before.Capture(f);

        var show = await f.ShowAsync();
        Assert.Contains("seller-listed-item-incarnation-changed", show, StringComparison.Ordinal);
        AssertUnchanged(f, before);
        var buy = await f.BuyAsync();
        Assert.Contains("seller-listed-item-incarnation-changed", buy, StringComparison.Ordinal);
        AssertUnchanged(f, before);
        var replay = await f.BuyAsync();
        Assert.Contains("seller-listed-item-incarnation-changed", replay, StringComparison.Ordinal);
        AssertUnchanged(f, before);
    }
}
