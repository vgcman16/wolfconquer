using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionRuntimeTests
{
    [Fact]
    public void ExplicitClosedConfigurationRetainsEveryDeclaredRouteAndDomainWithoutOpeningOrChangingTheRegistry()
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        fixture.WriteConfiguration();
        var files = fixture.Files();

        Assert.True(LegacyOwnedAdmissionOptions.TryReadFile(fixture.ConfigPath, out var options, out var reason), reason);

        Assert.Equal(fixture.Options(), options);
        var loader = Assert.IsType<LegacyOwnedAdmissionOptions>(options).CreateSnapshotLoader();
        Assert.Equal(fixture.Users, loader.UsersRoot);
        Assert.Equal(fixture.Items, loader.PlayerItemsRoot);
        Assert.Equal(fixture.Spells, loader.PlayerSpellsRoot);
        Assert.Equal(fixture.Proficiencies, loader.PlayerProficienciesRoot);
        Assert.Equal(fixture.Quests, loader.PlayerQuestsRoot);
        Assert.Equal(fixture.CombatGear, loader.PlayersCombatGearRoot);
        fixture.AssertFiles(files);
        using var runtime = Open(fixture); // Reading the config did not retain the process lease.
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("missing")]
    [InlineData("version")]
    [InlineData("versionType")]
    [InlineData("domainFormat")]
    [InlineData("domainEmpty")]
    [InlineData("relativeRoot")]
    [InlineData("floorOverflow")]
    [InlineData("evidenceEmpty")]
    [InlineData("evidenceMultiline")]
    [InlineData("alias")]
    public void MalformedClosedConfigurationCannotCreateDefaultAuthority(string fault)
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        var document = LegacyOwnedAdmissionRuntimeFixture.Configuration(fixture.Options());
        switch (fault)
        {
            case "unknown": document["Unexpected"] = true; break;
            case "missing": Assert.True(document.Remove("ExpectedDomain")); break;
            case "version": document["Version"] = 2; break;
            case "versionType": document["Version"] = "1"; break;
            case "domainFormat": document["ExpectedDomain"] = fixture.ExpectedDomain.ToString("D"); break;
            case "domainEmpty": document["ExpectedDomain"] = Guid.Empty.ToString("N"); break;
            case "relativeRoot": document["UsersRoot"] = "relative/Users"; break;
            case "floorOverflow": document["HistoricalHighWater"] = 4_294_967_296UL; break;
            case "evidenceEmpty": document["HistoricalEvidence"] = ""; break;
            case "evidenceMultiline": document["HistoricalEvidence"] = "owned\nnot-an-evidence-record"; break;
            default: document["version"] = 1; break;
        }
        File.WriteAllText(fixture.ConfigPath, document.ToJsonString(), new UTF8Encoding(false));
        var files = fixture.Files();

        Assert.False(LegacyOwnedAdmissionOptions.TryReadFile(fixture.ConfigPath, out var options, out var reason));

        Assert.Null(options);
        Assert.NotEmpty(reason);
        fixture.AssertFiles(files);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateOrOversizedConfigurationCannotBeAcceptedAsAValidDomain(bool oversized)
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        var text = LegacyOwnedAdmissionRuntimeFixture.Configuration(fixture.Options()).ToJsonString();
        text = oversized ? new string(' ', 65_537) : text.Replace("\"Version\":1", "\"Version\":1,\"Version\":1", StringComparison.Ordinal);
        File.WriteAllText(fixture.ConfigPath, text, new UTF8Encoding(false));
        var files = fixture.Files();

        Assert.False(LegacyOwnedAdmissionOptions.TryReadFile(fixture.ConfigPath, out var options, out var reason));

        Assert.Null(options);
        Assert.NotEmpty(reason);
        fixture.AssertFiles(files);
    }
}
