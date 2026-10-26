using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal WorldServerFrameHandler(CharacterSnapshotLoader loader, AccountHandoffService handoff,
        WorldDataCatalog worldData, LegacyMailboxPackageProducer mailboxPackages,
        LegacyOrdinaryAdmissionUidOwner ordinaryAdmissionUids, TextWriter? log = null,
        ConquerProtocolProfile protocolProfile = ConquerProtocolProfile.VisibleDx8D13)
        : this(loader, handoff, worldData, LegacyRuntimeUpdateLoopOptions.Disabled, log, protocolProfile: protocolProfile)
    {
        ArgumentNullException.ThrowIfNull(mailboxPackages);
        ArgumentNullException.ThrowIfNull(ordinaryAdmissionUids);
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(loader);
        if (!ReferenceEquals(mailboxPackages.Registry, ordinaryAdmissionUids.Registry) ||
            mailboxPackages.Domain != ordinaryAdmissionUids.Domain ||
            !ordinaryAdmissionUids.IsCurrent(roots) ||
            !mailboxPackages.IsCurrent(loader.UsersRoot, loader.PlayerItemsRoot, _mailboxes?.ClaimRoot, mailboxPackages.Recovery) ||
            !SameCombinedRoute(roots.CombatGear, mailboxPackages.CombatGearRoot) ||
            !SameCombinedRoute(ordinaryAdmissionUids.MailboxRoot, mailboxPackages.MailboxRoot) ||
            !mailboxPackages.Recovery.IsReadyForOrdinaryAdmission(roots))
            throw new ArgumentException("Combined admission requires both current explicit owners of the same complete storage domain.");
        // All actual owner/domain/route/readiness checks precede producer/Store
        // binding. No producer is created or inferred by this configuration join.
        _ordinaryAdmissionUids = ordinaryAdmissionUids;
        _mailboxPackages = mailboxPackages;
        _tradeRecovery = mailboxPackages.Recovery;
        mailboxPackages.BindPacketOperations(_worldOperations);
        _sessions.BindMailboxPackages(mailboxPackages);
    }

    private static bool SameCombinedRoute(string? expected, string actual) => !string.IsNullOrWhiteSpace(expected) &&
        Path.IsPathFullyQualified(expected) && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected)), actual,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
