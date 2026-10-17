using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyFullAccountItemMutationFixture;

namespace WolfConquer.Tests;

internal static class LegacyMaxRandomRelicTestData
{
    public static IReadOnlyList<uint> ItemIds { get; } =
        [4_100_001, 4_100_002, 4_100_003, 4_100_004, 4_100_005];

    public static IReadOnlyList<uint> ExpectedEncodedAttributes { get; } =
        [200_103, 100_104, 300_105, 400_109, 500_110];

    public static WorldDataCatalog CreateWorldData(bool includeRandomAttributes = true)
    {
        return LegacyItemWorldDataFixture.Create(
            ItemIds
                .Select(itemId => Fixture.ItemType(itemId, "MaxRelic" + itemId, durability: 0))
                .ToArray(),
            includeRandomAttributes ? CreateRandomAttributes() : []);
    }

    public static LegacyBossSpecialDeathDropCatalog CreateCatalog(
        bool includeRandomAttributes = true,
        bool epic = true)
    {
        var grouped = (includeRandomAttributes ? CreateRandomAttributes(epic) : [])
            .GroupBy(attribute => attribute.ItemTypeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<XuanbaoRandomAttribute>)group.ToArray());
        return new LegacyBossSpecialDeathDropCatalog(
            new Dictionary<ushort, IReadOnlyList<uint>>(),
            [],
            [],
            grouped);
    }

    private static IReadOnlyList<XuanbaoRandomAttribute> CreateRandomAttributes(bool epic = true)
    {
        var specs = new (LegacyChiAttributeType Type, int Minimum, int Maximum)[]
        {
            (LegacyChiAttributeType.Immunity, 100, epic ? 201 : 200),
            (LegacyChiAttributeType.Breakthrough, 30, epic ? 101 : 100),
            (LegacyChiAttributeType.Counteraction, 100, epic ? 301 : 300),
            (LegacyChiAttributeType.AddMagicDefense, 100, epic ? 401 : 400),
            (LegacyChiAttributeType.PhysicalDamageIncrease, 100, epic ? 501 : 500),
        };
        return ItemIds
            .SelectMany(itemId => specs.Select(spec => CreateAttribute(
                itemId,
                spec.Type,
                spec.Minimum,
                spec.Maximum)))
            .ToArray();
    }

    private static XuanbaoRandomAttribute CreateAttribute(
        uint itemId,
        LegacyChiAttributeType type,
        int minimum,
        int maximum)
    {
        var raw = FormattableString.Invariant(
            $"{itemId}@@{(byte)type}@@{minimum}@@{maximum}@@");
        return new XuanbaoRandomAttribute(
            checked((int)itemId),
            (byte)type,
            minimum,
            maximum,
            "fixture://xuanbao_rand_attr.txt",
            checked((int)itemId),
            raw,
            raw.Split("@@"));
    }
}
