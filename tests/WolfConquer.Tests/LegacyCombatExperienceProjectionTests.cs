using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyCombatExperienceProjectionTests
{
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
