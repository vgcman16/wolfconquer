namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class OwnedBasicPhysicalReceiveData
    {
        private IReadOnlyList<OwnedBasicPhysicalReceivePhase> PrepareOwnedPhysicalReceivePhases(
            WorldSession receiveTarget, WorldSession finalTarget, bool hawks, bool killed)
        {
            var phases = new List<OwnedBasicPhysicalReceivePhase>();
            uint uid = CheckedUInt32(TargetBinding.Uid, nameof(TargetBinding.Uid));
            if (hawks)
            {
                var words = LegacyWindWalkerStatusProjection.Read(receiveTarget.Character);
                phases.Add(new(OwnedBasicPhysicalReceiveAudience.TargetScreen,
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(uid, words).ToFrameBytes()));
            }
            if (!killed)
            {
                targetMetadata.Extend(finalTarget);
                uint maximum = GetLegacyEquipmentRuntimeMaxHitpoints(finalTarget, Context.WorldData);
                uint hitpoints = CheckedUInt32(finalTarget.Character.MinHitPoints, nameof(finalTarget.Character.MinHitPoints));
                foreach (var frame in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(uid, maximum, hitpoints))
                    phases.Add(new(OwnedBasicPhysicalReceiveAudience.Target, frame.ToFrameBytes()));
            }
            // Lethal historical death/query packets belong to the actual Root
            // Death plan; do not run that source executor or reissue its phases.
            return Array.AsReadOnly(phases.ToArray());
        }
    }
}
