using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Only the real private proc builder calls this after registering its
    // current monster incarnation and complete calculated damage. Every
    // participant remains concrete; no supplied callback can complete a phase.
    private static bool AppendDragonhowlMonsterRewardPhase(WorldSessionStore store,
        WorldSessionStore.OwnedDragonhowlOwner owner, WorldPacketHandlerContext context,
        WorldSessionStore.OwnedDragonhowlNonPlayer target, DragonhowlMonsterOutput output,
        ref WorldSession source, List<DragonhowlPhase> phases,
        Dictionary<ConquerTcpConnection, WorldSession> finals,
        Dictionary<ConquerTcpConnection, WorldSession> calculated,
        Dictionary<ConquerTcpConnection, DragonhowlLevelExperienceData> recipientData,
        out string reason)
    {
        reason = "ownedDragonhowlMonsterRewardPreparationUnavailable";
        if (!output.RequiresDeathContinuation ||
            !PreparedDragonhowlMonsterRewards.TryPrepare(store, owner, target, out var rewards, out reason) || rewards is null)
            return false;
        bool retained = false;
        try
        {
            if (!store.TryPrepareDragonhowlTeamExperience(owner, rewards.Death, rewards.CalculatedAfter,
                    calculated, out var team, out var recipients) || team is null)
            { reason = "ownedDragonhowlTeamExperiencePreparationUnavailable"; return false; }
            var phase = new DragonhowlPhase(context.Connection, source, source, DragonhowlAudience.Owner, [],
                nonPlayer: target, monsterOutput: output, monsterRewards: rewards, teamExperience: team);
            calculated[context.Connection] = rewards.CalculatedAfter;
            foreach (var row in recipients)
            {
                if (row.Gain == 0) continue;
                var connection = row.Member.Binding.Connection;
                calculated[connection] = row.ProjectedAfter;
                finals[connection] = row.ProjectedAfter;
                if (row.Levels is { } levels) recipientData[connection] = levels;
            }
            source = calculated[context.Connection];
            finals[context.Connection] = source;
            phases.Add(phase);
            retained = true; reason = string.Empty; return true;
        }
        finally { if (!retained) rewards.Dispose(); }
    }
}
