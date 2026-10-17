using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyDragonSwingChanceAttribute = "LegacyDragonSwingChance";

    private const ushort LegacyDragonSwingAttachStatusSpellId = 12_200;
    private const uint LegacyDragonSwingUpdateDamage = 33;

    private static async ValueTask<bool> HandleLegacyPlayerDragonSwingAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyDragonSwingAttachStatusSpellId)
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

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var resourceContext = context with { Session = resourceSession };
        var interaction = new MsgAttackPacket(query with { Uid = casterUid });
        await resourceContext.Connection.SendAsync(interaction.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        if (IsLegacyDragonSwingActiveDragonWarParticipant(resourceContext, resourceSession))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy DragonSwing AttachStatus magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                    "activeDragonWarParticipant=True; source=AttachStatus.cs case 12200 DragonWar guard.")
                .ConfigureAwait(false);
            return true;
        }

        if (HasLegacyStatusFlag(resourceSession.Character, LegacyDragonSwingFlag))
        {
            var toggledSession = resourceSession with
            {
                Character = ClearLegacyDragonSwingStatus(resourceSession.Character),
            };
            context.Sessions?.Set(context.Connection, toggledSession);
            var toggledContext = context with { Session = toggledSession };
            await SendPlayerStatusFlagVectorToVisibleAsync(toggledContext, toggledSession, cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy DragonSwing AttachStatus toggle " +
                    $"{FormatSession(toggledContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                    $"removedStatusFlag={LegacyDragonSwingFlag} source=AttachStatus.cs case 12200.")
                .ConfigureAwait(false);
            return true;
        }

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var targetIsCaster = query.OpponentUid == casterUid;
        var updatedSession = resourceSession;
        var updatedContext = resourceContext;
        var chance = CalculateLegacyDragonSwingChance(spell);
        if (targetIsCaster)
        {
            updatedSession = ApplyLegacyDragonSwingStatus(resourceSession, chance, DateTimeOffset.UtcNow);
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = context with { Session = updatedSession };

            await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyDragonSwingUpdateToVisibleAsync(
                    updatedContext,
                    updatedSession,
                    spell,
                    playerSpell,
                    cancellationToken)
                .ConfigureAwait(false);

            animationTargets.Add(new LegacySpellAnimationTarget(
                casterUid,
                0,
                Hit: true,
                MsgAttackEffect.None));
        }

        var progressSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                spell.Duration,
                cancellationToken)
            .ConfigureAwait(false);

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, progressSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy DragonSwing AttachStatus magic " +
                $"{FormatSession(updatedContext with { Session = progressSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} targetUid={query.OpponentUid} selfTarget={targetIsCaster} " +
                $"statusFlag={LegacyDragonSwingFlag} toggledOn={targetIsCaster} " +
                $"durationSeconds={LegacyDragonSwingSeconds} updateTime={spell.Duration} " +
                $"updateDamage={LegacyDragonSwingUpdateDamage} chance={chance}; " +
                "source=AttachStatus.cs case 12200.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyDragonSwingStatus(
        WorldSession session,
        byte chance,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithLegacySourceAddSpellFlag(session.Character, LegacyDragonSwingFlag, removeOnDead: false),
                RuntimeLegacyDragonSwingExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(LegacyDragonSwingSeconds)
                    .ToString("O", CultureInfo.InvariantCulture)),
            RuntimeLegacyDragonSwingChanceAttribute,
            chance.ToString(CultureInfo.InvariantCulture));

        return session with { Character = updatedCharacter };
    }

    private static CharacterSnapshot ClearLegacyDragonSwingStatus(CharacterSnapshot character)
    {
        return WithoutCharacterAttribute(
            WithoutCharacterAttribute(
                WithoutLegacyStatusFlag(character, LegacyDragonSwingFlag),
                RuntimeLegacyDragonSwingExpiresAtUtcAttribute),
            RuntimeLegacyDragonSwingChanceAttribute);
    }

    private static byte CalculateLegacyDragonSwingChance(LegacyMagicRuntimeSpell spell)
    {
        var sourceDamage = spell.RawDamage == 0 ? spell.Damage : spell.RawDamage;
        return checked((byte)Math.Min(byte.MaxValue, sourceDamage / 100U));
    }

    private static async ValueTask SendLegacyDragonSwingUpdateToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        PlayerSpellSnapshotEntry playerSpell,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.DragonSwing,
                    Value: 0,
                    Flag: checked((uint)LegacyDragonSwingFlag),
                    Time: spell.Duration,
                    Damage: LegacyDragonSwingUpdateDamage,
                    SpellLevel: playerSpell.Level),
            ]);

        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                context.Connection,
                session,
                packet.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsLegacyDragonSwingActiveDragonWarParticipant(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        if (context.DragonWarStates?.Process == LegacyDragonWarProcess.Alive &&
            session.Character.MapId == LegacyDragonWarRuntimeStateStore.DragonWarMapId &&
            ReadAttributeUInt32(session.Character.Attributes, "DynamicID", "DynamicId") != 0)
        {
            return true;
        }

        if (!ReadAttributeBool(
                session.Character.Attributes,
                "LegacyTournamentInTournament",
                "TournamentInTournament",
                "InTournament"))
        {
            return false;
        }

        var tournamentType = ReadAttributeString(
            session.Character.Attributes,
            "LegacyTournamentType",
            "TournamentType",
            "CurrentTournamentType");
        if (!IsLegacyPlayerReceiveTournamentType(tournamentType, LegacyDragonWarTypeName))
        {
            return false;
        }

        var process = ReadAttributeString(
            session.Character.Attributes,
            "LegacyTournamentProcess",
            "TournamentProcess",
            "CurrentTournamentProcess");
        return IsLegacyPlayerReceiveTournamentProcessAlive(process);
    }
}
