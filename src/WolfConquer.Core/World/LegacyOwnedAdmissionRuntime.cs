using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Opens a pre-existing explicit domain. This API never bootstraps a registry,
// creates starter rewards, or activates the separate mailbox package producer.
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
            if (!ordinary.IsReconciliationComplete)
            { reason = "ownedAdmissionRetainedWorkRecoveryRequired"; return false; }
            var recovery = new LegacyTradeRecoveryState(actual.UsersRoot, actual.ItemsRoot, actual.QuestsRoot,
                actual.MailboxRoot, actual.CombatGearRoot, actual.SpellsRoot, actual.ProficienciesRoot);
            // The real owner is registered before the existing escrow recovery
            // may write a Character; retained ordinary accounts remain fenced.
            if (!recovery.TryInitializeOwnedAdmission(actual.Roots, out reason)) return false;
            if (!recovery.IsReadyForOrdinaryAdmission(actual.Roots))
            { reason = "ownedAdmissionRecoveryReadinessUnproven"; return false; }
            runtime = new(acquired, actual, ordinary, recovery);
            acquired = null; reason = string.Empty; return true;
        }
        catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
        { reason = "ownedAdmissionStartupRefused:" + error.GetType().Name; return false; }
        finally
        {
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
