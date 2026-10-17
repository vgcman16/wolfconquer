using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyPlayerMagicReceiveAbortReason RefreshLegacyBackDamageParticipants(
        ref WorldPacketHandlerContext context,
        ref WorldActiveSession target,
        ref CharacterSnapshot attacker,
        long attackerUid,
        long targetUid)
    {
        if (context.Sessions is not { } sessions)
        {
            return LegacyPlayerMagicReceiveAbortReason.None;
        }

        if (!sessions.TryGet(context.Connection, out var currentAttacker) ||
            currentAttacker is null || currentAttacker.Character.Uid != attackerUid)
        {
            return LegacyPlayerMagicReceiveAbortReason.AttackerLost;
        }

        context = context with { Session = currentAttacker };
        attacker = currentAttacker.Character;
        if (!sessions.TryGet(target.Connection, out var currentTarget) ||
            currentTarget is null || currentTarget.Character.Uid != targetUid)
        {
            return LegacyPlayerMagicReceiveAbortReason.TargetLost;
        }

        target = target with { Session = currentTarget };
        return LegacyPlayerMagicReceiveAbortReason.None;
    }

    private static (WorldPacketHandlerContext Context, WorldActiveSession Target,
        CharacterSnapshot Attacker, LegacyPlayerMagicReceiveAbortReason AbortReason)
        CreateLegacyBackDamageAbort(
            WorldPacketHandlerContext context,
            WorldActiveSession target,
            CharacterSnapshot attacker,
            long attackerUid,
            LegacyPlayerMagicReceiveAbortReason reason)
    {
        if (reason == LegacyPlayerMagicReceiveAbortReason.TargetLost && context.Sessions is { } sessions)
        {
            if (!sessions.TryGet(context.Connection, out var current) ||
                current is null || current.Character.Uid != attackerUid)
            {
                reason = LegacyPlayerMagicReceiveAbortReason.AttackerLost;
            }
            else
            {
                context = context with { Session = current };
                attacker = current.Character;
            }
        }

        return (context, target, attacker, reason);
    }
}
