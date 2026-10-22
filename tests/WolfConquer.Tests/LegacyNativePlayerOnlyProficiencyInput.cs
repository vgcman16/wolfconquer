using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static WorldDataCatalog CreateNativePlayerOnlyProficiencyInputCatalog(
        Monster family, params MagicType[] rows)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = "111099"; fields[1] = "NativePlayerOnlyProficiencyHelmet";
        fields[18] = "4700"; fields[29] = "200";
        return CreateMagicCatalogCore(family, [], [], rows,
            items: [new ItemType(111_099, fields[1], fields),
                CreateStatusItemType(421_003, "NativePlayerOnlyProficiencyBacksword")]);
    }
}
