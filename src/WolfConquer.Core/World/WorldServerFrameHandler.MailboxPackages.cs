using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly LegacyMailboxPackageProducer? _mailboxPackages;

    internal WorldServerFrameHandler(CharacterSnapshotLoader snapshotLoader, AccountHandoffService handoffService,
        WorldDataCatalog worldData, LegacyMailboxPackageProducer mailboxPackages, TextWriter? log = null,
        ConquerProtocolProfile protocolProfile = ConquerProtocolProfile.RootD11_7899)
        : this(snapshotLoader, handoffService, worldData, LegacyRuntimeUpdateLoopOptions.Disabled, log,
            protocolProfile: protocolProfile)
    {
        ArgumentNullException.ThrowIfNull(mailboxPackages);
        if (!mailboxPackages.IsCurrent(snapshotLoader.UsersRoot, snapshotLoader.PlayerItemsRoot,
                _mailboxes?.ClaimRoot, mailboxPackages.Recovery) ||
            string.IsNullOrWhiteSpace(snapshotLoader.PlayersCombatGearRoot) ||
            !Path.TrimEndingDirectorySeparator(Path.GetFullPath(snapshotLoader.PlayersCombatGearRoot)).Equals(mailboxPackages.CombatGearRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("Mailbox package producer differs from this world's explicit storage owners.");
        _mailboxPackages = mailboxPackages;
        _tradeRecovery = mailboxPackages.Recovery;
        mailboxPackages.BindPacketOperations(_worldOperations);
        _sessions.BindMailboxPackages(mailboxPackages);
    }
}
