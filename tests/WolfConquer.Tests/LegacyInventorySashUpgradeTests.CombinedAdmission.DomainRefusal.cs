using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task CombinedAdmissionRefusesTwoActualDifferentRegistryDomainsWithoutBindingEitherProducer() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var owners = await join.PrepareCombinedAdmissionOwnersAsync();
            var files = fixture.PackageFiles();
            await RunPackageJoinAsync(async (other, otherJoin) =>
            {
                var foreign = await otherJoin.PrepareCombinedAdmissionOwnersAsync();
                var otherFiles = other.PackageFiles();
                Assert.NotEqual(owners.Ordinary.Domain, foreign.Ordinary.Domain);
                Assert.NotSame(owners.Mailbox.Registry, foreign.Ordinary.Registry);

                Assert.Throws<ArgumentException>(() => new WorldServerFrameHandler(owners.Loader, owners.Handoff,
                    PackageJoinCatalog(), owners.Mailbox, foreign.Ordinary, fixture.Log, ConquerProtocolProfile.VisibleDx8D13));

                AssertUnboundCombinedProducer(owners.Mailbox);
                AssertUnboundCombinedProducer(foreign.Mailbox);
                Assert.True(owners.Ordinary.IsCurrent(owners.OrdinaryRoots));
                Assert.True(foreign.Ordinary.IsCurrent(foreign.OrdinaryRoots));
                AssertPackageJoinFiles(fixture, files);
                AssertPackageJoinFiles(other, otherFiles);
            });
        });
}
