using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    internal sealed partial class CreationFixture
    {
        internal static WorldDataCatalog BuildCatalog()
        {
            long[] classes = [1000, 2000, 4000, 5000, 6000, 7000, 8000, 9000, 10000, 16000];
            var items = new Dictionary<uint, ItemType>();
            foreach (long code in classes)
            {
                Assert.True(LegacyFirstCharacterStarterProfiles.TryGet(code, out var profile));
                foreach (uint id in new[] { profile.ArmorItemId, profile.RightWeaponItemId, profile.LeftWeaponItemId, profile.RingItemId }.Where(id => id != 0))
                    items[id] = OwnedEquipmentBehaviorFixture.Item(id, "OwnedCreationStarter" + id,
                        new Dictionary<int, string> { [ItemType.DurabilityFieldIndex] = "321", [ItemType.ItemHPFieldIndex] = "7" });
            }
            foreach (uint id in new[] { RuneType, RuneType + 1 })
                items[id] = OwnedEquipmentBehaviorFixture.Item(id, "OwnedExistingBlueRune" + id,
                    new Dictionary<int, string> { [ItemType.RequiredClassFieldIndex] = "6000", [ItemType.RuneSpellIdFieldIndex] =
                        (1_416_000 + id % 100 - 1).ToString(CultureInfo.InvariantCulture), [ItemType.StackSizeFieldIndex] = "0" });
            foreach (uint id in new[] { 4_040_001U, 3_306_368U })
                items[id] = OwnedEquipmentBehaviorFixture.Item(id, "OwnedDecomposeReward" + id,
                    new Dictionary<int, string> { [ItemType.RequiredClassFieldIndex] = "0", [ItemType.DurabilityFieldIndex] = "1",
                        [ItemType.TimeFieldIndex] = "0", [ItemType.StackSizeFieldIndex] = "10000" });
            var professions = classes.Select((code, index) => ProfessionParser.ParseLevelBenefitRow("owned-creation-profession", index + 1,
                (index + 1).ToString(CultureInfo.InvariantCulture) + "@@" + code.ToString(CultureInfo.InvariantCulture) + "@@0@@60017@@0@@0@@0@@0@@")).ToArray();
            RuneStorageAttribute[] storage = [RuneParser.ParseStorageAttributeRow("owned-creation-rune-storage", 1, "1@@0@@1@@23@@0@@0@@0@@0@@")];
            var progress = new[] { "17@@402@@17@@94000@@", "18@@402@@18@@109000@@" }.Select((row, index) =>
                RuneParser.ParseLevelExperienceRow("owned-creation-rune-progress", index + 1, row)).ToArray();
            return NativeCallerWorldDataFixture.Create(items.Values.ToArray(), [], professionLevelBenefits: professions,
                runeStorageAttributes: storage, runeLevelExperience: progress,
                magicEffects: MagicEffectParser.Parse("[1416000]\r\nIsRuneSkill=1\r\n", "owned-creation-MagicEffect.ini"));
        }
    }
}
