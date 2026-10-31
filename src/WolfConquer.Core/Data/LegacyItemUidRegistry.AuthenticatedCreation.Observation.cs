using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // A deny-only raw-scan bound. This neither issues a receipt nor establishes
    // physical ownership; callers still need the complete configured scan.
    internal bool IsAuthenticatedCreationObservedMaximumInRange(LegacyOwnedAdmissionRuntime.CreationLease lease, uint maximum)
    {
        lease.RequirePlainOwnerCurrent();
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent();
            if (!ReferenceEquals(lease.Registry, this))
                throw new InvalidDataException("Creation observation belongs to a different registry owner.");
            return maximum <= state.HighWater;
        }
    }

    internal bool IsAuthenticatedCreationObservedMaximumInRange(
        LegacyOwnedAdmissionRuntime.AuthenticatedCreationStartupAuthority authority, uint maximum)
    {
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent();
            if (!authority.IsCurrentPlain(this) || !IsOrdinaryAdmissionOwner(authority.Ordinary))
                throw new InvalidDataException("Creation startup observation lacks its actual exclusive owner.");
            return maximum <= state.HighWater;
        }
    }
}
