using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyCombatGearMagneticBattlePowerRollAttribute =
        "LegacyCombatGearMagneticBattlePowerRoll";

    private static bool CanPrepareLegacyNestedProcTarget(WorldPacketHandlerContext context, WorldSession owner,
        WorldSession target, LegacyMagicRuntimeSpell spell, out LegacyPlayerMagicGateFailureEffect effect)
    {
        effect = LegacyPlayerMagicGateFailureEffect.None;
        if (owner.Character.MinHitPoints <= 0 || target.Character.MinHitPoints <= 0 ||
            !CanSeePlayer(owner.Character, target.Character) ||
            (spell.Sort is LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack) &&
                LegacyGetDistance(owner.Character.X, owner.Character.Y, target.Character.X, target.Character.Y) > spell.Range)
            return false;
        return CanLegacyPlayerMagicDamageTarget(context, owner, target, spell, out _, out effect) &&
            (spell.Id != LegacyCombatGearMagneticLightSpellId || context.WorldData is { } world &&
                CheckLegacyCombatGearMagneticBattlePowerRoll(owner, target, world));
    }

    private static async ValueTask SendLegacyNestedProcGateFailureAsync(WorldPacketHandlerContext context,
        ConquerTcpConnection targetConnection, WorldSession target, WorldSession owner,
        LegacyPlayerMagicGateFailureEffect effect, CancellationToken cancellationToken)
    {
        if (effect == LegacyPlayerMagicGateFailureEffect.ForbiddenPirate)
            await SendScalarUpdateAsync(context, owner.Character, LegacyMsgUpdateDataType.ForbiddenPirate, 1, cancellationToken)
                .ConfigureAwait(false);
        else if (effect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
            await TrySendLegacyPlayerHellVortexCounterAsync(context, owner,
                new WorldActiveSession(targetConnection, target), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<(WorldSession DefenderSession, WorldSession AttackerSession,
        bool Allowed, LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TryEnterLegacyCombatGearNestedPlayerTargetAsync(
            WorldPacketHandlerContext defenderContext,
            ConquerTcpConnection attackerConnection,
            WorldSession attackerSession,
            WorldSession defenderSession,
            LegacyMagicRuntimeSpell spell,
            LegacyNestedProcAuthorization authorization,
            CharacterSnapshot authorizedOwner,
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
        if (authorization.Receipt is { } receipt && !receipt.RequirePreparedCurrent()) return Result(false, Refresh());
        // Target permissions/RNG were prepared before debit. A changed actor
        // cannot inherit that authorization; this is a committed miss, not a
        // second admission, second roll or refund of the original outer hit.
        if (!ReferenceEquals(defenderSession.Character, authorizedOwner) ||
            !ReferenceEquals(attackerSession.Character, authorization.Target) ||
            !ReferenceEquals(defenderSession.PlayerItems, authorization.Items) ||
            !ReferenceEquals(defenderSession.PlayerSpells, authorization.Spells) ||
            defenderSession.AlternateEquipmentActive != authorization.AlternateEquipment ||
            !CanSeePlayer(defenderSession.Character, attackerSession.Character))
            return Result(false);

        defenderSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                defenderContext, defenderSession,
                [new LegacyPlayerMagicTarget(new WorldActiveSession(attackerConnection, attackerSession))],
                cancellationToken)
            .ConfigureAwait(false);
        reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None) return Result(false, reason);

        return Result(ReferenceEquals(attackerSession.Character, authorization.Target));
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
