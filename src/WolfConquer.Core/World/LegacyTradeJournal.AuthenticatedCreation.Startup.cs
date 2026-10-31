using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static void RequireOwnedStartupProducerWork(LegacyItemUidRegistry.OwnedStartupCustody custody)
    {
        custody.RequireCurrent();
        var configured = custody.Configuration;
        if (!TryCaptureRecovery(configured.UsersRoot, configured.ItemsRoot, configured.QuestsRoot,
                configured.MailboxRoot, configured.CombatGearRoot, configured.Roots, out var capture, out var reason) ||
            capture is null)
            throw new InvalidDataException("Owned startup producer clearance lacks its actual pending record: " + reason);
        if (capture.Record.Version == 19)
        {
            _ = ValidateAuthenticatedCreationImages(capture.Record, custody.HighWater);
            custody.RequireAuthenticatedCreationAllocation(capture.Record.OrdinaryCreation!);
        }
        else custody.RequireCompletedOrdinaryProducerHistory();
        custody.RequireCurrent();
    }
}
