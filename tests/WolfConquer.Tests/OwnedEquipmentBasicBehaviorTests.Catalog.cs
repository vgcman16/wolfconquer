using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentBasicBehaviorTests
{
    private static ItemType Item(uint id, IReadOnlyDictionary<int, string>? fields = null)
    {
        var row = OwnedEquipmentBehaviorFixture.Item(id, fields: fields);
        return ItemTypeParser.ParseLine(string.Join("@@", row.Fields), expectedFieldCount: 66);
    }
    private static WorldDataCatalog Catalog(params ItemType[] items) => LegacyItemWorldDataFixture.Create(items, []);

    private static WorldDataCatalog RequirementCatalog(uint itemId)
    {
        var fields = new Dictionary<int, string>();
        switch (itemId)
        {
            case 410003:
                fields[ItemType.RequiredLevelFieldIndex] = "5"; break;
            case 410023:
                fields[ItemType.RequiredLevelFieldIndex] = "15";
                fields[ItemType.RequiredStrengthFieldIndex] = "28"; break;
            case 150320:
                fields[ItemType.RequiredLevelFieldIndex] = "20";
                fields[ItemType.RequiredGenderFieldIndex] = "2"; break;
            case 101000:
                fields[ItemType.RequiredLevelFieldIndex] = "15";
                fields[ItemType.RequiredClassFieldIndex] = "16000"; break;
            default: throw new ArgumentOutOfRangeException(nameof(itemId));
        }
        // The original positive Pirate cases assert the complete eleven-frame
        // query, including21930/level10. Supply that actual parsed weekly rank1
        // input rather than deleting the rank-spell packet assertion.
        var rankFields = Enumerable.Repeat("0", 23).ToArray();
        rankFields[0] = OwnedEquipmentBehaviorFixture.Actor.ToString(CultureInfo.InvariantCulture);
        rankFields[1] = "HeroOne"; rankFields[2] = "40"; rankFields[3] = "1000"; rankFields[^1] = "";
        var ranks = RankingSnapshotParser.ParseCombatGearRanks("Count=1\r\n" + string.Join("/", rankFields) + "\r\n",
            "owned-equipment-requirement-rank.txt");
        var rank = Assert.Single(ranks);
        Assert.Equal(23, rank.Fields.Count);
        Assert.Equal((int)OwnedEquipmentBehaviorFixture.Actor, rank.CharacterId);
        Assert.Equal("40", rank.Fields[2]); Assert.Equal("1000", rank.Fields[3]);
        return LegacyItemWorldDataFixture.Create([Item(itemId, fields)], [], combatGearRanks: ranks);
    }
}
