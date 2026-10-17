using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

[Flags]
internal enum LegacyRemoveBuffersStatusSet
{
    None = 0,
    SoulShackle = 1 << 0,
    Poisoned = 1 << 1,
    PoisonStar = 1 << 2,
    ChillingSnow = 1 << 3,
}

public static partial class WorldPacketHandlers
{
    private const int LegacyRemoveBuffersCompareAndSetAttempts = 4;
    private const string RuntimeLegacyRemoveBuffersPoisonLevelAttribute = "PoisonLevel";
    private const string RuntimeLegacyRemoveBuffersPoisonedExpiresAtUtcAttribute =
        "LegacyPoisonedExpiresAtUtc";
    private const string RuntimeLegacyRemoveBuffersPoisonExpiresAtUtcAttribute =
        "LegacyPoisonExpiresAtUtc";
    private const string RuntimeLegacyRemoveBuffersToxicPoisonExpiresAtUtcAttribute =
        "LegacyToxicPoisonExpiresAtUtc";
    private const string RuntimeLegacyRemoveBuffersToxicFogPoisonExpiresAtUtcAttribute =
        "LegacyToxicFogPoisonExpiresAtUtc";

    private const LegacyRemoveBuffersStatusSet LegacyRemoveBuffersCoreStatusSet =
        LegacyRemoveBuffersStatusSet.SoulShackle |
        LegacyRemoveBuffersStatusSet.Poisoned |
        LegacyRemoveBuffersStatusSet.PoisonStar;

    private const LegacyRemoveBuffersStatusSet LegacyTranquilityExternalStatusSet =
        LegacyRemoveBuffersCoreStatusSet |
        LegacyRemoveBuffersStatusSet.ChillingSnow;

    private const LegacyRemoveBuffersStatusSet LegacyCompassionStatusSet =
        LegacyRemoveBuffersStatusSet.Poisoned |
        LegacyRemoveBuffersStatusSet.PoisonStar;

