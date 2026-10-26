namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // A bounded active-state membership query, without copying retained payloads
    // or treating archived completion history as pending work authority.
    internal bool TryIsActiveProducerIntent(Guid domain, Guid operation, out bool active, out string reason)
    {
        lock (gate)
        {
            active = false;
            reason = "itemUidProducerIntentInvalid";
            if (operation == Guid.Empty || !TryAdmitProducerOwner(domain, out reason)) return false;
            active = FindProducerIntent(operation.ToString("N")) is not null;
            reason = string.Empty;
            return true;
        }
    }
}
