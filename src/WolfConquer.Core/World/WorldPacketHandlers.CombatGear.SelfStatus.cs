using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyCombatGearSelfStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyCombatGearSelfStatusDefinition(
                spell.Id,
                spell,
                out var statusFlag,
                out var archiveDamage,
                out var archiveSpellLevel,
                out var storeWildPhoenixRuntimeValues))
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var updatedCharacter = WithLegacySourceAddSpellFlag(resourceSession.Character, statusFlag, removeOnDead: true);
        if (storeWildPhoenixRuntimeValues)
        {
            updatedCharacter = WithCharacterAttribute(
                WithCharacterAttribute(
                    updatedCharacter,
                    RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute,
                    archiveDamage),
                RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute,
                archiveSpellLevel);
        }

        var updatedSession = resourceSession with
        {
            Character = updatedCharacter,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                updatedContext,
                updatedSession,
                statusFlag,
                spell.Duration,
                archiveDamage,
                archiveSpellLevel,
                visible: true,
                cancellationToken)
            .ConfigureAwait(false);

        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            0,
            0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: false, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearPassiveEquipmentRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CombatGear self-status magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} statusFlag={statusFlag} " +
                $"duration={spell.Duration} archiveDamage={archiveDamage} " +
                $"archiveSpellLevel={archiveSpellLevel} equipmentRefreshPackets=1; " +
                "source=MsgAttackPacket.ProcescMagic -> CombatGearAttack.Execute.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryGetLegacyCombatGearSelfStatusDefinition(
        ushort spellId,
        LegacyMagicRuntimeSpell spell,
        out int statusFlag,
        out uint archiveDamage,
        out uint archiveSpellLevel,
        out bool storeWildPhoenixRuntimeValues)
    {
        switch (spellId)
        {
            case LegacyCombatGearPhoenixBlessingSpellId:
                statusFlag = LegacyCombatGearPhoenixBlessingFlag;
                archiveDamage = spell.DamageOnHuman;
                archiveSpellLevel = 0;
                storeWildPhoenixRuntimeValues = false;
                return true;
            case LegacyCombatGearHolyProtectionSpellId:
                statusFlag = LegacyCombatGearHolyProtectionFlag;
                archiveDamage = spell.DamageOnHuman;
                archiveSpellLevel = 0;
                storeWildPhoenixRuntimeValues = false;
                return true;
            case LegacyCombatGearWildPhoenixSpellId:
                statusFlag = LegacyCombatGearWildPhoenixFlag;
                archiveDamage = CalculateLegacyCombatGearWildPhoenixPackedDamage(spell);
                archiveSpellLevel = spell.Damage;
                storeWildPhoenixRuntimeValues = true;
                return true;
            default:
                statusFlag = 0;
                archiveDamage = 0;
                archiveSpellLevel = 0;
                storeWildPhoenixRuntimeValues = false;
                return false;
        }
    }

    private static uint CalculateLegacyCombatGearWildPhoenixPackedDamage(LegacyMagicRuntimeSpell spell)
    {
        return unchecked((spell.DamageOnHuman * 1_000U) + spell.Damage2);
    }
}