    internal static CharacterSnapshot RemoveLegacyRemoveBuffersStatusState(
        CharacterSnapshot character,
        LegacyRemoveBuffersStatusSet statuses)
    {
        ArgumentNullException.ThrowIfNull(character);

        var updatedCharacter = character;
        if ((statuses & LegacyRemoveBuffersStatusSet.SoulShackle) != 0)
        {
            updatedCharacter = WithoutLegacyStatusFlag(updatedCharacter, LegacySoulShackleStatusFlag);
            updatedCharacter = WithoutCharacterAttribute(
                updatedCharacter,
                RuntimeLegacySoulShackleExpiresAtUtcAttribute);
            updatedCharacter = WithoutCharacterAttribute(
                updatedCharacter,
                RuntimeLegacySoulShackleRemoveOnDeadAttribute);
        }

        if ((statuses & LegacyRemoveBuffersStatusSet.Poisoned) != 0)
        {
            updatedCharacter = WithoutLegacyStatusFlag(updatedCharacter, LegacyToxicFogPoisonedFlag);
            updatedCharacter = WithoutCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyRemoveBuffersPoisonLevelAttribute);
            updatedCharacter = WithoutCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyRemoveBuffersPoisonedExpiresAtUtcAttribute);
            updatedCharacter = WithoutCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyRemoveBuffersPoisonExpiresAtUtcAttribute);
            updatedCharacter = WithoutCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyRemoveBuffersToxicPoisonExpiresAtUtcAttribute);
            updatedCharacter = WithoutCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyRemoveBuffersToxicFogPoisonExpiresAtUtcAttribute);
        }

        if ((statuses & LegacyRemoveBuffersStatusSet.PoisonStar) != 0)
        {
            updatedCharacter = WithoutLegacyStatusFlag(updatedCharacter, LegacyPoisonStarFlag);
            updatedCharacter = WithoutCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyPoisonStarExpiresAtUtcAttribute);
            updatedCharacter = WithoutCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyPoisonStarRemoveOnDeadAttribute);
        }

        if ((statuses & LegacyRemoveBuffersStatusSet.ChillingSnow) != 0)
        {
            updatedCharacter = WithoutLegacyStatusFlag(updatedCharacter, LegacyChillingSnowFlag);
        }

        return updatedCharacter;
    }

    private static async ValueTask<LegacyRemoveBuffersStatusPublication> PublishLegacyRemoveBuffersStatusCleanupAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection subjectConnection,
        WorldSession expectedSession,
        LegacyRemoveBuffersStatusSet statuses,
        CancellationToken cancellationToken)
    {
        var sessions = context.Sessions;
        var currentSession = expectedSession;
        if (sessions?.TryGet(subjectConnection, out var storedSession) == true && storedSession is not null)
        {
            currentSession = storedSession;
        }

        for (var attempt = 0; attempt < LegacyRemoveBuffersCompareAndSetAttempts; attempt++)
        {
            var updatedCharacter = RemoveLegacyRemoveBuffersStatusState(
                currentSession.Character,
                statuses);
            if (ReferenceEquals(updatedCharacter, currentSession.Character))
            {
                return new LegacyRemoveBuffersStatusPublication(
                    currentSession,
                    StateChanged: false,
                    Published: false,
                    CommitFailed: false);
            }

            var updatedSession = currentSession with { Character = updatedCharacter };
            if (sessions is null ||
                sessions.TryCompareAndSet(subjectConnection, currentSession, updatedSession))
            {
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        context,
                        subjectConnection,
                        updatedSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                return new LegacyRemoveBuffersStatusPublication(
                    updatedSession,
                    StateChanged: true,
                    Published: true,
                    CommitFailed: false);
            }

            if (!sessions.TryGet(subjectConnection, out var latestSession) || latestSession is null)
            {
                break;
            }

            currentSession = latestSession;
        }

        await context.Log.WriteLineAsync(
                "WorldServer skipped legacy RemoveBuffers status publication " +
                $"targetUid={currentSession.Character.Uid}; " +
                "WorldSessionStore.TryCompareAndSet repeatedly rejected the cleanup transition.")
            .ConfigureAwait(false);
        return new LegacyRemoveBuffersStatusPublication(
            currentSession,
            StateChanged: false,
            Published: false,
            CommitFailed: true);
    }

    private static async ValueTask<LegacyRemoveBuffersCleanupResult> CleanLegacyRemoveBuffersSingleTargetAsync(
        WorldPacketHandlerContext context,
        WorldSession casterSession,
        uint targetUid,
        LegacyRemoveBuffersStatusSet selfStatuses,
        LegacyRemoveBuffersStatusSet externalStatuses,
        CancellationToken cancellationToken)
    {
        if (!TryResolveLegacyRemoveBuffersSingleTarget(
                context,
                casterSession,
                targetUid,
                out var target))
        {
            return LegacyRemoveBuffersCleanupResult.Empty(casterSession);
        }

        var targetIsCaster = ReferenceEquals(target.Connection, context.Connection);
        var publication = await PublishLegacyRemoveBuffersStatusCleanupAsync(
                context,
                target.Connection,
                target.Session,
                targetIsCaster ? selfStatuses : externalStatuses,
                cancellationToken)
            .ConfigureAwait(false);
        if (targetIsCaster)
        {
            casterSession = publication.Session;
        }

        return new LegacyRemoveBuffersCleanupResult(
            casterSession,
            [CreateLegacyRemoveBuffersAnimationTarget(publication.Session)],
            ResolvedTargets: 1,
            StateUpdates: publication.Published ? 1 : 0,
            CommitFailures: publication.CommitFailed ? 1 : 0);
    }

    private static async ValueTask<LegacyRemoveBuffersCleanupResult> CleanLegacyRemoveBuffersTeamAsync(
        WorldPacketHandlerContext context,
        WorldSession casterSession,
        LegacyRemoveBuffersStatusSet statuses,
        CancellationToken cancellationToken)
    {
        var targets = new List<LegacySpellAnimationTarget>();
        var resolvedTargets = 0;
        var stateUpdates = 0;
        var commitFailures = 0;
        foreach (var target in SnapshotLegacyRemoveBuffersTeamMembers(context, casterSession))
        {
            var publication = await PublishLegacyRemoveBuffersStatusCleanupAsync(
                    context,
                    target.Connection,
                    target.Session,
                    statuses,
                    cancellationToken)
                .ConfigureAwait(false);
            if (ReferenceEquals(target.Connection, context.Connection))
            {
                casterSession = publication.Session;
                context = context with { Session = casterSession };
            }

            targets.Add(CreateLegacyRemoveBuffersAnimationTarget(publication.Session));
            resolvedTargets++;
            stateUpdates += publication.Published ? 1 : 0;
            commitFailures += publication.CommitFailed ? 1 : 0;
        }

        return new LegacyRemoveBuffersCleanupResult(
            casterSession,
            targets,
            resolvedTargets,
            stateUpdates,
            commitFailures);
    }

    private static LegacySpellAnimationTarget CreateLegacyRemoveBuffersAnimationTarget(WorldSession session)
    {
        return new LegacySpellAnimationTarget(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            Damage: 0,
            Hit: true,
            MsgAttackEffect.None);
    }

    private readonly record struct LegacyRemoveBuffersStatusPublication(
        WorldSession Session,
        bool StateChanged,
        bool Published,
        bool CommitFailed);

    private readonly record struct LegacyRemoveBuffersCleanupResult(
        WorldSession CasterSession,
        IReadOnlyList<LegacySpellAnimationTarget> AnimationTargets,
        int ResolvedTargets,
        int StateUpdates,
        int CommitFailures)
    {
        public static LegacyRemoveBuffersCleanupResult Empty(WorldSession casterSession)
        {
            return new LegacyRemoveBuffersCleanupResult(casterSession, [], 0, 0, 0);
        }
    }
}
