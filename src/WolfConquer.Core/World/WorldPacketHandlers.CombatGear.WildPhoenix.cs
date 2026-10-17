using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private enum LegacyWildPhoenixDamageTargetKind
    {
        Player,
        MonsterOrSobNpc,
    }

    private static uint ApplyLegacyCombatGearWildPhoenixAttackEffect(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        uint damage,
        LegacyWildPhoenixDamageTargetKind targetKind)
    {
        if (damage == 0 ||
            !IsLegacyCombatGearWildPhoenixDamageSpell(spell.Id) ||
            !HasLegacyCombatGearEquippedForClass(
                session.CombatGear ?? CombatGearSnapshot.Empty,
                session.Character.Class,
                LegacyCombatGearTaoist5ItemId) ||
            !HasLegacyStatusFlag(session.Character, LegacyCombatGearWildPhoenixFlag) ||
            !TryReadUInt32Attribute(
                session.Character.Attributes,
                out var packedDamage,
                RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute))
        {
            return damage;
        }

        var percent = targetKind == LegacyWildPhoenixDamageTargetKind.Player
            ? packedDamage % 100U
            : packedDamage / 1_000U;
        if (percent == 0)
        {
            return damage;
        }

        var bonus = MulDivToUInt32(damage, percent, 100);
        return AddLegacySaturating(damage, bonus);
    }

    private static async ValueTask<(WorldActiveSession ActiveSession, bool Applied)>
        TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
            WorldPacketHandlerContext context,
            WorldActiveSession activeTarget,
            CancellationToken cancellationToken)
    {
        var targetSession = activeTarget.Session;
        if (!HasLegacyStatusFlag(targetSession.Character, LegacyCombatGearWildPhoenixFlag) ||
            !TryReadUInt32Attribute(
                targetSession.Character.Attributes,
                out var charges,
                RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute) ||
            charges == 0)
        {
            return (activeTarget, false);
        }

        var packedDamage = ReadAttributeUInt32(
            targetSession.Character.Attributes,
            RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute);
        var duration = 0U;
        if (context.WorldData is { } worldData &&
            TryResolveLegacyPlayerMagicRuntimeSpell(
                targetSession,
                worldData,
                LegacyCombatGearWildPhoenixSpellId,
                out _,
                out var spell))
        {
            packedDamage = CalculateLegacyCombatGearWildPhoenixPackedDamage(spell);
            duration = spell.Duration;
        }

        var nextCharges = charges - 1U;
        var updatedCharacter = targetSession.Character;
        if (charges == 1)
        {
            updatedCharacter = WithoutLegacyStatusFlag(updatedCharacter, LegacyCombatGearWildPhoenixFlag);
        }

        updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute,
                packedDamage),
            RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute,
            nextCharges);
        var updatedSession = targetSession with { Character = updatedCharacter };
        context.Sessions?.Set(activeTarget.Connection, updatedSession);
        var targetContext = context with
        {
            Connection = activeTarget.Connection,
            Session = updatedSession,
        };

        if (charges == 1)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    targetContext,
                    activeTarget.Connection,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                targetContext,
                updatedSession,
                LegacyCombatGearWildPhoenixFlag,
                duration,
                packedDamage,
                nextCharges,
                visible: true,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy WildPhoenix defender charge " +
                $"targetUid={updatedSession.Character.Uid} previousCharges={charges} " +
                $"remainingCharges={nextCharges} damageOverride=1; " +
                "source=Calculate.Magic/Physical/Range target.Owner.CombatGear.WildPhoenix().")
            .ConfigureAwait(false);
        return (activeTarget with { Session = updatedSession }, true);
    }

    private static bool IsLegacyCombatGearWildPhoenixDamageSpell(ushort spellId)
    {
        return spellId is 1_165 or 1_001 or 1_002;
    }
}
