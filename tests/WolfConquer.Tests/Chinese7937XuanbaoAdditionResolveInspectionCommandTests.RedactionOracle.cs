using System.Text.Json;
using Xunit.Sdk;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoAdditionResolveInspectionCommandTests
{
    private const string DuplicateRowFailure = "AdditionParser:DuplicateRowId";

    [Fact]
    public void RedactionOracleAcceptsTheExactStaticDuplicateRowFailureInTheClosedRefusalSchema()
    {
        var fixture = new Inputs(); // Path data only; no Initialize, I/O or process.
        AssertRefusal(RedactionRefusal(DuplicateRowFailure), fixture, DuplicateRowFailure);
    }

    [Theory]
    [InlineData("RowId", "8")]
    [InlineData("rowID", "8")]
    [InlineData("Detail", "RowId")]
    [InlineData("Detail", "C:\\private\\RowId\\catalog.dat")]
    [InlineData("Detail", "AdditionParser:DuplicateRowId")]
    [InlineData("Status", "AdditionParser:DuplicateRowId")]
    public void RedactionOracleStillRejectsRowPropertiesValuesPathsAndMisplacedStaticCodes(string name, string text)
    {
        Assert.ThrowsAny<XunitException>(() => AssertNoDisclosure(
            RedactionRefusal(DuplicateRowFailure, name, text), new Inputs()));
    }

    [Fact]
    public void RedactionOracleDoesNotAllowTheStaticCodeAtANestedFailureProperty()
    {
        var result = RedactionRefusal(DuplicateRowFailure, "Metadata",
            new Dictionary<string, string> { ["Failure"] = DuplicateRowFailure });
        Assert.ThrowsAny<XunitException>(() => AssertNoDisclosure(result, new Inputs()));
    }

    [Fact]
    public void RedactionOracleChecksDecodedStringsInsteadOfOnlyTheirJsonEscapeSpelling()
    {
        var result = RedactionRefusal(DuplicateRowFailure, "Detail", "RowId");
        result = result with { Error = result.Error.Replace("\"RowId\"", "\"Row\\u0049d\"", StringComparison.Ordinal) };
        Assert.ThrowsAny<XunitException>(() => AssertNoDisclosure(result, new Inputs()));
    }

    [Fact]
    public void RedactionOracleDoesNotAllowAPrivateSuffixAfterTheDeclaredFailureCode()
    {
        Assert.ThrowsAny<XunitException>(() => AssertNoDisclosure(
            RedactionRefusal(DuplicateRowFailure + ":private-row"), new Inputs()));
    }

    [Theory]
    [InlineData("CommonNum")]
    [InlineData("SYNTHETIC-PRIVATE-CLI-SENTINEL")]
    [InlineData("2700000001")]
    [InlineData("@@")]
    public void StaticFailureExemptionDoesNotRelaxTheOtherPrivateDataChecks(string hidden)
    {
        Assert.ThrowsAny<XunitException>(() => AssertNoDisclosure(
            RedactionRefusal(DuplicateRowFailure, "Detail", hidden), new Inputs()));
    }

    [Fact]
    public void StaticFailureExemptionDoesNotRelaxOwnedPathOrClosedPropertyChecks()
    {
        var fixture = new Inputs();
        Assert.ThrowsAny<XunitException>(() => AssertNoDisclosure(
            RedactionRefusal(DuplicateRowFailure, "Detail", fixture.InfoPath), fixture));
        Assert.ThrowsAny<XunitException>(() => AssertRefusal(
            RedactionRefusal(DuplicateRowFailure, "UnexpectedSummary", 0), fixture, DuplicateRowFailure));
    }

    private static Result RedactionRefusal(string failure, string? extraName = null, object? extraValue = null)
    {
        Dictionary<string, object?> fields = new()
        {
            ["FormatVersion"] = 1,
            ["ClientVersion"] = 7937,
            ["Status"] = "refused",
            ["Failure"] = failure,
        };
        foreach (var flag in BoundaryFlags) fields.Add(flag, false);
        if (extraName is not null) fields[extraName] = extraValue;
        return new(1, string.Empty, JsonSerializer.Serialize(fields));
    }
}
