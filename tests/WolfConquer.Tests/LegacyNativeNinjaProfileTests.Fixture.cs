using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeNinjaProfileTests
{
    // Genuine storage and live metadata references only. No packet, socket,
    // ranking, forced RNG, row-provider callback or gameplay mutation stand-in.
    private sealed class Scope : IDisposable
    {
        private readonly Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? priorTypes = LegacyNativeNinjaFile.gouyu_type;
        private readonly Dictionary<uint, LegacyNativeNinjaFile.Item>? priorImmortals = LegacyNativeNinjaFile.gouyu_immortals;
        private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo priorUiCulture = CultureInfo.CurrentUICulture;
        internal LegacyNativeGameClientRuntime Client { get; } = new();
        internal LegacyNativeNinja Ninja { get; }
        internal Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem> Metadata => LegacyNativeNinjaFile.gouyu_type;

        internal Scope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            LegacyNativeNinjaFile.gouyu_type = new();
            Client.Equipment = new LegacyNativeEquipItemStorage(Client);
            Ninja = new LegacyNativeNinja(Client);
            Client.MyNinja = Ninja;
        }

        internal LegacyNativeNinja.Item Item(uint key, uint id, uint level = 1,
            uint position = 0, uint experience = 0, uint uk = 0)
        {
            var item = new LegacyNativeNinja.Item
            {
                ItemID = id, Level = level, Position = position, Exp = experience, UK = uk,
            };
            Assert.True(Ninja.Items.TryAdd(key, item));
            return item;
        }

        internal LegacyNativeNinjaFile.NinjaSprintItem Meta(uint key, uint id, uint level, uint score)
        {
            var row = new LegacyNativeNinjaFile.NinjaSprintItem
            {
                ID = key, ItemID = id, Level = level, Score = score,
            };
            Metadata.Add(key, row);
            return row;
        }

        public void Dispose()
        {
            LegacyNativeNinjaFile.gouyu_type = priorTypes!;
            LegacyNativeNinjaFile.gouyu_immortals = priorImmortals!;
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUiCulture;
        }
    }

    private static uint Component(LegacyNativeNinja ninja, uint band) => band switch
    {
        100 => ninja.Fire_Score,
        200 => ninja.Water_Score,
        300 => ninja.Earth_Score,
        400 => ninja.Wind_Score,
        500 => ninja.Lightning_Score,
        600 => ninja.a_Score,
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };

    private static uint[] Components(LegacyNativeNinja ninja) =>
        [ninja.Fire_Score, ninja.Water_Score, ninja.Earth_Score, ninja.Wind_Score, ninja.Lightning_Score, ninja.a_Score];

    private static void SetAllElements(LegacyNativeNinja ninja, uint element, uint mastery)
    {
        ninja.Fire = ninja.Water = ninja.Earth = ninja.Wind = ninja.Lightning = element;
        ninja.FireMastery = ninja.WaterMastery = ninja.EarthMastery = ninja.WindMastery = ninja.LightningMastery = mastery;
    }
}
