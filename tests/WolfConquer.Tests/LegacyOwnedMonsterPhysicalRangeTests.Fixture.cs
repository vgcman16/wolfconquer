using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedMonsterPhysicalRangeTests
{
    private const int Map = 1011;
    private const uint Dynamic = 77;
    private sealed class RangeClock : TimeProvider
    {
        private long ticks;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero).AddTicks(ticks);
        internal void Advance(TimeSpan value) => ticks = checked(ticks + value.Ticks);
    }

    private static Task RunRangeAsync(int boss, Func<RangeFixture, Task> body)
    {
        var clock = new RangeClock();
        var family = new Monster(910, "OwnedRangeLifecycle", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = boss.ToString(CultureInfo.InvariantCulture),
            ["lookface"] = "910", ["level"] = "70", ["attack_range"] = "50", ["view_range"] = "30",
            ["attack_speed"] = "1000", ["move_speed"] = "500", ["attack_min"] = "5", ["attack_max"] = "6",
            ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family });
        var actor = OwnedEquipmentBehaviorFixture.Character(mapId: Map, x: 430, y: 380, minHitPoints: 5000) with
        {
            Spirit = 10, MinMana = 50, MaxMana = 50,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DynamicID"] = Dynamic.ToString(CultureInfo.InvariantCulture), ["Action"] = "0", ["Stamina"] = "0",
                ["MaxMana"] = "50", ["ExpBallUsed"] = "0",
                ["EpicTrojan"] = "0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/",
            },
        };
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(new(actor, PlayerItemSnapshot.Empty), catalog,
            new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock), async owner =>
            {
                var stateField = typeof(WorldServerFrameHandler).GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(stateField);
                var states = Assert.IsType<MonsterRuntimeStateStore>(stateField.GetValue(owner.Handler));
                var spawn = new MonsterSpawn(910, Map, family.Id, 450, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                    Map, family.Name, "fixture://owned-range/910.ini", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    { ["DynamicID"] = Dynamic.ToString(CultureInfo.InvariantCulture) });
                // The real store issues the monster UID; FromGenerator builds
                // the complete instance from this adopted family and spawn.
                var instance = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
                Assert.Same(instance, states.RegisterDynamicMonster(instance, family));
                var fixture = new RangeFixture(owner, states, catalog, family, instance, clock);
                owner.ActorFrames.Clear(); owner.BeforeSend = (_, _) => fixture.AssertPlayerUnchanged();
                try { await body(fixture); }
                finally { owner.BeforeSend = null; }
            });
    }

    private sealed class RangeFixture
    {
        internal readonly OwnedEquipmentBehaviorFixture Owner;
        internal readonly MonsterRuntimeStateStore States;
        internal readonly WorldDataCatalog Catalog;
        internal readonly Monster Family;
        internal readonly LegacyMonsterInstance Birth;
        internal readonly RangeClock Clock;
        private readonly WorldSession player;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly Dictionary<string, byte[]> files;
        private readonly object image, spells, proficiencies;
        private readonly ushort previousX, previousY;

        internal RangeFixture(OwnedEquipmentBehaviorFixture owner, MonsterRuntimeStateStore states, WorldDataCatalog catalog,
            Monster family, LegacyMonsterInstance birth, RangeClock clock)
        {
            Owner = owner; States = states; Catalog = catalog; Family = family; Birth = birth; Clock = clock;
            player = owner.Current(); binding = player.NativeGameClient!.OrdinarySpellBinding!;
            files = owner.Data.Files(); image = binding.Items.Image; spells = binding.Storage.ClientSpells;
            proficiencies = binding.Proficiencies.ClientProf; previousX = binding.Player.Px; previousY = binding.Player.Py;
            AssertPlayerUnchanged();
        }

        internal LegacyMonsterInstance Visible()
        {
            Assert.True(States.TryGetVisibleInstance(Birth, Clock.GetUtcNow(), out var current));
            return current;
        }

        internal int ReadRange()
        {
            Assert.True(Owner.Store.TryReadMonsterPhysicalRange(Visible(), Family, out var range));
            return range;
        }

        internal async Task Phase(string name)
        {
            // Invoke the real existing Frame phase. It captures its own private
            // loop/clock/target evidence; the test supplies no range event/token.
            var method = typeof(WorldServerFrameHandler).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            await Assert.IsType<ValueTask>(method.Invoke(Owner.Handler, new object[] { Clock.GetUtcNow(), CancellationToken.None }));
            AssertPlayerUnchanged();
        }

        internal void AssertPlayerUnchanged()
        {
            Owner.Data.AssertFiles(files); Assert.Same(player, Owner.Current());
            Assert.True(Owner.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(Owner.Recovery.IsBlocked);
            Assert.Same(image, binding.Items.Image); Assert.Same(spells, binding.Storage.ClientSpells);
            Assert.Same(proficiencies, binding.Proficiencies.ClientProf);
            Assert.Equal(player.Character.MinHitPoints, (long)binding.Player.HitPoints);
            Assert.Equal(player.Character.MinMana, (long)binding.Player.Mana);
            Assert.Equal(LegacyStaminaState.RequireCurrent(player.Character), binding.Player.Stamina);
            Assert.Equal(LegacyActionState.RequireCurrent(player.Character), (uint)binding.Player.Action);
            Assert.Equal((byte)player.Character.Reborn, binding.Player.Reborn); Assert.Equal((ushort)player.Character.Agility, binding.Player.Agility);
            Assert.Equal((uint)Map, binding.Player.Map); Assert.Equal(Dynamic, binding.Player.DynamicID);
            Assert.Equal((ushort)430, binding.Player.X); Assert.Equal((ushort)380, binding.Player.Y);
            Assert.Equal(previousX, binding.Player.Px); Assert.Equal(previousY, binding.Player.Py);
            Assert.False(LegacyTradeJournal.HasPending(Owner.Data.Users));
        }
    }
}
