using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyCombatExperienceProjectionTests
{
    [Fact]
    public void LevelProjectionKeepsCurrentResourcesUntilActualQueryAndRestore()
    {
        var row = new ClassLevelStat("Taoist", 11, 0, 0, 20, 20,
            "fixture://Taoist.ini", 1, "11", "0,0,20,20", "11=0,0,20,20", new[] { "0", "0", "20", "20" });
        var threshold = new LevelExperience(0, 10, 100, 0, 0, 1, "0 10 100 0 0", new[] { "0", "10", "100", "0", "0" });
        var catalog = LegacyItemWorldDataFixture.Create([], [], classLevelStats: new[] { row }, levelExperience: new[] { threshold });
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, level: 10, reborn: 0,
            strength: 0, agility: 0, minHitPoints: 123) with { Vitality = 0, Spirit = 10, MinMana = 45, MaxMana = 300, Experience = 90 };
        var session = new WorldSession(101, "fixture", character, PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
        var result = LegacyCombatExperienceProjection.Create(session, catalog, 10);
        Assert.True(result.LevelChanged); Assert.True(result.StatProgression.AppliedSourceStats);
        Assert.Equal(11, result.After.Level); Assert.Equal(20, result.After.Vitality); Assert.Equal(20, result.After.Spirit);
        Assert.Equal(123, result.After.MinHitPoints); Assert.Equal(45, result.After.MinMana); Assert.Equal(300, result.After.MaxMana);
    }

    [Fact]
    public void NoLevelGainDoesNotApplyAnAvailableAutoStatRow()
    {
        var row = new ClassLevelStat("Taoist", 10, 50, 40, 30, 20,
            "fixture://Taoist.ini", 1, "10", "50,40,30,20", "10=50,40,30,20", new[] { "50", "40", "30", "20" });
        var threshold = new LevelExperience(0, 10, 1000, 0, 0, 1, "0 10 1000 0 0", new[] { "0", "10", "1000", "0", "0" });
        var catalog = LegacyItemWorldDataFixture.Create([], [], classLevelStats: new[] { row }, levelExperience: new[] { threshold });
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, level: 10, reborn: 0,
            strength: 1, agility: 2) with { Vitality = 3, Spirit = 4, Experience = 100 };
        var session = new WorldSession(101, "fixture", character, PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
        var result = LegacyCombatExperienceProjection.Create(session, catalog, 25);
        Assert.False(result.LevelChanged); Assert.False(result.StatProgression.Changed);
        Assert.Equal(125, result.After.Experience);
        Assert.Equal(1, result.After.Strength); Assert.Equal(2, result.After.Agility);
        Assert.Equal(3, result.After.Vitality); Assert.Equal(4, result.After.Spirit);
    }

    [Fact]
    public void BoundedNoThresholdProjectionIsDataOnlyAndDoesNotMutateSource()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(level: 10) with { Experience = 125 };
        var session = new WorldSession(101, "fixture", character, PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
        var projection = LegacyCombatExperienceProjection.Create(session, null, 25);
        Assert.Same(character, projection.Before); Assert.Equal(125L, character.Experience);
        Assert.Equal(150L, projection.After.Experience); Assert.Equal(10L, projection.After.Level);
        Assert.False(projection.LevelChanged); Assert.Equal(25UL, projection.AppliedGain);
        Assert.Throws<InvalidOperationException>(() => LegacyCombatExperienceProjection.Create(session, null, 0));
        Assert.Throws<OverflowException>(() => LegacyCombatExperienceProjection.Create(
            session with { Character = character with { Experience = long.MaxValue } }, null, 1));
    }

    [Fact]
    public Task OrdinaryAdmissionHydratesSavedNonzeroExperienceIntoTheActualNativePlayer()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 1000) with { Experience = 125 };
        return OwnedEquipmentBehaviorFixture.RunAsync(new(character, PlayerItemSnapshot.Empty),
            OwnedEquipmentBehaviorFixture.Catalog(), f =>
        {
            var current = f.Current(); var binding = Assert.IsType<LegacyOrdinarySpellBinding>(current.NativeGameClient!.OrdinarySpellBinding);
            Assert.Equal(125L, current.Character.Experience);
            Assert.Equal(125UL, binding.Player.Experience);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Throws<InvalidOperationException>(() => binding.Player.Experience = 126);
            Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor),
                current with { Character = current.Character with { Experience = 126 } }));
            Assert.Same(current, f.Current());
            Assert.Equal(125UL, binding.Player.Experience);
            return Task.CompletedTask;
        });
    }
}
