using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryResolveLegacyPlayerMagicAttackQuery(
        WorldSession session,
        WorldDataCatalog worldData,
        MsgAttackQuery query,
        out MsgAttackQuery resolvedQuery,
        out WorldSession resolvedSession,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell,
        out string failureDiagnostic,
        bool deferFirstFamilyToken = false)
    {
        failureDiagnostic = "learned spell was not available";
        if (TryResolveLegacyPlayerMagicAttackCandidate(
                session,
                worldData,
                query,
                out resolvedSession,
                out playerSpell,
                out spell,
                out var candidateFailure,
                deferFirstFamilyToken))
        {
            resolvedQuery = query;
            return true;
        }
        failureDiagnostic = candidateFailure;

        if (query.SpellId != 0)
        {
            var decoded = MsgAttackPacket.DecodeMagicAttack(query);
            if (TryResolveLegacyPlayerMagicAttackCandidate(
                    session,
                    worldData,
                    decoded,
                    out resolvedSession,
                    out playerSpell,
                    out spell,
                    out candidateFailure,
                    deferFirstFamilyToken))
            {
                resolvedQuery = decoded;
                return true;
            }
            failureDiagnostic = candidateFailure;
        }

        if (query.Damage is > 0 and <= ushort.MaxValue)
        {
            var damageSpell = query with { SpellId = (ushort)query.Damage };
            if (TryResolveLegacyPlayerMagicAttackCandidate(
                    session,
                    worldData,
                    damageSpell,
                    out resolvedSession,
                    out playerSpell,
                    out spell,
                    out candidateFailure,
                    deferFirstFamilyToken))
            {
                resolvedQuery = damageSpell;
                return true;
            }
            failureDiagnostic = candidateFailure;
        }

        resolvedQuery = default;
        resolvedSession = session;
        playerSpell = default!;
        spell = default!;
        return false;
    }

    private static bool TryResolveLegacyPlayerMagicAttackCandidate(
        WorldSession session,
        WorldDataCatalog worldData,
        MsgAttackQuery query,
        out WorldSession resolvedSession,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell,
        out string failureDiagnostic,
        bool deferFirstFamilyToken)
    {
        resolvedSession = session;
        playerSpell = default!;
        spell = default!;
        failureDiagnostic = "learned spell was not available";

        if (query.SpellId == 0)
        {
            failureDiagnostic = "spell id was not available";
            return false;
        }

        if (!(deferFirstFamilyToken && TryResolveLegacyNativeRootFirstFamilyCandidate(
                session, worldData, query.SpellId, out playerSpell, out spell)) &&
            !TryResolveLegacyPlayerMagicRuntimeSpell(
                session,
                worldData,
                query.SpellId,
                out playerSpell,
                out spell))
        {
            return false;
        }

        // Native first-family CanUse consumes its token AFTER ProcescMagic's root gates.
        if ((deferFirstFamilyToken && IsLegacyRootFirstFamilyInvocation(session, spell)) ||
            TryConsumeLegacyWeaponProcSpellGate(session, spell, out resolvedSession))
        {
            return true;
        }

        failureDiagnostic = $"weapon-proc spell was not pending randomSpell={session.LegacyRandomSpell}";
        resolvedSession = session;
        playerSpell = default!;
        spell = default!;
        return false;
    }
}
