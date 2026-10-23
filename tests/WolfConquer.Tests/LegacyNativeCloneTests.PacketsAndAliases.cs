using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCloneTests
{
    [Fact]
    public Task CloneGetArrayReadsCurrentOwnerIdentityAndKeepsZeroStatusWordsAfterFlags()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var clone=new LegacyNativeClone(scope.Player,"Unused",3,scope.Packet);scope.Queue.Clear();
        scope.Player.Name="RenamedWolf";scope.Player.UID=91;scope.Player.X=21;scope.Player.Y=22;scope.Player.FlowerRank=7;
        scope.Client.Status.MaxHitpoints=1234;Set(scope.Player,"_minhitpoints",321);Set(scope.Player,"_level",(ushort)15);
        clone.AddFlag(LegacyNativeMsgUpdate.Flags.Poisoned);scope.Queue.Clear();
        clone.GetArray(scope.Packet);var frame=Bytes(scope.Packet);var fields=Scalars(frame);
        Assert.Equal(700101UL,Assert.Single(fields[2]));Assert.Equal(91UL,Assert.Single(fields[83]));
        Assert.Equal(321UL,Assert.Single(fields[21]));Assert.Equal(1234UL,Assert.Single(fields[23]));
        Assert.Equal(21UL,Assert.Single(fields[27]));Assert.Equal(22UL,Assert.Single(fields[28]));
        Assert.Equal(15UL,Assert.Single(fields[35]));Assert.Equal(10007UL,Assert.Single(fields[42]));
        Assert.Equal(7,fields[6].Count);Assert.All(fields[6],word=>Assert.Equal(0UL,word));
        Assert.Contains("ShadowClone(RenamedWolf)",Encoding.UTF8.GetString(frame));Assert.DoesNotContain("OwnedWolf",Encoding.UTF8.GetString(frame));
        Assert.Empty(scope.Queue);Assert.True(clone.BitVector.Contain((int)LegacyNativeMsgUpdate.Flags.Poisoned));
    });

    [Theory]
    [InlineData((byte)0)] [InlineData((byte)2)] [InlineData(byte.MaxValue)]
    public Task PetAttackPrimitiveCodecKeepsRawCountAndAllUidsWithoutCombatDispatch(byte count)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Packet.PetAttackCreate(uint.MaxValue,count,700101,700102);
        var expected=new List<byte>();expected.AddRange(BitConverter.GetBytes((ushort)17));expected.AddRange(BitConverter.GetBytes((ushort)2812));
        expected.AddRange(BitConverter.GetBytes(uint.MaxValue));expected.Add(count);expected.AddRange(BitConverter.GetBytes(700101u));expected.AddRange(BitConverter.GetBytes(700102u));
        Assert.Equal(expected.ToArray(),Bytes(scope.Packet));scope.Packet.Seek(4);scope.Packet.GetPetAttack(out uint target,out byte readCount,out uint first,out uint second);
        Assert.Equal(uint.MaxValue,target);Assert.Equal(count,readCount);Assert.Equal(700101u,first);Assert.Equal(700102u,second);Assert.Equal(17,scope.Packet.Position);
        Assert.Empty(scope.Queue);Assert.Empty(scope.Player.MyClones);
    });

    [Fact]
    public Task PetInfoPrimitiveUsesEightByteMeshAndExactFixedAsciiNameWithoutWorldBirth()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();const string name="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-long";
        scope.Packet.CreatePetInfo(91,4264,9,uint.MaxValue,14,65535,65534,name);
        var expected=new List<byte>();expected.AddRange(BitConverter.GetBytes((ushort)62));expected.AddRange(BitConverter.GetBytes((ushort)2192));
        expected.AddRange(BitConverter.GetBytes(91u));expected.AddRange(BitConverter.GetBytes(4264u));expected.AddRange(BitConverter.GetBytes(9u));
        expected.AddRange(BitConverter.GetBytes((ulong)uint.MaxValue));expected.AddRange(BitConverter.GetBytes((ushort)14));
        expected.AddRange(BitConverter.GetBytes(ushort.MaxValue));expected.AddRange(BitConverter.GetBytes((ushort)65534));expected.AddRange(Encoding.ASCII.GetBytes(name[..32]));
        Assert.Equal(expected.ToArray(),Bytes(scope.Packet));Assert.Empty(scope.Queue);Assert.Empty(scope.Player.MyClones);
    });
}
