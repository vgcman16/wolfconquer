using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // The existing source HP setter projection, shared by its actual death
    // receipt join. This pure projection supplies no persistence permission.
    internal static WorldSession ProjectAssignedLegacyHitpoints(WorldPacketHandlerContext context,
        WorldSession owner, long ownerUid, int value)
    {
        if (!LegacyEquipmentHitpointsStatus.TryGetForOwner(owner, out _))
        {
            var previousHitpoints = owner.Character.MinHitPoints > 0
                ? CheckedUInt32(owner.Character.MinHitPoints, nameof(owner.Character.MinHitPoints)) : 0U;
            owner = owner with
            {
                EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(ownerUid,
                    GetLegacyEquipmentRuntimeMaxHitpoints(owner, context.WorldData, previousHitpoints)),
            };
        }
        if (owner.Character.MinHitPoints == value) return owner;
        var changed = owner.Character with { MinHitPoints = value };
        var attributes = new Dictionary<string, string>(owner.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in CharacterSnapshotScalarPersistence.BuildPersistedValues(changed)) attributes[pair.Key] = pair.Value;
        CharacterSnapshotEpicTrojanSchema.ProjectRuntimeAttributes(attributes, attributes);
        return owner with { Character = changed with { Attributes = attributes } };
    }
}
