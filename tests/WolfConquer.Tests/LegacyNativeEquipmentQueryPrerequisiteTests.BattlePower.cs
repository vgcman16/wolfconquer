using WolfConquer.Core.World;
using Item = WolfConquer.Core.World.LegacyNativeGameItemRuntime;
using Gem = WolfConquer.Core.World.LegacyNativeGameItemFlags.Gem;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEquipmentQueryPrerequisiteTests
{
    [Theory]
    [InlineData(410005u,0)] [InlineData(410006u,1)] [InlineData(410007u,2)]
    [InlineData(410008u,3)] [InlineData(410009u,4)]
    public Task BattlePowerUsesNativeQualityLastDigitWithoutDurabilityOrPerfectionRequirement(uint id,int expected) => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var item=new Item{ITEM_ID=id,Durability=0,PerfectionLevel=uint.MaxValue,Position=99};
        s.Equipment.CurentEquip=new[]{item}; s.Equipment.BattlePower=999;
        s.Equipment.CalculateBattlePower(); Assert.Equal(expected,s.Equipment.BattlePower);
        Assert.Same(item,s.Equipment.CurentEquip[0]); Assert.Equal((ushort)99,item.Position); Assert.Empty(s.Native.Queue);
    });

    [Theory]
    [InlineData(2100005u)] [InlineData(181305u)] [InlineData(370001u)] [InlineData(350001u)]
    [InlineData(200000u)] [InlineData(4100000u)] [InlineData(619009u)]
    public Task BattlePowerExcludesLiteralItemIdFamiliesNotPhysicalEquipmentPosition(uint id) => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var item=new Item{ITEM_ID=id,Plus=12,Position=4,SocketOne=Gem.SuperDragonGem,SocketTwo=Gem.SuperDragonGem,AnimaItemID=19};
        s.Equipment.CurentEquip=new[]{item}; s.Equipment.CalculateBattlePower();
        Assert.Equal(0,s.Equipment.BattlePower); Assert.Equal((ushort)4,item.Position);
    });

    [Theory]
    [InlineData(0u,0)] [InlineData(1u,1)] [InlineData(2u,1)] [InlineData(3u,1)]
    [InlineData(4u,2)] [InlineData(19u,16)] [InlineData(99u,97)]
    public Task BattlePowerAddsRawAnimaResidueNativeSocketQualityAndUncappedPlus(uint anima,int animaBonus) => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var item=new Item{ITEM_ID=410009,Plus=255,SocketOne=Gem.EmptySocket,SocketTwo=Gem.SuperDragonGem,AnimaItemID=anima};
        s.Equipment.CurentEquip=new[]{item}; s.Equipment.CalculateBattlePower();
        Assert.Equal(4+255+1+2+animaBonus,s.Equipment.BattlePower);
        Assert.Equal((byte)255,item.Plus); Assert.Equal(Gem.EmptySocket,item.SocketOne);
    });

    [Theory]
    [InlineData(500009u,false,false,8)] [InlineData(500009u,true,false,4)] [InlineData(500009u,false,true,4)]
    [InlineData(421009u,true,true,8)] [InlineData(620009u,true,true,8)]
    public Task BattlePowerDoublesTwoHandOnlyWhenBothLeftSlotsAreFreeButBackswordAndEpicAlwaysDouble(uint id,bool left,bool altLeft,int expected) => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); if(left)s.Add(10,5); if(altLeft)s.Add(11,55);
        var item=new Item{ITEM_ID=id}; s.Equipment.CurentEquip=new[]{item};
        s.Equipment.CalculateBattlePower(); Assert.Equal(expected,s.Equipment.BattlePower);
    });

    [Fact]
    public Task BattlePowerCountsSharedArrayAliasesAndNullLaterEntryRetainsInitialZeroInsteadOfPartialTotal() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var item=new Item{ITEM_ID=410009}; s.Equipment.CurentEquip=new[]{item,item};
        s.Equipment.CalculateBattlePower(); Assert.Equal(8,s.Equipment.BattlePower);
        s.Equipment.CurentEquip=new Item[]{item,null!}; s.Equipment.BattlePower=77;
        Assert.Throws<NullReferenceException>(()=>s.Equipment.CalculateBattlePower()); Assert.Equal(0,s.Equipment.BattlePower);
        s.Equipment.CurentEquip=null!; s.Equipment.BattlePower=88;
        Assert.Throws<NullReferenceException>(()=>s.Equipment.CalculateBattlePower()); Assert.Equal(0,s.Equipment.BattlePower);
    });
}
