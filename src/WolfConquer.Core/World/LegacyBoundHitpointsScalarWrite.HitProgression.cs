namespace WolfConquer.Core.World;

internal sealed partial class LegacyBoundHitpointsScalarWrite
{
    internal bool IsPreparedForHitProgression(WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation hit) =>
        dragonhowlDeath is null && ReferenceEquals(basicPhysical, hit) && ReferenceEquals(expected, hit.ExpectedTarget) &&
        ReferenceEquals(binding, hit.TargetBinding) && OwnerIsCurrent();
}
