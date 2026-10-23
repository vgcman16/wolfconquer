using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAttackTests
{
    [Theory]
    [InlineData("DropHp",111u,50)] [InlineData("DropMp",222u,50)]
    [InlineData("DragonBalls",1088000u,150)] [InlineData("Letter14",3303131u,400)]
    [InlineData("LetterC",3303124u,200)] [InlineData("LetterO",3303125u,200)]
    [InlineData("LetterN",3303126u,200)] [InlineData("LetterQ",3303127u,200)]
    [InlineData("LetterU",3303128u,200)] [InlineData("LetterE",3303129u,200)]
    [InlineData("LetterR",3303130u,200)] [InlineData("Chi100",729476u,100)]
    [InlineData("Study20",723341u,70)] [InlineData("MoonBox",721080u,700)]
    [InlineData("Chi300",729478u,200)] [InlineData("Bomb",721261u,300)]
    [InlineData("LuckyAmulet",723087u,300)]
    public Task EveryNativeSpecialCounterUsesGenuineTableRowAndEarlyReturn(string watcherName,uint id,int tick) => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var f=Family();f.DropHPItem=111;f.DropMPItem=222;var generator=new LegacyNativeMobItemGenerator(f);
        var watcher=Watcher(generator,watcherName);for(int i=1;i<tick;i++)Assert.False((bool)watcher);
        var item=new LegacyNativeItemRuneMetadata {ID=id,Name="Owned"};LegacyNativePoolItems.ItemsBase!.Add(id,item);
        uint result=generator.GenerateItemId(1001,out byte quality,out bool special,out var row);
        Assert.Equal(id,result);Assert.Equal((byte)0,quality);Assert.True(special);Assert.Same(item,row);
        Assert.False((bool)watcher);Assert.Empty(s.Queue);
    });

    [Fact]
    public Task ExplicitSpecialsPrecedeAllOtherCountersAndKeepSharedRateObject() => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var f=Family();f.DropSpecials=[new(711u,1)];var generator=new LegacyNativeMobItemGenerator(f);
        var item=new LegacyNativeItemRuneMetadata {ID=711};LegacyNativePoolItems.ItemsBase!.Add(711,item);
        Assert.Equal(711u,generator.GenerateItemId(700,out byte quality,out bool special,out var row));Assert.Equal((byte)1,quality);Assert.True(special);Assert.Same(item,row);
        var hp=Watcher(generator,"DropHp");for(int i=1;i<50;i++)Assert.False((bool)hp);Assert.True((bool)hp);
        f.DropSpecials=null;Assert.Throws<NullReferenceException>(()=>generator.GenerateItemId(700,out _,out _,out _));
    });

    [Fact]
    public Task PurityCountersShortCircuitExactlyAsSource() => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var generator=new LegacyNativeMobItemGenerator(Family());
        for(int call=1;call<=65;call++)Assert.Equal(call%20==0?(byte)1:call is 31 or 63?(byte)2:(byte)0,generator.GeneratePurity());
    });

    [Theory]
    [InlineData(1)] [InlineData(7)] [InlineData(3)] [InlineData(4)]
    public Task SoulsUseActualNonemptyTablesAndTheSharedRandomOwner(ushort level) => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();LegacyNativeItemTypeRuneMetadata.PurificationItems[level]=new(){{777,new(){ID=777}}};LegacyNativeItemTypeRuneMetadata.Accessorys[888]=new(){ID=888};
        var generator=new LegacyNativeMobItemGenerator(Family());LegacyNativePoolRandom.GetRandom=new(79);var rng=new Random(79);var expected=new List<uint>();
        byte soulCount=(byte)((rng.Next()%1000)%3);for(int i=0;i<(soulCount==0?1:soulCount);i++){rng.Next(0,1);expected.Add(777);}
        byte accessoryCount=(byte)((rng.Next()%1000)%3);for(int i=0;i<accessoryCount;i++){rng.Next(0,1);expected.Add(888);}expected.Add(level<=3?3001044u:729463u);
        Assert.Equal(expected,generator.GenerateSoulsItems(level));Assert.Equal(rng.Next(),LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData(0u)] [InlineData(409999u)] [InlineData(410000u)] [InlineData(601999u)] [InlineData(602000u)] [InlineData(4294967295u)]
    public Task SocketAndBlessGeneratorsPreserveExactDrawAdmission(uint itemId) => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var generator=new LegacyNativeMobItemGenerator(Family());LegacyNativePoolRandom.GetRandom=new(83);var rng=new Random(83);
        byte expected=0;if(itemId>=410000&&itemId<=601999){int n=rng.Next(0,1000)%100;expected=n<5?(byte)2:n<20?(byte)1:(byte)0;}
        Assert.Equal(expected,generator.GenerateSocketCount(itemId));Assert.Equal(rng.Next(),LegacyNativePoolRandom.GetRandom.Next());
        LegacyNativePoolRandom.GetRandom=new(89);rng=new Random(89);expected=0;if(rng.Next(0,1000)<250){int n=rng.Next(0,100);expected=n<1?(byte)5:n<6?(byte)3:(byte)0;}
        Assert.Equal(expected,generator.GenerateBless());Assert.Equal(rng.Next(),LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public Task GoldIgnoresItsTwoSourceUnusedOptionsAndUsesRealMoneyClassifier(bool normal,bool twin) => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var generator=new LegacyNativeMobItemGenerator(Family());LegacyNativePoolRandom.GetRandom=new(97);var rng=new Random(97);uint amount=(uint)rng.Next(1,200000);
        uint expectedId=amount<100?1090000u:amount<399?1090010u:amount<5099?1090020u:amount<8099?1091000u:amount<12099?1091010u:1091020u;
        Assert.Equal(amount,generator.GenerateGold(out uint id,normal,twin));Assert.Equal(expectedId,id);Assert.Equal(rng.Next(),LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData(0,0)] [InlineData(1,140)] [InlineData(2,139)] [InlineData(3,140)] [InlineData(4,140)] [InlineData(5,140)]
    [InlineData(6,136)] [InlineData(8,129)] [InlineData(10,100)] [InlineData(11,100)] [InlineData(12,0)] [InlineData(17,30)] [InlineData(19,100)] [InlineData(65535,0)]
    public Task NativeItemMaxLevelKeepsAllLiteralBranches(ushort position,byte expected) => NativePacketTestThread.Run(() =>
    {
        Assert.Equal(expected,LegacyNativeItemTypeRuneMetadata.ItemMaxLevel((LegacyNativeGameItemFlags.ConquerItem)position));
    });

    [Theory]
    [InlineData(0,"",0)] [InlineData(2,"Wolf",0)] [InlineData(10,"龙拳",65535)] [InlineData(257,"abcdefghijklmnopqrstuvwxyz123456789",5)]
    public Task FullFloorCodecPreservesFixedWidthTruncationAndDefaultLife(ushort drop,string name,ushort maxLife) => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var item=LegacyNativeMsgItemPacket.Create();item.DropType=(LegacyNativeMsgDropID)drop;item.Name=name;item.MaxLife=maxLife;item.Plus=31;item.m_Color=97;
        s.Packet.Seek(300);s.Packet.ItemPacketCreate(item);Assert.Equal(FloorWire(item),NativeSpellTestFixture.Bytes(s.Packet));Assert.Equal(4,s.Packet.Position);
        s.Packet.Seek(4);s.Packet.Write(0xAABBCCDDu);s.Packet.Seek(4);s.Packet.GetItemPacket(out uint uid);Assert.Equal(0xAABBCCDDu,uid);Assert.Equal(8,s.Packet.Position);
    });
}
