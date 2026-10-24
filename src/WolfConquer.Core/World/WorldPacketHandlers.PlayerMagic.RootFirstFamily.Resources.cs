namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyRootPreparedCosts(WorldSession Candidate, byte[][] Frames);

    private static bool TryPrepareLegacyRootCosts(
        WorldPacketHandlerContext context, WorldSession expected, LegacyMagicRuntimeSpell spell,
        IReadOnlyDictionary<ushort, DateTimeOffset>? cooldowns, ushort useMana,
        LegacyRootManualMpPreparation? mp,
        out LegacyRootPreparedCosts plan, out string reason)
    {
        plan = null!;
        reason = string.Empty;
        var candidate = expected;
        var frames = new List<byte[]>();
        var uid = CheckedUInt32(expected.Character.Uid, nameof(expected.Character.Uid));
        var character = expected.Character;
        if (mp is not null) character = character with { MinMana = mp.Mana };
        if (character.MapId != 1_039)
        {
            var useStamina = (uint)spell.UseStamina +
                (HasLegacyStatusFlag(character, LegacyScurvyBombFlag) ? 6U : 0U);
            var stamina = ReadLegacyRuntimeStamina(character);
            if (stamina < useStamina)
            { reason = $"stamina={stamina} requiredStamina={useStamina}"; return false; }
            if (character.MinMana < useMana)
            { reason = $"mana={character.MinMana} requiredMana={useMana}"; return false; }
            if (useStamina > 0)
            {
                var remaining = stamina - useStamina;
                character = WithCharacterAttribute(character, "Stamina", remaining);
                frames.Add(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(uid, remaining).ToFrameBytes());
            }
            if (useMana > 0)
            {
                var remaining = character.MinMana - useMana;
                var wireMana = CheckedUInt32(remaining, nameof(character.MinMana));
                character = character with { MinMana = remaining };
                frames.Add(new LegacyMsgUpdatePacket(uid,
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, wireMana)]).ToFrameBytes());
            }
        }
        if (!ReferenceEquals(character, expected.Character)) candidate = candidate with { Character = character };
        if (!ReferenceEquals(cooldowns, expected.MagicCooldowns)) candidate = candidate with { MagicCooldowns = cooldowns };
        plan = new(candidate, frames.ToArray());
        return true;
    }

    private static ushort ReadLegacyRootRequiredMana(
        WorldPacketHandlerContext context, WorldSession expected, LegacyMagicRuntimeSpell spell) =>
        // Zero/bypassed mana must not depend on optional MPMaster/rune metadata.
        expected.Character.MapId == 1_039 || spell.UseMana == 0 ? (ushort)0 :
        LegacyMagicResourceCostCalculator.Calculate(spell, expected.PlayerItems,
            context.WorldData, expected.Character.Attributes,
            expected.AlternateEquipmentActive, expected.Character.Class).UseMana;

    private static bool TryPrepareLegacyRootCooldown(
        WorldPacketHandlerContext context, WorldSession expected, LegacyMagicRuntimeSpell spell,
        DateTimeOffset now, out IReadOnlyDictionary<ushort, DateTimeOffset>? cooldowns, out string reason)
    {
        cooldowns = expected.MagicCooldowns;
        reason = string.Empty;
        if (spell.ColdTime < 0) { reason = "coldTimeInvalid"; return false; }
        if (!LegacyMagicRuntime.UsesLegacyCooldown(spell) ||
            HasLegacyStatusFlag(expected.Character, LegacyCannonBarrageFlag) ||
            HasLegacyStatusFlag(expected.Character, LegacyBlackbeardsRageFlag)) return true;
        var provider = context.TimeProvider ?? TimeProvider.System;
        var localNow = LegacyArrowBladesFlagRegistration.LocalTime(provider, now);
        if (cooldowns is not null && cooldowns.TryGetValue(spell.Id, out var readyAt) &&
            LegacyArrowBladesFlagRegistration.LocalTime(provider, readyAt) > localNow)
        {
            reason = $"cooldownRemainingMs={LegacyMagicRuntime.GetLegacyCooldownRemainingMilliseconds(readyAt, now)}";
            return false;
        }
        var power = ReadLegacyRootColdSigilPower(context, expected, spell.Id);
        if (power < 0) { reason = "coldSigilPowerInvalid"; return false; }
        // A legitimate nonnegative reduction can remove the whole delay, never
        // wrap signed arithmetic or authorize a deadline in the past.
        var duration = Math.Max(0L, (long)spell.ColdTime - power);
        var localDeadline = localNow.AddMilliseconds(duration);
        var deadline = new DateTimeOffset(localDeadline, provider.LocalTimeZone.GetUtcOffset(localDeadline));
        var staged = cooldowns is null ? new Dictionary<ushort, DateTimeOffset>() :
            new Dictionary<ushort, DateTimeOffset>(cooldowns);
        staged[spell.Id] = deadline;
        cooldowns = staged;
        return true;
    }

    private static bool HasLegacyRootStaminaForPreflight(
        WorldSession session, LegacyMagicRuntimeSpell spell, out string reason)
    {
        reason = string.Empty;
        if (session.Character.MapId == 1_039) return true;
        var required = (uint)spell.UseStamina +
            (HasLegacyStatusFlag(session.Character, LegacyScurvyBombFlag) ? 6U : 0U);
        var available = ReadLegacyRuntimeStamina(session.Character);
        if (available >= required) return true;
        reason = $"stamina={available} requiredStamina={required}";
        return false;
    }
}
