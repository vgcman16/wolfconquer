namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyGenericMagicAdmissionPlan(WorldSession Candidate, byte[][] Frames);

    private static bool TryPrepareLegacyGenericMagicPlan(
        WorldPacketHandlerContext context, WorldSession expected, LegacyMagicRuntimeSpell spell,
        bool ignoreAttackStamp, DateTimeOffset now,
        out LegacyGenericMagicAdmissionPlan plan, out string reason)
    {
        plan = null!;
        reason = string.Empty;
        if (spell.ColdTime < 0) { reason = "coldTimeInvalid"; return false; }
        // Pounce's selected-left shield is required for the entire executor,
        // not a post-cost proc. Validate the captured current owner, rather
        // than the caller's possibly stale context, before cadence or costs.
        if (LegacyMagicSortCapabilities.IsSupportedPounceRuntimeSpell(spell.Sort, spell.Id) &&
            !TryGetLegacyPounceActiveShield(expected, out _))
        { reason = "activeLeftShield=false"; return false; }
        if (!TryPrepareLegacyGenericMagicCadence(
                expected, spell, ignoreAttackStamp, now, out var candidate, out reason)) return false;

        var frames = new List<byte[]>();
        if (!TryPrepareLegacyGenericMagicResources(
                context, candidate, spell, frames, out candidate, out var graceOfHeaven, out reason)) return false;
        if (!TryPrepareLegacyGenericMagicCooldown(
                candidate, spell, graceOfHeaven, now, frames, out candidate, out reason)) return false;
        plan = new(candidate, frames.ToArray());
        return true;
    }

    private static bool TryPrepareLegacyGenericMagicCadence(
        WorldSession expected, LegacyMagicRuntimeSpell spell, bool ignoreAttackStamp, DateTimeOffset now,
        out WorldSession candidate, out string reason)
    {
        candidate = expected;
        reason = string.Empty;
        if (ignoreAttackStamp || !TryGetLegacyPlayerMagicAttackStampCadence(spell, out var cadence)) return true;
        if (cadence < TimeSpan.Zero) { reason = "attackStampCadenceInvalid"; return false; }
        if (TryReadLegacyPlayerAttackStamp(expected.Character, out var stamp))
        {
            var remaining = LegacyMagicRuntime.GetLegacyCooldownRemainingMilliseconds(
                stamp.ToUniversalTime() + cadence, now);
            if (remaining > 0) { reason = $"attackStampRemainingMs={remaining}"; return false; }
        }
        candidate = expected with { Character = WithLegacyPlayerAttackStamp(expected.Character, now) };
        return true;
    }

    private static bool TryPrepareLegacyGenericMagicCooldown(
        WorldSession expected, LegacyMagicRuntimeSpell spell, bool graceOfHeaven, DateTimeOffset now,
        List<byte[]> frames, out WorldSession candidate, out string reason)
    {
        candidate = expected;
        reason = string.Empty;
        if (!LegacyMagicRuntime.UsesLegacyCooldown(spell) || graceOfHeaven ||
            HasLegacyStatusFlag(expected.Character, LegacyCannonBarrageFlag) ||
            HasLegacyStatusFlag(expected.Character, LegacyBlackbeardsRageFlag) ||
            HasLegacyStatusFlag(expected.Character, LegacyHeavensWrathFlag)) return true;

        DateTimeOffset? current = expected.MagicCooldowns is not null &&
            expected.MagicCooldowns.TryGetValue(spell.Id, out var readyAt) ? readyAt : null;
        var cooldown = LegacyMagicRuntime.TryStartLegacyCooldown(spell, now, current);
        if (!cooldown.Ready)
        {
            reason = $"cooldownRemainingMs={cooldown.RemainingMilliseconds}";
            return false;
        }
        var staged = expected.MagicCooldowns is null ? new Dictionary<ushort, DateTimeOffset>() :
            new Dictionary<ushort, DateTimeOffset>(expected.MagicCooldowns);
        staged[spell.Id] = cooldown.ReadyAtUtc;
        frames.Add(LegacyMagicColdTimePacket.FromStartedCooldown(spell).ToFrameBytes());
        candidate = expected with { MagicCooldowns = staged };
        return true;
    }
}
