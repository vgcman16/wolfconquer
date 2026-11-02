namespace WolfConquer.Core.World;

// Closed failure-only diagnostic. It cannot supply an owner, result or success.
internal enum LegacyRelicDecomposePublicationFault { None, BeforeRanking }

public sealed partial class LegacyOwnedAdmissionRuntime
{
    internal LegacyRelicDecomposePublicationFault RelicDecomposePublicationFault { get; set; }

    internal void RequireRelicDecomposePublicationPlan(WorldServerFrameHandler actualFrame)
    {
        lock (gate)
        {
            if (!Enum.IsDefined(RelicDecomposePublicationFault) ||
                !RelicDecomposeFrameIsCurrent(actualFrame, out _))
                throw new InvalidOperationException("relicDecomposePublicationPlanUnavailable");
        }
    }

    internal void ThrowBeforeRelicDecomposeRankingIfPlanned(WorldServerFrameHandler actualFrame)
    {
        lock (gate)
        {
            if (!Enum.IsDefined(RelicDecomposePublicationFault) ||
                !RelicDecomposeFrameIsCurrent(actualFrame, out _))
                throw new InvalidOperationException("relicDecomposePublicationPlanUnavailable");
            if (RelicDecomposePublicationFault != LegacyRelicDecomposePublicationFault.BeforeRanking) return;
            RelicDecomposePublicationFault = LegacyRelicDecomposePublicationFault.None;
            throw new InvalidOperationException("relicDecomposeInjectedBeforeRanking");
        }
    }
}
