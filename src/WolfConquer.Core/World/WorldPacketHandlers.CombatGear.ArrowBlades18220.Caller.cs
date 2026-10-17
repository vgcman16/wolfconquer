using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool HasLegacyStoneCrackerSource(WorldSession session) =>
        HasLegacyCombatGearEquippedForClass(session.CombatGear ?? CombatGearSnapshot.Empty,
            session.Character.Class, LegacyCombatGearStoneCrackerItemId);

    private static async ValueTask<bool> TryHandleLegacyCombatGearArrowBladesCallerAsync(
        WorldPacketHandlerContext context, WorldSession session, MsgAttackQuery query,
        CancellationToken cancellationToken)
    {
        var uid = session.Character.Uid;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session)) return true;
        if (!HasLegacyStoneCrackerSource(session) ||
            !HasLegacyStatusFlag(session.Character, LegacyArrowBladesFlagRegistration.WaterFlag) ||
            HasLegacyPirateCombatGearEquipped(session)) return false;
        context = context with { Session = session };
        if (context.WorldData is not { } worldData ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData,
                LegacyCombatGearStoneCrackerWaterAttackSpellId, out var playerSpell, out var spell))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer consumed selected StoneCracker18220 attackerUid={uid}; unavailable learned spell/catalog row; " +
                    "source=MyCombatGear.CheckCombatGear exact333; no availability resampling.")
                .ConfigureAwait(false);
            return true;
        }
        var prepared = await TryPrepareLegacyPlayerMagicCastAsync(
                context, session, spell, ignoreAttackStamp: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (prepared is null) return true;
        session = prepared;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session) || session.Character.MinHitPoints <= 0)
            return true;
        query = query with
        {
            AttackType = MsgAttackType.Magic, Damage = 0, KilledMonster = 0, ResponseDamage = 0,
            Effect = MsgAttackEffect.None, SpellId = spell.Id, SpellLevel = playerSpell.Level,
        };
        return await ExecuteLegacyPreparedCombatGearArrowBlades18220Async(
                context with { Session = session }, session, query, playerSpell, spell, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<bool> HandleLegacyPlayerArrowBladesMagicAsync(
        WorldPacketHandlerContext context, WorldSession session, MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell, LegacyMagicRuntimeSpell spell, bool ignoreAttackStamp,
        CancellationToken cancellationToken)
    {
        if (spell.Id != LegacyCombatGearStoneCrackerWaterAttackSpellId) return false;
        if (!session.PlayerSpells.ContainsSpell(spell.Id)) return true;
        var uid = session.Character.Uid;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session)) return true;
        // MagicType340 reads Passive from MagicEffect.ini[id + "00"].DescEx, not a magictype numeric column.
        if (!HasPreparedLegacyRootFirstFamilyInvocation(context, session, spell) &&
            !ignoreAttackStamp && !HasLegacyStatusFlag(session.Character, LegacyArrowBladesFlagRegistration.WaterFlag) &&
            context.WorldData is { } worldData &&
            worldData.MagicEffectsById.TryGetValue((long)spell.Id * 100, out var effects) &&
            effects.FirstOrDefault()?.DescriptionEx?.Contains("Passive", StringComparison.Ordinal) == true)
            return true; // ProcescMagic910-916 rejects BEFORE AttackStamp/resources.
        if (!HasPreparedLegacyRootFirstFamilyInvocation(context, session, spell) && !ignoreAttackStamp)
        {
            var stamped = TryStartLegacyArrowBladesMagicCadence(context, session, spell);
            if (stamped is null) return true;
            session = stamped;
        }
        var prepared = await TryPrepareLegacyPlayerMagicCastAsync(
                context with { Session = session }, session, spell,
                ignoreAttackStamp: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (prepared is null) return true;
        session = prepared;
        // CombatGearAttack19-23 evaluates Alive AFTER CanUseSpell.Verified resources/cooldown.
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session) || session.Character.MinHitPoints <= 0)
            return true;
        return await ExecuteLegacyPreparedCombatGearArrowBlades18220Async(
                context with { Session = session }, session, query, playerSpell, spell, cancellationToken)
            .ConfigureAwait(false);
    }

    private static WorldSession? TryStartLegacyArrowBladesMagicCadence(
        WorldPacketHandlerContext context, WorldSession session, LegacyMagicRuntimeSpell spell)
    {
        var provider = context.TimeProvider ?? TimeProvider.System;
        var now = provider.GetUtcNow();
        var localNow = LegacyArrowBladesFlagRegistration.LocalTime(provider, now);
        var milliseconds = spell.CoolDown == 0 ? 500 : (int)spell.CoolDown;
        if (milliseconds is > 1_000 and < 3_000) milliseconds = 2_500;
        if (TryReadLegacyPlayerAttackStamp(session.Character, out var stamp) &&
            localNow < LegacyArrowBladesFlagRegistration.LocalTime(provider, stamp).AddMilliseconds(milliseconds))
            return null;
        session = session with { Character = WithLegacyPlayerAttackStamp(session.Character, now) };
        context.Sessions?.Set(context.Connection, session);
        return session;
    }

    private static async ValueTask<WorldSession?> ReceiveLegacyStoneCrackerNonPlayerCounterAsync(
        WorldPacketHandlerContext context, WorldSession session, ushort x, ushort y, CancellationToken cancellationToken)
    {
        if (!HasLegacyStoneCrackerSource(session)) return session;
        var uid = session.Character.Uid;
        var outcome = await TrySendLegacyCombatGearStoneCrackerSobCounterUpdateAsync(
                context, session, x, y, cancellationToken).ConfigureAwait(false);
        if (outcome.AbortReason != LegacyPlayerMagicReceiveAbortReason.None) return null;
        session = outcome.Session;
        return TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session) ? session : null;
    }
}
