using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Explicit hold only; this helper never creates stat mutation authority.
    private static bool HasAttachedStatOwner(WorldPacketHandlerContext context, WorldSession session) =>
        session.NativeGameClient?.Player.HasOwnedActorScalarsAttachment == true ||
        context.Connection.NativeGameClient?.Player.HasOwnedActorScalarsAttachment == true;

    private static bool HasChangedOwnedLevelOrStats(CharacterSnapshot before, CharacterSnapshot after) =>
        before.Level != after.Level || before.Experience != after.Experience ||
        before.Strength != after.Strength || before.Agility != after.Agility || before.Vitality != after.Vitality ||
        before.Spirit != after.Spirit || before.AttributePoints != after.AttributePoints;
}
