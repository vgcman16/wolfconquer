using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedDragonhowlExperienceOverlapTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Peer = OwnedEquipmentBehaviorFixture.Peer;
    private sealed class OverlapClock : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    }

    private static Task Run(ushort spellId, Func<OwnedEquipmentBehaviorFixture, LegacyMonsterInstance,
        MonsterRuntimeStateStore, OverlapClock, Task> exercise)
    {
        var input = LegacyMagicAttackRuntimeTests.DragonhowlExperienceOverlapInputs(spellId);
        var clock = new OverlapClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearOptionsAsync(input.Actor, input.Catalog, options, async f =>
        {
            var actor = f.Current(); var peer = f.Current(Peer);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(actor.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            var nativeGear = Assert.IsType<LegacyNativeMyCombatGear>(actor.NativeGameClient.CombatGear);
            Assert.True(nativeGear.IsAdmissionOriginCurrent(binding, actor));
            Assert.True(f.NativeCombatGear!.IsCurrentFor(input.Catalog));
            Assert.Equal(201U, Assert.Single(new CombatGearSnapshotLoader(f.Data.CombatGear)
                .LoadByCharacterUid(Actor).Items).ItemID);
            Assert.True(new PlayerSpellSnapshotLoader(f.Data.Spells).LoadByCharacterUid(Actor).ContainsSpell(spellId));
            Assert.Equal(51_000L, actor.Character.MinHitPoints);
            Assert.Equal(30L, peer.Character.MinHitPoints);
            Assert.Equal(300U, LegacyPhysicalStatusOverlay.ForOwner(peer).MaxMana);
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(peer, out var maximum));
            Assert.Equal(30U, maximum);

            await f.SendAsync(Actor, new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes());
            await f.SendAsync(Actor, new MsgTeamPacket(MsgTeamType.InviteRequest, Peer).ToFrameBytes());
            await f.SendAsync(Peer, new MsgTeamPacket(MsgTeamType.AcceptInvitation, Actor).ToFrameBytes());
            var teams = Field<TeamSessionStateStore>(f.Handler, "_teamStates");
            var members = teams.GetConnections(f.Connection(Actor));
            Assert.Equal(2, members.Count);
            Assert.Contains(f.Connection(Actor), members); Assert.Contains(f.Connection(Peer), members);
            Assert.Equal(teams.GetTeamUid(f.Connection(Actor)), teams.GetTeamUid(f.Connection(Peer)));

            var monsters = Field<MonsterRuntimeStateStore>(f.Handler, "_monsterStates");
            var spawn = new MonsterSpawn(91_749, 1_018, input.Family.Id, 831, 584, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1_018, input.Family.Name, "fixture://owned-overlap/910.ini",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, input.Family, monsters.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, monsters.RegisterDynamicMonster(monster, input.Family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, input.Family, out _));
            Assert.Equal(500U, monster.Hitpoints); Assert.Equal(500U, monster.MaxLife);
            Assert.Equal((byte)10, monster.Level);
            f.ActorFrames.Clear(); f.Frames(Peer).Clear();
            await exercise(f, monster, monsters, clock);
        }, input.Peer);
    }

    private static T Field<T>(object owner, string name) => Assert.IsType<T>(owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));

    private static byte[] Physical(LegacyMonsterInstance monster) => new MsgAttackPacket(new MsgAttackQuery(
        Uid: Actor, OpponentUid: monster.Uid, X: monster.X, Y: monster.Y, AttackType: MsgAttackType.Physical,
        Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes();
}
