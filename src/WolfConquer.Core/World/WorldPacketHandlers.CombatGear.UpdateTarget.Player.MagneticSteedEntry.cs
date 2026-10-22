namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Only the source MyCombatGear.UpdateTarget20220 -> ProcescMagic steed-entry prerequisite.
    // A rejected invocation returns before CanUse; this does not decide any Jiang eligibility.
    private static async ValueTask<(WorldSession? Session, bool Rejected)>
        TryRejectLegacyCombatGearMagneticSteedEntryAsync(
            WorldPacketHandlerContext context, WorldSession session, CancellationToken cancellationToken)
    {
        var uid = session.Character.Uid;
        object? nativeIdentity = null;
        if (context.Sessions is { } world)
        {
            if (!LegacyNativeClientPublication.TryCaptureIdentity(world, context.Connection, uid, out var identity) ||
                !LegacyNativeClientPublication.TryCurrent(world, context.Connection, uid, identity, out session))
                return (null, true);
            nativeIdentity = identity;
        }
        if (!HasLegacyStatusFlag(session.Character, LegacyRideFlag) ||
            (LegacyPhysicalStatusOverlay.ForOwner(session).RidingCropItemId ?? 0U) != 0)
            return (session, false);

        if (nativeIdentity is { } capturedIdentity && context.Sessions is { } capturedWorld)
        {
            if (!LegacyNativeClientPublication.TryUpdate(capturedWorld, context.Connection, uid,
                    capturedIdentity, owner => owner with { Character = ClearLegacyMountRideStatus(owner.Character) },
                    out session)) return (null, true);
            var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                CheckedUInt32(uid, nameof(uid)), LegacyPlayerDeathState.CreateStatusFlagVector(
                    session.Character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
            var published = await LegacyNativeClientPublication.SendOwnerAndViewAsync(
                    capturedWorld, context.Connection, uid, capturedIdentity, frame,
                    (owner, peer) => CanSeePlayer(owner, peer) &&
                        ReadCharacterDynamicId(owner) == ReadCharacterDynamicId(peer), cancellationToken)
                .ConfigureAwait(false);
            return (published, true); // Native RemoveFlag sends owner, then view, then ProcescMagic returns.
        }

        // Preserve the existing no-Store publication shape without inventing a second cache authority.
        session = session with { Character = ClearLegacyMountRideStatus(session.Character) };
        return (await SendLegacyArrowBladesVectorAsync(context with { Session = session }, session, uid,
            cancellationToken).ConfigureAwait(false), true);
    }
}
