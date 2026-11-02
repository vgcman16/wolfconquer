using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoResolveTests
{
    [Theory]
    [InlineData("nil")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("and")]
    [InlineData("or")]
    [InlineData("not")]
    [InlineData("local")]
    [InlineData("in")]
    public void SelectedUnloadRefusesReservedWordsAsDirectResetNames(string reserved)
    {
        var module = Chinese7937XuanbaoResolveFixture.Module().Replace(
            "OtherInfo = nil", reserved + " = nil", StringComparison.Ordinal);
        Assert.Equal(Chinese7937XuanbaoInfoFailure.UnsupportedSyntax,
            Chinese7937XuanbaoResolveFixture.Refused(module));
    }

    [Fact]
    public void SelectedUnloadRetainsAnOrdinaryIdentifierResetAndItsCompleteRelationships()
    {
        var module = Chinese7937XuanbaoResolveFixture.Module().Replace(
            "OtherInfo = nil", "_otherReset42 = nil", StringComparison.Ordinal);
        var snapshot = Chinese7937XuanbaoResolveFixture.Parse(module);
        Assert.Equal(3, snapshot.Fields.Length);
        Assert.Equal(7U, snapshot.SelectNumber(false));
        Assert.Equal(17U, snapshot.SelectNumber(true));
        Assert.Equal(123U, snapshot.MaterialIdField);
    }
}
