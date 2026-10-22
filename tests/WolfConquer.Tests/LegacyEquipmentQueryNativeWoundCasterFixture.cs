using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed partial class DirectQueryHpFixture
    {
        public async Task PrepareQueryHpNativeWoundCasterAsync()
        {
            Assert.True(_sessions.TryGet(_connections[TargetPlayerUid], out var before));
            var original = Assert.IsType<WorldSession>(before);
            var hitpoints = original.Character.MinHitPoints;
            var start = Order.Count;
            await PacketHandlers.HandleAsync(_context with
                { Connection = _connections[TargetPlayerUid], Session = original },
                ReadFrame(new MsgActionPacket { ObjId = TargetPlayerUid, Type = (MsgActionType)251 }.ToFrameBytes()));
            var bytes = Assert.Single(Order.ToArray().Skip(start),
                entry => entry.Recipient == TargetPlayerUid &&
                    LegacyPlayerStatusPacket.TryParse(ReadFrame(entry.Bytes), out _)).Bytes;
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            Assert.Equal(500U, status.MaxHP);
            Assert.Equal(1U, status.MagicAttack);
            Assert.True(_sessions.TryGet(_connections[TargetPlayerUid], out var after));
            Assert.Equal(hitpoints, Assert.IsType<WorldSession>(after).Character.MinHitPoints);
        }
    }
    private sealed partial class DirectQueryTeamHpFixture
    {
        public async Task PrepareTeamHpNativeWoundCasterAsync()
        {
            Assert.True(_sessions.TryGet(_connections[TargetPlayerUid], out var before));
            var original = Assert.IsType<WorldSession>(before);
            var hitpoints = original.Character.MinHitPoints;
            var start = Order.Count;
            await PacketHandlers.HandleAsync(_context with
                { Connection = _connections[TargetPlayerUid], Session = original },
                ReadFrame(new MsgActionPacket { ObjId = TargetPlayerUid, Type = (MsgActionType)251 }.ToFrameBytes()));
            var bytes = Assert.Single(Order.ToArray().Skip(start),
                entry => entry.Recipient == TargetPlayerUid &&
                    LegacyPlayerStatusPacket.TryParse(ReadFrame(entry.Bytes), out _)).Bytes;
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            Assert.Equal(500U, status.MaxHP);
            Assert.Equal(1U, status.MagicAttack);
            Assert.True(_sessions.TryGet(_connections[TargetPlayerUid], out var after));
            Assert.Equal(hitpoints, Assert.IsType<WorldSession>(after).Character.MinHitPoints);
        }
    }
}
