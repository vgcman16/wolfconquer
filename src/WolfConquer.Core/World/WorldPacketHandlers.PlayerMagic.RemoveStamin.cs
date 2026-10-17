using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const int LegacyFrostArrowsStatusFlag = 0xDB;
    internal const string RuntimeLegacyFrostArrowsExpiresAtUtcAttribute =
        "LegacyFrostArrowsExpiresAtUtc";
    internal const string RuntimeLegacyFrostArrowsRemoveOnDeadAttribute =
        "LegacyFrostArrowsRemoveOnDead";
    private const int LegacyRemoveStaminSort = 88;
    private const ushort LegacyFrostGazeISpellId = 12_830;
    private const ushort LegacyFrostGazeIISpellId = 13_070;
    private const ushort LegacyFrostGazeIIISpellId = 13_080;
    private const ushort LegacyFrostArrowsSpellId = 14_280;
    private const int LegacyFrostArrowsCastPointRange = 8;

    private static async Task<bool> HandleLegacyPlayerRemoveStaminMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if ((int)spell.Sort != LegacyRemoveStaminSort)
        {
            return false;
        }

        // RemoveStamin.Execute calls CanUseSpell.Verified before its spell-ID switch.
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

        context = context with { Session = resourceSession };
        if (spell.Id is LegacyFrostGazeISpellId or LegacyFrostGazeIISpellId or LegacyFrostGazeIIISpellId)
        {
            await HandleLegacyFrostGazeRemoveStaminAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (spell.Id == LegacyFrostArrowsSpellId)
        {
            await HandleLegacyFrostArrowsRemoveStaminAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await context.Log.WriteLineAsync(
                "WorldServer source-backed returned legacy RemoveStamin magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; " +
                "source RemoveStamin.cs returns after CanUseSpell.Verified for unsupported IDs.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask HandleLegacyFrostGazeRemoveStaminAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var updatedSession = resourceSession;
        var casterBattlePower = CalculateLegacyPitchingBattlePower(updatedSession);
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var hitCount = 0;
        var missCount = 0;
        var consolidationBreak = false;

        foreach (var activeTarget in SnapshotLegacyRemoveStaminVisiblePlayers(context, updatedSession))
        {
            var targetSession = activeTarget.Session;
            var targetCharacter = targetSession.Character;
            if (LegacyGetDistance(
                    updatedSession.Character.X,
                    updatedSession.Character.Y,
                    targetCharacter.X,
                    targetCharacter.Y) > spell.Range ||
                !CanLegacyPlayerMagicDamageTarget(
                    context,
                    updatedSession,
                    targetSession,
                    spell,
                    out _))
            {
                continue;
            }

            updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                    context,
                    updatedSession,
                    [new LegacyPlayerMagicTarget(activeTarget)],
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };

            var targetBattlePower = CalculateLegacyPitchingBattlePower(targetSession);
            var successRate = unchecked(100 - (5 * (targetBattlePower - casterBattlePower)));
            var succeeded = CheckLegacyPitchingRateRoll(
                updatedSession.Character,
                successRate);
            var targetUid = CheckedUInt32(targetCharacter.Uid, nameof(targetCharacter.Uid));
            if (!succeeded)
            {
                animationTargets.Add(new LegacySpellAnimationTarget(
                    targetUid,
                    Damage: 0,
                    Hit: false,
                    MsgAttackEffect.None));
                missCount++;
                continue;
            }

            if (CheckLegacyConsolidationRateRoll(targetCharacter, targetSession.PlayerItems))
            {
                consolidationBreak = true;
                break;
            }

            var stamina = ReadLegacyRuntimeStamina(targetCharacter);
            var remainingStamina = stamina >= spell.Damage
                ? stamina - spell.Damage
                : 0;
            var updatedTargetSession = targetSession with
            {
                Character = WithCharacterAttribute(targetCharacter, "Stamina", remainingStamina),
            };
            context.Sessions?.Set(activeTarget.Connection, updatedTargetSession);
            await activeTarget.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeStaminaTickUpdate(targetUid, remainingStamina)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            animationTargets.Add(new LegacySpellAnimationTarget(
                targetUid,
                spell.Damage,
                Hit: true,
                MsgAttackEffect.None));
            hitCount++;
        }

        // IncreaseExperience.Up and UpdateSpell.CheckUpdate both receive source damage 0.
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalDamage: 0,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy RemoveStamin FrostGaze magic " +
                $"{FormatSession(context)} spellId={spell.Id} range={spell.Range} damage={spell.Damage} " +
                $"hitCount={hitCount} missCount={missCount} consolidationBreak={consolidationBreak} " +
                $"progressDamage=0 animationX={query.X} animationY={query.Y}; " +
                "source=RemoveStamin.cs cases 12830/13070/13080.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyFrostArrowsRemoveStaminAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var updatedSession = resourceSession;
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var statusAppliedCount = 0;
        var statusNow = DateTimeOffset.UtcNow;
        var expiresAtUtc = statusNow.AddSeconds(spell.Duration);
        var castPointDistance = LegacyGetDistance(
            updatedSession.Character.X,
            updatedSession.Character.Y,
            query.X,
            query.Y);

        foreach (var activeTarget in SnapshotLegacyRemoveStaminVisiblePlayers(context, updatedSession))
        {
            var targetSession = activeTarget.Session;
            var targetCharacter = targetSession.Character;
            if (LegacyGetDistance(
                    query.X,
                    query.Y,
                    targetCharacter.X,
                    targetCharacter.Y) > spell.Range ||
                castPointDistance > LegacyFrostArrowsCastPointRange ||
                !CanLegacyPlayerMagicDamageTarget(
                    context,
                    updatedSession,
                    targetSession,
                    spell,
                    out _))
            {
                continue;
            }

            updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                    context,
                    updatedSession,
                    [new LegacyPlayerMagicTarget(activeTarget)],
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };

            if (!HasLegacyStatusFlag(targetCharacter, LegacyFrostArrowsStatusFlag))
            {
                var updatedTargetCharacter = WithLegacyStatusFlag(
                    targetCharacter,
                    LegacyFrostArrowsStatusFlag);
                updatedTargetCharacter = WithCharacterAttribute(
                    updatedTargetCharacter,
                    RuntimeLegacyFrostArrowsExpiresAtUtcAttribute,
                    expiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
                updatedTargetCharacter = WithCharacterAttribute(
                    updatedTargetCharacter,
                    RuntimeLegacyFrostArrowsRemoveOnDeadAttribute,
                    "true");
                var updatedTargetSession = targetSession with
                {
                    Character = updatedTargetCharacter,
                };
                context.Sessions?.Set(activeTarget.Connection, updatedTargetSession);
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        context,
                        activeTarget.Connection,
                        updatedTargetSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                statusAppliedCount++;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(targetCharacter.Uid, nameof(targetCharacter.Uid)),
                Damage: 0,
                Hit: true,
                MsgAttackEffect.None));
        }

        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalDamage: 0,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy RemoveStamin FrostArrows magic " +
                $"{FormatSession(context)} spellId={spell.Id} targetX={query.X} targetY={query.Y} " +
                $"range={spell.Range} castPointDistance={castPointDistance} " +
                $"castPointGate={LegacyFrostArrowsCastPointRange} targetCount={animationTargets.Count} " +
                $"statusAppliedCount={statusAppliedCount} duration={spell.Duration} " +
                $"removeOnDead=True progressDamage=0; source=RemoveStamin.cs case 14280.")
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<WorldActiveSession> SnapshotLegacyRemoveStaminVisiblePlayers(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        if (context.Sessions is not { } sessions)
        {
            return [];
        }

        return sessions
            .Snapshot()
            .Where(active =>
                !ReferenceEquals(active.Connection, context.Connection) &&
                active.Session.Character.Uid != session.Character.Uid &&
                CanSeePlayer(session.Character, active.Session.Character))
            .ToArray();
    }
}
