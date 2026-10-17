using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyPlayerReceiveEarlyModifierExecution
    {
        public async ValueTask<bool> SendTargetAsync(byte[] frame, CancellationToken cancellationToken)
        {
            if (!Refresh()) return false;
            await Target.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            return Refresh();
        }

        public async ValueTask<bool> SendTargetVectorAsync(CancellationToken cancellationToken)
        {
            if (!Refresh()) return false;
            var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                    CheckedUInt32(targetUid, nameof(targetUid)),
                    LegacyPlayerDeathState.CreateStatusFlagVector(Target.Session.Character.Attributes,
                        LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
            if (!await SendTargetAsync(frame, cancellationToken).ConfigureAwait(false)) return false;
            if (context.Sessions is not { } sessions) return true;
            foreach (var candidate in sessions.Snapshot())
            {
                if (!Refresh()) return false;
                if (ReferenceEquals(candidate.Connection, Target.Connection) ||
                    !sessions.TryGet(candidate.Connection, out var peer) || peer is null ||
                    peer.Character.Uid != candidate.Session.Character.Uid ||
                    peer.Character.Uid == targetUid || !CanSeePlayer(Target.Session.Character, peer.Character))
                    continue;
                await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                if (!Refresh()) return false;
            }
            return true;
        }

        public async ValueTask<bool> LogHawksAsync()
        {
            await context.Log.WriteLineAsync(
                    "WorldServer consumed legacy HawksAmbition player-hit reduction " +
                    $"targetUid={targetUid} statusFlag={LegacyHawksAmbitionFlag}; " +
                    "source=ReceiveAttack.Player.Execute RemoveFlag(HawksAmbition).")
                .ConfigureAwait(false);
            return Refresh();
        }

        public ValueTask<bool> SendAzureAsync(uint left, uint level, CancellationToken cancellationToken,
            bool remainingTime = false)
        {
            if (!Refresh()) return ValueTask.FromResult(false);
            var time = remainingTime
                ? LegacyLightningShieldActivationLifetime.RemainingSeconds(Target.Session.Character,
                    (context.TimeProvider ?? TimeProvider.System).GetLocalNow().DateTime)
                : 10U; // Successful refill always sends10, even when AddFlag retained an older timer.
            var packet = new LegacyMsgUpdatePacket(CheckedUInt32(targetUid, nameof(targetUid)),
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.AzureShield, Value: 0,
                    Flag: LegacyLightningShieldActivatedFlag, Time: time, Damage: left, SpellLevel: level)]);
            return SendTargetAsync(packet.ToFrameBytes(), cancellationToken);
        }
    }
}
