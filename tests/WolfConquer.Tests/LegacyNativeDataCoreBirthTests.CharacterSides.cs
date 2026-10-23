using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDataCoreBirthTests
{
    [Theory]
    [InlineData((ushort)1008,1000u,1,102)] [InlineData((ushort)1008,2000u,1,102)]
    [InlineData((ushort)1008,4000u,1,102)] [InlineData((ushort)1008,10000u,1,102)]
    [InlineData((ushort)1008,5000u,103,107)] [InlineData((ushort)1008,6000u,109,133)]
    [InlineData((ushort)1008,7000u,154,158)] [InlineData((ushort)1008,8000u,164,168)]
    [InlineData((ushort)1008,16000u,174,178)] [InlineData((ushort)1008,0u,1,50)]
    [InlineData((ushort)2007,1000u,201,290)] [InlineData((ushort)2007,2000u,201,290)]
    [InlineData((ushort)2007,4000u,201,290)] [InlineData((ushort)2007,10000u,201,290)]
    [InlineData((ushort)2007,5000u,291,295)] [InlineData((ushort)2007,6000u,300,304)]
    [InlineData((ushort)2007,7000u,345,349)] [InlineData((ushort)2007,8000u,355,359)]
    [InlineData((ushort)2007,16000u,365,369)] [InlineData((ushort)2007,0u,201,250)]
    [InlineData((ushort)0,1000u,1,50)] [InlineData((ushort)65535,16000u,1,50)]
    [InlineData((ushort)1008,257000u,1,102)] [InlineData((ushort)2007,uint.MaxValue,201,250)]
    public Task WholeCharacterSidesKeepsEveryClassBodyRangeOneRealDrawAndByteClassNarrowing(ushort body,uint job,int minimum,int maximum)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Player.Body=body;SetClass(scope.Player,job);
        LegacyNativePoolRandom.GetRandom=new LegacyNativeSafeRandom(109);var control=new Random(109);
        ushort expected=(ushort)control.Next(minimum,maximum);LegacyNativeDataCoreAttributes.SetCharacterSides(scope.Player);
        Assert.Equal(expected,scope.Player.Face);Assert.Equal(body,scope.Player.Body);Assert.Equal(job,scope.Player.Class);
        Assert.Equal((uint)(expected*10000+body),scope.Player.Mesh);Assert.Equal(control.Next(),LegacyNativePoolRandom.GetRandom.Next());Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task WholeCharacterSidesUsesGenuineHotFaceSetterAndOwnerOnlyMeshPacket()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Player.Body=1008;SetClass(scope.Player,7000);scope.Client.FullLoading=true;
        LegacyNativePoolRandom.GetRandom=new LegacyNativeSafeRandom(113);var control=new Random(113);ushort face=(ushort)control.Next(154,158);
        LegacyNativeDataCoreAttributes.SetCharacterSides(scope.Player);uint mesh=(uint)(face*10000+1008);
        var update=new List<byte>{8,11,16};VarUInt(update,mesh);var payload=new List<byte>{8,17,18,(byte)update.Count};payload.AddRange(update);
        byte[] expected=new byte[4+payload.Count];BitConverter.GetBytes((ushort)expected.Length).CopyTo(expected,0);BitConverter.GetBytes((ushort)2176).CopyTo(expected,2);payload.CopyTo(expected,4);
        Assert.Equal(expected,Assert.Single(scope.Queue));Assert.Equal(face,scope.Player.Face);Assert.Null(scope.Client.Map);
        Assert.Equal(control.Next(),LegacyNativePoolRandom.GetRandom.Next());
    });

    [Fact]
    public Task NullSharedRandomFaultDoesNotAcknowledgeOrChangeExistingFace()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Player.Body=1008;scope.Player.Face=77;SetClass(scope.Player,1000);LegacyNativePoolRandom.GetRandom=null!;
        Assert.Throws<NullReferenceException>(()=>LegacyNativeDataCoreAttributes.SetCharacterSides(scope.Player));
        Assert.Equal((ushort)77,scope.Player.Face);Assert.Equal((ushort)1008,scope.Player.Body);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NullPlayerFailsBeforeTakingTheGenuineSharedDraw()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();LegacyNativePoolRandom.GetRandom=new LegacyNativeSafeRandom(127);var control=new Random(127);
        Assert.Throws<NullReferenceException>(()=>LegacyNativeDataCoreAttributes.SetCharacterSides(null!));
        Assert.Equal(control.Next(),LegacyNativePoolRandom.GetRandom.Next());Assert.Empty(scope.Queue);
    });

    private static void VarUInt(List<byte> bytes,uint value)
    {
        while(value>=128){bytes.Add((byte)(value|128));value>>=7;}bytes.Add((byte)value);
    }
}
