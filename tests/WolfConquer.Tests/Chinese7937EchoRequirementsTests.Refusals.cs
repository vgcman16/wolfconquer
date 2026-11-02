using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoRequirementsTests
{
    public static IEnumerable<object[]> EveryRequiredName() => Fields().Keys.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(EveryRequiredName))]
    public void MissingOrCaseChangedMemberRefusesTheCompleteProjection(string name)
    {
        var fields = Fields();
        var value = fields[name];
        fields.Remove(name);
        Refuses(Parse(fields), Chinese7937EchoRequirementsFailure.UnsupportedShape);
        fields.Add(name.ToUpperInvariant(), value);
        Refuses(Parse(fields), Chinese7937EchoRequirementsFailure.UnsupportedShape);
    }

    [Fact]
    public void AdditionalFieldCannotBeSilentlyDiscarded()
    {
        var fields = Fields();
        fields.Add("future_member", 0);
        Refuses(Parse(fields), Chinese7937EchoRequirementsFailure.UnsupportedShape);
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(8U)]
    [InlineData(4294967295U)]
    public void UnusableNamedMaximumRefusesWithoutTruncation(uint maximum) =>
        Refuses(Parse(Fields(maximum)), Chinese7937EchoRequirementsFailure.MaximumHoleBounds);

    [Theory]
    [InlineData(1U)]
    [InlineData(7U)]
    public void NamedMaximumIncludesBothKnownShapeEdges(uint maximum) =>
        Assert.Equal(maximum, Create(Parse(Fields(maximum))).CatalogMaximumHoleNumber);

    [Fact]
    public void OlderDeclaredInfoIsNotRelabeledAsCurrent7937() =>
        Refuses(Parse(Fields(), Chinese7937XuanbaoInfoVersion.Catalog7931),
            Chinese7937EchoRequirementsFailure.UnsupportedVersion);

    [Fact]
    public void MissingSourceReturnsExplicitFailure() =>
        Refuses(null!, Chinese7937EchoRequirementsFailure.SourceRequired);

    [Fact]
    public void LaterRefusalCannotChangeAnAlreadyCreatedProjection()
    {
        var requirements = Create(Parse(Fields()));
        var digest = requirements.Source.ContentSha256;
        var fields = Fields();
        fields.Remove("open_hole7_maxexp");
        Refuses(Parse(fields), Chinese7937EchoRequirementsFailure.UnsupportedShape);
        Assert.Equal(digest, requirements.Source.ContentSha256);
        Assert.True(requirements.TryGetOpening(7, out var row));
        Assert.Equal(708U, row.MaximumExperienceField);
    }

    [Fact]
    public void StrictSelectedParserRejectsDuplicateBeforeAnyTypedProjection()
    {
        var fields = Fields().ToList();
        fields.Add(new("cost_money_1", 999));
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryParseDecoded(Module(fields),
            Chinese7937XuanbaoInfoVersion.Catalog7937, out var info, out var failure));
        Assert.Null(info);
        Assert.Equal(Chinese7937XuanbaoInfoFailure.DuplicateField, failure);
    }

    private static void Refuses(Chinese7937XuanbaoInfoSnapshot info, Chinese7937EchoRequirementsFailure expected)
    {
        Assert.False(Chinese7937EchoRequirements.TryCreate(info, out var requirements, out var failure));
        Assert.Null(requirements);
        Assert.Equal(expected, failure);
    }
}
