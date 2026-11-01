namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class OwnedDragonhowlPreparation
    {
        // The same ordered, read-only checks as the boolean input guard, with
        // the first failing component retained for actual phase diagnostics.
        internal bool TryReadCurrentInputs(out string failure)
        {
            for (int index = 0; index < metadata.Length; index++)
                if (!metadata[index].IsCurrentPlain())
                { failure = "metadata:" + index; return false; }
            for (int index = 0; index < receives.Length; index++)
            {
                var row = receives[index];
                if (!row.IsCurrentPlain(Store.IsDragonhowlReceiveCompleted(Owner, row.TargetConnection)))
                { failure = "receive:" + index; return false; }
            }
            if (eligibility is not null && !eligibility.IsCurrentPlain())
            { failure = "eligibility"; return false; }
            for (int index = 0; index < Phases.Count; index++)
            {
                var row = Phases[index];
                if (!row.IsCurrentPlain())
                { failure = "phase:" + index; return false; }
                if (row.TeamExperience is { } team && !Store.DragonhowlTeamExperienceInputsCurrent(team))
                { failure = "teamExperience:" + index; return false; }
            }
            failure = string.Empty;
            return true;
        }
    }
}
