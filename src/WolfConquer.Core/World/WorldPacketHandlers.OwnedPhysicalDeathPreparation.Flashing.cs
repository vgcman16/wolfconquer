namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        // A captured lease is still DATA here. Only this actual death plan and
        // its exact canonical after-image can authorize the native participant.
        internal bool TryReadNativeFlashingDeathProposal(LegacyOrdinarySpellBinding binding,
            WorldSession canonicalTarget,
            out LegacyNativeStatusFlagsBigVector32.OwnedDragonhowlFlashingLease? lease)
        {
            lease = nativeStatus.Flashing;
            if (!IsCurrentPlain(binding) || !CanonicalTargetMatches(canonicalTarget)) return false;
            if (lease is null) return true;
            return lease.IsCurrentPlain(ExpectedTarget) &&
                canonicalTarget.CanAttackFlashingRegistration is null &&
                !LegacyPlayerDeathState.HasStatusFlag(canonicalTarget.Character.Attributes, 0) &&
                !canonicalTarget.Character.Attributes.Keys.Any(key =>
                    key.Equals(LegacyStatusFlagDeathRegistration.Key(0), StringComparison.OrdinalIgnoreCase));
        }
    }
}
