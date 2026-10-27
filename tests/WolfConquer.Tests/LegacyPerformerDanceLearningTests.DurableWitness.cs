using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPerformerDanceLearningTests
{
    private static WorldSession DurableSession(long uid = CharacterUid)
    {
        var session = CreateSession(9, CreateSpells(CreateExistingSpell(), new(901, 6, 2345, 2, 3, 2)));
        var attributes = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["DurableLearningSentinel"] = "retained" };
        return session with
        {
            Character = session.Character with { Uid = uid, Money = 123456, ConquerPoints = 654,
                BoundConquerPoints = 321, BankConquerPoints = 999, Experience = 4321, MinHitPoints = 800,
                MinMana = 90, Attributes = attributes },
            PlayerProficiencies = new([new(410, 5, 3, 765, 1600000)]),
        };
    }

    private sealed class DurableSessionImage
    {
        private readonly WorldSession original;
        private readonly Dictionary<PropertyInfo, object?> sessionScalars, characterScalars;
        private readonly KeyValuePair<string, string>[] attributes, nobility;
        private readonly PlayerSpellSnapshotEntry[] spells;
        private readonly PlayerProficiencySnapshotEntry[] profs;
        private readonly PlayerItemSnapshotItem[] inventory;
        private readonly Dictionary<PropertyInfo, object?>[] itemScalars;

        internal DurableSessionImage(WorldSession session)
        {
            original = session; sessionScalars = Scalars(session); characterScalars = Scalars(session.Character);
            attributes = session.Character.Attributes.ToArray(); nobility = session.Character.PaidNobility.ToArray();
            spells = session.PlayerSpells.Spells.ToArray(); profs = session.PlayerProficiencies.Proficiencies.ToArray();
            inventory = session.PlayerItems.InventoryItems.ToArray(); itemScalars = inventory.Select(Scalars).ToArray();
        }

        internal void RequireResources(WorldSession actual)
        {
            Assert.Same(original.Character, actual.Character);
            Assert.Same(original.PlayerItems, actual.PlayerItems);
            Assert.Same(original.PlayerProficiencies, actual.PlayerProficiencies);
            RequireScalars(sessionScalars, actual); RequireScalars(characterScalars, actual.Character);
            Assert.Equal(attributes, actual.Character.Attributes.ToArray()); Assert.Equal(nobility, actual.Character.PaidNobility.ToArray());
            Assert.Equal(profs, actual.PlayerProficiencies.Proficiencies);
            Assert.Equal(inventory, actual.PlayerItems.InventoryItems);
            for (int index = 0; index < inventory.Length; index++) RequireScalars(itemScalars[index], inventory[index]);
        }

        internal void RequireUnchanged()
        {
            RequireResources(original);
            Assert.Equal(spells, original.PlayerSpells.Spells);
        }

        private static Dictionary<PropertyInfo, object?> Scalars(object value) => value.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType.IsValueType || property.PropertyType == typeof(string))
            .ToDictionary(property => property, property => property.GetValue(value));

        private static void RequireScalars(Dictionary<PropertyInfo, object?> expected, object actual)
        { foreach (var field in expected) Assert.Equal(field.Value, field.Key.GetValue(actual)); }
    }
}
