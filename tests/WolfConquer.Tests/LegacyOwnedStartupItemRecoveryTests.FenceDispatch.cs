using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupItemRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualStartupRecoveryCannotBorrowCleanOrEndedForeignOwnerOnSharedParticipant(bool ended) => WithFixture(scope =>
    {
        scope.Stage(0); // Genuine captured journal, never a handwritten recovery grant.
        using var foreign = new LegacyOwnedAdmissionRuntimeFixture();
        Assert.True(LegacyItemUidRegistry.TryOpen(foreign.RegistryOptions(), foreign.ExpectedDomain,
            out var registry, out var reason), reason);
        using (registry)
        {
            var roots = foreign.Options().Roots with { Spells = scope.Data.Items };
            Assert.True(LegacyOrdinaryAdmissionUidOwner.TryCreate(registry!, foreign.ExpectedDomain, roots,
                foreign.Mailbox, out var issued, out reason), reason);
            var owner = Assert.IsType<LegacyOrdinaryAdmissionUidOwner>(issued);
            Assert.False(owner.IsPending(Uid));
            if (ended) registry!.Dispose();
            var foreignFiles = foreign.Files();

            scope.AssertRefused();

            foreign.AssertFiles(foreignFiles);
            Assert.Equal(ended, owner.IsPending(Uid));
            Assert.True(LegacyTradeJournal.HasPending(scope.Data.Users));
        }
    });
}
