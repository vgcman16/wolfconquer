using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    // Native MyCombatGear154..223. Resolve the complete source list before any
    // native spell writer or notification, including later Jade/table failures.
    public void IsPirate(byte x)
    {
        var calls = PreparePirateSpellCalls(x);
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        foreach (var call in calls)
        {
            if (x == 1) user.MySpells!.Add(stream, call.Id, call.Level, 0, 0);
            else user.MySpells!.Remove(call.Id, stream);
        }
    }

    private readonly record struct PirateSpellCall(ushort Id, ushort Level);

    private PirateSpellCall[] PreparePirateSpellCalls(byte mode)
    {
        var calls = new List<PirateSpellCall>();
        foreach (Weapon value in user.CombatGear!.Items.Values)
        {
            foreach (BlackJade jade in value.BlackJades)
            {
                var row = jade.DBItem;
                if (row.attr_type1 == 24) AddEncoded(row.attr_value1);
                if (row.attr_type2 == 24) AddEncoded(row.attr_value2);
            }
            if (value.ItemID >= TypeID.Pirate1 && value.ItemID <= TypeID.Pirate3)
                foreach (uint encoded in value.DBItem.Skills)
                    if (encoded != 0) AddEncoded(encoded);
            calls.Add(new(21540, 0));
            calls.Add(new(21920, 0));
        }
        return calls.ToArray();

        void AddEncoded(uint encoded)
        {
            ushort id = (ushort)(encoded / 1000);
            if (id == 0)
                throw new InvalidOperationException("Pirate spell metadata contains an invalid zero spell identity.");
            // Preserve the source's width and level extraction for valid rows.
            ushort level = mode == 1 ? (ushort)(encoded % 100) : (ushort)0;
            calls.Add(new(id, level));
        }
    }
}
