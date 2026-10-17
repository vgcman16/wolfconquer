using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyCombatGearMagneticBattlePowerRollAttribute =
        "LegacyCombatGearMagneticBattlePowerRoll";

    private static async ValueTask<(WorldSession DefenderSession, WorldSession AttackerSession,
        bool Allowed, LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TryEnterLegacyCombatGearNestedPlayerTargetAsync(
            WorldPacketHandlerContext defenderContext,
            ConquerTcpConnection attackerConnection,
            WorldSession attackerSession,
            WorldSession defenderSession,
            LegacyMagicRuntimeSpell spell,
            CancellationToken cancellationToken)
    {
        var attackerUid = attackerSession.Character.Uid;
        var defenderUid = defenderSession.Character.Uid;
        LegacyPlayerMagicReceiveAbortReason Refresh() =>
            RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
                ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
                attackerUid, defenderUid);
        (WorldSession, WorldSession, bool, LegacyPlayerMagicReceiveAbortReason) Result(
            bool allowed, LegacyPlayerMagicReceiveAbortReason reason = LegacyPlayerMagicReceiveAbortReason.None) =>
            (defenderSession, attackerSession, allowed, reason);

        var reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None) return Result(false, reason);
        // This is the NEW nested handler's View lookup, not an incoming Receive re-gate.
        if (!CanSeePlayer(defenderSession.Character, attackerSession.Character))
            return Result(false);

        if (!CanLegacyPlayerMagicDamageTarget(
                defenderContext, defenderSession, attackerSession, spell, out _, out var failureEffect))
        {
            if (failureEffect == LegacyPlayerMagicGateFailureEffect.ForbiddenPirate)
            {
                await SendScalarUpdateAsync(defenderContext, defenderSession.Character,
                        LegacyMsgUpdateDataType.ForbiddenPirate, 1, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
            {
                await TrySendLegacyPlayerHellVortexCounterAsync(
                        defenderContext, defenderSession,
                        new WorldActiveSession(attackerConnection, attackerSession), cancellationToken)
                    .ConfigureAwait(false);
            }
            reason = Refresh();
            return Result(false, reason);
        }

        defenderSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                defenderContext, defenderSession,
                [new LegacyPlayerMagicTarget(new WorldActiveSession(attackerConnection, attackerSession))],
                cancellationToken)
            .ConfigureAwait(false);
        reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None) return Result(false, reason);

        // Source Verified's side effects precede this independent integer Rate draw.
        return Result(spell.Id != LegacyCombatGearMagneticLightSpellId ||
            defenderContext.WorldData is { } worldData &&
            CheckLegacyCombatGearMagneticBattlePowerRoll(defenderSession, attackerSession, worldData));
    }

    private static bool CheckLegacyCombatGearMagneticBattlePowerRoll(
        WorldSession caster, WorldSession target, WorldDataCatalog worldData)
    {
        var casterBattlePower = unchecked((int)CalculateLegacyPlayerPlayerPhysicalBattlePower(caster, worldData));
        var targetBattlePower = unchecked((int)CalculateLegacyPlayerPlayerPhysicalBattlePower(target, worldData));
        var chance = unchecked(100 - 2 * (targetBattlePower - casterBattlePower));
        // Core.Rate(int) draws even for chance <= 0 and chance >= 100.
        var roll = TryReadUInt32Attribute(caster.Character.Attributes, out var forcedRoll,
                RuntimeLegacyCombatGearMagneticBattlePowerRollAttribute)
            ? checked((int)(forcedRoll % 100))
            : Random.Shared.Next(0, 100);
        return chance > roll;
    }
}
