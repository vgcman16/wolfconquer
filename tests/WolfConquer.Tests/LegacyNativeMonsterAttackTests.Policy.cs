using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Setting = WolfConquer.Core.World.LegacyNativeMonsterSettings;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAttackTests
{
    [Theory]
    [InlineData(0x62u,true,"Pheasant",false)]
    [InlineData(0xE2u,true,"Pheasant",false)]
    [InlineData(0x35u,false,"Pheasant",false)]
    [InlineData(0x35u,true,"x[GM]y",false)]
    [InlineData(0x35u,true,"x[PM]y",false)]
    [InlineData(0x35u,true,"x[gm]y",true)]
    [InlineData(0x40u,true,"Pheasant",true)]
    [InlineData(0x800u,true,"Pheasant",true)]
    public Task CompleteAttackAdmissionPreservesCompositeAndNameRules(uint settings,bool alive,string name,bool allowed) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var role=s.Monster();role.Family.Settings=(Setting)settings;role.HitPoints=alive?1u:0u;role.Name=name;
        Assert.Equal(allowed,LegacyNativeCanAttackMonster.Verified(null!,role,null));Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(0,false)] [InlineData(1,false)] [InlineData(2,false)] [InlineData(3,false)] [InlineData(7,false)] [InlineData(11,false)] [InlineData(17,true)]
    public Task LotusOnlyProtectsExactOwnerUid(uint ownerUid,bool sameOwner) => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var role=s.Monster();role.Family.Settings=Setting.Lottus;
        var owner=NativeSpellTestFixture.CreateOwner(new());owner.Player.UID=ownerUid;role.OwnerFloor=owner;
        Assert.Equal(!sameOwner,LegacyNativeCanAttackMonster.Verified(s.Client,role,null));
        role.OwnerFloor=null;Assert.True(LegacyNativeCanAttackMonster.Verified(null!,role,null));
    });

    [Theory]
    [InlineData(0,true)] [InlineData(1,false)] [InlineData(2,false)] [InlineData(3,false)] [InlineData(4,false)] [InlineData(5,false)] [InlineData(6,false)] [InlineData(7,false)] [InlineData(11,false)]
    public Task GuardRequiresPkModeAndUsesActualPlayerFlagOwner(byte mode,bool allowed) => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var role=s.Monster();role.Family.Settings=Setting.Guard;s.Player.PkMode=(LegacyNativePkMode)mode;
        Assert.Equal(allowed,LegacyNativeCanAttackMonster.Verified(s.Client,role,null));
        Assert.Equal(allowed,s.Player.ContainFlag(Flag.FlashingName));
        if(allowed){var row=s.Player.BitVector.ArrayFlags[(int)Flag.FlashingName];Assert.Equal(30,row.Secounds);Assert.True(row.RemoveOnDead);Assert.Equal((ushort)2176,Opcode(Assert.Single(s.Queue)));}
        else Assert.Empty(s.Queue);
    });

    [Fact]
    public Task GuardRetainsExistingFlagAndNativeFaultPrefixes() => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var role=s.Monster();role.Family.Settings=Setting.Guard;s.Player.PkMode=LegacyNativePkMode.PK;
        s.Player.BitVector.TryAdd((int)Flag.FlashingName,9,false,2);var old=s.Player.BitVector.ArrayFlags[(int)Flag.FlashingName];
        Assert.True(LegacyNativeCanAttackMonster.Verified(s.Client,role,null));Assert.Same(old,s.Player.BitVector.ArrayFlags[(int)Flag.FlashingName]);Assert.Empty(s.Queue);
        role.Family.Settings=Setting.Reviver;role.Name=null;Assert.False(LegacyNativeCanAttackMonster.Verified(null!,role,null));
        role.Family.Settings=Setting.Standard;role.HitPoints=0;Assert.False(LegacyNativeCanAttackMonster.Verified(null!,role,null));
        role.HitPoints=1;Assert.Throws<NullReferenceException>(()=>LegacyNativeCanAttackMonster.Verified(null!,role,null));
        role.Name="Guard";role.Family.Settings=Setting.Guard;Assert.Throws<NullReferenceException>(()=>LegacyNativeCanAttackMonster.Verified(null!,role,null));
    });
}
