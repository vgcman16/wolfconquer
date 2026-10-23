namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    // Complete native Rune37..119, preserving first matching slot and fallback levels.
    public uint PerfectionRedRune
    {
        get
        {
            LegacyNativeGameItemRuntime? msgGameItem = WornObjects.Where((LegacyNativeGameItemRuntime i) => i.Position == 101 || i.Position == 121).FirstOrDefault();
            if (msgGameItem == null)
            {
                return 0u;
            }
            return 3000u;
        }
    }

    public uint PerfectionBlueRune
    {
        get
        {
            LegacyNativeGameItemRuntime? msgGameItem = WornObjects.Where((LegacyNativeGameItemRuntime i) => i.Position == 102 || i.Position == 122).FirstOrDefault();
            if (msgGameItem == null)
            {
                return 0u;
            }
            return (msgGameItem.ITEM_ID % 100) switch
            {
                2u => 1300u,
                3u => 1500u,
                4u => 1800u,
                5u => 2300u,
                6u => 2800u,
                7u => 3400u,
                8u => 4000u,
                9u => 4600u,
                10u => 5300u,
                11u => 6000u,
                12u => 6900u,
                13u => 7900u,
                14u => 8900u,
                15u => 9900u,
                16u => 11000u,
                17u => 13000u,
                18u => 14000u,
                19u => 15500u,
                20u => 17000u,
                21u => 18600u,
                22u => 20500u,
                23u => 22000u,
                24u => 24500u,
                25u => 26500u,
                26u => 29000u,
                27u => 31000u,
                _ => 1100u,
            };
        }
    }

    public uint PerfectionYellowRune
    {
        get
        {
            uint num = 0u;
            byte x;
            for (x = 0; x < 12; x++)
            {
                LegacyNativeGameItemRuntime? msgGameItem = WornObjects.Where((LegacyNativeGameItemRuntime i) => i.Position == 103 + x || i.Position == 123 + x).FirstOrDefault();
                if (msgGameItem != null)
                {
                    num = (msgGameItem.ITEM_ID % 100) switch
                    {
                        2u => num + 1600,
                        3u => num + 2200,
                        4u => num + 3000,
                        5u => num + 4000,
                        6u => num + 5300,
                        7u => num + 6900,
                        8u => num + 8800,
                        9u => num + 11000,
                        _ => num + 1200,
                    };
                }
            }
            return num;
        }
    }
}
