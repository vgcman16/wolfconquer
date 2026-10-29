using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedEquipmentWeaponBehaviorTests
{
    private static PlayerItemSnapshotItem Row(uint uid, uint id, ushort position) => new()
    { Uid = uid, ItemId = id, Position = position, Durability = 1, MaximumDurability = 1, StackSize = 0 };
    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
    private static void Usage(byte[] bytes, uint uid, ulong parameter, MsgItemUsageAction action)
    {
        Assert.True(MsgItemUsagePacket.TryParse(Frame(bytes), out var packet));
        Assert.Equal(new MsgItemUsagePacket(uid, parameter, action, 0, 0, 0, 0), packet);
    }
    private static void ItemFrame(byte[] bytes, uint uid, uint itemId, byte position, ushort mode)
    {
        Assert.True(LegacyGameItemPacket.TryParse(Frame(bytes), out var item));
        Assert.Equal(uid, item.Uid); Assert.Equal(itemId, item.ItemId);
        Assert.Equal(position, item.Position); Assert.Equal(mode, item.Mode);
    }
    private static void AssertItemClassifications(uint item, uint right, uint left)
    {
        foreach (var id in new[] { item, right, left }.Where(id => id != 0))
        {
            if (id is 410003 or 500109 or 511000 or 613000) Assert.Equal((ushort)4, ItemType.GetLegacyEquipmentPosition(id));
            if (id is 900000 or 619000 or 1050000) Assert.Equal((ushort)5, ItemType.GetLegacyEquipmentPosition(id));
            if (id == 613000) Assert.True(ItemType.IsLegacyKnife(id));
            if (id == 490003) Assert.True(ItemType.IsLegacyDagger(id));
            if (id == 900000) { Assert.True(ItemType.IsLegacyShield(id)); Assert.False(ItemType.IsLegacyArrow(id)); }
            if (id == 619000) { Assert.True(ItemType.IsLegacyHossu(id)); Assert.False(ItemType.IsLegacyShield(id)); Assert.False(ItemType.IsLegacyArrow(id)); Assert.False(ItemType.IsLegacyKnife(id)); Assert.False(ItemType.IsLegacyDagger(id)); }
            if (id == 612000) Assert.True(ItemType.IsLegacyPistol(id));
            if (id == 611000) Assert.True(ItemType.IsLegacyRapier(id));
            if (id == 410003) { Assert.False(ItemType.IsLegacyKnife(id)); Assert.False(ItemType.IsLegacyDagger(id)); Assert.False(ItemType.IsLegacyRapier(id)); Assert.False(ItemType.IsLegacyTwoHandWeapon(id)); Assert.False(ItemType.IsLegacyBow(id)); }
            if (id == 421003) { Assert.True(ItemType.IsLegacyBacksword(id)); Assert.True(ItemType.IsLegacyTwoHandWeapon(id)); }
            if (id == 500109) { Assert.True(ItemType.IsLegacyTwoHandWeapon(id)); Assert.True(ItemType.IsLegacyBow(id)); }
            if (id == 1050000) Assert.True(ItemType.IsLegacyArrow(id));
        }
    }
    private static MsgItemUsagePacket Equip(ushort slot) => new(2001, slot, MsgItemUsageAction.Equip, 1234, 0, 0, 0);
}
