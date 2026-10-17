using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyArcherPreProcCrescentShadowSpellId = 18_750;
    private const ushort LegacyArcherPreProcNebulousHuntSpellId = 10_990;
    private const string RuntimeLegacyArcherPreProcRateRollAttribute = "LegacyArcherPreProcRateRoll";

    private static async ValueTask<bool> TryHandleLegacyArcherPreKineticMonsterProcAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        LegacyMonsterInstance monster,
        CancellationToken cancellationToken)
    {
        if (packet.Query.AttackType != MsgAttackType.Archer ||
            context.WorldData is not { } worldData)
        {
            return false;
        }

        var distance = LegacyGetDistance(
            session.Character.X,
            session.Character.Y,
            monster.X,
            monster.Y);

        if (!TrySelectLegacyArcherPreKineticMonsterProcSpell(
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
        var procQuery = packet.Query with
        {
            AttackType = MsgAttackType.Magic,
            OpponentUid = monster.Uid,
            X = monster.X,
            Y = monster.Y,
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
        ClearLegacyWeaponProcRandomSpellIfPending(context, spell.Id);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Archer pre-Kinetic monster proc " +
                $"{FormatSession(procContext)} spellId={spell.Id} monsterUid={monster.Uid} " +
                $"distance={distance}; source=MsgAttackPacket.cs AttackID.Archer monster branch lines 443-467.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyArcherPreKineticSobNpcProcAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (packet.Query.AttackType != MsgAttackType.Archer ||
            context.WorldData is not { } worldData)
        {
            return false;
        }

        if (!TryFindVisibleSobNpcByUid(
                context,
                session.Character,
                packet.Query.OpponentUid,
                out var sobNpc))
        {
            return false;
        }

        if (!CanLegacySobNpcMagicDamageTarget(context, session.Character, sobNpc, out var failureDiagnostic))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer rejected legacy Archer pre-Kinetic SOB NPC target " +
                    $"{FormatSession(context)} targetUid={sobNpc.Id}; " +
                    $"source=MsgAttackPacket.cs AttackID.Archer SOB branch CanAttackNpc.Verified; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var distance = LegacyGetDistance(
            session.Character.X,
            session.Character.Y,
            sobNpc.X,
            sobNpc.Y);
        if (!TrySelectLegacyArcherPreKineticSobNpcProcSpell(
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
        var procQuery = packet.Query with
        {
            AttackType = MsgAttackType.Magic,
            OpponentUid = checked((uint)sobNpc.Id),
            X = CheckedUInt16(sobNpc.X, nameof(sobNpc.X)),
            Y = CheckedUInt16(sobNpc.Y, nameof(sobNpc.Y)),
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
        ClearLegacyWeaponProcRandomSpellIfPending(context, spell.Id);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Archer pre-Kinetic SOB NPC proc " +
                $"{FormatSession(procContext)} spellId={spell.Id} sobNpcUid={sobNpc.Id} " +
                $"distance={distance}; source=MsgAttackPacket.cs AttackID.Archer SOB branch lines 525-562.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TrySelectLegacyArcherPreKineticMonsterProcSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        long distance,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        if (distance <= 2 &&
            TrySelectLegacyArcherPreKineticMonsterProcSpell(
                session,
                worldData,
                LegacyArcherPreProcCrescentShadowSpellId,
                out playerSpell,
                out spell))
        {
            return true;
        }

        return TrySelectLegacyArcherPreKineticMonsterProcSpell(
            session,
            worldData,
            LegacyArcherPreProcNebulousHuntSpellId,
            out playerSpell,
            out spell);
    }

    private static bool TrySelectLegacyArcherPreKineticMonsterProcSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        ushort spellId,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        playerSpell = default!;
        spell = default!;
        return LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                spellId,
                out playerSpell,
                out spell) &&
            CheckLegacyArcherPreProcRateRoll(session.Character, spellId);
    }

    private static bool TrySelectLegacyArcherPreKineticSobNpcProcSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        long distance,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        if (distance <= 2 &&
            TrySelectLegacyArcherPreKineticMonsterProcSpell(
                session,
                worldData,
                LegacyArcherPreProcCrescentShadowSpellId,
                out playerSpell,
                out spell))
        {
            return true;
        }

        return TrySelectLegacyArcherPreKineticMonsterProcSpell(
            session,
            worldData,
            LegacyArcherPreProcNebulousHuntSpellId,
            out playerSpell,
            out spell);
    }

    private static bool CheckLegacyArcherPreProcRateRoll(
        CharacterSnapshot character,
        ushort spellId)
    {
        var spellIdText = spellId.ToString(CultureInfo.InvariantCulture);
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyArcherPreProcRateRollAttribute + "." + spellIdText,
            RuntimeLegacyArcherPreProcRateRollAttribute + spellIdText,
            RuntimeLegacyArcherPreProcRateRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return 30 > roll;
    }
}
