using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionRuntimeTests
{
    [Fact]
    public void UnconfiguredStorageWithoutAnExistingNamespaceRemainsUnownedAndIsNeverBootstrapped()
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        var files = fixture.Files();

        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(fixture.Loader, null, out var runtime, out var reason), reason);

        Assert.Null(runtime);
        Assert.False(Directory.Exists(fixture.Registry));
        fixture.AssertFiles(files);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("empty")]
    [InlineData("file")]
    public void ExistingNamespaceWithoutExplicitConfigurationCannotFallBackToUnownedAdmission(string shape)
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: shape == "valid");
        if (shape == "empty") Directory.CreateDirectory(fixture.Registry);
        if (shape == "file") File.WriteAllText(fixture.Registry, "owned invalid namespace path type");
        var files = fixture.Files();

        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(fixture.Loader, null, out var runtime, out var reason));

        Assert.Null(runtime);
        Assert.NotEmpty(reason);
        if (shape == "empty") Assert.Empty(Directory.GetFileSystemEntries(fixture.Registry));
        fixture.AssertFiles(files);
    }

    [Fact]
    public void ExplicitConfigurationWithoutItsActualLoaderCannotOpenAnOwnerOrAlterTheRegistry()
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        var files = fixture.Files();

        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(null, fixture.Options(), out var runtime, out var reason));

        Assert.Null(runtime);
        Assert.NotEmpty(reason);
        fixture.AssertFiles(files);
    }

    [Fact]
    public void ExplicitConfigurationCannotBootstrapAMissingNamespace()
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        var options = fixture.Options() with { ExpectedDomain = Guid.NewGuid() };
        var files = fixture.Files();

        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(fixture.Loader, options, out var runtime, out var reason));

        Assert.Null(runtime);
        Assert.NotEmpty(reason);
        Assert.False(Directory.Exists(fixture.Registry));
        fixture.AssertFiles(files);
    }

    [Theory]
    [InlineData("domain")]
    [InlineData("users")]
    [InlineData("items")]
    [InlineData("spells")]
    [InlineData("proficiencies")]
    [InlineData("quests")]
    [InlineData("gear")]
    [InlineData("mailbox")]
    [InlineData("redeem")]
    [InlineData("claim")]
    [InlineData("floor")]
    [InlineData("evidence")]
    public void ExplicitDomainAndRouteMismatchCannotAuthorizeOrModifyTheExistingNamespace(string field)
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        var original = fixture.Options();
        var changed = Path.Combine(fixture.Root, "different-owned-route");
        var options = field switch
        {
            "domain" => original with { ExpectedDomain = Guid.NewGuid() },
            "users" => original with { UsersRoot = changed },
            "items" => original with { ItemsRoot = changed },
            "spells" => original with { SpellsRoot = changed },
            "proficiencies" => original with { ProficienciesRoot = changed },
            "quests" => original with { QuestsRoot = changed },
            "gear" => original with { CombatGearRoot = changed },
            "mailbox" => original with { MailboxRoot = changed },
            "redeem" => original with { RedeemFile = changed },
            "claim" => original with { ClaimFile = changed },
            "floor" => original with { HistoricalHighWater = LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1 },
            _ => original with { HistoricalEvidence = "different owned historical evidence" },
        };
        var files = fixture.Files();

        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(fixture.Loader, options, out var runtime, out var reason));

        Assert.Null(runtime);
        Assert.NotEmpty(reason);
        fixture.AssertFiles(files);
        Assert.False(File.Exists(changed));
        Assert.False(Directory.Exists(changed));
        using var valid = Open(fixture); // Failed acquisition released any lease it opened.
    }

    [Fact]
    public void ARealPublishedPendingSnapshotJournalRemainsBlockedAndIsNotSilentlyReplayedAtStartup()
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        fixture.StagePendingSnapshotJournal();
        var files = fixture.Files();

        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(fixture.Loader, fixture.Options(), out var runtime, out var reason));

        Assert.Null(runtime);
        Assert.NotEmpty(reason);
        Assert.True(LegacyTradeJournal.HasPending(fixture.Users));
        fixture.AssertFiles(files);
    }

    [Fact]
    public void ActualRuntimeLeasePreventsASecondOwnerUntilDisposalWithoutChangingTheLedger()
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        var files = fixture.Files();
        using (var first = Open(fixture))
        {
            Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(fixture.Loader, fixture.Options(), out var second, out var reason));
            Assert.Null(second);
            Assert.NotEmpty(reason);
            fixture.AssertFiles(files);
        }
        using var reopened = Open(fixture);
        fixture.AssertFiles(files);
    }

    private static LegacyOwnedAdmissionRuntime Open(LegacyOwnedAdmissionRuntimeFixture fixture)
    {
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(fixture.Loader, fixture.Options(), out var runtime, out var reason), reason);
        return Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime);
    }
}
