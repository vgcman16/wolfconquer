namespace WolfConquer.Core.World;

public sealed partial class LegacyOwnedAdmissionRuntime
{
    internal bool IsNativeCombatGearFrameCurrent(WorldServerFrameHandler actualFrame)
    {
        lock (gate)
            return !closed && !stopping && ReferenceEquals(frame, actualFrame) &&
                Ordinary.IsCurrent(configured.Roots) && Ordinary.IsReconciliationComplete &&
                Recovery.IsReadyForOrdinaryAdmission(configured.Roots);
    }
}
