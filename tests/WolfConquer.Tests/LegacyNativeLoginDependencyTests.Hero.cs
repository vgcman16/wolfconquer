using System.Reflection;
using System.Text;
using ProtoBuf;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeLoginDependencyTests
{
    [Fact]
    public void EntireHeroContractKeepsFiftyRequiredTagsAndOriginalNumericKinds()
    {
        string[] names={"UID","AparenceType","Mesh","Hair","Money","ConquerPoints","Experience","ServerInfoID","SetLocationType",
            "SpecialTitleID","SpecialWingID","VirtutePoints","Strength","Agility","Vitality","Spirit","Atributes","HitPoints","Mana","PKPoints",
            "Level","Class","FirstClass","SecoundeClass","NobilityRank","Reborn","inittransfer","QuizPoints","MainFlag","Enilghten",
            "EnlightenReceive","IDK_32","IDK_33","VipLevel","MyTitle","BoundConquerPoints","ActiveSublass","SubClassHashPoint",
            "RacePoints","CountryID","IDK_41","DominoCode","ClassExperience","IDK_44","IDK_45","Name","Spouse","IDK_48","IDK_49","Bayo"};
        var fields=typeof(LegacyNativeHeroInfoPackets.HeroInfoProto).GetFields(BindingFlags.Public|BindingFlags.Instance)
            .OrderBy(p=>p.GetCustomAttribute<ProtoMemberAttribute>()!.Tag).ToArray();
        Assert.Equal(names,fields.Select(p=>p.Name));
        for(int i=0;i<50;i++) { var tag=fields[i].GetCustomAttribute<ProtoMemberAttribute>()!;Assert.Equal(i+1,tag.Tag);Assert.True(tag.IsRequired); }
        Assert.Equal(typeof(long),fields[4].FieldType);Assert.Equal(typeof(uint),fields[5].FieldType);
        Assert.Equal(typeof(ushort),fields[7].FieldType);Assert.Equal(typeof(ushort),fields[8].FieldType);
        Assert.Equal(typeof(ulong),fields[26].FieldType);Assert.Equal(typeof(long),fields[42].FieldType);
        var row=new LegacyNativeHeroInfoPackets.HeroInfoProto();Assert.Null(row.Name);Assert.Null(row.Spouse);Assert.Null(row.Bayo);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("Wolf")] [InlineData("狼")]
    public Task WholeLoaderHeroInfoWritesLiteral22210FrameAndNativeNarrowing(string? name) => NativePacketTestThread.Run(() =>
    {
        var oldRandom=LegacyNativePoolRandom.GetRandom;var oldReset=LegacyNativePoolRandom.ResetRandom;
        var oldServer=LegacyNativeGroupServerList.MyServerInfo;
        LegacyNativePoolRandom.GetRandom=new(71);LegacyNativePoolRandom.ResetRandom=new DateTime(2026,1,1);
        try
        {
            using var native=new NativeSpellTestFixture();var player=native.Owner.Player;
            player.UID=0x12345678;player.Name=name!;
            typeof(LegacyNativePlayerRuntime).GetField("_level",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(player,(ushort)513);
            typeof(LegacyNativePlayerRuntime).GetField("_class",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(player,0xDEADBEEFu);
            player.NobilityRank=(LegacyNativeNobilityRank)255;player.VipLevel=7;
            LegacyNativeGroupServerList.MyServerInfo=new() { ID=0x10002,Name="owned" };
            uint mesh=player.Mesh;
            var expected=new List<byte>(BitConverter.GetBytes((ushort)0));expected.AddRange(BitConverter.GetBytes((ushort)22210));
            expected.AddRange(BitConverter.GetBytes(0x12345678u));expected.AddRange(BitConverter.GetBytes(mesh));
            expected.AddRange(BitConverter.GetBytes((ushort)2));expected.Add(1);expected.Add(255);expected.Add(7);
            expected.AddRange(BitConverter.GetBytes(0xDEADBEEFu));expected.AddRange(BitConverter.GetBytes((ushort)100));
            expected.Add(1);byte[] text=Encoding.UTF8.GetBytes(name??"");expected.AddRange(BitConverter.GetBytes((ushort)text.Length));expected.AddRange(text);
            byte[] size=BitConverter.GetBytes((ushort)expected.Count);expected[0]=size[0];expected[1]=size[1];
            var returned=native.Packet.LoaderHeroInfo(player);
            Assert.Same(native.Packet,returned);Assert.Equal(expected.ToArray(),NativeSpellTestFixture.Bytes(returned));
        }
        finally { LegacyNativePoolRandom.GetRandom=oldRandom;LegacyNativePoolRandom.ResetRandom=oldReset;LegacyNativeGroupServerList.MyServerInfo=oldServer; }
    });

    [Fact]
    public Task HeroLoaderMissingIdentityFaultPreservesWrittenUidMeshAndDoesNotFinalize() => NativePacketTestThread.Run(() =>
    {
        var oldRandom=LegacyNativePoolRandom.GetRandom;var oldReset=LegacyNativePoolRandom.ResetRandom;
        var oldServer=LegacyNativeGroupServerList.MyServerInfo;
        LegacyNativePoolRandom.GetRandom=new(71);LegacyNativePoolRandom.ResetRandom=new DateTime(2026,1,1);
        try
        {
            using var native=new NativeSpellTestFixture();LegacyNativeGroupServerList.MyServerInfo=null!;
            native.Packet.Size=321;Assert.Throws<NullReferenceException>(()=>native.Packet.LoaderHeroInfo(native.Owner.Player));
            Assert.Equal(12,native.Packet.Position);Assert.Equal(321,native.Packet.Size);
        }
        finally { LegacyNativePoolRandom.GetRandom=oldRandom;LegacyNativePoolRandom.ResetRandom=oldReset;LegacyNativeGroupServerList.MyServerInfo=oldServer; }
    });
}
