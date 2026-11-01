using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedDragonhowlFineRainAwardTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Peer = OwnedEquipmentBehaviorFixture.Peer;
    private const ushort FineRain = 14_500;
    private const string Opaque = "Opaque/Keep-Original-Spelling/01";
    private sealed class RainClock : TimeProvider
    {
        private long ticks;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero)
            .AddTicks(Interlocked.Read(ref ticks));
        internal void Advance(int milliseconds) => Interlocked.Add(ref ticks, TimeSpan.FromMilliseconds(milliseconds).Ticks);
    }
    private sealed record RainCase(OwnedEquipmentBehaviorFixture Owner, LegacyMonsterInstance Monster,
        MonsterRuntimeStateStore Monsters, RainClock Clock, ILegacySignedMillisecondClock LegacyClock,
        ushort Spell, bool Threshold, QueryImage InitialQuery)
    {
        internal int BeforeDeathHp => Threshold ? 640 : 130;
        internal uint AfterDeathMaximum => Threshold ? 540U : 30U;
        internal int Stamp => Threshold ? 16_001 : 10_000;
        internal byte[] Request() => new MsgAttackPacket(new MsgAttackQuery(Actor, Monster.Uid, Monster.X, Monster.Y,
            MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0)).ToFrameBytes();
    }
    private sealed record QueryImage(object Entry, object Epoch, object Completion);

    private static Task Run(ushort spell, bool threshold, Func<RainCase, Task> body, bool early = false)
    {
        var input = LegacyMagicAttackRuntimeTests.DragonhowlExperienceOverlapInputs(spell);
        var fine = LegacyMagicAttackRuntimeTests.CreateOwnedPhysicalEquipmentInputs("distinct", Actor, Peer);
        var fineRow = Assert.Single(fine.World.Magic, row => row.Id == FineRain);
        var thresholds = new[]
        {
            new LevelExperience(0, 10, threshold ? 1 : 1_000_000, 0, 0, 1, threshold ? "0 10 1 0 0" : "0 10 1000000 0 0",
                new[] { "0", "10", threshold ? "1" : "1000000", "0", "0" }),
            new LevelExperience(0, 11, 1_000_000, 0, 0, 2, "0 11 1000000 0 0",
                new[] { "0", "11", "1000000", "0", "0" }),
        };
        var catalog = LegacyItemWorldDataFixture.Create(Array.Empty<ItemType>(), [],
            classLevelStats: input.Catalog.ClassLevelStats, monsters: new[] { input.Family },
            magic: input.Catalog.Magic.Concat(new[] { fineRow }).ToArray(), levelExperience: thresholds,
            combatGear: input.Catalog.CombatGear);
        var fields = new Dictionary<string, string>(input.Peer.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["OpaqueFineRain"] = Opaque };
        var peer = input.Peer with { Character = input.Peer.Character with { Attributes = fields },
            Spells = new PlayerSpellSnapshot(new[] { new PlayerSpellSnapshotEntry(FineRain, 0, 0, 0, 0, 0) }) };
        var clock = new RainClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearOptionsAsync(input.Actor, catalog, options, async f =>
        {
            // Earn14500 before Team creation, so only the real caster is buffed.
            await f.SendAsync(Peer, new MsgAttackPacket(new MsgAttackQuery(Peer, Peer, 832, 584,
                MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0, SpellId: FineRain)).ToFrameBytes());
            var earned = f.Current(Peer); var p = Binding(f, Peer); var a = Binding(f, Actor);
            AssertFineRain(earned, 10_000);
            Assert.Equal(130L, earned.Character.MinHitPoints); Assert.Equal(130, p.Player.HitPoints);
            Assert.Equal(130U, p.Client.Status.MaxHitpoints); Assert.Equal(51_000, a.Player.HitPoints);
            Assert.Equal(30L, input.Peer.Character.MinHitPoints);
            Assert.False(LegacyEquipmentRebuildAdmission.ApplyCurrent(earned, LegacyEquipmentRebuildReason.PlayerDeath,
                options.LegacyClock!).Admitted);
            var query = ReadQuery(p);
            Assert.True(f.NativeCombatGear!.IsCurrentFor(catalog));
            Assert.True(a.Client.CombatGear!.IsAdmissionOriginCurrent(a, f.Current()));
            Assert.Equal(201U, Assert.Single(new CombatGearSnapshotLoader(f.Data.CombatGear).LoadByCharacterUid(Actor).Items).ItemID);
            Assert.True(new PlayerSpellSnapshotLoader(f.Data.Spells).LoadByCharacterUid(Actor).ContainsSpell(spell));
            await f.SendAsync(Actor, new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes());
            await f.SendAsync(Actor, new MsgTeamPacket(MsgTeamType.InviteRequest, Peer).ToFrameBytes());
            await f.SendAsync(Peer, new MsgTeamPacket(MsgTeamType.AcceptInvitation, Actor).ToFrameBytes());
            Assert.Equal(2, Field<TeamSessionStateStore>(f.Handler, "_teamStates").GetConnections(f.Connection(Actor)).Count);
            var monsters = Field<MonsterRuntimeStateStore>(f.Handler, "_monsterStates");
            var spawn = new MonsterSpawn(91_890, 1_018, input.Family.Id, 831, 584, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1_018, input.Family.Name, "fixture://earned-finerain/910.ini",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, input.Family, monsters.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, monsters.RegisterDynamicMonster(monster, input.Family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, input.Family, out _));
            if (threshold && !early) clock.Advance(6_001);
            f.ActorFrames.Clear(); f.Frames(Peer).Clear();
            await body(new(f, monster, monsters, clock, options.LegacyClock!, spell, threshold, query));
        }, peer);
    }

    private static LegacyOrdinarySpellBinding Binding(OwnedEquipmentBehaviorFixture f, uint uid) =>
        Assert.IsType<LegacyOrdinarySpellBinding>(f.Current(uid).NativeGameClient!.OrdinarySpellBinding);
    private static T Field<T>(object owner, string name) => Assert.IsType<T>(owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));
    private static QueryImage ReadQuery(LegacyOrdinarySpellBinding binding)
    {
        object Read(object registry, object key)
        {
            object?[] args = [key, null];
            Assert.True((bool)registry.GetType().GetMethod("TryGetValue")!.Invoke(registry, args)!);
            return Assert.IsAssignableFrom<object>(args[1]);
        }
        // Assertion-only observation of actual query registrations/epoch.
        var type = typeof(LegacyNativeMagicQueryClient);
        var entries = type.GetField("Entries", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var completions = type.GetField("CurrentMaximumCompletions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var entry = Read(entries, binding.NativeIdentity);
        var epoch = entry.GetType().GetField("Epoch", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(entry)!;
        return new(entry, epoch, Read(completions, entry));
    }
    private static object ReadEpoch(LegacyOrdinarySpellBinding binding) => ReadQuery(binding).Epoch;
}
