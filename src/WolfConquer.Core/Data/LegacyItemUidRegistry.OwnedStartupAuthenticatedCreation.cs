using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal sealed partial class OwnedStartupCustody
    {
        internal void RequireCompletedOrdinaryProducerHistory()
        {
            RequireCurrent();
            _ = RequireCompletedOrdinaryHistory(paths, retained, Configuration,
                ReadArchiveCatalog(paths, retained, null, allowNext: false));
        }

        internal void RequireAuthenticatedCreationAllocation(LegacyAuthenticatedOrdinaryCreationOperation operation)
        {
            RequireCurrent();
            RequireAuthenticatedCreationIntent(paths, retained, Configuration,
                ReadArchiveCatalog(paths, retained, null, allowNext: false), operation);
            RequireCurrent();
        }
    }
}
