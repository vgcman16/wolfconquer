namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        // One original Player.Dead receiver for every vector/explicit removal in this death operation.
        public LegacyNativePlayerRuntime? NormalDeathPlayerReceiver { get; init; }

        public async ValueTask<bool> SendNormalDeathVectorAsync()
        {
            if (!Refresh()) return false;
            var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                    CheckedUInt32(Request.Scope.OwnerUid, nameof(Request.Scope.OwnerUid)),
                    LegacyNativePlayerFlag6Lifecycle.CaptureVector(NormalDeathPlayerReceiver,
                        LegacyPlayerDeathState.CreateStatusFlagVector(Session.Character.Attributes,
                            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)))
                .ToFrameBytes();
            // One source UpdateFlagOffset captures its vector once, self first.
            if (!await SendAsync(frame, LegacyEquipmentQueryPublishedPhases.None).ConfigureAwait(false))
                return false;
            if (Context.Sessions is not { } sessions) return true;
            foreach (var candidate in sessions.Snapshot())
            {
                if (!Refresh()) return false;
                if (ReferenceEquals(candidate.Connection, Context.Connection) ||
                    !sessions.TryGet(candidate.Connection, out var peer) || peer is null ||
                    peer.Character.Uid != candidate.Session.Character.Uid ||
                    peer.Character.Uid == Request.Scope.OwnerUid ||
                    !CanSeePlayer(Session.Character, peer.Character))
                    continue;
                await candidate.Connection.SendAsync(frame, CancellationToken).ConfigureAwait(false);
                if (!Refresh()) return false;
            }

            return true;
        }
    }
}
