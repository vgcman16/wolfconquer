using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedMonsterRepeatLeaseTests
{
    [Fact]
    public Task RealMonsterHitOutputFailureFencesCommittedDamageBeforeXpOrRepeat()
    {
        var family = new Monster(910, "OutputTarget", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family });
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 139, reborn: 1,
            strength: 100, agility: 0, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            MinMana = 0, MaxMana = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100" },
        };
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor, PlayerItemSnapshot.Empty), catalog, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://monster-output-fence/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
            var files = f.Data.Files(); int attempts = 0; f.ActorFrames.Clear();
            f.BeforeSend = (_, bytes) =>
            {
                if (!MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var attack) ||
                    attack.Query.OpponentUid != monster.Uid || attack.Query.AttackType != MsgAttackType.Physical) return;
                Assert.Equal(0, attempts++);
                Assert.True(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var hit));
                Assert.True(hit.Hitpoints < monster.Hitpoints); f.Data.AssertFiles(files);
                throw new IOException("monster hit output interrupted");
            };
            try
            {
                var packet = new MsgAttackPacket(new MsgAttackQuery(OwnedEquipmentBehaviorFixture.Actor, monster.Uid,
                    monster.X, monster.Y, MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0));
                await Assert.ThrowsAnyAsync<Exception>(() => f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, packet.ToFrameBytes()));
            }
            finally { f.BeforeSend = null; }
            Assert.Equal(1, attempts); Assert.True(f.Recovery.IsBlocked); Assert.True(binding.IsRetired);
            Assert.False(binding.Client.OnAutoAttack); Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
            Assert.Empty(f.ActorFrames); f.Data.AssertFiles(files);
            Assert.True(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damaged));
            Assert.True(damaged.Hitpoints < monster.Hitpoints);
        });
    }

    [Fact]
    public Task ActualAdmittedMonsterFrameRejectsSharedDisposedAndForeignOperationsBeforeDamage()
    {
        var family = new Monster(910, "LeaseTarget", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family });
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 139, reborn: 1,
            strength: 100, agility: 0, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            MinMana = 0, MaxMana = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100" },
        };
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor, PlayerItemSnapshot.Empty), catalog, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://monster-lease/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            var before = f.Current(); var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            var files = f.Data.Files(); var stamp = binding.Player.AttackStamp;
            var barrier = Assert.IsType<WorldOperationBarrier>(typeof(WorldServerFrameHandler)
                .GetField("_worldOperations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var discarded = await barrier.EnterExclusiveAsync(); discarded.Dispose();
            using var shared = await barrier.EnterSharedAsync();
            var otherBarrier = new WorldOperationBarrier(); using var foreign = await otherBarrier.EnterExclusiveAsync();
            var core = typeof(WorldServerFrameHandler).GetMethod("HandleFrameCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var request = new MsgAttackPacket(new MsgAttackQuery(OwnedEquipmentBehaviorFixture.Actor, monster.Uid,
                monster.X, monster.Y, MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0)).ToFrameBytes();
            f.ActorFrames.Clear();
            foreach (var lease in new[] { shared, discarded, foreign })
            {
                // Invoke the real Frame core with an actual invalid lease so its
                // own request capture executes; no token or binding is injected.
                await Assert.IsType<ValueTask>(core.Invoke(f.Handler,
                    [f.Connection(OwnedEquipmentBehaviorFixture.Actor), OwnedEquipmentBehaviorFixture.ReadFrame(request), CancellationToken.None, lease, null]));
                Assert.Same(before, f.Current()); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.Equal(stamp, binding.Player.AttackStamp); Assert.False(binding.Client.OnAutoAttack);
                Assert.True(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visible));
                Assert.Equal(monster.Hitpoints, visible.Hitpoints);
                Assert.Empty(f.ActorFrames); f.Data.AssertFiles(files);
            }
        });
    }
}
