namespace WolfConquer.Tests;

public sealed partial class LegacyNativeGearMetadataLoadTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RealReadSharingFailureIsSwallowedWithoutChangingThePriorCache(int kind)
    {
        using var fixture = new LoadFixture();
        var prior = LoadFixture.Current(kind);
        var priorRow = LoadFixture.Seed(kind, 999);
        var values = LoadFixture.Values(kind, priorRow).ToArray();
        fixture.Write(kind, LoadFixture.Row(kind, 1));
        // Actual Windows file-sharing failure on a GUID-owned fixture, no callback
        // or production fault provider; preparation cannot open this held file.
        using var locked = new FileStream(fixture.PathFor(kind), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Null(Record.Exception(() => fixture.Load(kind)));

        Assert.Same(prior, LoadFixture.Current(kind));
        Assert.Single(LoadFixture.Current(kind));
        Assert.Same(priorRow, prior[999u]);
        Assert.Equal(values, LoadFixture.Values(kind, priorRow));
        Assert.Single(prior);
        Assert.Contains(typeof(IOException).FullName!, fixture.Log);
        Assert.DoesNotContain("!File.Exists:", fixture.Log);
    }
}
