using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedDragonhowlFloorDropTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor;
    private const int Map = 1_018;
    private const uint Potion = 1_001_000, Money = 1_090_000;
    private sealed class FloorClock : TimeProvider
    {
        private long ticks;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero)
            .AddTicks(Interlocked.Read(ref ticks));
        internal void Advance(TimeSpan amount) => Interlocked.Add(ref ticks, amount.Ticks);
    }

    private static Task Run(ushort spellId, bool money, bool walkable, Func<FloorFixture, Task> body)
    {
        // Reuse only the frozen saved DATA recipe. Real native19 publication,
        // ordinary login, catalog ownership and Physical selection remain earned.
        var original = LegacyMagicAttackRuntimeTests.DragonhowlExperienceOverlapInputs(spellId);
        var fields = new Dictionary<string, string>(original.Actor.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyMonsterDropRoll"] = money ? "49" : "504",
            ["LegacyMonsterDropMoneyAmount"] = "50",
        };
        var actor = original.Actor with { Character = original.Actor.Character with { X = 10, Y = 10, Attributes = fields } };
        var family = original.Family with { Attributes = new Dictionary<string, string>(original.Family.Attributes,
            StringComparer.OrdinalIgnoreCase) { ["drop_hp"] = "1001000" } };
        var catalog = LegacyItemWorldDataFixture.Create(
            new[] { new ItemType((int)Potion, "FloorLifePotion", []), new ItemType((int)Money, "FloorMoney50", []) }, [],
            monsters: new[] { family }, magic: original.Catalog.Magic,
            classLevelStats: original.Catalog.ClassLevelStats, levelExperience: original.Catalog.LevelExperience,
            combatGear: original.Catalog.CombatGear);
        var cells = new LegacyDMapCell[64 * 64];
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                cells[y * 64 + x] = new(x, y, walkable ? (short)0 : (short)1, 1, 0);
        var terrain = new LegacyGameMapTerrainQuery([new LegacyDMapTerrain(Map,
            "fixture://owned-dragonhowl-floor.dmap", 64, 64, cells)]);
        var clock = new FloorClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearTerrainAsync(actor, catalog, options, terrain, async owner =>
        {
            var monsters = Field<MonsterRuntimeStateStore>(owner.Handler, "_monsterStates");
            var floor = Field<FloorItemRuntimeStateStore>(owner.Handler, "_floorItems");
            var spawn = new MonsterSpawn(91_749, Map, family.Id, 11, 10, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                Map, family.Name, "fixture://owned-floor/910.ini",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, monsters.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, monsters.RegisterDynamicMonster(monster, family));
            Assert.True(owner.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            Assert.Equal(500U, monster.Hitpoints); Assert.Equal(500U, monster.MaxLife);
            var current = owner.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(current.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(owner.NativeCombatGear!.IsCurrentFor(catalog));
            Assert.True(Assert.IsType<LegacyNativeMyCombatGear>(current.NativeGameClient.CombatGear)
                .IsAdmissionOriginCurrent(binding, current));
            Assert.Equal(201U, Assert.Single(new CombatGearSnapshotLoader(owner.Data.CombatGear)
                .LoadByCharacterUid(Actor).Items).ItemID);
            Assert.True(new PlayerSpellSnapshotLoader(owner.Data.Spells).LoadByCharacterUid(Actor).ContainsSpell(spellId));
            Assert.Equal(51_000, binding.Player.HitPoints); Assert.Equal(500U, binding.Player.Mana);
            Assert.Equal(100, (int)binding.Player.Stamina);
            owner.ActorFrames.Clear();
            await body(new(owner, monster, monsters, floor, clock, binding, spellId, money));
        });
    }

    private static T Field<T>(object owner, string name) => Assert.IsType<T>(owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));

    private static uint FloorUid() => unchecked((uint)Assert.IsType<int>(typeof(WorldPacketHandlers)
        .GetField("s_legacyFloorItemUid", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)));

    private sealed class FloorFixture(OwnedEquipmentBehaviorFixture owner, LegacyMonsterInstance monster,
        MonsterRuntimeStateStore monsters, FloorItemRuntimeStateStore floor, FloorClock clock,
        LegacyOrdinarySpellBinding binding, ushort spellId, bool money)
    {
        internal readonly OwnedEquipmentBehaviorFixture Owner = owner;
        internal readonly LegacyMonsterInstance Monster = monster;
        internal readonly MonsterRuntimeStateStore Monsters = monsters;
        internal readonly FloorItemRuntimeStateStore Floor = floor;
        internal readonly FloorClock Clock = clock;
        internal readonly LegacyOrdinarySpellBinding Binding = binding;
        internal readonly ushort SpellId = spellId;
        internal readonly bool IsMoney = money;
        internal uint ItemId => IsMoney ? Money : Potion;
        internal byte[] Request() => new MsgAttackPacket(new MsgAttackQuery(Uid: Actor, OpponentUid: Monster.Uid,
            X: Monster.X, Y: Monster.Y, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
            ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes();
        internal Task Attack() => Owner.SendAsync(Actor, Request());
        internal ValueTask RawAttack() => Owner.Handler.HandleFrameAsync(Owner.Connection(Actor),
            OwnedEquipmentBehaviorFixture.ReadFrame(Request()));
        internal IReadOnlyList<FloorItemRuntimeEntry> SeedNormalDropNeighborhood()
        {
            // ScatterRange0 searches radius1 around the real monster(11,10).
            // Occupy all9 legal tiles without invalidating the attack terrain;
            // (1,1) deliberately remains free to expose a bad retry-origin reset.
            var entries = new List<FloorItemRuntimeEntry>();
            uint first = unchecked(FloorUid() + 65_536U);
            for (ushort y = 9; y <= 11; y++)
                for (ushort x = 10; x <= 12; x++)
                    entries.Add(Seed(unchecked(first + (uint)entries.Count), x, y, Clock.GetUtcNow()));
            Assert.Equal(9, entries.Count);
            return entries;
        }

        internal FloorItemRuntimeEntry Seed(uint uid, ushort x, ushort y, DateTimeOffset created)
        {
            var packet = LegacyFloorItemPacket.FromMonsterDrop(Potion, x, y, Owner.Current().Character, uid);
            var entry = FloorItemRuntimeEntry.ForItem(Map, packet, ownerOnly: true, dynamicId: 77);
            Floor.AddVisible(entry, created);
            return entry;
        }
    }
}
