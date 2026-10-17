using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private enum LegacyPlayerMagicReceiveAbortReason
    {
        None,
        AttackerLost,
        TargetLost,
    }

    private readonly record struct LegacyPlayerMagicReceiveOutcome(
        LegacyPlayerMagicDamageResult? Result,
        WorldSession? AttackerSession,
        LegacyPlayerMagicReceiveAbortReason AbortReason = LegacyPlayerMagicReceiveAbortReason.None);

    // A pending result is private preparation data, never a completed receive.
    private readonly record struct LegacyPlayerMagicReceivePreparation(
        LegacyPlayerMagicDamageResult Result,
        CharacterSnapshot Attacker,
        bool RequiresDamageCommit = false,
        bool FinishAttackerTransform = false,
        bool FinishTargetTransform = false,
        ushort DeathX = 0,
        ushort DeathY = 0);
}
