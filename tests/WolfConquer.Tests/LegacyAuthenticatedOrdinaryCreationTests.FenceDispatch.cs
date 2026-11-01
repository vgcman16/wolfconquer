using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task ActualAuthenticatedProducerKeepsItsCreationPermissionWithUnrelatedRetainedOwner() => Run(async f =>
    {
        using var unrelated = new LegacyOwnedAdmissionRuntimeFixture();
        Assert.True(LegacyItemUidRegistry.TryOpen(unrelated.RegistryOptions(), unrelated.ExpectedDomain,
            out var registry, out var reason), reason);
        using (registry)
        {
            Assert.True(LegacyOrdinaryAdmissionUidOwner.TryCreate(registry!, unrelated.ExpectedDomain,
                unrelated.Options().Roots, unrelated.Mailbox, out var issued, out reason), reason);
            var owner = Assert.IsType<LegacyOrdinaryAdmissionUidOwner>(issued);
            registry!.Dispose();
            Assert.True(owner.IsPending(CreationFixture.Account));
            var unrelatedFiles = unrelated.Files();

            await CreateAndAssert(f); // Real authentication/permit/producer/journal/native output assertions.

            unrelated.AssertFiles(unrelatedFiles);
            Assert.True(owner.IsPending(CreationFixture.Account));
            var created = f.Data.Files(); var holder = f.Current; var frames = f.Sent.Count;
            await f.CreateAsync();
            Assert.Same(holder, f.Current); Assert.Equal(frames, f.Sent.Count); f.Data.AssertFiles(created);
        }
    });
}
