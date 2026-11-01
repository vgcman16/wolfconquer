using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedMonsterRepeatRetirementTests
{
    [Fact]
    public Task RemovingActualHolderDuringCommittedHitOutputCannotArmARepeat()
    {
        var clock = new AttackClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10000));
        var family = new Monster(910, "RetirementTarget", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var threshold = new LevelExperience(0, 139, 1, 0, 0, 1, "0 139 1 0 0", new[] { "0", "139", "1", "0", "0" });
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family }, levelExperience: new[] { threshold });
        OwnedEquipmentBehaviorInput Player(uint uid, string name) => new(
            OwnedEquipmentBehaviorFixture.Character(uid, name, classCode: 1000, level: 139, reborn: 1,
                strength: 100, agility: 0, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
            {
                MinMana = 0, MaxMana = 0,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1" },
            }, PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(
            Player(OwnedEquipmentBehaviorFixture.Actor, "RetiredActor"), catalog, options, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://monster-retirement/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            var connection = f.Connection(OwnedEquipmentBehaviorFixture.Actor);
            var before = f.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            var peer = f.Current(OwnedEquipmentBehaviorFixture.Peer);
            var peerBinding = Assert.IsType<LegacyOrdinarySpellBinding>(peer.NativeGameClient!.OrdinarySpellBinding);
            var files = f.Data.Files(); int removals = 0;
            f.ActorFrames.Clear(); f.Frames(OwnedEquipmentBehaviorFixture.Peer).Clear();
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != OwnedEquipmentBehaviorFixture.Actor ||
                    !MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var hit) ||
                    hit.Query.OpponentUid != monster.Uid || hit.Query.AttackType != MsgAttackType.Physical) return;
                Assert.Equal(0, removals++);
                Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var damaged));
                Assert.True(damaged.Hitpoints < monster.Hitpoints && damaged.Hitpoints > 0);
                Assert.Same(before, f.Current()); f.Data.AssertFiles(files);
                Assert.True(f.Store.TryRemove(connection, out var removed)); Assert.Same(before, removed);
                Assert.True(binding.IsRetired);
            };
            try
            {
                var packet = new MsgAttackPacket(new MsgAttackQuery(OwnedEquipmentBehaviorFixture.Actor, monster.Uid,
                    monster.X, monster.Y, MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0));
                await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, packet.ToFrameBytes());
            }
            finally { f.BeforeSend = null; }
            Assert.Equal(1, removals); Assert.True(f.Recovery.IsBlocked);
            Assert.False(f.Store.TryGet(connection, out _)); Assert.True(binding.IsRetired);
            Assert.False(binding.Client.OnAutoAttack); Assert.Null(binding.Client.AutoAttack);
            Assert.Same(peer, f.Current(OwnedEquipmentBehaviorFixture.Peer));
            Assert.False(peerBinding.IsRetired);
            Assert.Same(peerBinding, peer.NativeGameClient!.OrdinarySpellBinding);
            Assert.Same(peerBinding.Player, peer.NativeGameClient.Player);
            Assert.False(f.Store.IsBoundSpellOwnerCurrent(peerBinding)); // Shared recovery fence denies actions to both holders.
            f.Data.AssertFiles(files); Assert.Empty(f.Frames(OwnedEquipmentBehaviorFixture.Peer));
            var output = Assert.Single(f.ActorFrames);
            Assert.True(MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(output), out var response));
            Assert.Equal(monster.Uid, response.Query.OpponentUid); Assert.Equal(MsgAttackType.Physical, response.Query.AttackType);
            Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var first));
            clock.Advance(700);
            await f.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
            Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var afterDue));
            Assert.Equal(first.Hitpoints, afterDue.Hitpoints); Assert.Single(f.ActorFrames);
            Assert.False(binding.Client.OnAutoAttack); Assert.Null(binding.Client.AutoAttack);
            Assert.Same(peer, f.Current(OwnedEquipmentBehaviorFixture.Peer)); Assert.False(peerBinding.IsRetired);
            Assert.True(f.Recovery.IsBlocked); f.Data.AssertFiles(files);
        }, Player(OwnedEquipmentBehaviorFixture.Peer, "UnaffectedPeer"));
    }

    private sealed class AttackClock : TimeProvider
    {
        private long ticks;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero).AddTicks(Interlocked.Read(ref ticks));
        internal void Advance(int milliseconds) => Interlocked.Add(ref ticks, TimeSpan.FromMilliseconds(milliseconds).Ticks);
    }
}
