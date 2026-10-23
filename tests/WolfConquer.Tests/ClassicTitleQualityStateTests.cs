using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class ClassicTitleQualityStateTests
{
    [Fact]
    public void CompleteByteDomainKeepsFirstOwnedOrderAndRejectsAnOverflowingTail()
    {
        var expected = Enumerable.Range(1, byte.MaxValue).Select(value => (byte)value).ToArray();
        var text = "0;" + string.Join(',', expected.Select(value => value.ToString(CultureInfo.InvariantCulture))) + "/0/7/";
        var fields = new Dictionary<string, string> { ["MyTitle"] = "255", ["Titles"] = text };
        Assert.True(LegacyClassicTitleState.TryRead(fields, out var selected, out var owned));
        Assert.Equal(byte.MaxValue, selected);
        Assert.Equal(expected, owned);
        fields["Titles"] += "256";
        Assert.False(LegacyClassicTitleState.TryRead(fields, out _, out owned));
        Assert.Empty(owned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttributeCaptureValidatesTheEntireTailNotOnlyTheTwoTitleKeys(bool duplicate)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["MyTitle"] = "7", ["Titles"] = "2,5,7", ["Name"] = "synthetic-owner" };
        fields.Add(duplicate ? "NAME" : "UnrelatedTail", duplicate ? "different-value" : null!);
        Assert.False(LegacyClassicTitleState.TryRead(fields, out _, out var owned));
        Assert.Empty(owned);
    }

    [Fact]
    public void OversizedButOtherwiseValidWhitespaceTailCannotRetainAuthorizationPrefix()
    {
        var fields = new Dictionary<string, string>
        { ["MyTitle"] = "7", ["Titles"] = "7" + new string(' ', CharacterSnapshotFileBaseline.MaximumBytes) };
        Assert.False(LegacyClassicTitleState.TryRead(fields, out _, out var owned));
        Assert.Empty(owned);
    }
}
