using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGmPolicyTests
{
    private const string DomainText = "5ea475af-f3ef-48b1-848e-e29dd06bcb87";
    private static readonly Guid Domain = Guid.ParseExact(DomainText, "D");

    [Fact]
    public void MatchingRequiresExactDomainAccountCharacterAndOneDefinedCapability()
    {
        var policy = Parse(Document("[" + Grant(capabilities: "[\"inspect.self\",\"mail.testkit.self\"]") + "]"));
        Assert.Equal(Domain, policy.ExpectedDomain);
        Assert.True(policy.Allows(Domain, 2, 1293600, LegacyGmCapability.InspectSelf));
        Assert.True(policy.Allows(Domain, 2, 1293600, LegacyGmCapability.MailTestKitSelf));
        Assert.False(policy.Allows(Guid.Empty, 2, 1293600, LegacyGmCapability.InspectSelf));
        Assert.False(policy.Allows(Guid.Parse("66666666-1111-2222-3333-444444444444"), 2, 1293600, LegacyGmCapability.InspectSelf));
        Assert.False(policy.Allows(Domain, 3, 1293600, LegacyGmCapability.InspectSelf));
        Assert.False(policy.Allows(Domain, 2, 1293601, LegacyGmCapability.InspectSelf));
        Assert.False(policy.Allows(Domain, 0, 1293600, LegacyGmCapability.InspectSelf));
        Assert.False(policy.Allows(Domain, 2, 0, LegacyGmCapability.InspectSelf));
        Assert.False(policy.Allows(Domain, 2, 1293600, (LegacyGmCapability)0));
        Assert.False(policy.Allows(Domain, 2, 1293600, (LegacyGmCapability)3));
        Assert.False(policy.Allows(Domain, 2, 1293600, (LegacyGmCapability)(-1)));
    }

    [Fact]
    public void DistinctCharactersAndMaximumUintIdentitiesAreNotWildcardGrants()
    {
        var policy = Parse(Document("[" + Grant() + "," + Grant(character: "7", capabilities: "[\"mail.testkit.self\"]") +
            "," + Grant(account: "4294967295", character: "4294967295") + "]"));
        Assert.Equal(3, policy.Grants.Count);
        Assert.True(policy.Allows(Domain, 2, 1293600, LegacyGmCapability.InspectSelf));
        Assert.False(policy.Allows(Domain, 2, 1293600, LegacyGmCapability.MailTestKitSelf));
        Assert.True(policy.Allows(Domain, 2, 7, LegacyGmCapability.MailTestKitSelf));
        Assert.False(policy.Allows(Domain, 2, 7, LegacyGmCapability.InspectSelf));
        Assert.True(policy.Allows(Domain, uint.MaxValue, uint.MaxValue, LegacyGmCapability.InspectSelf));
    }

    [Fact]
    public void PolicyOwnsImmutableValuesAfterInputBytesAreChanged()
    {
        var input = Encoding.UTF8.GetBytes(Document("[" + Grant() + "]"));
        var policy = LegacyGmPolicy.Parse(input);
        Array.Fill(input, (byte)'x');
        Assert.True(policy.Allows(Domain, 2, 1293600, LegacyGmCapability.InspectSelf));
        Assert.Throws<NotSupportedException>(() => ((IList<LegacyGmGrant>)policy.Grants).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LegacyGmCapability>)policy.Grants[0].Capabilities).Add(LegacyGmCapability.MailTestKitSelf));
        Assert.Single(policy.Grants);
        Assert.Single(policy.Grants[0].Capabilities);
    }

    [Fact]
    public void EmptyGrantsAndMaximumThirtyTwoGrantsAreBoundedAndNonGrantingElsewhere()
    {
        var empty = Parse(Document("[]"));
        Assert.Empty(empty.Grants);
        Assert.False(empty.Allows(Domain, 2, 1293600, LegacyGmCapability.InspectSelf));
        var rows = Enumerable.Range(1, LegacyGmPolicy.MaximumGrants).Select(i => Grant(account: i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var maximum = Parse(Document("[" + string.Join(',', rows) + "]"));
        Assert.Equal(32, maximum.Grants.Count);
        Assert.True(maximum.Allows(Domain, 32, 1293600, LegacyGmCapability.InspectSelf));
        Assert.Throws<InvalidDataException>(() => Parse(Document("[" + string.Join(',', rows.Append(Grant(account: "33"))) + "]")));
    }

    [Fact]
    public void ByteLimitIncludesWhitespaceAndRefusesBeforeAcceptingOversizePolicy()
    {
        var text = Document("[]");
        var exact = Encoding.UTF8.GetBytes(text.PadRight(LegacyGmPolicy.MaximumBytes));
        Assert.Equal(16384, exact.Length);
        Assert.Empty(LegacyGmPolicy.Parse(exact).Grants);
        Assert.Throws<InvalidDataException>(() => LegacyGmPolicy.Parse(exact.Concat(new byte[] { 32 }).ToArray()));
        Assert.Throws<InvalidDataException>(() => LegacyGmPolicy.Parse(Array.Empty<byte>()));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,\"expectedDomain\":\"5ea475af-f3ef-48b1-848e-e29dd06bcb87\",\"grants\":[],\"extra\":0}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"expectedDomain\":\"5ea475af-f3ef-48b1-848e-e29dd06bcb87\",\"grants\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"schem\\u0061Version\":1,\"expectedDomain\":\"5ea475af-f3ef-48b1-848e-e29dd06bcb87\",\"grants\":[]}")]
    [InlineData("{\"SchemaVersion\":1,\"expectedDomain\":\"5ea475af-f3ef-48b1-848e-e29dd06bcb87\",\"grants\":[]}")]
    [InlineData("{\"schemaVersion\":2,\"expectedDomain\":\"5ea475af-f3ef-48b1-848e-e29dd06bcb87\",\"grants\":[]}")]
    [InlineData("{\"schemaVersion\":1.0,\"expectedDomain\":\"5ea475af-f3ef-48b1-848e-e29dd06bcb87\",\"grants\":[]}")]
    [InlineData("{\"schemaVersion\":1e0,\"expectedDomain\":\"5ea475af-f3ef-48b1-848e-e29dd06bcb87\",\"grants\":[]}")]
    [InlineData("{\"schemaVersion\":\"1\",\"expectedDomain\":\"5ea475af-f3ef-48b1-848e-e29dd06bcb87\",\"grants\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"expectedDomain\":\"5ea475af-f3ef-48b1-848e-e29dd06bcb87\",\"grants\":null}")]
    [InlineData("{\"schemaVersion\":1,\"expectedDomain\":\"5ea475af-f3ef-48b1-848e-e29dd06bcb87\",\"grants\":{},}")]
    [InlineData("/*comment*/{\"schemaVersion\":1,\"expectedDomain\":\"5ea475af-f3ef-48b1-848e-e29dd06bcb87\",\"grants\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"expectedDomain\":\"5ea475af-f3ef-48b1-848e-e29dd06bcb87\",\"grants\":[]} {}")]
    [InlineData("{\"schemaVersion\":1,\"expectedDomain\":\"\\ud800\",\"grants\":[]}")]
    public void MalformedOrNoncanonicalDocumentsRefuse(string json) =>
        Assert.Throws<InvalidDataException>(() => Parse(json));

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("5ea475aff3ef48b1848ee29dd06bcb87")]
    [InlineData("{5ea475af-f3ef-48b1-848e-e29dd06bcb87}")]
    [InlineData(" 5ea475af-f3ef-48b1-848e-e29dd06bcb87")]
    [InlineData("5ea475af-f3ef-48b1-848e-e29dd06bcb87 ")]
    [InlineData("5ea475af-f3ef-48b1-848e-e29dd06bcb8z")]
    public void DomainRequiresNonzeroUnpaddedExactDFormat(string domain) =>
        Assert.Throws<InvalidDataException>(() => Parse(Document("[]", domain)));

    [Fact]
    public void DomainHexCaseAndPropertyOrderDoNotAlterTheTypedIdentity()
    {
        var policy = Parse("{\"grants\":[],\"expectedDomain\":\"" + DomainText.ToUpperInvariant() + "\",\"schemaVersion\":1}");
        Assert.Equal(Domain, policy.ExpectedDomain);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("4294967296")]
    [InlineData("\"2\"")]
    [InlineData("2.0")]
    [InlineData("2e0")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[]")]
    public void BothIdentityFieldsRequirePositiveUInt32IntegerTokens(string identity)
    {
        Assert.Throws<InvalidDataException>(() => Parse(Document("[" + Grant(account: identity) + "]")));
        Assert.Throws<InvalidDataException>(() => Parse(Document("[" + Grant(character: identity) + "]")));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"inspect.self\"")]
    [InlineData("[\"inspect.self\",\"inspect.self\"]")]
    [InlineData("[\"inspect.self\",\"inspect.\\u0073elf\"]")]
    [InlineData("[\"inspect.self\",\"mail.testkit.self\",\"inspect.self\"]")]
    [InlineData("[\"Inspect.Self\"]")]
    [InlineData("[\"*\"]")]
    [InlineData("[\"teleport.self\"]")]
    [InlineData("[0]")]
    [InlineData("[null]")]
    public void CapabilitiesAreClosedNonemptyAndDistinct(string capabilities) =>
        Assert.Throws<InvalidDataException>(() => Parse(Document("[" + Grant(capabilities: capabilities) + "]")));

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"accountId\":2,\"characterUid\":1293600}")]
    [InlineData("{\"accountId\":2,\"characterUid\":1293600,\"capabilities\":[\"inspect.self\"],\"username\":\"GM\"}")]
    [InlineData("{\"accountId\":2,\"accountId\":2,\"characterUid\":1293600,\"capabilities\":[\"inspect.self\"]}")]
    [InlineData("{\"accountId\":2,\"characterUid\":1293600,\"capabilities\":[\"inspect.self\"],\"capabilities\":[\"mail.testkit.self\"]}")]
    public void GrantShapeRejectsMissingUnknownAndDuplicateProperties(string grant) =>
        Assert.Throws<InvalidDataException>(() => Parse(Document("[" + grant + "]")));

    [Fact]
    public void DuplicateIdentityRefusesEvenWhenCapabilitySubsetsDiffer() =>
        Assert.Throws<InvalidDataException>(() => Parse(Document("[" + Grant() + "," + Grant(capabilities: "[\"mail.testkit.self\"]") + "]")));

    [Fact]
    public void BomAndInvalidUtf8AreNotAcceptedAsPolicyDocuments()
    {
        var bytes = Encoding.UTF8.GetBytes(Document("[]"));
        Assert.Throws<InvalidDataException>(() => LegacyGmPolicy.Parse(new byte[] { 0xef, 0xbb, 0xbf }.Concat(bytes).ToArray()));
        Assert.Throws<InvalidDataException>(() => LegacyGmPolicy.Parse(bytes.Concat(new byte[] { 0xff }).ToArray()));
        var malformedText = bytes.ToArray();
        malformedText[Array.IndexOf(malformedText, (byte)'5')] = 0xff;
        Assert.Throws<InvalidDataException>(() => LegacyGmPolicy.Parse(malformedText));
    }

    private static LegacyGmPolicy Parse(string json) => LegacyGmPolicy.Parse(Encoding.UTF8.GetBytes(json));
    private static string Document(string grants, string domain = DomainText) =>
        "{\"schemaVersion\":1,\"expectedDomain\":\"" + domain + "\",\"grants\":" + grants + "}";
    private static string Grant(string account = "2", string character = "1293600", string capabilities = "[\"inspect.self\"]") =>
        "{\"accountId\":" + account + ",\"characterUid\":" + character + ",\"capabilities\":" + capabilities + "}";
}
