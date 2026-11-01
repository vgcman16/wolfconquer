using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task GenuineMonsterAiAndOwnedStaminaBothPublishFromTheSameScheduledTicks()
    {
        const uint uid = OwnedEquipmentBehaviorFixture.Actor;
        var family = new Monster(931, "ConcurrentStaminaMonster", new Dictionary<string, string>
        {
            ["id"] = "931", ["life"] = "5000", ["lookface"] = "931", ["level"] = "1", ["Boss"] = "0",
            ["attack_range"] = "1", ["view_range"] = "6", ["attack_speed"] = "1", ["move_speed"] = "1",
            ["attack_min"] = "5", ["attack_max"] = "5",
        }, []);
        var spawn = new MonsterSpawn(931, 1011, 931, 200, 200, 1, 1, 1, 10, 1, 0, 0, 0, 0,
            1011, family.Name, "fixture://monster-ai-stamina/1.ini", new Dictionary<string, string>
            { ["id"] = "931", ["mapid"] = "1011", ["npctype"] = "931" });
        var catalog = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(monsters: new[] { family }, monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family,
            catalog.GetLegacyMonsterRuntimeUidBase(spawn)));
        var interval = TimeSpan.FromMilliseconds(100);
        var clock = new BossRuntimeManualTimeProvider(DateTimeOffset.UtcNow.AddSeconds(20));
        var actor = OwnedEquipmentBehaviorFixture.Character(uid, classCode: 2001, mapId: 1011,
            x: 200, y: 200, minHitPoints: 10_000) with
        { Attributes = new Dictionary<string, string> { ["Stamina"] = "0", ["Action"] = "0" } };
        await OwnedEquipmentBehaviorFixture.RunWithEnabledSchedulingAsync(new(actor, PlayerItemSnapshot.Empty), catalog,
            CreateDeterministicMonsterRuntimeLoopOptions(interval, clock), async owner =>
        {
            var frames = new RuntimeThreadSafeFrameCapture();
            owner.BeforeSend = (_, bytes) =>
            {
                frames.Add(bytes);
                if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
                    packet.Uid == uid && packet.Updates.Any(value => value.Type == LegacyMsgUpdateDataType.XPCircle && value.Value == 1))
                {
                    var published = owner.Current();
                    Assert.Equal("1", published.Character.Attributes["XPCount"]);
                    Assert.True(owner.Store.IsBoundSpellOwnerCurrent(published.NativeGameClient!.OrdinarySpellBinding!));
                    Assert.False(Monitor.IsEntered(owner.Handler.RequireActionLifecycleGate(owner.Store)));
                }
            };
            try
            {
                await owner.SendAsync(uid, new MsgActionPacket { ObjId = uid, Timestamp = 1234, Type = MsgActionType.SetLocation }.ToFrameBytes());
                await WaitForMonsterRuntimeTimerRegistrationAsync(clock);
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(owner.Current().NativeGameClient!.OrdinarySpellBinding);
                Assert.Equal((ushort)0, binding.Player.Stamina);
                for (var tick = 1; tick <= 2; tick++)
                {
                    var previous = owner.Current();
                    var firstFrame = frames.Count;
                    var timers = clock.CaptureScheduledTimers();
                    Assert.Equal(2, timers.Length);
                    // Both genuine workers become due together. The test supplies
                    // no target phase, successful result, or publication callback.
                    clock.Advance(tick == 1 ? interval : TimeSpan.FromSeconds(3));
                    await WaitForMonsterRuntimeTimerReplacementAsync(clock, timers);
                    var current = owner.Current();
                    Assert.NotSame(previous, current);
                    Assert.Equal(checked((ushort)(tick * 5)), LegacyStaminaState.RequireCurrent(current.Character));
                    Assert.Equal(LegacyStaminaState.RequireCurrent(current.Character), binding.Player.Stamina);
                    Assert.True(current.Character.MinHitPoints < previous.Character.MinHitPoints,
                        $"Scheduled tick {tick} did not commit the monster hit. Log={owner.Log}");
                    Assert.Equal(current.Character.MinHitPoints, (long)binding.Player.HitPoints);
                    Assert.True(ContainsMonsterPhysicalAttackAfter(frames, firstFrame, monster.Uid, uid));
                    Assert.True(GetMonsterStates(owner.Handler).TryGetTargetLockState(monster, clock.GetUtcNow(), out var target));
                    Assert.Equal(uid, target.LastTargetUid);
                    Assert.NotNull(target.LastAttackAt);
                    Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
                    Assert.False(owner.Recovery.IsBlocked);
                }
            }
            finally { owner.BeforeSend = null; }
        });
    }
}
