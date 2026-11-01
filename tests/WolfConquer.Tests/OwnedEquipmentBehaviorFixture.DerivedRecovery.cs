using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class OwnedEquipmentBehaviorFixture
{
    // Recovery-only: close the real actor/runtime, reopen the exact configured
    // owner/files, and do not claim that a new login or pending query completed.
    internal async Task ReopenDerivedContinuationRecoveryOnlyAsync()
    {
        await connections[actorUid].CompleteTransportCloseAsync();
        await Runtime.DisposeAsync();
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var reopened, out var reason), reason);
        Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(reopened);
    }
}
