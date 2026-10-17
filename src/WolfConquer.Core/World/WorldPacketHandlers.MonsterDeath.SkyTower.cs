using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly HashSet<int> s_legacySkyTowerObjectiveMonsterFamilyIds =
        CreateLegacySkyTowerObjectiveMonsterFamilyIds();

    private static async ValueTask<WorldSession> TryApplyLegacySkyTowerMonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession killerSession,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        var killerDynamicId = ReadCharacterDynamicId(killerSession.Character);
        var activeChallengeId = ReadAttributeUInt32(
            killerSession.Character.Attributes,
            LegacySkyTowerActiveChallengeAttribute);
        var floorAttribute = ReadAttributeUInt32(
            killerSession.Character.Attributes,
            LegacySkyTowerFloorAttribute);
        var activeCompletionId = ReadAttributeUInt32(
            killerSession.Character.Attributes,
            LegacySkyTowerActiveCompletionAttribute);
        var challenge = default(LegacySkyTowerChallenge);
        string? eligibilityReason = null;

        if (context.ProtocolProfile != ConquerProtocolProfile.VisibleDx8D13)
        {
            eligibilityReason = "protocol-profile-mismatch";
        }
        else if (monster.MapId != LegacyInstanceInfoMapId)
        {
            eligibilityReason = "monster-map-mismatch";
        }
        else if (killerDynamicId != monster.DynamicId)
        {
            eligibilityReason = "dynamic-id-mismatch";
        }
        else if (activeChallengeId == 0)
        {
            eligibilityReason = "missing-active-challenge";
        }
        else if (!LegacySkyTowerCatalog.TryResolve(activeChallengeId, out challenge))
        {
            eligibilityReason = "unknown-active-challenge";
        }
        else if (monsterFamily.Id != checked((int)challenge.MonsterFamilyId))
        {
            eligibilityReason = "monster-family-mismatch";
        }
        else if (activeCompletionId == activeChallengeId)
        {
            eligibilityReason = "active-challenge-already-completed";
        }

        if (eligibilityReason is not null)
        {
            if (monster.MapId == LegacyInstanceInfoMapId ||
                s_legacySkyTowerObjectiveMonsterFamilyIds.Contains(monsterFamily.Id))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped likely Sky Tower monster death " +
                        $"{FormatSession(context with { Session = killerSession })} " +
                        $"protocolProfile={context.ProtocolProfile} " +
                        $"killerMap={killerSession.Character.MapId} killerDynamicId={killerDynamicId} " +
                        $"monsterMap={monster.MapId} monsterDynamicId={monster.DynamicId} " +
                        $"monsterFamilyId={monsterFamily.Id} activeChallengeId={activeChallengeId} " +
                        $"floorAttribute={floorAttribute} activeCompletionId={activeCompletionId} " +
                        $"eligibilityReason={eligibilityReason}.")
                    .ConfigureAwait(false);
            }

            return killerSession;
        }

        context.MonsterStates?.SuppressDynamicMonsterRespawns(monster.MapId, monster.DynamicId);
        var remainingTargets = context.MonsterStates?
            .GetVisibleDynamicMonsters(monster.MapId, monster.DynamicId, DateTimeOffset.UtcNow)
            .Count(entry => entry.MonsterFamily.Id == checked((int)challenge.MonsterFamilyId)) ?? 0;
        if (remainingTargets > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer advanced Sky Tower objective " +
                    $"{FormatSession(context)} challengeId={activeChallengeId} floor={challenge.Floor} " +
                    $"monsterId={challenge.MonsterFamilyId} remaining={remainingTargets} required={challenge.MonsterCount}.")
                .ConfigureAwait(false);
            return killerSession;
        }

        var completedChallengeId = activeChallengeId;
        var previousCompletedChallengeId = ReadAttributeUInt32(
            killerSession.Character.Attributes,
            LegacySkyTowerCompletedChallengeAttribute);
        if (LegacySkyTowerCatalog.TryResolve(previousCompletedChallengeId, out var previousChallenge) &&
            previousChallenge.Floor > challenge.Floor)
        {
            completedChallengeId = previousCompletedChallengeId;
        }

        var availableAttempts = ReadLegacySkyTowerAvailableAttempts(
            killerSession.Character.Attributes);
        var remainingAttempts = availableAttempts == 0 ? 0 : availableAttempts - 1;

        var attributes = CopyCharacterAttributes(killerSession.Character.Attributes);
        attributes[LegacySkyTowerActiveCompletionAttribute] =
            activeChallengeId.ToString(CultureInfo.InvariantCulture);
        attributes[LegacySkyTowerCompletedChallengeAttribute] =
            completedChallengeId.ToString(CultureInfo.InvariantCulture);
        attributes[LegacySkyTowerFloorAttribute] =
            challenge.Floor.ToString(CultureInfo.InvariantCulture);
        attributes[LegacySkyTowerAvailableAttemptsAttribute] =
            remainingAttempts.ToString(CultureInfo.InvariantCulture);

        var updatedSession = killerSession with
        {
            Character = killerSession.Character with { Attributes = attributes },
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                VisibleDx8D13InstanceInfoPacket.CreateUserInstanceProgress(
                        completedChallengeId,
                        completed: true)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                VisibleDx8D13InstanceInfoPacket.CreateAvailableAttempts(
                        LegacySkyTowerCatalog.RootChallengeId,
                        remainingAttempts)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer completed Sky Tower challenge " +
                $"{FormatSession(context with { Session = updatedSession })} challengeId={activeChallengeId} " +
                $"floor={challenge.Floor} monsterId={challenge.MonsterFamilyId} " +
                $"completedChallengeId={completedChallengeId} availableAttempts={remainingAttempts} " +
                $"opcode={VisibleDx8D13InstanceInfoPacket.Opcode} type=0+1; " +
                "exact D13 CUserInstanceInfo field1 plus field3 unlocks TowerLevel+1, then Type=1 refreshes attempts.")
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static HashSet<int> CreateLegacySkyTowerObjectiveMonsterFamilyIds()
    {
        var monsterFamilyIds = new HashSet<int>();
        for (var floor = 1;
             LegacySkyTowerCatalog.TryResolveByFloor(floor, out var challenge);
             floor++)
        {
            monsterFamilyIds.Add(checked((int)challenge.MonsterFamilyId));
        }

        return monsterFamilyIds;
    }
}
