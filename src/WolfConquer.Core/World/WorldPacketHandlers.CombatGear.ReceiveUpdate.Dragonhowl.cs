using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyCombatGearDragonhowlReceiveSpellId = 17_620;
    private const int LegacyCombatGearDragonPierceCritFlag = 0x133;
    private const int LegacyCombatGearDragonPierceTortoiseFlag = 0x134;
    private const int LegacyCombatGearDragonPierceBreakFlag = 0x135;
    private const int LegacyCombatGearDragonPierceStaminaFlag = 0x136;
    private const uint LegacyCombatGearDragonhowlStatusDuration = 10;
    private const string RuntimeLegacyCombatGearDragonPierceTortoiseAttribute = "DragonPierceTortoise";
    private const string RuntimeLegacyCombatGearDragonPierceBreakAttribute = "DragonPierceBreak";
    private const string RuntimeLegacyCombatGearDragonPierceStaminaAttribute = "DragonPierceStamina";
    private const string RuntimeLegacyCombatGearDragonhowlRollAttribute = "LegacyCombatGearDragonhowlRoll";
    private const string RuntimeLegacyCombatGearDragonhowlTortoiseRollAttribute =
        RuntimeLegacyCombatGearDragonhowlRollAttribute + ".17620.Tortoise";
    private const string RuntimeLegacyCombatGearDragonhowlBreakRollAttribute =
        RuntimeLegacyCombatGearDragonhowlRollAttribute + ".17620.Break";

    private static async ValueTask<(WorldSession AttackerSession, bool Applied)>
        TrySendLegacyCombatGearDragonhowlReceivePlayerUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            ushort targetX,
            ushort targetY,
            CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData ||
            !HasLegacyCombatGearEquippedForClass(
                attackerSession.CombatGear ?? CombatGearSnapshot.Empty,
                attackerSession.Character.Class,
                LegacyCombatGearDragonhowlItemId) ||
            !TryResolveLegacyPlayerMagicRuntimeSpell(
                attackerSession,
                worldData,
                LegacyCombatGearDragonhowlReceiveSpellId,
                out var playerSpell,
                out var spell) ||
            HasLegacyStatusFlag(attackerSession.Character, LegacyCombatGearDragonPierceCritFlag))
        {
            return (attackerSession, false);
        }

        var dragonPierceTortoise = spell.DamageOnHuman;
        var dragonPierceBreak = spell.Damage2;
        var dragonPierceStamina = spell.Damage3;

        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    attackerSession.Character,
                    RuntimeLegacyCombatGearDragonPierceTortoiseAttribute,
                    dragonPierceTortoise),
                RuntimeLegacyCombatGearDragonPierceBreakAttribute,
                dragonPierceBreak),
            RuntimeLegacyCombatGearDragonPierceStaminaAttribute,
            dragonPierceStamina);

        updatedCharacter = WithLegacySourceAddFlag(updatedCharacter, LegacyCombatGearDragonPierceCritFlag, removeOnDead: true);
        var rolledTortoise = CheckLegacyCombatGearDragonhowlRoll(
            updatedCharacter,
            RuntimeLegacyCombatGearDragonhowlTortoiseRollAttribute,
            "Tortoise");
        updatedCharacter = WithLegacySourceAddFlag(
            updatedCharacter,
            rolledTortoise
                ? LegacyCombatGearDragonPierceTortoiseFlag
                : LegacyCombatGearDragonPierceStaminaFlag, removeOnDead: true);

        var rolledBreak = CheckLegacyCombatGearDragonhowlRoll(
            updatedCharacter,
            RuntimeLegacyCombatGearDragonhowlBreakRollAttribute,
            "Break");
        updatedCharacter = WithLegacySourceAddFlag(
            updatedCharacter,
            rolledBreak || HasLegacyStatusFlag(updatedCharacter, LegacyCombatGearDragonPierceStaminaFlag)
                ? LegacyCombatGearDragonPierceBreakFlag
                : LegacyCombatGearDragonPierceStaminaFlag, removeOnDead: true);

        var updatedSession = attackerSession with
        {
            Character = updatedCharacter,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyCombatGearDragonhowlArchiveSkillUpdatesAsync(
                updatedContext,
                updatedSession,
                dragonPierceTortoise,
                dragonPierceBreak,
                dragonPierceStamina,
                cancellationToken)
            .ConfigureAwait(false);

        var attackerUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            attackerUid,
            0,
            0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(attackerUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy CombatGear Update player Dragonhowl " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} targetX={targetX} targetY={targetY} " +
                $"tortoise={HasLegacyStatusFlag(updatedCharacter, LegacyCombatGearDragonPierceTortoiseFlag)} " +
                $"break={HasLegacyStatusFlag(updatedCharacter, LegacyCombatGearDragonPierceBreakFlag)} " +
                $"stamina={HasLegacyStatusFlag(updatedCharacter, LegacyCombatGearDragonPierceStaminaFlag)} " +
                $"dragonPierceTortoise={dragonPierceTortoise} dragonPierceBreak={dragonPierceBreak} " +
                $"dragonPierceStamina={dragonPierceStamina}; source=ReceiveAttack.Player.Execute -> " +
                "client.CombatGear.Update(attacked) -> MyCombatGear.Update Dragonhowl -> CombatGearAttack 17620.")
            .ConfigureAwait(false);
        return (updatedSession, true);
    }

    private static async ValueTask SendLegacyCombatGearDragonhowlArchiveSkillUpdatesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint dragonPierceTortoise,
        uint dragonPierceBreak,
        uint dragonPierceStamina,
        CancellationToken cancellationToken)
    {
        if (HasLegacyStatusFlag(session.Character, LegacyCombatGearDragonPierceCritFlag))
        {
            await SendLegacyCombatGearArchiveSkillUpdateAsync(
                    context,
                    session,
                    LegacyCombatGearDragonPierceCritFlag,
                    LegacyCombatGearDragonhowlStatusDuration,
                    damage: 0,
                    spellLevel: 0,
                    visible: false,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (HasLegacyStatusFlag(session.Character, LegacyCombatGearDragonPierceTortoiseFlag))
        {
            await SendLegacyCombatGearArchiveSkillUpdateAsync(
                    context,
                    session,
                    LegacyCombatGearDragonPierceTortoiseFlag,
                    LegacyCombatGearDragonhowlStatusDuration,
                    dragonPierceTortoise,
                    spellLevel: 0,
                    visible: false,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (HasLegacyStatusFlag(session.Character, LegacyCombatGearDragonPierceBreakFlag))
        {
            await SendLegacyCombatGearArchiveSkillUpdateAsync(
                    context,
                    session,
                    LegacyCombatGearDragonPierceBreakFlag,
                    LegacyCombatGearDragonhowlStatusDuration,
                    dragonPierceBreak,
                    spellLevel: 0,
                    visible: false,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (HasLegacyStatusFlag(session.Character, LegacyCombatGearDragonPierceStaminaFlag))
        {
            await SendLegacyCombatGearArchiveSkillUpdateAsync(
                    context,
                    session,
                    LegacyCombatGearDragonPierceBreakFlag,
                    LegacyCombatGearDragonhowlStatusDuration,
                    dragonPierceStamina,
                    spellLevel: 0,
                    visible: false,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static bool CheckLegacyCombatGearDragonhowlRoll(
        CharacterSnapshot character,
        string specificRollAttribute,
        string rollName)
    {
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            specificRollAttribute,
            RuntimeLegacyCombatGearDragonhowlRollAttribute + "." + rollName,
            RuntimeLegacyCombatGearDragonhowlRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < 50;
    }
}
