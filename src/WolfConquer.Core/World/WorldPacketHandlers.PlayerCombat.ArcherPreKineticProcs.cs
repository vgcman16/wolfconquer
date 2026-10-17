using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyArcherPreKineticCloseProcSpellId = 18_750;
    private const ushort LegacyArcherPreKineticFallbackProcSpellId = 10_990;
    private const string RuntimeLegacyArcherPreKineticProcRateRollAttribute = "LegacyArcherPreProcRateRoll";

    private static async ValueTask<bool> TryHandleLegacyArcherPreKineticPlayerProcAttackAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (packet.Query.AttackType != MsgAttackType.Archer ||
            context.WorldData is not { } worldData ||
            !TryGetVisibleSessionByUid(context, session, packet.Query.OpponentUid, out var primary))
        {
            return false;
        }

        var distance = LegacyGetDistance(
            session.Character.X,
            session.Character.Y,
            primary.Session.Character.X,
            primary.Session.Character.Y);
        if (!TryResolveLegacyArcherPreKineticProcCandidate(
                session,
                worldData,
                distance,
                out var gateSpell))
        {
            return false;
        }

        if (!await TryPassLegacyArcherPlayerCallerCanAttackAsync(
                context,
                session,
                primary,
                gateSpell,
                "Archer pre-Kinetic player proc",
                cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (!TrySelectLegacyArcherPreKineticProcSpell(
                session,
                worldData,
                distance,
                out var playerSpell,
                out var spell))
        {
            return false;
        }

        var procSession = session with { LegacyRandomSpell = spell.Id };
        context.Sessions?.Set(context.Connection, procSession);
        var procContext = context with { Session = procSession };
        var targetCharacter = primary.Session.Character;
        var procQuery = packet.Query with
        {
            AttackType = MsgAttackType.Magic,
            OpponentUid = CheckedUInt32(targetCharacter.Uid, nameof(targetCharacter.Uid)),
            X = CheckedUInt16(targetCharacter.X, nameof(targetCharacter.X)),
            Y = CheckedUInt16(targetCharacter.Y, nameof(targetCharacter.Y)),
            Damage = 0,
            KilledMonster = 0,
            ResponseDamage = 0,
            Effect = MsgAttackEffect.None,
            SpellId = spell.Id,
            SpellLevel = playerSpell.Level,
        };

        await HandleLegacyPlayerMonsterMagicAttackAsync(
                procContext,
                new MsgAttackPacket(procQuery),
                cancellationToken)
            .ConfigureAwait(false);
        ClearLegacyArcherPreKineticProcRandomSpellIfPending(context, spell.Id);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Archer pre-Kinetic player proc " +
                $"{FormatSession(procContext)} spellId={spell.Id} targetUid={targetCharacter.Uid}; " +
                "source=MsgAttackPacket.cs AttackID.Archer player branch before KineticSpark.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryResolveLegacyArcherPreKineticProcCandidate(
        WorldSession session,
        WorldDataCatalog worldData,
        long targetDistance,
        out LegacyMagicRuntimeSpell spell)
    {
        if (targetDistance <= 2 &&
            LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyArcherPreKineticCloseProcSpellId,
                out _,
                out spell))
        {
            return true;
        }

        return LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            LegacyArcherPreKineticFallbackProcSpellId,
            out _,
            out spell);
    }

    private static bool TrySelectLegacyArcherPreKineticProcSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        long targetDistance,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        if (targetDistance <= 2 &&
            LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyArcherPreKineticCloseProcSpellId,
                out playerSpell,
                out spell) &&
            CheckLegacyArcherPreKineticProcRateRoll(session.Character, LegacyArcherPreKineticCloseProcSpellId))
        {
            return true;
        }

        return LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyArcherPreKineticFallbackProcSpellId,
                out playerSpell,
                out spell) &&
            CheckLegacyArcherPreKineticProcRateRoll(session.Character, LegacyArcherPreKineticFallbackProcSpellId);
    }

    private static bool CheckLegacyArcherPreKineticProcRateRoll(
        CharacterSnapshot character,
        ushort spellId)
    {
        var spellIdText = spellId.ToString(CultureInfo.InvariantCulture);
        var roll = TryReadLegacyArcherPreKineticProcRateRoll(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyArcherPreKineticProcRateRollAttribute + "." + spellIdText,
            RuntimeLegacyArcherPreKineticProcRateRollAttribute + spellIdText,
            RuntimeLegacyArcherPreKineticProcRateRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return 30 > roll;
    }

    private static bool TryReadLegacyArcherPreKineticProcRateRoll(
        IReadOnlyDictionary<string, string> attributes,
        out uint value,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var raw) &&
                uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static async ValueTask<bool> TryPassLegacyArcherPlayerCallerCanAttackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession target,
        LegacyMagicRuntimeSpell spell,
        string activity,
        CancellationToken cancellationToken)
    {
        if (CanLegacyPlayerMagicDamageTarget(
                context,
                session,
                target.Session,
                spell,
                out var failureDiagnostic,
                out var failureEffect))
        {
            return true;
        }

        if (failureEffect == LegacyPlayerMagicGateFailureEffect.ForbiddenPirate)
        {
            await SendScalarUpdateAsync(
                    context,
                    session.Character,
                    LegacyMsgUpdateDataType.ForbiddenPirate,
                    1,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
        {
            await TrySendLegacyPlayerHellVortexCounterAsync(
                    context,
                    session,
                    target,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                $"WorldServer rejected legacy {activity} target " +
                $"{FormatSession(context)} spellId={spell.Id} targetUid={target.Session.Character.Uid}; " +
                failureDiagnostic + ".")
            .ConfigureAwait(false);
        return false;
    }

    private static void ClearLegacyArcherPreKineticProcRandomSpellIfPending(
        WorldPacketHandlerContext context,
        ushort spellId)
    {
        if (context.Sessions is not { } sessions ||
            !sessions.TryGet(context.Connection, out var currentSession) ||
            currentSession is null ||
            currentSession.LegacyRandomSpell != spellId)
        {
            return;
        }

        sessions.Set(context.Connection, currentSession with { LegacyRandomSpell = 0 });
    }
}
