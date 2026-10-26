using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionFixtureTests
{
    [Fact]
    public void ExplicitOfflineCreationPublishesCanonicalSnapshotsAnExactUidHistoryAndOpenableExpectedDomainConfiguration()
    {
        var result = Create();

        Assert.Equal(Destination, result.RootPath);
        Assert.Equal(ConfigurationPath, result.ConfigurationPath);
        Assert.Equal(AccountUid, result.AccountKey);
        Assert.Equal(AccountUid, result.CharacterUid);
        Assert.Equal(ItemUid, result.ItemUid);
        Assert.NotEqual(Guid.Empty, result.DomainId);
        Assert.False(Directory.Exists(Staging));
        AssertSnapshotSemantics(Destination);
        AssertInitialNamespace(Destination, result.DomainId);
        Assert.Equal(9, Directory.GetFiles(Destination, "*", SearchOption.AllDirectories).Length);
        var files = Files(Destination);
        Assert.True(LegacyOwnedAdmissionOptions.TryReadFile(ConfigurationPath, out var parsed, out var reason), reason);
        var options = Assert.IsType<LegacyOwnedAdmissionOptions>(parsed);
        Assert.Equal(result.DomainId, options.ExpectedDomain);
        Assert.Equal(ItemUid, options.HistoricalHighWater);
        Assert.Equal(Path.Combine(Destination, "Users"), options.UsersRoot);
        Assert.Equal(Path.Combine(Destination, "PlayersItems"), options.ItemsRoot);
        Assert.Equal(Path.Combine(Destination, "PlayersSpells"), options.SpellsRoot);
        Assert.Equal(Path.Combine(Destination, "PlayersProfs"), options.ProficienciesRoot);
        Assert.Equal(Path.Combine(Destination, "Quests"), options.QuestsRoot);
        Assert.Equal(Path.Combine(Destination, "PlayersCombatGear"), options.CombatGearRoot);
        Assert.Equal(Path.Combine(Destination, "PlayersMailList"), options.MailboxRoot);
        Assert.Equal(Path.Combine(Destination, "RedeemContainer.bin"), options.RedeemFile);
        Assert.Equal(Path.Combine(Destination, "ClaimContainer.bin"), options.ClaimFile);

        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(options.CreateSnapshotLoader(), options, out var runtime, out reason), reason);
        using (Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime))
        {
            AssertFiles(Destination, files);
            AssertInitialNamespace(Destination, result.DomainId);
        }
        AssertFiles(Destination, files);
        AssertInitialNamespace(Destination, result.DomainId);
    }

    [Fact]
    public void SecondCreationCannotResetTheDomainHistoryOrReplaceTheExistingFixture()
    {
        var first = Create();
        var files = Files(Destination);

        Assert.False(LegacyOwnedAdmissionFixture.TryCreate(Destination, AccountUid, AccountUid, ItemUid, out var second, out var reason));

        Assert.Null(second);
        Assert.NotEmpty(reason);
        AssertFiles(Destination, files);
        AssertInitialNamespace(Destination, first.DomainId);
        Assert.False(Directory.Exists(Staging));
    }

    [Theory]
    [InlineData("AfterSnapshots")]
    [InlineData("AfterBootstrap")]
    public void StopOnlyCreationBoundaryRetainsTheActualPartialRootWithoutAPublishedSuccessConfig(string boundary)
    {
        var interruption = Enum.Parse<LegacyOwnedAdmissionFixtureInterruption>(boundary);
        Assert.False(LegacyOwnedAdmissionFixture.TryCreate(Destination, AccountUid, AccountUid, ItemUid,
            interruption, out var result, out var reason));

        Assert.Null(result);
        Assert.Equal("ownedAdmissionFixtureInterrupted" + boundary, reason);
        Assert.True(Directory.Exists(Destination));
        Assert.False(File.Exists(ConfigurationPath));
        AssertSnapshotSemantics(Destination);
        var registry = Path.Combine(Destination, "PlayersItems", ".managed-item-uids");
        if (interruption == LegacyOwnedAdmissionFixtureInterruption.AfterSnapshots) Assert.False(Directory.Exists(registry));
        else
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(registry, "state.json")));
            var domain = Guid.ParseExact(document.RootElement.GetProperty("Domain").GetString()!, "N");
            AssertInitialNamespace(Destination, domain);
        }
        var files = Files(Destination);

        Assert.False(LegacyOwnedAdmissionFixture.TryCreate(Destination, AccountUid, AccountUid, ItemUid, out var retried, out _));

        Assert.Null(retried);
        AssertFiles(Destination, files);
        Assert.False(File.Exists(ConfigurationPath));
    }
}
