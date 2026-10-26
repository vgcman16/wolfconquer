using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("domain")]
    [InlineData("recovery")]
    [InlineData("users")]
    [InlineData("items")]
    [InlineData("mailbox")]
    [InlineData("gear")]
    public Task ExplicitMailboxPackageProducerRejectsForeignDomainRootsOrIncompleteRecoveryWithoutEffects(string fault) =>
        RunPackageJoinAsync((fixture, join) =>
        {
            var other = Path.Combine(fixture.Root, "ForeignOwner");
            Directory.CreateDirectory(other);
            var incomplete = new LegacyTradeRecoveryState(fixture.Users, fixture.Items);
            Assert.True(incomplete.TryEnsureInitialized(out var reason), reason);
            var before = CapturePackageJoin(fixture, join);
            Assert.False(LegacyMailboxPackageProducer.TryCreate(join.Registry,
                fault == "domain" ? Guid.NewGuid() : join.Domain,
                fault == "recovery" ? incomplete : join.Recovery,
                fault == "users" ? other : fixture.Users,
                fault == "items" ? other : fixture.Items,
                fault == "mailbox" ? other : fixture.PackageMailboxRoot,
                fault == "gear" ? other : fixture.PackageGearRoot,
                out var producer, out _));
            Assert.Null(producer);
            AssertPackageJoinUnchanged(fixture, join, before);
            AssertNoPackageJoinIntents(join);
            return Task.CompletedTask;
        });
}
