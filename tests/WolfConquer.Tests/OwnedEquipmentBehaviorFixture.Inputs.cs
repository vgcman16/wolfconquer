using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

// Saved input data only; neither this record nor its factories grant authority.
internal sealed record OwnedEquipmentBehaviorInput(CharacterSnapshot Character, PlayerItemSnapshot Items,
    PlayerSpellSnapshot? Spells = null, PlayerProficiencySnapshot? Proficiencies = null,
    CombatGearSnapshot? CombatGear = null);

internal sealed partial class OwnedEquipmentBehaviorFixture
{
    internal const uint Actor = 1_293_600, Peer = 1_293_601;

    // The old Frame fixtures' explicit neutral scalar defaults. Tests may change
    // typed fields/attributes before saving; real loaders/admission read the file.
    internal static CharacterSnapshot Character(uint uid = Actor, string name = "HeroOne", long classCode = 7051,
        long level = 140, long reborn = 2, long strength = 0, long agility = 36, long body = 1008,
        long mapId = 1015, long x = 830, long y = 584, long minHitPoints = 61_258) => new(
            Uid: uid, Name: name, Body: body, Face: 154, Hair: 27,
            Class: classCode, FirstClass: 0, SecondClass: 7050, Reborn: reborn, Level: level,
            Experience: 0, MapId: mapId, X: x, Y: y, PreviousMapId: 1000, PreviousMapX: 500, PreviousMapY: 650,
            Strength: strength, Agility: agility, Vitality: 447, Spirit: 0, AttributePoints: 12,
            MinHitPoints: minHitPoints, MinMana: 5000, Money: 98_602_439_956, ConquerPoints: 639_778_904,
            BoundConquerPoints: 456, BankConquerPoints: 123, PkPoints: 0, Online: 0,
            SourceFile: uid.ToString(CultureInfo.InvariantCulture) + ".ini",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>());

    internal static ItemType Item(uint id, string? name = null, IReadOnlyDictionary<int, string>? fields = null)
    {
        var values = Enumerable.Repeat("0", 66).ToArray();
        values[0] = id.ToString(CultureInfo.InvariantCulture);
        values[1] = name ?? "OwnedEquipment" + values[0];
        values[ItemType.DurabilityFieldIndex] = "100";
        values[ItemType.StackSizeFieldIndex] = "1";
        if (fields is not null)
            foreach (var (index, value) in fields)
            {
                if (index < 2 || index >= values.Length || value is null)
                    throw new ArgumentException("Explicit owned item metadata index/value is invalid.", nameof(fields));
                values[index] = value;
            }
        return new ItemType(checked((int)id), values[1], values);
    }

    internal static WorldDataCatalog Catalog(params ItemType[] items) => LegacyItemWorldDataFixture.Create(items, []);
}
