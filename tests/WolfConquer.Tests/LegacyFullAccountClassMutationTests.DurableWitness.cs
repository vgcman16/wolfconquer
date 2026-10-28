using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyFullAccountClassMutationTests
{
    private sealed class DurableClassImage
    {
        private readonly WorldSession original;
        private readonly Dictionary<PropertyInfo, object?> sessionScalars, characterScalars;
        private readonly KeyValuePair<string, string>[] attributes, nobility;
        private readonly PlayerSpellSnapshotEntry[] spells;
        private readonly PlayerProficiencySnapshotEntry[] proficiencies;
        private readonly PlayerItemSnapshotItem[] items;
        private readonly Dictionary<PropertyInfo, object?>[] itemScalars;

        internal DurableClassImage(WorldSession session)
        {
            original = session;
            sessionScalars = Scalars(session); characterScalars = Scalars(session.Character);
            attributes = session.Character.Attributes.ToArray(); nobility = session.Character.PaidNobility.ToArray();
            spells = session.PlayerSpells.Spells.ToArray();
            proficiencies = session.PlayerProficiencies.Proficiencies.ToArray();
            items = session.PlayerItems.InventoryItems.ToArray();
            itemScalars = items.Select(Scalars).ToArray();
        }

        internal void RequireUnchanged()
        {
            RequireScalars(sessionScalars, original);
            RequireScalars(characterScalars, original.Character);
            Assert.Equal(attributes, original.Character.Attributes.ToArray());
            Assert.Equal(nobility, original.Character.PaidNobility.ToArray());
            Assert.Equal(spells, original.PlayerSpells.Spells.ToArray());
            Assert.Equal(proficiencies, original.PlayerProficiencies.Proficiencies.ToArray());
            Assert.Equal(items, original.PlayerItems.InventoryItems.ToArray());
            for (var index = 0; index < items.Length; index++) RequireScalars(itemScalars[index], items[index]);
        }

        internal void RequireUnrelatedResources(WorldSession current)
        {
            Assert.Same(original.PlayerItems, current.PlayerItems);
            Assert.Same(original.PlayerProficiencies, current.PlayerProficiencies);
            Assert.Equal(original.Character.Money, current.Character.Money);
            Assert.Equal(original.Character.ConquerPoints, current.Character.ConquerPoints);
            Assert.Equal(original.Character.BoundConquerPoints, current.Character.BoundConquerPoints);
            Assert.Equal(original.Character.BankConquerPoints, current.Character.BankConquerPoints);
            Assert.Equal(original.Character.PkPoints, current.Character.PkPoints);
            Assert.Equal(original.Character.PaidNobility.ToArray(), current.Character.PaidNobility.ToArray());
            Assert.Equal(items, current.PlayerItems.InventoryItems.ToArray());
            for (var index = 0; index < items.Length; index++) RequireScalars(itemScalars[index], items[index]);
            Assert.Equal(proficiencies, current.PlayerProficiencies.Proficiencies.ToArray());
            Assert.Equal("keep-me", current.Character.Attributes["Sentinel"]);
        }

        private static Dictionary<PropertyInfo, object?> Scalars(object value) => value.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType.IsValueType || property.PropertyType == typeof(string))
            .ToDictionary(property => property, property => property.GetValue(value));

        private static void RequireScalars(Dictionary<PropertyInfo, object?> expected, object current)
        { foreach (var entry in expected) Assert.Equal(entry.Value, entry.Key.GetValue(current)); }
    }
}
