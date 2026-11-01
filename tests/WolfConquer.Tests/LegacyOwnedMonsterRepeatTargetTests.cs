using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedMonsterRepeatTargetTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task ActualRegisteredBirthWitnessRejectsDeathReplacementOrMetadataChange(int mutation)
    {
        var family = new Monster(910, "RepeatTarget", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family });
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 139, reborn: 1,
            strength: 100, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            MinMana = 0, MaxMana = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100" },
        };
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor, PlayerItemSnapshot.Empty), catalog, f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://owned-repeat-target/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out var expectedRange));
            var witness = Assert.IsType<MonsterRuntimeStateStore.OwnedMonsterRepeatTarget>(
                MonsterRuntimeStateStore.OwnedMonsterRepeatTarget.TryCapture(states, f.Handler, catalog, monster, family));
            Assert.True(witness.TryReadCurrent(out var current, out var actualFamily, out var range));
            Assert.Equal(monster, current); Assert.Same(family, actualFamily); Assert.Equal(expectedRange, range);
            if (mutation == 1)
                Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            else if (mutation == 0)
                Assert.True(states.ApplyDamage(monster, monster.MaxLife, DateTimeOffset.UtcNow).Killed);
            else
                Assert.IsType<Dictionary<string, string>>(family.Attributes)["view_range"] = "31";
            Assert.False(witness.TryReadCurrent(out _, out _, out _));
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Current().NativeGameClient!.OrdinarySpellBinding!));
            return Task.CompletedTask;
        });
    }
}
