using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyPlayerReceiveEarlyModifierResult>
        ApplyLegacyPlayerReceiveEarlyModifiersAsync(
            WorldPacketHandlerContext context, WorldActiveSession target,
            CharacterSnapshot attacker, uint damage, CancellationToken cancellationToken,
            bool applyMudWall)
    {
        var phase = new LegacyPlayerReceiveEarlyModifierExecution(context, target, attacker, damage);
        if (!phase.Refresh()) return phase.Complete();
        if (phase.AttackerSession is { } attackerSession)
        {
            phase.InfiniteMist = TryApplyLegacyPlayerReceiveInfiniteMist(
                attackerSession, phase.Target.Session, context.WorldData, out var mistAttacker);
            if (!mistAttacker.Equals(attackerSession.Character) &&
                !phase.CommitAttacker(_ => mistAttacker)) return phase.Complete();
        }

        if (!phase.CommitAttacker(current => WithCharacterAttribute(current,
                RuntimeLegacyThundercloudSightAttribute, CheckedUInt32(target.Session.Character.Uid,
                    nameof(target.Session.Character.Uid)))) ||
            !phase.CommitTarget(current => WithCharacterAttribute(current,
                RuntimeLegacyThundercloudSightAttribute, CheckedUInt32(attacker.Uid, nameof(attacker.Uid)))))
            return phase.Complete();
        if (HasLegacyStatusFlag(phase.Attacker, LegacyHookMoonCasterFlag))
            phase.Damage = MulDivToUInt32(phase.Damage,
                ReadAttributeUInt32(phase.Attacker.Attributes, "HookMoonPower"), 100);
        if (HasLegacyStatusFlag(phase.Attacker, LegacyCleanSweepFlag))
            phase.Damage = AddLegacySaturating(phase.Damage, MulDivToUInt32(phase.Damage,
                ReadAttributeUInt32(phase.Attacker.Attributes, "CleanSweepPower", "LegacyCleanSweepPower"), 100));
        if (HasLegacyStatusFlag(phase.Attacker, LegacyAxeShadowFlag))
            phase.Damage = AddLegacySaturating(phase.Damage, MulDivToUInt32(phase.Damage,
                ReadAttributeUInt32(phase.Attacker.Attributes, "AxeShadowPower", "LegacyAxeShadowPower"), 100));

        if (HasLegacyStatusFlag(phase.Target.Session.Character, LegacyHawksAmbitionFlag))
        {
            var reduction = MulDivToUInt32(phase.Damage,
                ReadAttributeUInt32(phase.Attacker.Attributes, "HawksAmbitionPower"), 100);
            phase.Damage = reduction >= phase.Damage ? 0 : phase.Damage - reduction;
            if (!phase.CommitTarget(current => WithoutLegacyStatusFlag(current, LegacyHawksAmbitionFlag)))
                return phase.Complete();
            phase.HawksConsumed = true;
            if (!await phase.SendTargetVectorAsync(cancellationToken).ConfigureAwait(false) ||
                !await phase.LogHawksAsync().ConfigureAwait(false)) return phase.Complete();
        }

        // The source next reads current attacker state after Hawks RemoveFlag.
        if (HasLegacyStatusFlag(phase.Attacker, LegacyAttackUpFlag))
            phase.Damage = MulDivToUInt32(phase.Damage, 2, 1);
        // Preexisting compatibility placement remains; prepared physical paths pass false.
        if (applyMudWall)
            phase.Damage = ApplyLegacyPlayerReceiveMudWall(phase.Target.Session, context.WorldData, phase.Damage);
        if (!await ApplyLegacyPlayerReceiveLightningShieldPrefixAsync(
                context, phase, cancellationToken).ConfigureAwait(false)) return phase.Complete();
        return phase.Complete();
    }
}
