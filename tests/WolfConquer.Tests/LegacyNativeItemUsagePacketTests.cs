using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;
using ActionID = WolfConquer.Core.World.LegacyNativeItemUsagePacket.ItemUsuageID;

namespace WolfConquer.Tests;

// Only initialized owned allocations <=512 bytes; no unbacked or malformed unsafe reads.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed unsafe partial class LegacyNativeItemUsagePacketTests
{
    [Fact]
    public void CompleteNativeEnumRetainsItsUIntWidthNamesValuesAndDuplicateAlias()
    {
        const string literal = "CreateSocketItem=43;BuyItemFromForging=55;GemCompose=39;ToristSuper=51;AddBless=40;GarmentShop=53;BuyItem=1;SellItem=2;RemoveInventory=3;Equip=4;SetEquipPosition=5;Unequip=6;UpgradeEnchant=7;ShowWarehouseMoney=9;DepositWarehouse=10;WarehouseWithdraw=11;RepairItem=14;UpdateDurability=17;RemoveEquipment=18;UpgradeDragonball=19;UpgradeMeteor=20;ShowVendingList=21;AddVendingItemGold=22;RemoveVendingItem=23;BuyVendingItem=24;UpdateArrowCount=25;ParticleEffect=26;Ping=27;UpdateEnchant=28;AddVendingItemConquerPts=29;UpdatePurity=35;DropItem=37;DropGold=38;RedeemGear=32;ClaimGear=33;UnAlternante=44;ActiveItems=41;Alternante=45;SocketTalismanWithItem=35;SocketTalismanWithCPs=36;MergeStackableItems=48;SplitStack=49;ReturnedItems=50;ShowItem=52;DegradeEquipment=54;OpenInventorySash=56;EquipRune=57;UnequipRune=58;Rune=61;DropRune=62;ComposeRune=63;ChanceRune=64;DecomposeRune=65;RedRuneProgressControl=66;DecomposeRelics=67;HossuSocket=68;EmbedAnima=69;RemoveAnima=70;MythForggingUseSilver=74;MythForggingExtract=75;MythForggingCombine=76;MythForggingEnhance=77;MythForggingMythsoulBagAdd=78;MythForggingMythsoulBagRemove=79;MythForggingInstant=80";
        var expected = literal.Split(';').Select(entry => entry.Split('='))
            .ToDictionary(parts => parts[0], parts => uint.Parse(parts[1]), StringComparer.Ordinal);
        var actual = typeof(ActionID).GetFields(BindingFlags.Public | BindingFlags.Static)
            .ToDictionary(field => field.Name, field => (uint)field.GetRawConstantValue()!, StringComparer.Ordinal);
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(ActionID)));
        Assert.Null(typeof(ActionID).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
        Assert.Equal(ActionID.UpdatePurity, ActionID.SocketTalismanWithItem);
    }

    [Fact]
    public Task NullStreamsFailNaturallyWithoutArgumentRepair() => NativePacketTestThread.Run(() =>
    {
        Assert.Throws<NullReferenceException>(() =>
        {
            ((Packet)null!).GetUsageItem(out _, out _, out _, out _, out _, out _, out _, out _, out _);
        });
        Assert.Throws<NullReferenceException>(() =>
        {
            ((Packet)null!).ItemUsageCreate(ActionID.BuyItem, 0, 0, 1, 0, 0, 0);
        });
    });

    private static byte[] Copy(Packet packet, int count)
    {
        byte[] result = new byte[count];
        Marshal.Copy((IntPtr)packet.Memory, result, 0, count);
        return result;
    }

    private static byte[] ReaderInput(int start, ushort rawAction, uint count)
    {
        byte[] result = Enumerable.Repeat((byte)0xD6, 512).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(start), 0x11223344);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(start + 4), 0x0123456789ABCDEFul);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(start + 12), rawAction);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(start + 14), 0x55667788);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(start + 18), count);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(start + 22), 0xA1A2A3A4);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(start + 26), 0xB1B2B3B4);
        result[start + 44] = 0x5A;
        int backedCount = count is > 0 and < 50 ? (int)count : 0;
        for (int i = 0; i < backedCount; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(start + 87 + i * 4), 0xABC00000u + (uint)i);
        return result;
    }

    private sealed class SealScope : IDisposable
    {
        private static readonly FieldInfo Field = typeof(Packet).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? old = Field.GetValue(null);
        internal SealScope(string? value) => Field.SetValue(null, value);
        public void Dispose() => Field.SetValue(null, old);
    }
}
