using System.Buffers.Binary;
using WolfConquer.Core.World;
using Equip = WolfConquer.Core.World.LegacyNativeEquipItemStorage;
using Item = WolfConquer.Core.World.LegacyNativeGameItemRuntime;
using Color = WolfConquer.Core.World.LegacyNativeGameItemFlags.Color;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEquipmentQueryPrerequisiteTests
{
    private static byte[] ShowFrame(ushort uid,uint alternate,params uint[] items)
    {
        var bytes=new byte[87]; BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0,2),87);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2,2),2137); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8,4),alternate);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16,2),uid); Array.Fill(bytes,(byte)0xA5,18,13);
        for(int index=0;index<items.Length;index++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(31+index*4,4),items[index]);
        return bytes;
    }

    [Fact]
    public Task AppendItemsBuildsActualNormalAndAlternateSlotMessageAndPreservesUninitializedThirteenByteGap() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); ushort[] positions=[1,2,3,4,5,6,7,8,9,15,16,17,18,19];
        var items=positions.Select((position,index)=>new Item{Position=position,Uid=(uint)index+100}).ToArray(); s.Equipment.Alternante=true;
        s.Equipment.AppendItems(true,items,s.Native.Packet);
        Assert.Equal(ShowFrame(46,1,Enumerable.Range(100,14).Select(value=>(uint)value).ToArray()),Assert.Single(s.Native.Queue));
        Assert.Equal(4,s.Native.Packet.Position); Assert.Equal(87,s.Native.Packet.Size);
        Assert.All(items,item=>Assert.Equal(0u,item.ITEM_ID)); // UID slots, not item IDs or state adoption.
        s.Native.Queue.Clear(); Fill(s.Native.Packet); s.Player.SpecialGarment=1234;
        var alternateHead=new Item{Uid=777,Position=51}; var sameSlotLater=new Item{Uid=888,Position=1};
        s.Equipment.AppendItems(true,new Item[]{alternateHead,null!,sameSlotLater},s.Native.Packet);
        var expectedItems=new uint[14]; expectedItems[0]=888; expectedItems[8]=uint.MaxValue-1;
        Assert.Equal(ShowFrame(46,1,expectedItems),Assert.Single(s.Native.Queue)); Assert.Equal(0u,s.Player.HeadId);
    });

    [Fact]
    public Task AppendFalseGateSkipsMissingOwnersAndInputsWhileTrueNullArrayKeepsWriterUntouched() => NativePacketTestThread.Run(()=>
    {
        var ownerless=new Equip(null); ownerless.AppendItems(false,null!,null!);
        using var s=new Scope(); Assert.Throws<NullReferenceException>(()=>s.Equipment.AppendItems(true,null!,s.Native.Packet));
        Assert.Equal(237,s.Native.Packet.Position); Assert.Equal(321,s.Native.Packet.Size); Assert.Empty(s.Native.Queue);
        s.Equipment.SendAlowAlternante(s.Native.Packet);
        Assert.Equal(ShowFrame(44,0),Assert.Single(s.Native.Queue));
    });

    [Fact]
    public Task ShowEquipmentCodecIgnoresStampPreservesAllFourteenFieldsAndGapAndNarrowsNoUintFields() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var row=new LegacyNativeMsgShowEquipment
        {Stamp=int.MinValue,wParam=uint.MaxValue,Alternante=0xAABBCCDD,UID=ushort.MaxValue,Head=1,Necklace=2,Armor=3,RightWeapon=4,LeftWeapon=5,Ring=6,Bottle=7,Boots=8,Garment=9,RightWeaponAccessory=10,LeftWeaponAccessory=11,SteedMount=12,RidingCrop=13,Wing=14};
        s.Native.Packet.ShowEquipmentCreate(row); var expected=ShowFrame(ushort.MaxValue,0xAABBCCDD,Enumerable.Range(1,14).Select(value=>(uint)value).ToArray());
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4,4),uint.MaxValue);
        Assert.Equal(expected,NativeSpellTestFixture.Bytes(s.Native.Packet)); Assert.Empty(s.Native.Queue);
        Fill(s.Native.Packet); Assert.Throws<NullReferenceException>(()=>s.Native.Packet.ShowEquipmentCreate(null!));
        Assert.Equal(4,s.Native.Packet.Position); Assert.Equal(321,s.Native.Packet.Size);
    });

    [Fact]
    public Task AddSpawnMutatesActualPlayerAppearanceAndRetainsWingProgressUnlessPlusIsTwelve() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var item=new Item{Position=53,ITEM_ID=130009,Color=(Color)65537,Purification=new(){PurificationItemID=800001}};
        s.Equipment.AddSpawn(item); Assert.Equal(130009u,s.Player.ArmorId); Assert.Equal((ushort)1,s.Player.ColorArmor); Assert.Equal(800001u,s.Player.ArmorSoul);
        item.Position=12;item.Plus=12;item.SocketProgress=123;item.PlusProgress=456;
        s.Equipment.AddSpawn(item); Assert.Equal(130009u,s.Player.SteedId); Assert.Equal(123u,s.Player.SteedColor); Assert.Equal(12u,s.Player.SteedPlus); Assert.Equal((byte)12,s.Equipment.SteedPlus); Assert.Equal(456u,s.Equipment.SteedPlusPorgres);
        item.Position=19;s.Equipment.AddSpawn(item);Assert.Equal(456u,s.Player.WingProgress);Assert.Equal((byte)12,s.Player.WingPlus);
        item.Plus=11;item.PlusProgress=999;s.Equipment.AddSpawn(item);Assert.Equal(456u,s.Player.WingProgress);Assert.Equal((byte)11,s.Player.WingPlus);
        s.Player.SpecialGarment=777;item.Position=0;s.Equipment.AddSpawn(item);Assert.Equal(777u,s.Player.GarmentId);
        Assert.Empty(s.Native.Queue);
    });

    [Fact]
    public Task ClearItemsSpawnResetsAllActualAppearanceSlotsButPreservesSpecialGarmentAndEquipmentProgress() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();
        string[] fields=["HeadId","GarmentId","WingId","WingProgress","ArmorId","LeftWeaponId","RightWeaponId","LeftWeaponAccessoryId","RightWeaponAccessoryId","SteedId","MountArmorId","Bottle","SteedPlus","SteedColor","HeadSoul","ArmorSoul","LeftWeapsonSoul","RightWeapsonSoul"];
        foreach(string name in fields)typeof(LegacyNativePlayerRuntime).GetField(name)!.SetValue(s.Player,99u);
        s.Player.ColorArmor=1;s.Player.ColorShield=2;s.Player.ColorHelment=3;s.Player.WingPlus=4;s.Player.SpecialGarment=888;s.Equipment.SteedPlusPorgres=555;
        s.Equipment.ClearItemSpawn();
        foreach(string name in fields)Assert.Equal(0u,(uint)typeof(LegacyNativePlayerRuntime).GetField(name)!.GetValue(s.Player)!);
        Assert.Equal((ushort)0,s.Player.ColorArmor);Assert.Equal((ushort)0,s.Player.ColorShield);Assert.Equal((ushort)0,s.Player.ColorHelment);Assert.Equal((byte)0,s.Player.WingPlus);
        Assert.Equal(888u,s.Player.SpecialGarment);Assert.Equal(555u,s.Equipment.SteedPlusPorgres);Assert.Empty(s.Native.Queue);
    });
}
