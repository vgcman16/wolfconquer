using System.Reflection;
using WolfConquer.Core.World;
using Equip = WolfConquer.Core.World.LegacyNativeEquipItemStorage;
using Item = WolfConquer.Core.World.LegacyNativeGameItemRuntime;
using Position = WolfConquer.Core.World.LegacyNativeGameItemFlags.ConquerItem;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEquipmentQueryPrerequisiteTests
{
    [Fact]
    public Task PositionQueriesUseCurrentValuesNotDictionaryKeyUidOrIdAndRetainFirstReference() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var one=s.Add(100,4,999999); var two=s.Add(200,4,500009);
        one.Uid=77; two.Uid=88;
        Item first=s.Equipment.ClientItems.Values.First(item=>item.Position==4);
        Assert.False(s.Equipment.FreeEquip((Position)4)); Assert.True(s.Equipment.FreeEquip((Position)77));
        Assert.True(s.Equipment.TryGetEquip((Position)4,out var found)); Assert.Same(first,found);
        Assert.Same(first,s.Equipment.TryGetEquip((Position)4));
        Assert.False(s.Equipment.TryGetEquip((Position)5,out var missing)); Assert.Null(missing);
        Assert.True(s.Equipment.TryGetValue(100,out var keyed)); Assert.Same(one,keyed);
        Assert.False(s.Equipment.TryGetValue(77,out missing)); Assert.Null(missing);
        Assert.Equal(77u,one.Uid); Assert.Equal(2,s.Equipment.ClientItems.Count);
        s.Equipment.ClientItems=new(); Assert.Null(s.Equipment.TryGetEquip((Position)4));
        Assert.True(s.Equipment.FreeEquip((Position)4));
    });

    [Theory]
    [InlineData(false,false,false)] [InlineData(true,false,false)]
    [InlineData(true,true,false)] [InlineData(true,true,true)]
    public Task ComputeWeaponsPreservesMainAndAlternateFallbacksWithoutOwnerPublication(bool alternate,bool altRight,bool mainRightMissing) => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var mainRight=mainRightMissing?null:s.Add(1,4); var mainLeft=s.Add(2,5);
        var alternateRight=altRight?s.Add(3,54):null; var alternateLeft=s.Add(4,55);
        s.Equipment.Alternante=alternate;
        var pair=Weapons(s.Equipment);
        bool useAlt=alternate&&altRight;
        Assert.Same(useAlt?alternateRight:mainRight,pair.Item1);
        Assert.Same(useAlt?alternateLeft:mainLeft,pair.Item2);
        Assert.Equal(alternate,s.Equipment.Alternante); Assert.Equal(altRight?4:3,s.Equipment.ClientItems.Count+(mainRightMissing?1:0));
        Assert.Empty(s.Native.Queue);
    });

    [Theory]
    [InlineData(500009u,1050000u,false,true)]
    [InlineData(560009u,1050000u,false,false)]
    [InlineData(560009u,900009u,false,false)]
    [InlineData(560009u,900009u,true,true)]
    [InlineData(560009u,410009u,false,false)]
    [InlineData(410009u,560009u,false,false)]
    [InlineData(410009u,420009u,false,true)]
    public Task ComputeWeaponsKeepsNativeTwoHandBowArrowShieldAndSpell10311Branches(uint rightId,uint leftId,bool shieldSpell,bool keepLeft) => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Add(1,4); var right=s.Add(2,54,rightId); var left=s.Add(3,5,leftId);
        s.Equipment.Alternante=true;
        if(shieldSpell)s.Client.MySpells!.ClientSpells[10311]=new LegacyNativeMsgSpell{ID=10311};
        var pair=Weapons(s.Equipment);
        Assert.Same(right,pair.Item1); Assert.Same(keepLeft?left:null,pair.Item2);
        Assert.Equal(54,right.Position); Assert.Equal(5,left.Position); Assert.Same(left,s.Equipment.ClientItems[3]);
        Assert.Empty(s.Native.Queue);
    });

    [Fact]
    public Task ComputeWeaponsReturnsNullLeftWhenBothRelevantLeftSlotsAreAbsent() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Add(1,4); var alternate=s.Add(2,54); s.Equipment.Alternante=true;
        var pair=Weapons(s.Equipment); Assert.Same(alternate,pair.Item1); Assert.Null(pair.Item2);
    });

    [Fact]
    public Task ComputeWeaponsShieldSpellOwnerFaultDoesNotMutateEitherSelectedItem() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Add(1,4); var right=s.Add(2,54,560009); var left=s.Add(3,5,900009);
        s.Equipment.Alternante=true; s.Client.MySpells=null!;
        var fault=Assert.Throws<TargetInvocationException>(()=>Weapons(s.Equipment));
        Assert.IsType<NullReferenceException>(fault.InnerException);
        Assert.Equal(560009u,right.ITEM_ID); Assert.Equal(900009u,left.ITEM_ID); Assert.Equal(3,s.Equipment.ClientItems.Count);
    });

    [Fact]
    public Task PositionQueriesPreserveNullDictionaryFaultWithoutReplacingOwner() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Equipment.ClientItems=null!;
        Assert.Throws<NullReferenceException>(()=>s.Equipment.FreeEquip((Position)4));
        Assert.Throws<NullReferenceException>(()=>s.Equipment.TryGetEquip((Position)4));
        Assert.Null(s.Equipment.ClientItems);
    });
}
