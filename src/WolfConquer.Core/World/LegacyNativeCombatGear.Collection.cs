#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCombatGear
{
    public List<LegacyNativeDictCollectionTable.CQ> GetNutBook()
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return null;
        }
        List<LegacyNativeDictCollectionTable.CQ> list = new List<LegacyNativeDictCollectionTable.CQ>();
        for (int i = 700; i < 704; i++)
        {
            int num = 0;
            switch (i)
            {
            case 701:
            case 702:
            case 703:
            {
                if (Items.TryGetValue((TypeID)i, out var value))
                {
                    num += (int)(value.DBItem.Score + value.MasteryScore);
                }
                foreach (BlackJade value2 in BlackJadeBag.Values)
                {
                    if (value2.DBItem.TypeCombatGear == i)
                    {
                        num += (int)value2.DBItem.score;
                    }
                }
                break;
            }
            case 700:
                foreach (BlackJade value3 in BlackJadeBag.Values)
                {
                    if (value3.DBItem.TypeCombatGear == 0 && value3.DBItem.TypeRune % 100 == 7)
                    {
                        num += (int)value3.DBItem.score;
                    }
                }
                break;
            }
            foreach (LegacyNativeDictCollectionTable.CQ value4 in LegacyNativeDictCollectionTable.Info.Values)
            {
                if (value4.Type == i && num >= value4.Min && num <= value4.Mix)
                {
                    list.Add(value4);
                }
            }
        }
        return list;
    }
}
