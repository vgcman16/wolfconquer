#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCombatGear
{
    // Native948..966: no wire-load precheck; bag mutation precedes the implicit wire gate.
    public byte[] AddBlackJade(uint Id, uint exp)
    {
        if (LegacyNativeDaoqiDictTypeTable.Info.TryGetValue(Id, out var item) &&
            BlackJadeBag.Values.FirstOrDefault((BlackJade p) => p.DBItem.TypeRune == item.TypeRune) == null)
        {
            BlackJade value = new BlackJade(Id, exp);
            BlackJadeBag.TryAdd(Id, value);
            return new LegacyNativeMsgDaoQi
            {
                Type = LegacyNativeMsgDaoQi.Types.ADDOBJ,
                Items = new List<LegacyNativeMsgDaoQi.Item>
                {
                    new LegacyNativeMsgDaoQi.Item
                    {
                        Id = Id
                    }
                }
            };
        }
        return null;
    }
}
