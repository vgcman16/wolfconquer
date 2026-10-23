using WolfConquer.Core.World;
using Item = WolfConquer.Core.World.LegacyNativeGameItemRuntime;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEquipmentQueryPrerequisiteTests
{
    [Theory]
    [InlineData((ushort)12)] [InlineData((ushort)9)] [InlineData((ushort)7)] [InlineData((ushort)57)]
    [InlineData((ushort)59)] [InlineData((ushort)17)] [InlineData((ushort)13)] [InlineData((ushort)15)] [InlineData((ushort)16)]
    public Task FullSuperExemptsExactNativePhysicalPositionsWithoutCheckingTheirQuality(ushort position) => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Equipment.SuperArmor=true; s.Equipment.CurentEquip=new[]{new Item{Position=position,ITEM_ID=410005}};
        Assert.True(s.Equipment.FullSuper); s.Equipment.CurentEquip[0].Position=62; Assert.False(s.Equipment.FullSuper);
        s.Equipment.CurentEquip[0].ITEM_ID=410009; Assert.True(s.Equipment.FullSuper);
    });

    [Fact]
    public Task FullSuperAndEpicQueriesKeepDistinctNullAndEmptyArrayGates() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Equipment.CurentEquip=null!; Assert.False(s.Equipment.FullSuper);
        s.Equipment.SuperArmor=true; Assert.Throws<NullReferenceException>(()=>s.Equipment.FullSuper);
        s.Equipment.CurentEquip=Array.Empty<Item>(); Assert.True(s.Equipment.FullSuper);
        Assert.False(s.Equipment.UseEpicTrojanWeapon()); Assert.False(s.Equipment.UseEpicNinjaWeapon());
        s.Equipment.CurentEquip=new Item[]{null!,new(){ITEM_ID=616}};
        Assert.True(s.Equipment.UseEpicNinjaWeapon()); Assert.False(s.Equipment.UseEpicTrojanWeapon());
        s.Equipment.CurentEquip[1].ITEM_ID=614000; Assert.True(s.Equipment.UseEpicTrojanWeapon()); Assert.False(s.Equipment.UseEpicNinjaWeapon());
    });

    [Theory]
    [InlineData(0,0,0,0,1)] [InlineData(5,0,2,3,8)] [InlineData(0,7,0,0,7)]
    [InlineData(5,8,0,0,6)] [InlineData(-5,0,-2,-3,-7)]
    public Task AttackRangeKeepsNativeAverageSizeAndSignedIntegerTruncation(int right,int left,int ownSize,int targetSize,int expected) => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Equipment.rangeR=right; s.Equipment.rangeL=left; s.Equipment.SizeAdd=ownSize;
        Assert.Equal(expected,s.Equipment.GetAttackRange(targetSize)); Assert.Equal(right,s.Equipment.rangeR);
    });

    [Fact]
    public Task AttackSpeedUsesStepwiseFloorRealAgilityCycloneAndBooleanOverloadIgnoresArgument() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); Assert.Equal(700,s.Equipment.AttackSpeed(true)); Assert.Equal(700,s.Equipment.AttackSpeed(false));
        s.Equipment.SpeedR=30; s.Equipment.SpeedL=40; s.Equipment.SpeedRing=50; s.Player.Agility=101;
        Assert.Equal(530,s.Equipment.AttackSpeed(800));
        s.Player.BitVector.TryAdd((int)Flag.Cyclone,60,false,0);
        Assert.Equal(380,s.Equipment.AttackSpeed(800));
        s.Equipment.SpeedR=-100; s.Equipment.SpeedL=0; s.Equipment.SpeedRing=0; s.Player.Agility=0;
        s.Player.BitVector.TryRemove((int)Flag.Cyclone);
        Assert.Equal(400,s.Equipment.AttackSpeed(200));
        Assert.Empty(s.Native.Queue);
    });

    [Fact]
    public Task AttackSpeedReadsRealEquippedRuneLevelAndAppliesNativeDeadlySightReduction() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Player.Reborn=2; SetLevel(s.Player,15);
        const uint id=4031109;
        LegacyNativePoolItems.ItemsBase![id]=new(){ID=id,Name="DeadlySight",Class=0};
        var rune=new Item{Uid=99,Position=101,ITEM_ID=id}; Assert.True(s.Client.Rune!.Add(rune));
        Assert.Equal(585,s.Equipment.AttackSpeed(800)); // 700 -(25+9*10).
        s.Client.Rune=null;
        Assert.Throws<NullReferenceException>(()=>s.Equipment.AttackSpeed(800));
        Assert.Equal(9u,rune.ITEM_ID%100); Assert.Empty(s.Native.Queue);
    });

    [Fact]
    public Task PerfectionEligibilityUsesCurrentArrayMaximumAndDoesNotNormalizeCountOrItemOwner() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var incoming=new Item{PerfectionLevel=0};
        Assert.True(s.Equipment.CanUpdatePerfectionItem(incoming)); incoming.PerfectionLevel=1; Assert.False(s.Equipment.CanUpdatePerfectionItem(incoming));
        var held=new Item{PerfectionLevel=5,OwnerUID=0}; s.Equipment.CurentEquip=new[]{held,held};
        incoming.PerfectionLevel=4; Assert.True(s.Equipment.CanUpdatePerfectionItem(incoming));
        incoming.PerfectionLevel=6; Assert.False(s.Equipment.CanUpdatePerfectionItem(incoming));
        held.PerfectionLevel=6; Assert.True(s.Equipment.CanUpdatePerfectionItem(incoming));
        Assert.Same(held,s.Equipment.CurentEquip[0]); Assert.Equal(0u,held.OwnerUID);
    });

    [Theory]
    [InlineData(626003u,626439u,true)] [InlineData(626002u,626439u,false)] [InlineData(626003u,626440u,false)]
    public Task WindWalkerWeaponQueryRequiresBothRawWeaponIdsAtInclusiveNativeBounds(uint left,uint right,bool expected) => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Equipment.LeftWeapon=left; s.Equipment.RightWeapon=right;
        Assert.Equal(expected,s.Equipment.UseWindWalkerWeapons()); Assert.Empty(s.Equipment.ClientItems);
    });
}
