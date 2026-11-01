using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeAttributePropertyCompatibilityTests
{
    [Fact]
    public void UnboundSourceNamedUInt16PropertiesRetainDefaultAndWidth()
    {
        var player = new LegacyNativeGameClientRuntime().Player;
        foreach (var name in new[] { "Strength", "Agility", "Vitality", "Spirit", "Atributes" })
        {
            var property = typeof(LegacyNativePlayerRuntime).GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.NotNull(property); Assert.Equal(typeof(ushort), property.PropertyType);
            Assert.True(property.GetMethod!.IsPublic && property.SetMethod!.IsPublic);
            Assert.Null(typeof(LegacyNativePlayerRuntime).GetField(name, BindingFlags.Public | BindingFlags.Instance));
            Assert.Equal((ushort)0, property.GetValue(player));
            property.SetValue(player, ushort.MaxValue);
            Assert.Equal(ushort.MaxValue, property.GetValue(player));
            property.SetValue(player, (ushort)0);
            Assert.Equal((ushort)0, property.GetValue(player));
        }
    }

    [Fact]
    public Task AdmittedNativeOwnerRejectsDirectAttributeSettersWithoutAnyPublishedEffects()
    {
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 4, strength: 3,
            minHitPoints: 200) with { Vitality = 5, Spirit = 6, AttributePoints = 7 };
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor, PlayerItemSnapshot.Empty),
            OwnedEquipmentBehaviorFixture.Catalog(), owner =>
        {
            var before = owner.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            var player = binding.Player;
            Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(((ushort)3, (ushort)4, (ushort)5, (ushort)6, (ushort)7),
                (player.Strength, player.Agility, player.Vitality, player.Spirit, player.Atributes));
            var files = owner.Data.Files(); int frames = owner.ActorFrames.Count;
            Assert.Throws<InvalidOperationException>(() => player.Strength = 8);
            Assert.Throws<InvalidOperationException>(() => player.Agility = 8);
            Assert.Throws<InvalidOperationException>(() => player.Vitality = 8);
            Assert.Throws<InvalidOperationException>(() => player.Spirit = 8);
            Assert.Throws<InvalidOperationException>(() => player.Atributes = 8);
            Assert.Equal(((ushort)3, (ushort)4, (ushort)5, (ushort)6, (ushort)7),
                (player.Strength, player.Agility, player.Vitality, player.Spirit, player.Atributes));
            Assert.Same(before, owner.Current()); Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(frames, owner.ActorFrames.Count); owner.Data.AssertFiles(files);
            return Task.CompletedTask;
        });
    }
}
