using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task GenuineUnarmedAdmissionStartsBothTimersAndScheduledMonsterAttack()
    {
        const uint uid = OwnedEquipmentBehaviorFixture.Actor;
        var family = new Monster(930, "UnarmedScheduler", new Dictionary<string, string>
        {
            ["id"] = "930", ["life"] = "5000", ["lookface"] = "930", ["level"] = "1", ["Boss"] = "1",
            ["attack_range"] = "1", ["view_range"] = "6", ["attack_speed"] = "1", ["move_speed"] = "1",
            ["attack_min"] = "5", ["attack_max"] = "5",
        }, []);
        var spawn = new MonsterSpawn(930, 1011, 930, 200, 200, 1, 1, 1, 10, 1, 0, 0, 0, 0,
            1011, family.Name, "fixture://unarmed-scheduler/1.ini", new Dictionary<string, string>
            { ["id"] = "930", ["mapid"] = "1011", ["npctype"] = "930" });
        // This real catalog builder retains its unrelated opaque Items backing;
        // no fake selected row or scheduler event is introduced by the test.
        var catalog = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(monsters: new[] { family }, monsterSpawns: [spawn]);
        Assert.False(catalog.Items is ItemType[] or List<ItemType>);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family,
            catalog.GetLegacyMonsterRuntimeUidBase(spawn)));
        var interval = TimeSpan.FromMilliseconds(100);
        var clock = new BossRuntimeManualTimeProvider(DateTimeOffset.UtcNow.AddSeconds(20));
        var actor = OwnedEquipmentBehaviorFixture.Character(uid, classCode: 2001, mapId: 1011,
            x: 200, y: 200, minHitPoints: 10_000);
        await OwnedEquipmentBehaviorFixture.RunWithEnabledSchedulingAsync(new(actor, PlayerItemSnapshot.Empty), catalog,
            CreateDeterministicMonsterRuntimeLoopOptions(interval, clock), async owner =>
        {
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(owner.Current().NativeGameClient!.OrdinarySpellBinding);
            Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(binding, out var speed));
            Assert.NotNull(speed);
            Assert.Equal((ushort)0, speed.Right); Assert.Equal((ushort)0, speed.Left); Assert.Equal((ushort)0, speed.Ring);
            Assert.True(speed.IsCurrentPlain(binding));
            var frames = new RuntimeThreadSafeFrameCapture();
            owner.BeforeSend = (_, bytes) => frames.Add(bytes);
            try
            {
                await owner.SendAsync(uid, new MsgActionPacket { ObjId = uid, Timestamp = 1234, Type = MsgActionType.SetLocation }.ToFrameBytes());
                await WaitForMonsterRuntimeTimerRegistrationAsync(clock);
                Assert.Contains(frames, bytes => LegacyMonsterSpawnPacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == monster.Uid);
                Assert.True(GetMonsterStates(owner.Handler).TryGetVisibleInstance(monster, clock.GetUtcNow(), out var visible));
                Assert.True(owner.Store.TryReadMonsterPhysicalRange(visible, family, out var birthRange));
                Assert.Equal(3, birthRange);
                Assert.False(ContainsMonsterPhysicalAttackAfter(frames, 0, monster.Uid, uid));
                var beforeHp = owner.Current().Character.MinHitPoints;
                clock.Advance(interval);
                await WaitForProactiveMonsterActionAsync(() => ContainsMonsterPhysicalAttackAfter(frames, 0, monster.Uid, uid), owner.Log);
                Assert.True(owner.Current().Character.MinHitPoints < beforeHp);
                Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.False(owner.Recovery.IsBlocked);
            }
            finally { owner.BeforeSend = null; }
        });
    }
}
