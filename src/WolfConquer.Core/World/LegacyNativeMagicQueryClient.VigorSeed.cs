namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    // A read-only observation of this exact binding's actual query entry.
    // A UID or an independently constructed object cannot mint an entry.
    internal static bool IsVigorSeedEpochCurrentPlain(LegacyOrdinarySpellBinding binding, WorldSession expected) =>
        WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
        binding.RuntimeInspectionPlayerOwnerIsCurrentPlain() && binding.Store.IsBoundSpellOwnerCurrent(binding) &&
        binding.Store.TryGet(binding.Connection, out var actual) && ReferenceEquals(actual, expected) &&
        (!Entries.TryGetValue(binding.NativeIdentity, out var entry) ||
            !entry.VigorQueryAttempted && entry.Epoch is null && !entry.Initialized);
}
