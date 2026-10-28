using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record FullAccountClassFrame(byte[] Bytes, bool Visible = false);
    private sealed record FullAccountClassPeer(ConquerTcpConnection Connection, long Uid, object Identity);

    private static FullAccountClassFrame[] BuildFullAccountClassFrames(LegacyFullAccountClassOwner owner,
        LegacyFullAccountClassProjection projection, LegacyFullAccountClassGrant grant)
    {
        var uid = checked((uint)projection.UpdatedCharacter.Uid);
        var frames = new List<FullAccountClassFrame>();
        foreach (var update in CreateLegacyFullAccountInitialStatUpdates(uid, projection.InitialCharacter)) frames.Add(new(update.ToFrameBytes()));
        foreach (var spellId in projection.RemovedSpellIds)
            frames.Add(new(new MsgActionPacket { ObjId = uid, DwParam = spellId, Type = LegacyFullAccountClassRemoveSpellAction }.ToFrameBytes()));
        if (projection.LevelChanged)
        {
            frames.Add(new(LegacyMsgUpdatePacket.CreateLegacyRuntimeLevelUpdate(uid, checked((ulong)LegacyFullAccountClassTargetLevel)).ToFrameBytes()));
            frames.Add(new(new MsgActionPacket { ObjId = uid, Type = LegacyFullAccountClassLevelAction,
                PositionX = checked((uint)LegacyFullAccountClassTargetLevel) }.ToFrameBytes(), Visible: true));
            if (projection.SendLevelSourceStats)
                foreach (var update in CreateLegacyFullAccountLevelSourceStatUpdates(uid, projection.LeveledCharacter)) frames.Add(new(update.ToFrameBytes()));
            else if (projection.LevelAttributePointsAdded != 0)
                frames.Add(new(LegacyMsgUpdatePacket.CreateLegacyRuntimeAttributePointsUpdate(uid,
                    CheckedUInt64(projection.LeveledCharacter.AttributePoints, nameof(projection.LeveledCharacter.AttributePoints))).ToFrameBytes()));

            var preHeal = owner.Inputs.Stable with { Character = WithLegacyFullAccountClassAttributes(projection.LeveledCharacter with
                { MinHitPoints = Math.Min(owner.Expected.Character.MinHitPoints, projection.LeveledCharacter.MinHitPoints) }) };
            var current = checked((uint)preHeal.Character.MinHitPoints);
            var maximum = ComputeLegacyRuntimeMaxHitpoints(preHeal.Character, current, preHeal.PlayerItems,
                preHeal.CombatGear!, owner.Context.WorldData!, preHeal.AlternateEquipmentActive);
            foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(uid, maximum, Math.Min(current, maximum)))
                frames.Add(new(update.ToFrameBytes()));
            frames.Add(new(CreateNativeMagicCachedPlayerStatusResponse(owner.Context, preHeal, owner.Context.Connection, owner.Identity).ToFrameBytes()));
            frames.Add(new(LegacyUserTotalRefineLevPacket.FromEquipmentPerfection(uid, preHeal.PlayerItems).ToFrameBytes()));
            var healed = checked((uint)projection.LeveledCharacter.MinHitPoints);
            foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(uid, healed, healed)) frames.Add(new(update.ToFrameBytes()));
        }
        frames.Add(new(new LegacyMsgUpdatePacket(uid, [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Class, grant.TargetClass)]).ToFrameBytes()));
        foreach (var spell in projection.AddedSpellPackets) frames.Add(new(LegacySpellPacket.FromSnapshot(spell).ToFrameBytes()));
        return frames.ToArray();
    }

    private static FullAccountClassPeer[] CaptureFullAccountClassPeers(LegacyFullAccountClassOwner owner, LegacyFullAccountClassProjection projection)
    {
        if (!projection.LevelChanged) return [];
        var peers = new List<FullAccountClassPeer>();
        foreach (var active in owner.Context.Sessions!.Snapshot())
        {
            if (ReferenceEquals(active.Connection, owner.Context.Connection) || active.Session.Character.Uid == owner.Expected.Character.Uid ||
                !CanSeePlayer(active.Session.Character, projection.UpdatedCharacter)) continue;
            if (!active.Connection.HasSendRoute) throw new FullAccountClassRefusal("fullAccountClassPeerSenderUnavailable");
            if (!LegacyNativeClientPublication.TryCaptureIdentity(owner.Context.Sessions, active.Connection, active.Session.Character.Uid, out var identity))
                throw InvalidFullAccountClassState();
            peers.Add(new(active.Connection, active.Session.Character.Uid, identity));
        }
        return peers.ToArray();
    }

    private sealed partial class LegacyFullAccountClassPrepared
    {
        internal async ValueTask<bool> PublishAsync(CancellationToken cancellationToken)
        {
            foreach (var frame in frames)
            {
                if (!Owner.IsPlainCurrent(Updated)) return false;
                await Owner.Context.Connection.SendAsync(frame.Bytes, cancellationToken).ConfigureAwait(false);
                if (!Owner.IsPlainCurrent(Updated)) return false;
                if (!frame.Visible) continue;
                foreach (var peer in peers)
                {
                    if (!Owner.IsPlainCurrent(Updated)) return false;
                    if (!peer.Connection.HasSendRoute || !Owner.PeerCurrent(peer.Connection, peer.Uid, peer.Identity, out var current) ||
                        !CanSeePlayer(current.Character, Updated.Character)) continue;
                    await peer.Connection.SendAsync(frame.Bytes, cancellationToken).ConfigureAwait(false);
                    if (!Owner.IsPlainCurrent(Updated)) return false;
                }
            }
            if (!Owner.IsPlainCurrent(Updated)) return false;
            await Owner.Context.Connection.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }
}
