using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeWarehousePublicationTests
{
    [Fact]
    public Task WholeWarehouseHeaderAndReaderKeepTheirNativeWidthAsymmetryAndCursor() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); var packet=scope.Packet;
        Assert.Same(packet,packet.WarehouseCreate(0x11223344,(LegacyNativeMsgWarehouse.DepositActionID)0xFFFF,
            0x55667788,-1,65537));
        Assert.Equal(24,packet.Position); Assert.Equal(321,packet.Size);
        packet.FinalizeWarehouse(); byte[] expected=new byte[24]; U16(expected,0,24); U16(expected,2,2080);
        U32(expected,4,0x11223344); U32(expected,12,0xFFFF); U16(expected,16,0xFFFF);
        U32(expected,18,0x55667788); U16(expected,22,1);
        Assert.Equal(expected,NativeSpellTestFixture.Bytes(packet));
        packet.GetWarehouse(out uint bank,out var action,out uint uid);
        Assert.Equal(0x11223344u,bank); Assert.Equal((ushort)0xFFFF,(ushort)action); Assert.Equal(0x55667788u,uid);
        Assert.Equal(22,packet.Position); Assert.Equal((ushort)1,packet.ReadUInt16()); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task WholeWarehouseItemCodecWritesLiteralWidthsReservedTailMinuteWrapAndActualRelicCount() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); var item=Item(17);
        item.SocketOne=LegacyNativeGameItemFlags.Gem.SuperDragonGem; item.SocketTwo=LegacyNativeGameItemFlags.Gem.EmptySocket;
        item.SpellID=0x11223344; item.Effect=(LegacyNativeGameItemFlags.ItemEffect)0x12345678;
        item.Plus=12; item.Bless=7; item.Bound=1; item.Enchant=99; item.ProgresGreen=0x123456AB; item.Locked=2;
        item.SocketProgress=11; item.PlusProgress=22; item.TimeLeftInMinutes=uint.MaxValue; item.StackSize=9;
        item.Purification.PurificationItemID=1234567; item.PerfectionLevel=54; item.PerfectionProgress=100;
        item.OwnerUID=99; item.OwnerName="A"; item.Signature="B"; item.RuneEXP=1000;
        item.RelicAttributes=new[] { new LegacyNativeRelicAttribute(1),new LegacyNativeRelicAttribute(100104) };
        item.AnimaItemID=4200001; item.MythSoul=300; item.MythSoulPoints=400;
        scope.Packet.WarehouseCreate(8,LegacyNativeMsgWarehouse.DepositActionID.Show,0,60,1);
        Assert.Same(scope.Packet,scope.Packet.AddItemWarehouse(item)); scope.Packet.FinalizeWarehouse();
        byte[] expected=new byte[236]; U16(expected,0,236); U16(expected,2,2080); U32(expected,4,8);
        U32(expected,12,2560); U16(expected,16,60); U16(expected,22,1); int o=24;
        U32(expected,o,17); U32(expected,o+4,1000000); expected[o+9]=13; expected[o+10]=255;
        U32(expected,o+11,0x11223344); U16(expected,o+15,0x5678); expected[o+17]=12; expected[o+18]=7;
        expected[o+19]=1; expected[o+20]=99; expected[o+21]=0xAB; U32(expected,o+22,0x12345678); expected[o+26]=2;
        U32(expected,o+28,11); U32(expected,o+32,22); U32(expected,o+36,4294967236); U16(expected,o+44,9);
        U32(expected,o+46,1234567); U32(expected,o+52,54); U32(expected,o+56,100); U32(expected,o+60,99);
        expected[o+64]=65; expected[o+96]=66; U32(expected,o+156,1000); U32(expected,o+160,1); U32(expected,o+164,100104);
        U32(expected,o+168,4200001); U32(expected,o+176,300); U32(expected,o+180,400);
        Assert.Equal(expected,NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(4,scope.Packet.Position);
        Assert.Equal(uint.MaxValue,item.TimeLeftInMinutes); Assert.Equal(0x123456ABu,item.ProgresGreen);
    });

    [Fact]
    public Task WarehouseCodecRetainsRealSealAndSuppressesMinuteStorageForActivatedItems() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); var item=Item(17); item.TimeLeftInMinutes=100; item.Activate=1;
        LegacyNativePacket.SealString="TQServer";
        scope.Packet.WarehouseCreate(8,LegacyNativeMsgWarehouse.DepositActionID.Show,0,60,1);
        scope.Packet.AddItemWarehouse(item); scope.Packet.FinalizeWarehouse();
        byte[] expected=Frame(8,60,LegacyNativeMsgWarehouse.DepositActionID.Show,17)
            .Concat(new byte[] {84,81,83,101,114,118,101,114}).ToArray();
        Assert.Equal(expected,NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(256,scope.Packet.Size);
    });

    [Theory]
    [InlineData(8u,true)] [InlineData(10012u,true)] [InlineData(44u,true)]
    [InlineData(10027u,true)] [InlineData(10011u,true)] [InlineData(10028u,true)]
    [InlineData(7762u,true)] [InlineData(4101u,true)] [InlineData(65535u,true)]
    [InlineData(65534u,false)] [InlineData(0u,false)]
    public void WholeWarehouseIdentityPredicateUsesOnlyTheExactNativeNpcSet(uint id,bool expected)=>
        Assert.Equal(expected,LegacyNativeWarehouseRuntime.IsWarehouse((LegacyNativeNpcID)id));
}
