using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyInvisibilityAttachStatusSpellId = 1_075;
    private const ushort LegacyStarOfAccuracyAttachStatusSpellId = 1_085;
    private const ushort LegacyMagicShieldAttachStatusSpellId = 1_090;
    private const ushort LegacyStigmaAttachStatusSpellId = 1_095;

    internal const string RuntimeLegacyInvisibilityExpiresAtUtcAttribute =
        "LegacyInvisibilityExpiresAtUtc";
    internal const string RuntimeLegacyInvisibilityRemoveOnDeadAttribute =
        "LegacyInvisibilityRemoveOnDead";

    private static async ValueTask<bool> HandleLegacyPlayerClassicBuffAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyClassicBuffDefinition(
                spell.Id,
                out var statusFlag,
                out var expiresAtAttribute,
                out var removeOnDeadAttribute,
                out var suppressWhenActive))
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

        var resourceContext = context with { Session = resourceSession };
        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var animationTargets = new List<LegacySpellAnimationTarget>(1);
        var targetMatched = false;
        var statusApplied = false;
        var alreadyActive = false;
        var targetKind = "none";
        var progressSession = resourceSession;
        var now = DateTimeOffset.UtcNow;

        if (query.OpponentUid == casterUid)
        {
            targetMatched = true;
            targetKind = "self";
            alreadyActive = suppressWhenActive &&
                HasLegacyStatusFlag(resourceSession.Character, statusFlag);
            if (!alreadyActive)
            {
                progressSession = ApplyLegacyClassicBuffToPlayer(
                    resourceSession,
                    statusFlag,
                    expiresAtAttribute,
                    removeOnDeadAttribute,
                    spell.Duration,
                    now);
                context.Sessions?.Set(context.Connection, progressSession);
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        resourceContext with { Session = progressSession },
                        progressSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                animationTargets.Add(new LegacySpellAnimationTarget(
                    casterUid,
                    Damage: 0,
                    Hit: true,
                    MsgAttackEffect.None));
                statusApplied = true;
            }
        }
        else if (TryFindVisibleMonsterTargetByUid(
                     resourceContext,
                     resourceSession.Character,
                     query.OpponentUid,
                     out var monsterTarget))
        {
            targetMatched = true;
            targetKind = "monster";
            if (resourceContext.MonsterStates is { } monsterStates)
            {
                var applyResult = monsterStates.ApplyTimedStatusFlag(
                    monsterTarget.Monster,
                    statusFlag,
                    TimeSpan.FromSeconds(spell.Duration),
                    now,
                    refreshIfActive: !suppressWhenActive);
                alreadyActive = applyResult.AlreadyActive;
                if (applyResult.Applied)
                {
                    var statusFlags = monsterStates.CreateStatusFlagVector(
                        monsterTarget.Monster,
                        now,
                        LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
                    await SendMonsterStatusFlagVectorToVisibleAsync(
                            resourceContext,
                            resourceSession,
                            monsterTarget.Monster,
                            statusFlags,
                            cancellationToken)
                        .ConfigureAwait(false);
                    animationTargets.Add(new LegacySpellAnimationTarget(
                        monsterTarget.Monster.Uid,
                        Damage: 0,
                        Hit: true,
                        MsgAttackEffect.None));
                    statusApplied = true;
                }
            }
        }
        else if (TryGetVisibleSessionByUid(
                     resourceContext,
                     resourceSession,
                     query.OpponentUid,
                     out var activeTarget))
        {
            targetMatched = true;
            targetKind = "player";
            alreadyActive = suppressWhenActive &&
                HasLegacyStatusFlag(activeTarget.Session.Character, statusFlag);
            if (!alreadyActive)
            {
                var updatedTargetSession = ApplyLegacyClassicBuffToPlayer(
                    activeTarget.Session,
                    statusFlag,
                    expiresAtAttribute,
                    removeOnDeadAttribute,
                    spell.Duration,
                    now);
                context.Sessions?.Set(activeTarget.Connection, updatedTargetSession);
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        resourceContext,
                        activeTarget.Connection,
                        updatedTargetSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(
                        updatedTargetSession.Character.Uid,
                        nameof(updatedTargetSession.Character.Uid)),
                    Damage: 0,
                    Hit: true,
                    MsgAttackEffect.None));
                statusApplied = true;
            }
        }

        var progressContext = resourceContext with { Session = progressSession };
        progressSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                progressContext,
                progressSession,
                playerSpell,
                spell,
                spell.Duration,
                cancellationToken)
            .ConfigureAwait(false);
        progressContext = progressContext with { Session = progressSession };

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(
                progressContext,
                progressSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy classic AttachStatus buff " +
                $"{FormatSession(progressContext)} spellId={spell.Id} statusFlag={statusFlag} " +
                $"targetUid={query.OpponentUid} targetKind={targetKind} targetMatched={targetMatched} " +
                $"statusApplied={statusApplied} alreadyActive={alreadyActive} duration={spell.Duration} " +
                $"progressDamage={spell.Duration} animationTargets={animationTargets.Count} " +
                $"statusOpcode={LegacyMsgUpdatePacket.Opcode} statusType={(uint)LegacyMsgUpdateDataType.StatusFlag} " +
                $"animationOpcode={LegacySpellAnimationPacket.Opcode}; source=AttachStatus.cs case {spell.Id}.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyClassicBuffToPlayer(
        WorldSession session,
        int statusFlag,
        string expiresAtAttribute,
        string removeOnDeadAttribute,
        uint durationSeconds,
        DateTimeOffset now)
    {
        var character = WithLegacyStatusFlag(
            LegacyRootItemEffectFlagClock.ForgetProjection(session.Character, statusFlag), statusFlag);
        character = WithCharacterAttribute(
            character,
            expiresAtAttribute,
            now.ToUniversalTime()
                .AddSeconds(durationSeconds)
                .ToString("O", CultureInfo.InvariantCulture));
        character = WithCharacterAttribute(
            character,
            removeOnDeadAttribute,
            bool.TrueString);
        return session with { Character = character };
    }

    private static bool TryGetLegacyClassicBuffDefinition(
        ushort spellId,
        out int statusFlag,
        out string expiresAtAttribute,
        out string removeOnDeadAttribute,
        out bool suppressWhenActive)
    {
        (statusFlag, expiresAtAttribute, removeOnDeadAttribute, suppressWhenActive) = spellId switch
        {
            LegacyInvisibilityAttachStatusSpellId =>
                (LegacyInvisibilityFlag,
                    RuntimeLegacyInvisibilityExpiresAtUtcAttribute,
                    RuntimeLegacyInvisibilityRemoveOnDeadAttribute,
                    false),
            LegacyStarOfAccuracyAttachStatusSpellId =>
                (LegacyStarOfAccuracyFlag,
                    RuntimeLegacyAccuracyExpiresAtUtcAttribute,
                    RuntimeLegacyAccuracyRemoveOnDeadAttribute,
                    false),
            LegacyMagicShieldAttachStatusSpellId =>
                (LegacyMagicShieldFlag,
                    RuntimeLegacyMagicShieldExpiresAtUtcAttribute,
                    RuntimeLegacyMagicShieldRemoveOnDeadAttribute,
                    true),
            LegacyStigmaAttachStatusSpellId =>
                (LegacyStigmaFlag,
                    RuntimeLegacyStigmaExpiresAtUtcAttribute,
                    RuntimeLegacyStigmaRemoveOnDeadAttribute,
                    false),
            _ => (-1, string.Empty, string.Empty, false),
        };

        return statusFlag >= 0;
    }
}
