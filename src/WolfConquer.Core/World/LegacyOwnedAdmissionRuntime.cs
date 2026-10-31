using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Opens a pre-existing explicit domain. It never bootstraps a registry or
// infers starter entitlement; fresh creation needs its actual authenticated
// permit, and startup completes only known already-issued durable intent.
// The separate mailbox package producer is not activated here.
public sealed partial class LegacyOwnedAdmissionRuntime : IDisposable, IAsyncDisposable
{
    private readonly LegacyItemUidRegistry registry;
    private readonly LegacyOwnedAdmissionOptions configured;
    internal LegacyOrdinaryAdmissionUidOwner Ordinary { get; }
    internal LegacyTradeRecoveryState Recovery { get; }
    private readonly object gate = new();
    private readonly CancellationTokenSource stop = new();
    private WorldServerFrameHandler? frame;
    private Task? disposal;
    private bool closed, stopping;

    private LegacyOwnedAdmissionRuntime(LegacyItemUidRegistry registry, LegacyOwnedAdmissionOptions configured,
        LegacyOrdinaryAdmissionUidOwner ordinary, LegacyTradeRecoveryState recovery)
    { this.registry = registry; this.configured = configured; Ordinary = ordinary; Recovery = recovery; }

    public static bool TryOpen(CharacterSnapshotLoader? loader, LegacyOwnedAdmissionOptions? options,
        out LegacyOwnedAdmissionRuntime? runtime, out string reason)
    {
        runtime = null; reason = "ownedAdmissionConfigurationRequired";
        LegacyItemUidRegistry? acquired = null;
        LegacyOwnedAdmissionRuntime? opening = null;
        try
        {
            if (options is null)
            {
                if (loader is not null && (LegacyOrdinaryAdmissionReconciliation.NamespaceMayExist(loader) ||
                    LegacyTradeJournal.HasPending(loader.UsersRoot))) return false;
                reason = string.Empty; return true;
            }
            if (loader is null || !options.TryNormalize(out var actual, out reason)) return false;
            if (actual.Roots != LegacyOrdinaryAdmissionRoots.FromLoader(loader) ||
                !MatchesMailboxRoute(actual))
            { reason = "ownedAdmissionRoutesMismatch"; return false; }
            LegacyTradeJournal.ValidateOwnedAdmissionRoutes(actual.Roots, actual.MailboxRoot, actual.RedeemFile, actual.ClaimFile);
            if (!LegacyItemUidRegistry.TryOpenOwnedAdmission(actual, out acquired, out reason) || acquired is null)
                return false;
            if (!LegacyOrdinaryAdmissionUidOwner.TryCreate(acquired, actual.ExpectedDomain, actual.Roots,
                    actual.MailboxRoot, out var ordinary, out reason) || ordinary is null) return false;
            var recovery = new LegacyTradeRecoveryState(actual.UsersRoot, actual.ItemsRoot, actual.QuestsRoot,
                actual.MailboxRoot, actual.CombatGearRoot, actual.SpellsRoot, actual.ProficienciesRoot);
            // This private candidate cannot bind a Frame or escape TryOpen.
            // Its real existing lease/ordinary owner completes only the exact
            // recorded authenticated creation before normal readiness exists.
            opening = new LegacyOwnedAdmissionRuntime(acquired, actual, ordinary, recovery);
            acquired = null; // The unpublished candidate now owns this same handle.
            if (!opening.TryCompleteAuthenticatedCreationStartup(out reason)) return false;
            if (!ordinary.IsReconciliationComplete)
            { reason = "ownedAdmissionRetainedWorkRecoveryRequired"; return false; }
            // The real owner is registered before the existing escrow recovery
            // may write a Character; retained ordinary accounts remain fenced.
            if (!recovery.TryInitializeOwnedAdmission(actual.Roots, out reason)) return false;
            if (!recovery.IsReadyForOrdinaryAdmission(actual.Roots))
            { reason = "ownedAdmissionRecoveryReadinessUnproven"; return false; }
            if (!opening.TryCompleteRuneDecomposeNamespaceStartup(out reason)) return false;
            runtime = opening;
            opening = null; reason = string.Empty; return true;
        }
        catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
        { reason = "ownedAdmissionStartupRefused:" + error.GetType().Name; return false; }
        finally
        {
            if (opening is not null)
                try { opening.Dispose(); }
                catch (Exception closeError) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(closeError))
                { reason += ";startupClose:" + closeError.GetType().Name; }
            if (acquired is not null)
                try { acquired.Dispose(); }
                catch (Exception closeError) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(closeError))
                { reason += ";leaseClose:" + closeError.GetType().Name; }
        }
    }

    private static bool MatchesMailboxRoute(LegacyOwnedAdmissionOptions options)
    {
        // Existing Frame mailbox storage uses this sibling. This comparison
        // grants no authority; a differing explicit route is refused.
        var parent = Directory.GetParent(options.UsersRoot);
        return parent is not null && SameRoute(options.MailboxRoot, Path.Combine(parent.FullName, "PlayersMailList"));
    }

    private static bool SameRoute(string first, string? second) => second is not null && string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
