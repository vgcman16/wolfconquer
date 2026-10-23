using System.Reflection;
using ProtoBuf;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEquipmentQueryPrerequisiteTests
{
    [Fact]
    public Task WholeUpdatePerfectionLevelUsesActualPrestigeAndPlayerIdentityInRequiredProtoFields() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); s.Client.PrestigeLevel=uint.MaxValue;
        s.Client.UpdatePerfectionLevel(s.Native.Packet);
        Assert.Equal(Convert.FromHexString("0E0050080800101118FFFFFFFF0F"),Assert.Single(s.Native.Queue));
        Assert.Equal(4,s.Native.Packet.Position);Assert.Equal(14,s.Native.Packet.Size);Assert.Equal(uint.MaxValue,s.Client.PrestigeLevel);
        s.Native.Queue.Clear();s.Client.Fake=true;s.Client.PrestigeLevel=300;
        s.Client.UpdatePerfectionLevel(s.Native.Packet);
        Assert.Equal(Convert.FromHexString("0B0050080800101118AC02"),NativeSpellTestFixture.Bytes(s.Native.Packet));Assert.Empty(s.Native.Queue);
    });

    [Fact]
    public Task RequiredZeroProtoFieldsRemainPresentAndNullPlayerFaultPrecedesWriter() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();s.Native.Packet.UserTotalRefineLevCreate(new LegacyNativeMsgUserTotalRefineLev.TotalRefineLev());
        Assert.Equal(Convert.FromHexString("0A005008080010001800"),NativeSpellTestFixture.Bytes(s.Native.Packet));
        Fill(s.Native.Packet);s.Client.Player=null!;
        Assert.Throws<NullReferenceException>(()=>s.Client.UpdatePerfectionLevel(s.Native.Packet));
        Assert.Equal(237,s.Native.Packet.Position);Assert.Equal(321,s.Native.Packet.Size);Assert.Empty(s.Native.Queue);
    });

    [Fact]
    public void TotalRefineProtoContractKeepsThreeRequiredUintFieldTags()
    {
        var fields=typeof(LegacyNativeMsgUserTotalRefineLev.TotalRefineLev).GetFields().OrderBy(field=>field.MetadataToken).ToArray();
        Assert.Equal(new[]{"Type","UID","Level"},fields.Select(field=>field.Name));
        Assert.Equal(new[]{1,2,3},fields.Select(field=>field.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        Assert.All(fields,field=>{Assert.Equal(typeof(uint),field.FieldType);Assert.True(field.GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired);});
    }
}
