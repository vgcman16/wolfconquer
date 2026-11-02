using System.Collections;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoRuleCatalogsTests
{
    [Fact]
    public void IndependentPackedFixturesProduceCompleteSourceOrderedTypedRecordsAndHashes()
    {
        var compose = Chinese7937XuanbaoRulesFixture.ComposePacked();
        var random = Chinese7937XuanbaoRulesFixture.RandomPacked();
        var catalogs = Chinese7937XuanbaoRulesFixture.ParsePacked(compose, random);
        Assert.Equal(7937, catalogs.ClientVersion);
        Assert.Equal(new[] { 2, 1 }, catalogs.ComposeLimits.Select(row => row.Id));
        Assert.Equal(new[] { (42, 3), (7, 2), (42, 1) }, catalogs.RandomAttributes.Select(row => (row.ItemTypeId, row.AttributeType)));
        Assert.True(catalogs.TryGetComposeLimit(2, out var limit));
        Assert.Equal(new[] { 0, 4, 400 }, limit!.Values);
        Assert.Equal("2@@000@@004@@400@@", limit.RawLine);
        Assert.Equal(new[] { "2", "000", "004", "400", "" }, limit.Fields);
        Assert.Equal(1, limit.LineNumber);
        Assert.Equal(Chinese7937XuanbaoRuleCatalogs.ComposeSourceFile, limit.SourceFile);
        Assert.True(catalogs.TryGetRandomAttribute(42, 3, out var attribute));
        Assert.Equal(100, attribute!.MinValue); Assert.Equal(201, attribute.MaxValue);
        Assert.Equal("0000000042@@003@@0000000100@@0000000201@@", attribute.RawLine);
        Assert.Equal(new[] { "0000000042", "003", "0000000100", "0000000201", "" }, attribute.Fields);
        Assert.Equal(1, attribute.LineNumber);
        Assert.Equal(Chinese7937XuanbaoRuleCatalogs.RandomSourceFile, attribute.SourceFile);
        Assert.Equal(Hash(compose), catalogs.ComposePackedSha256);
        Assert.Equal(Hash(random), catalogs.RandomPackedSha256);
        Assert.Equal(Hash(Encoding.ASCII.GetBytes(Chinese7937XuanbaoRulesFixture.ComposeText)), catalogs.ComposeDecodedSha256);
        Assert.Equal(Hash(Encoding.ASCII.GetBytes(Chinese7937XuanbaoRulesFixture.RandomText)), catalogs.RandomDecodedSha256);
        Assert.Equal(Chinese7937XuanbaoRulesFixture.ComposeText, string.Concat(catalogs.ComposeLimits.Select(row => row.RawLine + "\r\n")));
        Assert.Equal(Chinese7937XuanbaoRulesFixture.RandomText, string.Concat(catalogs.RandomAttributes.Select(row => row.RawLine + "\r\n")));
    }

    [Fact]
    public void CallerBuffersAndDeepCollectionsCannotChangeParsedValuesOrSpellings()
    {
        var compose = Chinese7937XuanbaoRulesFixture.ComposePacked(); var random = Chinese7937XuanbaoRulesFixture.RandomPacked();
        var composePin = SHA256.HashData(compose); var randomPin = SHA256.HashData(random);
        Assert.True(Chinese7937XuanbaoRuleCatalogs.TryParsePacked(7937, compose, random, composePin, randomPin, out var result, out _));
        var catalogs = Assert.IsType<Chinese7937XuanbaoRuleCatalogs>(result);
        Array.Clear(compose); Array.Clear(random); Array.Clear(composePin); Array.Clear(randomPin);
        Assert.Throws<NotSupportedException>(() => ((IList)catalogs.ComposeLimits).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList)catalogs.RandomAttributes).Clear());
        Assert.Throws<NotSupportedException>(() => { ((IList)catalogs.ComposeLimits[0].Values)[0] = 999; });
        Assert.Throws<NotSupportedException>(() => ((IList)catalogs.ComposeLimits[0].Fields).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList)catalogs.RandomAttributes[0].Fields).Clear());
        Assert.Equal(new[] { 0, 4, 400 }, catalogs.ComposeLimits[0].Values);
        Assert.Equal("0000000042", catalogs.RandomAttributes[0].Fields[0]);
    }

    [Fact]
    public void ZeroAndInt32MaximumIncludingLeadingZeroSpellingsAreRawData()
    {
        var catalogs = Chinese7937XuanbaoRulesFixture.Parse("000@@0@@0002147483647@@0@@\r\n",
            "0002147483647@@000@@0002147483647@@0@@\r\n");
        Assert.True(catalogs.TryGetComposeLimit(0, out var limit));
        Assert.Equal(new[] { 0, int.MaxValue, 0 }, limit!.Values);
        Assert.True(catalogs.TryGetRandomAttribute(int.MaxValue, 0, out var row));
        Assert.Equal(int.MaxValue, row!.MinValue); Assert.Equal(0, row.MaxValue);
        // Real range/cap admission belongs to the separate runtime owner.
    }

    [Fact]
    public void MissingLookupReturnsNoSubstituteAndLaterRefusalPreservesExistingData()
    {
        var catalogs = Chinese7937XuanbaoRulesFixture.Parse();
        Assert.False(catalogs.TryGetComposeLimit(9, out var missing)); Assert.Null(missing);
        Assert.False(catalogs.TryGetRandomAttribute(42, 9, out var absent)); Assert.Null(absent);
        Refuses(Chinese7937XuanbaoRulesFixture.Pack("1@@0@@1@@400@@\n"), Chinese7937XuanbaoRulesFixture.RandomPacked(),
            Chinese7937XuanbaoRulesFailure.InvalidTable);
        Assert.Equal(400, catalogs.ComposeLimits[0].Values[^1]);
        Assert.Equal(201, catalogs.RandomAttributes[0].MaxValue);
    }

    [Fact]
    public void MaximumRowCountIsFiniteAndAnExtraRowNeverPublishes()
    {
        var edge = string.Concat(Enumerable.Range(0, Chinese7937XuanbaoRuleCatalogs.MaximumRows)
            .Select(id => FormattableString.Invariant($"{id}@@0@@1@@400@@\r\n")));
        Assert.Equal(Chinese7937XuanbaoRuleCatalogs.MaximumRows, Chinese7937XuanbaoRulesFixture.Parse(edge).ComposeLimits.Count);
        Refuses(Chinese7937XuanbaoRulesFixture.Pack(edge + "9000@@0@@1@@400@@\r\n"),
            Chinese7937XuanbaoRulesFixture.RandomPacked(), Chinese7937XuanbaoRulesFailure.RowBounds);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
