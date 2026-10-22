#nullable disable

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeCombatGearWire
{
    // CQ_COMBAT_GEAR52..68: EVERY scalar is required, including zero.
    private static void Serialize(Stream output, LegacyNativeMsgCombatGear message)
    {
        if (message is null)
            return;
        Field(output, 1, unchecked((ulong)(long)(int)message.Type));
        Field(output, 2, message.UID);
        if (message.Items is null)
            return;
        foreach (LegacyNativeMsgCombatGear.Item item in message.Items)
        {
            if (item is null)
                continue;
            using MemoryStream nested = new MemoryStream();
            Field(nested, 1, unchecked((ulong)(long)(int)(uint)item.ID));
            Field(nested, 2, item.Level);
            Field(nested, 3, item.Exp);
            Field(nested, 4, item.Equip);
            Field(nested, 5, item.Transform);
            Field(nested, 6, item.Data1);
            Field(nested, 7, item.Data2);
            Field(nested, 8, item.Data3);
            Field(nested, 9, item.Data4);
            Field(nested, 10, item.Data5);
            Field(nested, 11, item.Data6);
            Field(nested, 12, item.MasteryScore);
            Nested(output, 3, nested);
        }
    }

    // CQ_COMBAT_GEAR80..92, required scalar/enum fields and unpacked message array.
    private static void Serialize(Stream output, LegacyNativeMsgCombatGearSkin message)
    {
        if (message is null)
            return;
        Field(output, 1, message.Type);
        if (message.Weapon is null)
            return;
        foreach (LegacyNativeMsgCombatGearSkin.WeaponInfo item in message.Weapon)
        {
            if (item is null)
                continue;
            using MemoryStream nested = new MemoryStream();
            Field(nested, 1, item.ID);
            Field(nested, 2, unchecked((ulong)(long)(int)item.Type));
            Field(nested, 3, item.Data1);
            Field(nested, 4, item.Data2);
            Field(nested, 5, item.Data3);
            Field(nested, 6, item.Data4);
            Field(nested, 7, item.Data5);
            Field(nested, 8, item.Data6);
            Field(nested, 9, item.Data7);
            Nested(output, 2, nested);
        }
    }
}
