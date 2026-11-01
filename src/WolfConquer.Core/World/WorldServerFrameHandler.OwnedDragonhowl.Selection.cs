using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Called only inside the Store's real ordered owner scope. Selection is
    // retained even if a subsequent proof refuses; the wire cannot reroll.
    internal bool TryPrepareOwnedDragonhowlRequest(WorldPacketHandlerContext context, MsgAttackPacket packet,
        OwnedDragonhowlPrimary primary, out OwnedDragonhowlRequest? request)
    {
        request = null;
        if (!Monitor.IsEntered(_tradeStates.SyncRoot) || context.BasicPhysicalRequest is not { } wireToken ||
            !basicPhysicalRequests.TryGetValue(wireToken, out var wire) || wire.Query != packet.Query ||
            context.Session is not { } actor || _worldData is null ||
            !WorldPacketHandlers.HasOwnedDragonhowlCandidate(actor) ||
            !TryPrepareActionContext(_sessions, context, actor, out var actual) ||
            !TryReadDragonhowlPrimaryPlayer(primary, out var player, out var target) ||
            !DragonhowlPrimaryBeforeCurrent(primary, wireToken, out var targetUid, out var targetX, out var targetY) ||
            targetUid != packet.Query.OpponentUid ||
            !TryCaptureNativeCombatGearMetadata(_sessions, out var tables) || tables is null ||
            string.IsNullOrWhiteSpace(_snapshotLoader?.PlayersCombatGearRoot)) return false;
        if (dragonhowlByWire.TryGetValue(wireToken, out var previous))
        {
            if (!dragonhowlRequests.TryGetValue(previous, out var old) ||
                !ReferenceEquals(old.Context.Session, actor) || !ReferenceEquals(old.Primary, primary) ||
                !TryReadOwnedDragonhowl(previous, out _)) return false;
            request = previous;
            return true;
        }
        var binding = wire.Slot.Readiness.Binding;
        var gear = LegacyNativeMyCombatGear.PhysicalProcInputs.Capture(binding, actor);
        if (gear is null) return false;
        var actorMetadata = LegacyOwnedPhysicalMetadataWitness.CaptureDragonhowl(binding, _worldData, actor);
        var targetMetadata = target is null ? null : LegacyOwnedPhysicalMetadataWitness.CaptureDragonhowl(target, _worldData, player!.Session);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_snapshotLoader.PlayersCombatGearRoot));
        var path = Path.Combine(root, binding.Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        var image = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            CharacterSnapshotFileBaseline.TryReadBytes(path, "ownedDragonhowlGear", out var bytes, out _) &&
                gear.MatchesSavedImage(bytes) ? bytes : null);
        if (image is null) return false;
        var state = new DragonhowlRequestState(wireToken, wire, actual, primary, binding, target,
            gear, tables, actorMetadata, targetMetadata, path, image);
        if (!DragonhowlBeforeCurrent(state)) return false;
        var token = new OwnedDragonhowlRequest();
        dragonhowlRequests.Add(token, state);
        dragonhowlByWire.Add(wireToken, token);
        state.Selection = WorldPacketHandlers.SelectOwnedDragonhowl(actor, targetX, targetY, _worldData, actorMetadata);
        state.Evaluated = true;
        if (!TryReadOwnedDragonhowl(token, out _) || !OwnedDragonhowlSavedGearCurrent(token)) return false;
        request = token;
        return true;
    }
}
