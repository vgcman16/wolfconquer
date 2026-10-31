using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyAuthenticatedOrdinaryCreationStartup
{
    internal static bool TryComplete(LegacyOwnedAdmissionRuntime.AuthenticatedCreationStartupAuthority authority,
        out string reason)
    {
        reason = "authenticatedCreationStartupUnproven";
        try
        {
            var registry = authority.Registry; var configured = authority.Configuration;
            if (!authority.IsCurrentPlain(registry) || !registry.TryReadActiveProducerIntents(configured.ExpectedDomain,
                    out var active, out reason)) return false;
            if (active.Length == 0)
            {
                registry.RequireAuthenticatedCreationStartupNoDebt(authority);
                LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
                if (!LegacyItemUidRawCatalog.TryScan(configured.ItemsRoot, configured.MailboxRoot,
                        configured.RedeemFile, configured.ClaimFile, out var observed, out reason)) return false;
                if (!registry.IsAuthenticatedCreationObservedMaximumInRange(authority, observed.Maximum))
                { reason = LegacyItemUidRegistry.RecoveryRequired + ":authenticatedCreationHighWaterExceeded"; return false; }
                reason = string.Empty; return true;
            }
            if (active.Length != 1 || active[0].Request.Writer != LegacyOrdinaryAdmissionUidOwner.Writer)
            { reason = "authenticatedCreationStartupUnknownOrMultipleProducerDebt"; return false; }
            var intent = active[0];
            var binding = LegacyOrdinaryProducerRecipe.Read(intent, configured.ExpectedDomain, configured.Roots, configured.MailboxRoot);
            if (binding.Creation is not { } recipe || !recipe.IsBefore(binding.CaptureCurrent()))
            { reason = "authenticatedCreationStartupBeforeImagesUnproven"; return false; }
            LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
            LegacyCharacterCreationRecoveryEvidence.RequireBeforeAllocation(configured, recipe.Account,
                recipe.Projection.Name, recipe.Projection.CopyCharacterImage());
            registry.RequireAuthenticatedCreationStartupPreflight(authority, intent);
            if (!LegacyItemUidRawCatalog.TryScan(configured.ItemsRoot, configured.MailboxRoot,
                    configured.RedeemFile, configured.ClaimFile, out var before, out reason)) return false;
            if (!registry.IsAuthenticatedCreationObservedMaximumInRange(authority, before.Maximum))
            { reason = LegacyItemUidRegistry.RecoveryRequired + ":authenticatedCreationHighWaterExceeded"; return false; }
            if (intent.Allocation is null)
            {
                if (!registry.TryReserveProducerIntent(configured.ExpectedDomain, intent.Request, intent.CopyPayload(),
                        out var allocated, out reason) || allocated?.Allocation is null) return false;
                intent = allocated;
            }
            if (intent.Allocation is not { } receipt) throw new InvalidDataException("Creation startup reservation has no allocation.");
            var operation = new LegacyAuthenticatedOrdinaryCreationOperation(intent.CopyPayload(), intent.PreparedGeneration, receipt);
            var custody = LegacyItemUidRegistry.OwnedStartupCustody.BorrowAuthenticatedCreation(authority, operation);
            if (!LegacyTradeJournal.TryCompleteAuthenticatedCreationStartup(custody, operation, out reason)) return false;
            if (!authority.IsCurrentPlain(registry) || !authority.Ordinary.TryComplete(intent.Request.OperationId, out reason)) return false;
            LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
            if (!LegacyItemUidRawCatalog.TryScan(configured.ItemsRoot, configured.MailboxRoot,
                    configured.RedeemFile, configured.ClaimFile, out var after, out reason)) return false;
            if (!registry.IsAuthenticatedCreationObservedMaximumInRange(authority, after.Maximum))
            { reason = LegacyItemUidRegistry.RecoveryRequired + ":authenticatedCreationHighWaterExceeded"; return false; }
            reason = string.Empty; return true;
        }
        catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
        { reason = "authenticatedCreationStartupRefused:" + error.GetType().Name; return false; }
    }
}
