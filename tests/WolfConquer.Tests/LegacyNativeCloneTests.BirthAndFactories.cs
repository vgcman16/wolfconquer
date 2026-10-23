using System.Text;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCloneTests
{
    [Theory]
    [InlineData(3,3u)] [InlineData(10003,10003u)] [InlineData(-1,65535u)]
    [InlineData(65536,0u)] [InlineData(131071,65535u)]
    public Task WholeCloneBirthKeepsActualOwnerBitmapUidOrderAndFlagNarrowing(int flag,uint narrowed)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var held=scope.Player.MyClones;
        var clone=new LegacyNativeClone(scope.Player,"IgnoredCloneName",flag,scope.Packet);
        Assert.Same(scope.Client,clone.Owner);Assert.Equal(700101u,clone.UID);Assert.Equal(700101u,LegacyNativeClone.CounterUID.Count);
        Assert.Equal(flag,clone.cloneflag);Assert.Equal("IgnoredCloneName",clone.clonename);Assert.Equal(7,clone.BitVector.bits.Length);
        Assert.True(clone.BitVector.Contain((int)Flag.Invisibility));Assert.Empty(held);Assert.Same(held,scope.Player.MyClones);
        Assert.Equal(new ushort[]{2227,2176},scope.Queue.Select(Opcode).ToArray());
        var spawn=scope.Queue.First();var fields=Scalars(spawn);Assert.Equal(700101UL,Assert.Single(fields[2]));
        Assert.Equal(17UL,Assert.Single(fields[83]));Assert.Equal(2UL,Assert.Single(fields[81]));
        if(narrowed==0)Assert.False(fields.ContainsKey(82));else Assert.Equal((ulong)narrowed,Assert.Single(fields[82]));
        Assert.Equal(7,fields[6].Count);Assert.All(fields[6],value=>Assert.Equal(0UL,value));
        Assert.Contains("ShadowClone(OwnedWolf)",Encoding.UTF8.GetString(spawn));
        Assert.DoesNotContain("IgnoredCloneName",Encoding.UTF8.GetString(spawn));
    });

    [Fact]
    public Task FactoriesAppendRealNewReferencesToSamePhysicalListWithoutClearingPriorClones()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var held=scope.Player.MyClones;
        LegacyNativeClone.CreateShadowClone(scope.Player,scope.Packet);
        Assert.Equal(new[]{3,10003},held.Select(clone=>clone.cloneflag).ToArray());
        Assert.Equal(new uint[]{700101,700102},held.Select(clone=>clone.UID).ToArray());
        LegacyNativeClone.CreateShadowClone(scope.Player,91,scope.Packet);
        Assert.Same(held,scope.Player.MyClones);Assert.Equal(3,held.Count);Assert.Equal(91,held[2].cloneflag);
        Assert.NotSame(held[0],held[1]);Assert.NotSame(held[1],held[2]);
        Assert.All(held,clone=>Assert.Same(scope.Client,clone.Owner));Assert.Equal(6,scope.Queue.Count);
        var other=NativeSpellTestFixture.CreateOwner(new());Assert.NotSame(held,other.Player.MyClones);Assert.Empty(other.Player.MyClones);
    });

    [Fact]
    public Task ConstructorNullRoleFailsBeforeUidButNullPacketFailsAfterUidAndBeforeFactoryAppend()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();
        Assert.Throws<NullReferenceException>(()=>new LegacyNativeClone(null!,"x",3,scope.Packet));
        Assert.Equal(700100u,LegacyNativeClone.CounterUID.Count);
        Assert.Throws<NullReferenceException>(()=>LegacyNativeClone.CreateShadowClone(scope.Player,null!));
        Assert.Equal(700101u,LegacyNativeClone.CounterUID.Count);Assert.Empty(scope.Player.MyClones);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task MissingRealEquipmentFaultRetainsUidPrefixAndDoesNotFakeFactorySuccess()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Client.Equipment=null;
        Assert.Throws<NullReferenceException>(()=>LegacyNativeClone.CreateShadowClone(scope.Player,scope.Packet));
        Assert.Equal(700101u,LegacyNativeClone.CounterUID.Count);Assert.Empty(scope.Player.MyClones);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task CloneCounterUsesActualSimpleUnsignedWrapRatherThanSlotAssignment()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();LegacyNativeClone.CounterUID=new(uint.MaxValue);
        var clone=new LegacyNativeClone(scope.Player,"x",3,scope.Packet);
        Assert.Equal(0u,clone.UID);Assert.Equal(0u,LegacyNativeClone.CounterUID.Count);
        Assert.Equal(0UL,Assert.Single(Scalars(scope.Queue.First())[2]));
    });
}
