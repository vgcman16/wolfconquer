using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyRootEntryPreparation(
        PlayerSpellSnapshotEntry Book, LegacyMagicRuntimeSpell SourceSpell,
        LegacyMagicRuntimeSpell EffectiveSpell, LegacyNativeMagicRow NativeRow,
        LegacyRootPreparedCosts Costs, LegacyRootManualMpPreparation? Mp);

    private static bool TryPrepareLegacyRootEntry(
        WorldPacketHandlerContext context, WorldSession expected, MsgAttackQuery query,
        ushort requestedId, bool ignoreAttackStamp,
        out LegacyRootEntryPreparation plan, out string reason)
    {
        plan = null!;
        reason = string.Empty;
        if (context.WorldData is not { } data ||
            !LegacyMagicRuntime.TryResolveNativeLearnedSpell(
                expected.PlayerSpells, data, requestedId, out var book, out var spell, out var native))
            return false;
        var passive = spell.Id is >= 15_860 and <= 15_940 ||
            (data.MagicEffectsById.TryGetValue((long)spell.Id * 100, out var effects) &&
             effects.FirstOrDefault()?.DescriptionEx?.Contains("Passive", StringComparison.Ordinal) == true);
        if (passive && expected.LegacyRandomSpell != spell.Id &&
            !HasLegacyStatusFlag(expected.Character, LegacyArrowBladesFlagRegistration.WaterFlag)) return false;
        var weaponSpell = IsLegacyNativeRootWeaponSpell(data, spell.Id);
        if (weaponSpell && expected.LegacyRandomSpell != spell.Id) return false;
        if (!IsLegacyRootEntryRangeValid(context, expected, query, spell)) return false;
        if (!PassesLegacyRootPlayerBasicEligibility(context, expected, query, spell, out reason)) return false;

        var now = (context.TimeProvider ?? TimeProvider.System).GetUtcNow();
        if (!TryPrepareLegacyRootCadence(context, expected, spell, now, ignoreAttackStamp,
                out var cadence, out reason) ||
            !HasLegacyRootStaminaForPreflight(expected, spell, out reason) ||
            !TryPrepareLegacyRootCooldown(context, expected, spell, now, out var cooldowns, out reason))
            return false;
        ValidateLegacyRootKnownManualBounds(context, expected, now);
        var requiredMana = ReadLegacyRootRequiredMana(context, expected, spell);
        var mp = PrepareLegacyRootManualMp(context, expected, spell.Id, requiredMana);
        if (!TryPrepareLegacyRootCosts(context, expected, spell, cooldowns, requiredMana, mp,
                out var costs, out reason))
            return false;

        var candidate = costs.Candidate;
        if (!ignoreAttackStamp)
            candidate = candidate with { Character = WithLegacyPlayerAttackStamp(candidate.Character, now) };
        // Validate/consume the original entitlement before a manual MP proc can
        // earn its own distinct 1175 entitlement. Neither writes live state here.
        if (weaponSpell) candidate = candidate with { LegacyRandomSpell = 0 };
        if (mp is not null) candidate = candidate with { LegacyRandomSpell = LegacyWeaponEffectMpSpellId };
        plan = new(book, spell, spell with { CoolDown = cadence }, native,
            costs with { Candidate = candidate }, mp);
        return true;
    }

    private static bool IsLegacyRootEntryRangeValid(
        WorldPacketHandlerContext context, WorldSession expected, MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell) =>
        spell.Sort is not (LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack) ||
        IsInLegacyRootFirstFamilyRange(context, expected, query.OpponentUid, spell.Range);

    private static bool IsLegacyRootEntryPreparationCurrent(
        WorldPacketHandlerContext context, WorldSession expected, MsgAttackQuery query,
        LegacyRootEntryPreparation plan) =>
        context.WorldData is { } data &&
        LegacyMagicRuntime.TryResolveNativeLearnedSpell(expected.PlayerSpells, data,
            plan.SourceSpell.Id, out var book, out var spell, out var native) &&
        ReferenceEquals(book, plan.Book) && ReferenceEquals(native, plan.NativeRow) &&
        spell == plan.SourceSpell && IsLegacyRootEntryRangeValid(context, expected, query, spell) &&
        PassesLegacyRootPlayerBasicEligibility(context, expected, query, spell, out _);
}
