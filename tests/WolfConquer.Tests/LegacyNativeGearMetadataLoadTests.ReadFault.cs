namespace WolfConquer.Tests;

public sealed partial class LegacyNativeGearMetadataLoadTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RealReadSharingFailureIsSwallowedAfterPublishingANewEmptyCache(int kind)
    {
        using var fixture = new LoadFixture();
        var prior = LoadFixture.Current(kind);
        var priorRow = LoadFixture.Seed(kind, 999);
        fixture.Write(kind, LoadFixture.Row(kind, 1));
        // Actual Windows file-sharing failure on a GUID-owned fixture, no callback
        // or production fault provider. File.Exists succeeds before ReadAllLines.
        using var locked = new FileStream(fixture.PathFor(kind), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Null(Record.Exception(() => fixture.Load(kind)));

        Assert.NotSame(prior, LoadFixture.Current(kind));
        Assert.Equal(0, LoadFixture.Current(kind).Count);
        Assert.Same(priorRow, prior[999u]);
        Assert.Equal(1, prior.Count);
        Assert.Contains(typeof(IOException).FullName!, fixture.Log);
        Assert.DoesNotContain("!File.Exists:", fixture.Log);
    }
}
