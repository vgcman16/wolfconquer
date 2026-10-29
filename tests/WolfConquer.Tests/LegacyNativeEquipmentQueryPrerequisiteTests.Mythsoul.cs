using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Equip = WolfConquer.Core.World.LegacyNativeEquipItemStorage;
using Item = WolfConquer.Core.World.LegacyNativeGameItemRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEquipmentQueryPrerequisiteTests
{
    private sealed class RecycledScope : IDisposable
    {
        private static readonly FieldInfo Bin=typeof(LegacyNativePacketRecycle).GetField("bin",BindingFlags.Static|BindingFlags.NonPublic)!;
        private readonly object? prior=Bin.GetValue(null); private readonly List<LegacyNativePacket> packets=new();
        internal RecycledScope()
        {
            var bin=new ConcurrentQueue<LegacyNativePacket>();
            for(int index=0;index<4;index++){var packet=new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);Fill(packet);packets.Add(packet);bin.Enqueue(packet);}
            Bin.SetValue(null,bin);
        }
        public void Dispose(){Bin.SetValue(null,prior);foreach(var packet in packets)packet.Dispose();}
    }

    [Theory]
    [InlineData(19u,"VenomLevel")] [InlineData(49u,"BloodthirstLevel")] [InlineData(89u,"EtherealLevel")]
    [InlineData(179u,"ElanLevel")] [InlineData(189u,"SolidLevel")] [InlineData(10019u,"VenomLevel")]
    public Task MythsoulAddsLiteralResidueToOnlyMatchingActualByteAccumulator(uint id,string field) => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var item=new Item{MythSoul=id}; s.Equipment.LoadMythsoul(item);
        foreach(var current in typeof(LegacyNativeMsgStatus).GetFields().Where(info=>info.FieldType==typeof(byte)))
            Assert.Equal(current.Name==field?(byte)9:(byte)0,(byte)current.GetValue(s.Status)!);
        s.Equipment.LoadMythsoul(item); Assert.Equal((byte)18,(byte)typeof(LegacyNativeMsgStatus).GetField(field)!.GetValue(s.Status)!);
        Assert.Equal(id,item.MythSoul); Assert.Empty(s.Native.Queue);
    });

    [Fact]
    public Task MythsoulUnknownAndZeroSkipMissingOwnerWhileRecognizedByteAdditionWraps() => NativePacketTestThread.Run(()=>
    {
        var ownerless=new Equip(null);ownerless.LoadMythsoul(new Item());ownerless.LoadMythsoul(new Item{MythSoul=29});
        Assert.Throws<NullReferenceException>(()=>ownerless.LoadMythsoul(null!));
        using var s=new Scope();s.Status.VenomLevel=250;s.Equipment.LoadMythsoul(new Item{MythSoul=19});Assert.Equal((byte)3,s.Status.VenomLevel);
    });

    [Fact]
    public Task MythsoulBagSendsRealVenomSpellThenAppliesEtherealTruncationAndAllFourteenBagStats() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();using var recycle=new RecycledScope();s.Client.GearMythsoulBag=new();s.Status.VenomLevel=3;s.Status.EtherealLevel=12;
        for(uint index=1;index<=14;index++)
        {
            uint id=1000+index;var item=new Item{Uid=index,ITEM_ID=id};s.Client.GearMythsoulBag[index]=item;
            LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute[id]=new(){ID=999,Type=index,value=10+index};
        }
        Assert.Empty(s.Client.GearMythsoulBag.MyArray);Assert.True(s.Client.GearMythsoulBag.Update);
        long revision=s.Client.GearMythsoulBag.Revision;object gate=s.Client.GearMythsoulBag.SyncRoot;
        var rows=s.Client.GearMythsoulBag.Values.ToArray();var uids=rows.Select(item=>item.Uid).ToArray();
        s.Equipment.LoadGearMythsoulBag();
        var expectedSpell=new byte[25];BinaryPrimitives.WriteUInt16LittleEndian(expectedSpell.AsSpan(0,2),25);BinaryPrimitives.WriteUInt16LittleEndian(expectedSpell.AsSpan(2,2),2101);
        BinaryPrimitives.WriteUInt16LittleEndian(expectedSpell.AsSpan(8,2),20000);BinaryPrimitives.WriteUInt16LittleEndian(expectedSpell.AsSpan(12,2),3);
        Assert.Equal(expectedSpell,Assert.Single(s.Native.Queue));Assert.Equal((ushort)3,s.Client.MySpells!.ClientSpells[20000].Level);
        Assert.Equal(1300u,s.Status.DodgeRate); // (uint)13.4 *100, NOT (uint)(13.4*100).
        Assert.Equal(new uint[]{20,21,22,24,23,11,12,12,14,15,16,17,18,19,13,0},StatusValues(s.Status));
        Assert.Equal(14,s.Client.GearMythsoulBag.Count);
        Assert.Same(gate,s.Client.GearMythsoulBag.SyncRoot);Assert.Equal(revision,s.Client.GearMythsoulBag.Revision);
        Assert.True(s.Client.GearMythsoulBag.Update);Assert.Empty(s.Client.GearMythsoulBag.MyArray);
        var current=s.Client.GearMythsoulBag.Values.ToArray();Assert.Equal(rows.Length,current.Length);
        for(int index=0;index<rows.Length;index++)Assert.Same(rows[index],current[index]);
        Assert.Equal(uids,current.Select(item=>item.Uid));
    });

    [Fact]
    public Task MythsoulMissingBagFaultKeepsEarlierSpellAndDodgeChangesAndZeroVenomRemovesRealSpell() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();using var recycle=new RecycledScope();s.Status.VenomLevel=2;s.Status.EtherealLevel=12;
        Assert.Null(s.Client.GearMythsoulBag);Assert.Throws<NullReferenceException>(()=>s.Equipment.LoadGearMythsoulBag());
        Assert.True(s.Client.MySpells!.ClientSpells.ContainsKey(20000));Assert.Single(s.Native.Queue);Assert.Equal(1300u,s.Status.DodgeRate);
        s.Native.Queue.Clear();s.Status.VenomLevel=0;s.Client.GearMythsoulBag=new();s.Equipment.LoadGearMythsoulBag();
        Assert.False(s.Client.MySpells.ClientSpells.ContainsKey(20000));Assert.Equal((ushort)2046,BitConverter.ToUInt16(Assert.Single(s.Native.Queue),2));
        Assert.Equal(2600u,s.Status.DodgeRate);
    });

    [Theory]
    [InlineData(1u,1.0,3.0,45.0,2000.0,5.0,3.0,500.0)]
    [InlineData(12u,6.5,13.4,56.0,2800.0,6.0,8.5,6000.0)]
    [InlineData(19u,10.0,16.2,63.0,3150.0,6.0,11.2,9100.0)]
    [InlineData(20u,10.2,16.6,64.0,3200.0,6.0,11.3,9200.0)]
    [InlineData(49u,13.1,19.8,93.0,4650.0,8.0,14.2,12100.0)]
    [InlineData(50u,0.0,0.0,0.0,0.0,0.0,0.0,0.0)]
    public void SpecialEffectLookupsKeepNativeFractionalTransitionsAndFreshPairArrays(uint level,double blood,double ethereal,double elan,double solid0,double solid1,double venom0,double venom1)
    {
        Assert.Equal(blood,LegacyNativeSpecialEffectAttributeTable.GetBloodthirst(level));
        Assert.Equal(ethereal,LegacyNativeSpecialEffectAttributeTable.GetEthereal(level));Assert.Equal(elan,LegacyNativeSpecialEffectAttributeTable.GetElan(level));
        var solid=LegacyNativeSpecialEffectAttributeTable.GetSolid(level);var venom=LegacyNativeSpecialEffectAttributeTable.GetVenom(level);
        Assert.Equal(new[]{solid0,solid1},solid);Assert.Equal(new[]{venom0,venom1},venom);
        Assert.NotSame(solid,LegacyNativeSpecialEffectAttributeTable.GetSolid(level));Assert.NotSame(venom,LegacyNativeSpecialEffectAttributeTable.GetVenom(level));
        solid[0]=999;Assert.Equal(solid0,LegacyNativeSpecialEffectAttributeTable.GetSolid(level)[0]);
    }
}
