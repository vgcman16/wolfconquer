using System.Security.Cryptography;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task RegisteredD13AllMergeCannotSilentlyReplaceAnOverlappingAlreadyAdmittedLegacyAllocatingRecipe() =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            // The genuine earlier WMP1 producer ignored existing compatible stacks.
            // Retain that real typed intent/range as an upgrade/reconciliation fixture;
            // it is data admission only, never permission to materialize six new rows.
            var legacy = CreateLegacyPackageJoinRecipe(fixture, join, initial);
            var payload = legacy.CopyPayload();
            var request = new LegacyItemUidReservationRequest(legacy.OperationId, LegacyMailboxPackageProducer.Writer,
                legacy.AccountId, legacy.CharacterUid, Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), legacy.Count);
            Assert.Equal(6, request.Count);
            Assert.True(join.Registry.TryPrepareProducerIntent(join.Domain, request, payload, out _, out var reason), reason);
            Assert.True(join.Registry.TryReserveProducerIntent(join.Domain, request, payload, out var retained, out reason), reason);
            Assert.NotNull(retained!.Allocation);
            var before = CapturePackageJoin(fixture, join);

            await join.FrameAsync(PackageJoinRequest(claimAll: false));

            AssertPackageJoinUnchanged(fixture, join, before);
            Assert.True(join.Registry.TryReadProducerIntent(join.Domain, legacy.OperationId, out var stillRetained, out reason), reason);
            Assert.Equal(payload, stillRetained!.CopyPayload());
            Assert.Equal(request, stillRetained.Request);
            Assert.Equal(retained.Allocation, stillRetained.Allocation);
            Assert.Equal(PackageHistoricalFloor + 1, stillRetained.Allocation!.First);
            Assert.Equal(6, stillRetained.Allocation.Count);
            Assert.Equal(initial.Json, before.Json);
        });
}
