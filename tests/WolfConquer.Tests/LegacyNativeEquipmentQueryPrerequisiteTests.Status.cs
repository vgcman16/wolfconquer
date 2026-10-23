using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Stat = WolfConquer.Core.World.LegacyNativeChiAttribute;
using HWAttr = WolfConquer.Core.Data.LegacyNativeHundredWeaponsTable.AttributeType;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEquipmentQueryPrerequisiteTests
{
    [Fact]
    public Task AddChiMutatesSameCurrentStatusEveryFieldWithNativePercentTruncationAndNoRecompute() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var chi=ChiValues(); var old=s.Status;
        s.Equipment.AddChiAtribute(chi,50);
        Assert.Same(old,s.Status);
        Assert.Equal(new uint[]{50,101,151,202,252,303,353,353,404,454,505,555,606,656,0,0},StatusValues(old));
        s.Client.Status=new LegacyNativeMsgStatus(); s.Equipment.AddChiAtribute(chi);
        Assert.Equal(new uint[]{101,202,303,404,505,606,707,707,808,909,1010,1111,1212,1313,0,0},StatusValues(s.Status));
        Assert.Equal(50u,old.CriticalStrike); Assert.Equal(101u,chi.CriticalStrike);
        Assert.Empty(s.Native.Queue); Assert.Null(s.Player.MyChi); // Supplied physical owner, no hidden bootstrap.
    });

    [Fact]
    public Task AddChiPreservesUnsignedMultiplyBeforeDivisionZeroPercentAndNullFaults() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var chi=ChiValues(); chi.CriticalStrike=uint.MaxValue;
        s.Equipment.AddChiAtribute(chi); Assert.Equal(42949671u,s.Status.CriticalStrike);
        uint[] original=StatusValues(s.Status); s.Equipment.AddChiAtribute(chi,0); Assert.Equal(original,StatusValues(s.Status));
        Assert.Throws<NullReferenceException>(()=>s.Equipment.AddChiAtribute(null!,0)); Assert.Equal(original,StatusValues(s.Status));
        chi.CriticalStrike=uint.MaxValue; s.Client.Status=new(); s.Equipment.AddChiAtribute(chi,uint.MaxValue);
        Assert.Equal(0u,s.Status.CriticalStrike); // MaxValue*MaxValue wraps1, then /100.
        s.Client.Status=null!; Assert.Throws<NullReferenceException>(()=>s.Equipment.AddChiAtribute(chi));
        Assert.Null(s.Client.Status);
    });

    [Fact]
    public Task BeastsAddsAllNativeStatsWithFinalDamageUshortNarrowingAndCurrentMetadataAlias() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var row=new LegacyNativeBeastsAtrribute.Attribute
        {Level=1,HPAdd=11,PAttack=12,MAttack=13,PDefense=14,MDefense=15,FinalMDmgDealt=65537,FinalPDmgDealt=65538,FinalPDmgReceived=65539,FinalMDmgReceived=65540};
        LegacyNativeBeastsAtrribute.Attributes[1]=row; s.Client.Beasts!.Level=1; s.Client.Beasts.Activated=true;
        var status=s.Status; s.Equipment.AddBeastsAtributes();
        Assert.Same(status,s.Status); Assert.Equal(11u,status.MaxHitpoints); Assert.Equal(12u,status.MinAttack); Assert.Equal(12u,status.MaxAttack);
        Assert.Equal(13u,status.MagicAttack); Assert.Equal(14u,status.Defence); Assert.Equal(15u,status.MagicDefence);
        Assert.Equal(1u,status.MagicDamageIncrease); Assert.Equal(2u,status.PhysicalDamageIncrease);
        Assert.Equal(3u,status.PhysicalDamageDecrease); Assert.Equal(4u,status.MagicDamageDecrease);
        row.HPAdd=100; s.Equipment.AddBeastsAtributes(); Assert.Equal(111u,status.MaxHitpoints); Assert.Empty(s.Native.Queue);
    });

    [Fact]
    public Task BeastsInactiveGateSkipsMissingStatusAndTableButActiveMissingMetadataFaultsBeforeMutation() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Client.Status=null!; LegacyNativeBeastsAtrribute.Attributes=null!;
        s.Equipment.AddBeastsAtributes(); Assert.Null(s.Client.Status);
        s.Client.Status=new(); LegacyNativeBeastsAtrribute.Attributes=new(); s.Client.Beasts!.Activated=true;
        Assert.Throws<KeyNotFoundException>(()=>s.Equipment.AddBeastsAtributes());
        Assert.All(StatusValues(s.Status),value=>Assert.Equal(0u,value));
        s.Client.Beasts=null; Assert.Throws<NullReferenceException>(()=>s.Equipment.AddBeastsAtributes());
    });

    [Fact]
    public Task HundredWeaponStatsUseOnlyFirstBitOneObjectAndNativeTwoPercentIntegerReduction() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var one=Hundred(51,49,101,201,301); var two=Hundred(999,999,999,999,999);
        s.Client.HundredWeapons!.Objects[Weapon.Blade]=one; s.Client.HundredWeapons.Objects[Weapon.Sword]=two;
        Assert.False(s.Client.HundredWeapons.Unlocked); // Native helper does not gate by Unlocked/Valid.
        s.Equipment.AddHWStats(); Assert.Equal(50u,s.Status.MaxHitpoints); Assert.Equal(49u,s.Status.MinAttack); Assert.Equal(49u,s.Status.MaxAttack);
        Assert.Equal(99u,s.Status.Defence); Assert.Equal(197u,s.Status.MagicAttack); Assert.Equal(295u,s.Status.MDefence); Assert.Equal(0u,s.Status.MagicDefence);
        one.BitVector.Remove(1); s.Equipment.AddHWStats(); Assert.Equal(1030u,s.Status.MaxHitpoints); //999-floor(1998/100)=980.
        Assert.Same(one,s.Client.HundredWeapons.Objects[Weapon.Blade]); Assert.Empty(s.Native.Queue);
    });

    [Fact]
    public Task HundredWeaponMissingLaterAttributeKeepsNativeEarlierApplicationsWithoutFallback() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); var one=Hundred(51,49,101,201,301); one.Attributes.Remove(HWAttr.PhysicalDefense);
        s.Client.HundredWeapons!.Objects[Weapon.Blade]=one;
        Assert.Throws<KeyNotFoundException>(()=>s.Equipment.AddHWStats());
        Assert.Equal(50u,s.Status.MaxHitpoints); Assert.Equal(49u,s.Status.MinAttack); Assert.Equal(49u,s.Status.MaxAttack);
        Assert.Equal(0u,s.Status.Defence); Assert.Equal(0u,s.Status.MagicAttack); Assert.Equal(0u,s.Status.MDefence);
    });

    [Fact]
    public Task HundredWeaponNegativeAndOverflowInputsRetainUncheckedSignedToUnsignedArithmetic() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Client.HundredWeapons!.Objects[Weapon.Blade]=Hundred(-1,0,0,0,int.MaxValue);
        s.Equipment.AddHWStats(); Assert.Equal(4252017623u,s.Status.MaxHitpoints); Assert.Equal(2104533975u,s.Status.MDefence);
        Assert.Equal(0u,s.Status.MagicDefence);
    });

    [Fact]
    public Task ProfessionUsesCurrentPlayerClassIgnoresUnsupportedTypesAndKeepsFaultPrefixOrder() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); SetClass(s.Player,1001);
        var hp=new LegacyNativeProfessionTable.AttributeValue{Type=Stat.HPAdd,Value=11};
        var attack=new LegacyNativeProfessionTable.AttributeValue{Type=Stat.AddAttack,Value=12};
        LegacyNativeProfessionTable.Benefits[1001]=new(){Class=999,BattlePower=255,AttributeValue=new LegacyNativeProfessionTable.AttributeValue[]{hp,attack,new(){Type=Stat.Breakthrough,Value=129},new(){Type=Stat.Counteraction,Value=99},new(){Type=Stat.AddMagicDefense,Value=500},new(){Type=Stat.None,Value=uint.MaxValue}}};
        s.Equipment.AddProfessionAtributes(); Assert.Equal(11u,s.Status.MaxHitpoints); Assert.Equal(12u,s.Status.MinAttack); Assert.Equal(12u,s.Status.MaxAttack);
        Assert.Equal(12u,s.Status.Breakthrough); Assert.Equal(9u,s.Status.Counteraction); Assert.Equal(0u,s.Status.MagicDefence);
        s.Client.Status=new(); LegacyNativeProfessionTable.Benefits[1001].AttributeValue=new[]{hp,null!,attack};
        Assert.Throws<NullReferenceException>(()=>s.Equipment.AddProfessionAtributes()); Assert.Equal(11u,s.Status.MaxHitpoints); Assert.Equal(0u,s.Status.MaxAttack);
        SetClass(s.Player,2001); s.Client.Status=null!; s.Equipment.AddProfessionAtributes(); Assert.Null(s.Client.Status);
    });

    [Fact]
    public Task PerfectionBenefitsNarrowPrestigeLookupAndDefenseButRetainUintAttackValues() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Client.PrestigeLevel=65537;
        var row=new LegacyNativeItemRefineUpgrade.Effect{Stars=999,PAttack=uint.MaxValue,MAttack=22,PDefense=65537,MDefense=65538};
        LegacyNativeItemRefineUpgrade.Effects[1]=row; var status=s.Status;
        s.Equipment.AddPerfectionAtributes(); Assert.Same(status,s.Status);
        Assert.Equal(uint.MaxValue,status.MaxAttack); Assert.Equal(uint.MaxValue,status.MinAttack); Assert.Equal(22u,status.MagicAttack);
        Assert.Equal(1u,status.Defence); Assert.Equal(2u,status.MagicDefence);
        s.Client.PrestigeLevel=2; s.Client.Status=null!; s.Equipment.AddPerfectionAtributes(); Assert.Null(s.Client.Status);
        LegacyNativeItemRefineUpgrade.Effects=null!; Assert.Throws<NullReferenceException>(()=>s.Equipment.AddPerfectionAtributes());
    });
}
