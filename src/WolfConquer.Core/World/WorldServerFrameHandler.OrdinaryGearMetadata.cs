using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Ordinary admission must use this exact Frame's configured metadata owner.
    internal bool TryCaptureOrdinaryGearMetadata(out LegacyNativeCombatGearTable.MailboxLoadProof? proof) =>
        TryCaptureNativeCombatGearMetadata(_sessions, out proof);
}
