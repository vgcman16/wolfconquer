using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeamOwnerPacketTests
{
    [Theory]
    [InlineData((ushort)2039,0u)] [InlineData((ushort)2288,2u)] [InlineData((ushort)65535,uint.MaxValue)]
    public Task EliteNameWholeCodecPreservesBothProtocolIdsAndRawState(ushort opcode,uint state)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();const string name="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-more";
        scope.Packet.TeamEliteSetTeamName(opcode,(LegacyNativeMsgTeamEliteSetTeamName.State)state,uint.MaxValue,name);
        var body=new List<byte>();U32(body,state);U32(body,uint.MaxValue);Fixed(body,name,32);
        Assert.Equal(Frame(opcode,body),Bytes(scope.Packet));Assert.Equal(44,scope.Packet.Size);Assert.Equal(1,Marshal.SizeOf<LegacyNativeMsgTeamEliteSetTeamName>());
        scope.Packet.Seek(4);scope.Packet.GetEliteSetTeamName(out uint first,out uint second,out string read);
        Assert.Equal(state,first);Assert.Equal(uint.MaxValue,second);Assert.Equal(name[..32],read);Assert.Equal(44,scope.Packet.Position);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task EliteNameReaderConsumesFixed32BytesEvenWhenEmbeddedNullEndsReturnedName()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Packet.TeamEliteSetTeamName(2039,LegacyNativeMsgTeamEliteSetTeamName.State.SuccessfulName,91,"A\0Z");
        scope.Packet.Seek(4);scope.Packet.GetEliteSetTeamName(out uint first,out uint second,out string name);
        Assert.Equal(1u,first);Assert.Equal(91u,second);Assert.Equal("A",name);Assert.Equal(44,scope.Packet.Position);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NullEliteNameRefusesWithoutAnyTargetOrQueueChange()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope(); byte[] before=TeamUiAllocation(scope.Packet);
        Assert.Throws<ArgumentNullException>(()=>scope.Packet.TeamEliteSetTeamName(2039,LegacyNativeMsgTeamEliteSetTeamName.State.Remove,91,null!));
        Assert.Equal(237,scope.Packet.Position);Assert.Equal(321,scope.Packet.Size);Assert.Empty(scope.Queue);
        Assert.Equal(before,TeamUiAllocation(scope.Packet));
    });

    [Theory]
    [InlineData((ushort)2270,72)] [InlineData((ushort)2283,68)] [InlineData((ushort)0,68)]
    public Task WholeMatchUiCodecKeepsSpecial2270WordAndExactFixedPayload(ushort opcode,int size)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Packet.TeamElitePKMatchUICreate(opcode,LegacyNativeMsgTeamElitePKMatchUI.State.Reward,LegacyNativeMsgTeamElitePKMatchUI.EffectTyp.Effect_Win,91,"EnemyWolf",uint.MaxValue);
        var body=new List<byte>();U32(body,8);U32(body,1);if(opcode==2270)U32(body,0);U32(body,91);U32(body,0);Fixed(body,"EnemyWolf",32);
        body.AddRange(new byte[8]);U32(body,uint.MaxValue);U32(body,0);
        Assert.Equal(Frame(opcode,body),Bytes(scope.Packet));Assert.Equal(size,scope.Packet.Size);Assert.Equal(1,Marshal.SizeOf<LegacyNativeMsgTeamElitePKMatchUI>());Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData((ushort)2270)] [InlineData((ushort)2283)]
    public Task MatchUiNullNameRefusesBeforeConditionalPrefixOrAcknowledgement(ushort opcode)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope(); byte[] before=TeamUiAllocation(scope.Packet);
        Assert.Throws<ArgumentNullException>(()=>scope.Packet.TeamElitePKMatchUICreate(opcode,(LegacyNativeMsgTeamElitePKMatchUI.State)uint.MaxValue,(LegacyNativeMsgTeamElitePKMatchUI.EffectTyp)uint.MaxValue,91,null!,30));
        Assert.Equal(237,scope.Packet.Position);Assert.Equal(321,scope.Packet.Size);Assert.Empty(scope.Queue);
        Assert.Equal(before,TeamUiAllocation(scope.Packet));
    });

    private static unsafe byte[] TeamUiAllocation(LegacyNativePacket packet)
    {
        var bytes=new byte[packet.Capacity]; Marshal.Copy((nint)packet.Memory,bytes,0,bytes.Length); return bytes;
    }
}
