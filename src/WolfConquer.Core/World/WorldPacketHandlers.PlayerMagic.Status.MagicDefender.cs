using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyMagicDefenderExpiresAtUtcAttribute = "LegacyMagicDefenderExpiresAtUtc";

    private const ushort LegacyMagicDefenderAttachStatusSpellId = 30_000;
    private const uint LegacyMagicDefenderAzureShieldDefence = 50_000;
    private const string RuntimeLegacyMagicDefenderRemoveOnDeadAttribute = "LegacyMagicDefenderRemoveOnDead";
    private const string RuntimeLegacyMagicDefenderAzureShieldLevelAttribute = "AzureShieldLevel";
    private const string RuntimeLegacyMagicDefenderAzureShieldDefenceAttribute = "AzureShieldDefence";

    private static async ValueTask<bool> HandleLegacyPlayerMagicDefenderAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyMagicDefenderAttachStatusSpellId)
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
        var targetIsCaster = query.OpponentUid == casterUid;
        var targetApplied = false;
        var targetConnection = context.Connection;
        var targetSession = resourceSession;
        var sendContext = resourceContext;
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var sourceDurationSeconds = checked((uint)(15 * playerSpell.Level));

        if (!targetIsCaster &&
            TryGetVisibleSessionByUid(resourceContext, resourceSession, query.OpponentUid, out var activeTarget))
        {
            targetConnection = activeTarget.Connection;
            targetSession = activeTarget.Session;
        }

        if (targetIsCaster || targetSession.Character.Uid == query.OpponentUid)
        {
            var updatedTargetSession = ApplyLegacyMagicDefenderStatus(
                targetSession,
                playerSpell,
                sourceDurationSeconds,
                DateTimeOffset.UtcNow);
            context.Sessions?.Set(targetConnection, updatedTargetSession);

            sendContext = context with
            {
                Connection = targetConnection,
                Session = updatedTargetSession,
            };
            await SendPlayerStatusFlagVectorToVisibleAsync(sendContext, updatedTargetSession, cancellationToken)
                .ConfigureAwait(false);

            var targetUid = CheckedUInt32(
                updatedTargetSession.Character.Uid,
                nameof(updatedTargetSession.Character.Uid));
            animationTargets.Add(new LegacySpellAnimationTarget(
                targetUid,
                0,
                Hit: true,
                MsgAttackEffect.None));
            targetApplied = true;
            if (targetIsCaster)
            {
                resourceSession = updatedTargetSession;
                resourceContext = sendContext;
            }
        }

        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: 0,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(sendContext, sendContext.Session, animation, cancellationToken)
            .ConfigureAwait(false);

        var experienceContext = context with { Session = resourceSession };
        var updatedExperienceSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                experienceContext,
                resourceSession,
                playerSpell,
                spell,
                spell.Duration,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy MagicDefender AttachStatus magic " +
                $"{FormatSession(experienceContext with { Session = updatedExperienceSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} statusFlag={LegacyAzureShieldFlag} targetUid={query.OpponentUid} " +
                $"targetApplied={targetApplied} duration={sourceDurationSeconds} spellDuration={spell.Duration} " +
                $"azureShieldLevel={playerSpell.Level} azureShieldDefence={LegacyMagicDefenderAzureShieldDefence}; " +
                "source=AttachStatus.cs case 30000.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyMagicDefenderStatus(
        WorldSession session,
        PlayerSpellSnapshotEntry playerSpell,
        uint sourceDurationSeconds,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithLegacyStatusFlag(session.Character, LegacyAzureShieldFlag);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyMagicDefenderAzureShieldLevelAttribute,
            playerSpell.Level);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyMagicDefenderAzureShieldDefenceAttribute,
            LegacyMagicDefenderAzureShieldDefence);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyMagicDefenderRemoveOnDeadAttribute,
            "true");

        if (sourceDurationSeconds > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyMagicDefenderExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(sourceDurationSeconds)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        return session with { Character = updatedCharacter };
    }
}
