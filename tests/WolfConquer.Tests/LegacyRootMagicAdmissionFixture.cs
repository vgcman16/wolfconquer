using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed partial class RootMagicEntryFixture
    {
        public sealed record AdmissionImage(WorldSession Owner, WorldSession Peer,
            string OwnerValues, string PeerValues, object Identity, int FrameCount,
            LegacyNativeMagicRow Row, ushort CoolDown, float Damage, string[] SourceFields);

        public AdmissionImage CaptureAdmission(ushort id = 1_000)
        {
            var owner = Owner;
            var peer = Peer;
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(
                sessions, connections[PlayerUid], owner.Character.Uid, out var identity));
            var row = NativeRow(id);
            return new(owner, peer, JsonSerializer.Serialize(owner), JsonSerializer.Serialize(peer),
                identity, Order.Count, row, row.CoolDown, row.Damage, row.Source.Fields.ToArray());
        }

        public void AssertAdmissionRejected(AdmissionImage before)
        {
            Assert.Same(before.Owner, Owner);
            Assert.Same(before.Peer, Peer);
            Assert.Equal(before.OwnerValues, JsonSerializer.Serialize(Owner));
            Assert.Equal(before.PeerValues, JsonSerializer.Serialize(Peer));
            Assert.True(LegacyNativeClientPublication.TryCurrent(sessions, connections[PlayerUid],
                before.Owner.Character.Uid, before.Identity, out var current));
            Assert.Same(before.Owner, current);
            Assert.Same(before.Owner.Character.Attributes, Owner.Character.Attributes);
            Assert.Same(before.Owner.PlayerItems, Owner.PlayerItems);
            Assert.Same(before.Owner.PlayerSpells, Owner.PlayerSpells);
            Assert.Same(before.Owner.MagicCooldowns, Owner.MagicCooldowns);
            Assert.Same(before.Row, NativeRow(before.Row.Id));
            Assert.Equal(before.CoolDown, before.Row.CoolDown);
            Assert.Equal(before.Damage, before.Row.Damage);
            Assert.Equal(before.SourceFields, before.Row.Source.Fields);
            Assert.Empty(Order.ToArray().Skip(before.FrameCount)); // ALL owner/peer frames, not only cost packets.
        }

        public void MoveAdmissionPeer(long x) => sessions.Set(connections[TargetPlayerUid],
            Peer with { Character = Peer.Character with { X = x } });

        public void EnableAdmissionPeerCaster()
        {
            var peer = Peer;
            sessions.Set(connections[TargetPlayerUid], peer with
            {
                Character = peer.Character with { Class = Owner.Character.Class },
                PlayerSpells = Owner.PlayerSpells,
            }); // Declared second caster's learned input; no stamp/resource/cooldown result is seeded.
        }

        public Task CastAdmissionPeerAsync(ushort id)
        {
            var peer = Peer;
            var query = new MsgAttackQuery(Uid: TargetPlayerUid, OpponentUid: PlayerUid,
                X: checked((ushort)Owner.Character.X), Y: checked((ushort)Owner.Character.Y),
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: id);
            return PacketHandlers.HandleAsync(context with
                { Connection = connections[TargetPlayerUid], Session = peer },
                ReadFrame(new MsgAttackPacket(query).ToFrameBytes())).AsTask();
        }
    }
}
