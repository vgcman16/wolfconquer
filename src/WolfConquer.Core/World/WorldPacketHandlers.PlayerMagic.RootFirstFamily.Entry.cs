using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession Session, PlayerSpellSnapshotEntry Book,
        LegacyMagicRuntimeSpell Spell, LegacyRootFirstFamilyInvocation Capture)?> TryEnterLegacyRootFirstFamilyAsync(
        WorldPacketHandlerContext context, WorldSession session, MsgAttackQuery query,
        LegacyMagicRuntimeSpell selected, bool ignoreAttackStamp, CancellationToken cancellationToken)
    {
        var uid = session.Character.Uid;
        bool Refresh() => TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session);
        if (!Refresh() || context.WorldData is not { } worldData) return null;
        // Native ProcescMagic requires the real learned book/level BEFORE passive and AttackStamp.
        if (!LegacyMagicRuntime.TryResolveNativeLearnedSpell(
                session.PlayerSpells, worldData, selected.Id, out _, out var rootSpell, out _))
            return null;
        selected = rootSpell;
        var passive = selected.Id is >= 15_860 and <= 15_940 ||
            (worldData.MagicEffectsById.TryGetValue((long)selected.Id * 100, out var effects) &&
             effects.FirstOrDefault()?.DescriptionEx?.Contains("Passive", StringComparison.Ordinal) == true);
        if (passive && session.LegacyRandomSpell != selected.Id &&
            !HasLegacyStatusFlag(session.Character, LegacyArrowBladesFlagRegistration.WaterFlag)) return null;

        var provider = context.TimeProvider ?? TimeProvider.System;
        var now = provider.GetUtcNow();
        var localNow = LegacyArrowBladesFlagRegistration.LocalTime(provider, now);
        var hasStamp = TryReadLegacyPlayerAttackStamp(session.Character, out var stamp);
        var localStamp = hasStamp ? LegacyArrowBladesFlagRegistration.LocalTime(provider, stamp) : DateTime.MinValue;
        var milliseconds = (int)LegacyMagicRuntime.ReadNativeCoolDown(worldData, selected);
        if (milliseconds == 0)
        {
            milliseconds = 500;
            LegacyMagicRuntime.NormalizeNativeRootCoolDown(worldData, selected, 500);
        }
        if ((selected.Id == 1_045 && localNow < localStamp.AddMilliseconds(500)) ||
            (selected.Id == 1_115 && localNow < localStamp.AddMilliseconds(800)) ||
            (selected.Id == 11_110 && localNow < localStamp.AddMilliseconds(650)))
        {
            var specialCadence = selected.Id == 1_045 ? 500 : selected.Id == 1_115 ? 800 : 650;
            var remaining = LegacyMagicRuntime.GetLegacyCooldownRemainingMilliseconds(
                stamp.ToUniversalTime().AddMilliseconds(specialCadence), now);
            await RejectLegacyRootFirstFamilyWithDiagnosticAsync(
                    context, session, selected, $"attackStampRemainingMs={remaining}").ConfigureAwait(false);
            return null;
        }
        if (!ignoreAttackStamp)
        {
            if (milliseconds is > 1_000 and < 3_000)
            {
                milliseconds = 2_500;
                LegacyMagicRuntime.NormalizeNativeRootCoolDown(worldData, selected, 2_500);
            }
            var cadence = selected.Id == 11_660 ? 800 : selected.Id == 11_610 ? 1_000 : milliseconds;
            if (selected.Id is not 1_045 and not 1_046 and not 11_005 && localNow < localStamp.AddMilliseconds(cadence))
            {
                var remaining = LegacyMagicRuntime.GetLegacyCooldownRemainingMilliseconds(
                    stamp.ToUniversalTime().AddMilliseconds(cadence), now);
                await RejectLegacyRootFirstFamilyWithDiagnosticAsync(
                        context, session, selected, $"attackStampRemainingMs={remaining}").ConfigureAwait(false);
                return null;
            }
            session = session with { Character = WithLegacyPlayerAttackStamp(session.Character, now) };
            context.Sessions?.Set(context.Connection, session);
            if (!Refresh()) return null;
        }

        // Native public HandlerProcess148 sets OnAutoAttack=false before ProcescMagic952-960.
        var prefix = await ApplyLegacyRootManualItemEffectsAsync(
            context with { Session = session }, session, selected.Id, cancellationToken).ConfigureAwait(false);
        if (prefix is null) return null;
        session = prefix;

        // Pool.WeaponSpells membership is built across every level row before any invocation.
        if (IsLegacyNativeRootWeaponSpell(worldData, selected.Id))
        {
            if (session.LegacyRandomSpell != selected.Id) return null;
            session = session with { LegacyRandomSpell = 0 };
            context.Sessions?.Set(context.Connection, session);
            if (!Refresh()) return null;
        }
        if (!LegacyMagicRuntime.TryResolveNativeLearnedSpell(
                session.PlayerSpells, worldData, selected.Id, out var book, out var spell, out _))
            return null;
        if ((spell.Sort is LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack) &&
            !IsInLegacyRootFirstFamilyRange(context, session, query.OpponentUid, spell.Range)) return null;
        if (spell.Level == selected.Level) spell = spell with { CoolDown = checked((ushort)milliseconds) };
        var capture = new LegacyRootFirstFamilyInvocation(context.Connection, uid, spell.Id, book.Level,
            book.UseSpellSoul > 0 ? book.Id : (ushort)0, NativeCanUseEntered: true, Prepared: false);
        // Later producer package publishes capture.SoulSpellId HERE, before even a failed resource check.
        // This prerequisite deliberately creates no OnSoul session field and no Jiang consumer.
        var prepared = await TryPrepareLegacyRootFirstFamilyCanUseAsync(
                context with { Session = session }, session, spell, cancellationToken).ConfigureAwait(false);
        if (prepared is null) return null;
        return (prepared, book, spell, capture with { Prepared = true });
    }

    private static bool IsLegacyNativeRootWeaponSpell(WorldDataCatalog worldData, ushort spellId) =>
        IsLegacyWeaponProcSpellId(spellId) &&
        worldData.MagicByTypeId.TryGetValue(spellId, out var rows) &&
        rows.Any(row => row.Fields.Count > 22 && uint.TryParse(row.Fields[22], out var type) &&
            HasLegacyWeaponProcSpellWeaponTypes(type));

    private static bool IsInLegacyRootFirstFamilyRange(
        WorldPacketHandlerContext context, WorldSession session, uint opponentUid, byte range)
    {
        // Native IsInSpellRange uses RoleView.TryGetValue -> raw MapView blocks, NOT CanSee.
        // Preserve Monster -> Player -> SobNpc priority and each first-found object's range verdict.
        if (FindLegacyHellVortexSelectedMonster(context, session, opponentUid) is { } monster)
            return LegacyGetDistance(session.Character.X, session.Character.Y, monster.X, monster.Y) <= range;
        if (context.Sessions is { } sessions &&
            FindLegacyMapBlockPlayer(sessions, session, opponentUid) is { } player)
            return LegacyGetDistance(session.Character.X, session.Character.Y, player.Session.Character.X, player.Session.Character.Y) <= range;
        if (opponentUid <= int.MaxValue && context.WorldData is { } worldData &&
            worldData.SobNpcsById.TryGetValue((int)opponentUid, out var sobs))
        {
            foreach (var sob in sobs)
            {
                if (sob.MapId == session.Character.MapId &&
                    Math.Abs(sob.X / 18 - session.Character.X / 18) <= 1 &&
                    Math.Abs(sob.Y / 18 - session.Character.Y / 18) <= 1)
                    return LegacyGetDistance(session.Character.X, session.Character.Y, sob.X, sob.Y) <= range;
            }
        }
        return false;
    }
}
