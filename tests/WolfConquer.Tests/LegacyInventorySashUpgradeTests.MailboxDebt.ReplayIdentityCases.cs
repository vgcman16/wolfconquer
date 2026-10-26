using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("extra")]
    [InlineData("duplicate")]
    [InlineData("id")]
    public Task ActualReadOnlyRecoveryRejectsDifferentPackageRequestWithoutPoisoningTheOriginalReplay(string fault) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            var intent = PrepareDebtIntent(fixture, join, initial);
            var producer = ActualDebtFrameOwner<LegacyMailboxPackageProducer>(join, "_mailboxPackages");
            Assert.True(join.SessionStore.TryRemove(join.Connection!, out var retired));
            var candidate = LoadDebtRecoveryCandidate(fixture, Assert.IsType<WorldSession>(retired));
            Assert.True(join.SessionStore.TrySetMailboxRecoveryOwner(join.Connection!, candidate, producer, out _, out var reason), reason);
            var before = CapturePackageJoin(fixture, join);
            var wrong = fault switch
            {
                "extra" => new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimAll, 0, [AtomicMailId, 22]).ToFrameBytes(),
                "duplicate" => new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimAll, 0, [AtomicMailId, AtomicMailId]).ToFrameBytes(),
                "id" => new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimAll, 99, [AtomicMailId]).ToFrameBytes(),
                _ => throw new ArgumentOutOfRangeException(nameof(fault)),
            };
            var context = ActualDebtContext(fixture, join);
            await DebtRegisteredAsync(fixture, context, wrong, direct: true);
            AssertDebtRetained(fixture, join, before, intent);
            await DebtRegisteredAsync(fixture, context, wrong, direct: false);
            AssertDebtRetained(fixture, join, before, intent);
            Assert.False(join.Recovery.IsBlocked);
            var started = DateTime.Now;
            await DebtRegisteredAsync(fixture, context, PackageJoinRequest(claimAll: true), direct: false);
            AssertDebtPackageCommitted(fixture, join, initial, intent);
            AssertPackageJoinFrames(fixture, join, initial, true, started, DateTime.Now, before.FirstFrame);
        });
}
