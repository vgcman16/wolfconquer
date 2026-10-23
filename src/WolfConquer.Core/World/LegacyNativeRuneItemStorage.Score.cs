using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    // Complete native Rune20..35: Objects, not WornObjects, and native unchecked UInt32 math.
    public uint Score
    {
        get
        {
            uint num = 0u;
            LegacyNativeGameItemRuntime[] array = Objects;
            foreach (LegacyNativeGameItemRuntime msgGameItem in array)
            {
                if (msgGameItem.Position != 0 && msgGameItem.Position != 211)
                {
                    num += 500 + ((msgGameItem.ITEM_ID / 10000 != 401) ? (300 * (msgGameItem.ITEM_ID % 100)) : 0);
                }
            }
            return num;
        }
    }

    // Complete native Rune213..232. Every eligible row is additive, not just the final tier.
    public uint HPAdd
    {
        get
        {
            uint num = 0u;
            IEnumerable<LegacyNativeRuneTable.Attribute> enumerable = LegacyNativeRuneTable.Attributes.Values.Where((LegacyNativeRuneTable.Attribute i) => i.Score <= Score);
            foreach (LegacyNativeRuneTable.Attribute item in enumerable)
            {
                foreach (LegacyNativeRuneTable.AttributeValue item2 in item.AttributeValue!.Where((LegacyNativeRuneTable.AttributeValue i) => i.Type != LegacyNativeRuneTable.RuneAttribute.None))
                {
                    LegacyNativeRuneTable.RuneAttribute type = item2.Type;
                    LegacyNativeRuneTable.RuneAttribute runeAttribute = type;
                    if (runeAttribute == LegacyNativeRuneTable.RuneAttribute.HPAdd)
                    {
                        num += item2.Value;
                    }
                }
            }
            return num;
        }
    }
}
