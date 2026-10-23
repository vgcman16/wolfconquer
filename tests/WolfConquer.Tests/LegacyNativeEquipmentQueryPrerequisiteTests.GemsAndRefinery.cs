using WolfConquer.Core.World;
using Equip = WolfConquer.Core.World.LegacyNativeEquipItemStorage;
using Gem = WolfConquer.Core.World.LegacyNativeGameItemFlags.Gem;
using RefType = WolfConquer.Core.World.LegacyNativeRifinery.RefineryType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEquipmentQueryPrerequisiteTests
{
    [Theory]
    [InlineData((byte)103,500,0,0)] [InlineData((byte)123,500,0,0)]
    [InlineData((byte)102,300,0,0)] [InlineData((byte)122,300,0,0)]
    [InlineData((byte)101,100,0,0)] [InlineData((byte)121,100,0,0)]
    [InlineData((byte)11,5,5,0)] [InlineData((byte)12,10,10,0)] [InlineData((byte)13,15,15,0)]
    [InlineData((byte)2,10,0,10)] [InlineData((byte)3,15,0,15)]
    [InlineData((byte)71,2,0,0)] [InlineData((byte)72,4,0,0)] [InlineData((byte)73,6,0,0)]
    [InlineData((byte)31,10,0,10)] [InlineData((byte)32,15,0,15)] [InlineData((byte)33,25,0,0)]
    public Task EquipmentGemWrapperUsesActualClientArrayAndNativeSpecialPercentSideEffects(byte gem,int value,int physical,int magic) => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); ushort[] held=s.Client.Gems; var status=s.Status;
        s.Equipment.AddGem((Gem)gem);
        Assert.Same(held,s.Client.Gems); Assert.Same(status,s.Status);
        for(int index=0;index<held.Length;index++)Assert.Equal(index==gem/10?(ushort)value:(ushort)0,held[index]);
        Assert.Equal((uint)physical,status.PhysicalPercent); Assert.Equal((uint)magic,status.MagicPercent);
        Assert.Equal((byte)(gem>=71&&gem<=73?1:0),s.Client.TortoiseGems); Assert.Empty(s.Native.Queue);
    });

    [Fact]
    public Task UnsupportedGemSkipsOwnerButSupportedGemRetainsTortoiseCapAndNullArrayFaultPrefix() => NativePacketTestThread.Run(()=>
    {
        var ownerless=new Equip(null); foreach(byte value in new byte[]{0,1,41,51,61,255})ownerless.AddGem((Gem)value);
        Assert.Throws<NullReferenceException>(()=>ownerless.AddGem(Gem.SuperDragonGem));
        using var s=new Scope(); s.Client.TortoiseGems=8; s.Equipment.AddGem(Gem.SuperTortoiseGem);
        Assert.Equal((byte)9,s.Client.TortoiseGems); Assert.Equal((ushort)0,s.Client.Gems[7]);
        s.Equipment.AddGem(Gem.SuperDragonGem); Assert.Equal(15u,s.Status.PhysicalPercent); Assert.Equal((ushort)0,s.Client.Gems[1]);
        s.Client.TortoiseGems=0; s.Client.Gems=null!;
        Assert.Throws<NullReferenceException>(()=>s.Equipment.AddGem(Gem.NormalTortoiseGem));
        Assert.Equal((byte)1,s.Client.TortoiseGems); Assert.Null(s.Client.Gems);
    });

    private sealed class RefineryFiles : IDisposable
    {
        private readonly string oldLocation=LegacyNativeServerConfig.DbLocation;
        private readonly LegacyNativeRifinery? oldOwner=LegacyNativePoolItems.RifineryItems;
        private readonly Dictionary<uint,Dictionary<uint,LegacyNativeRifinery.Item>> oldCache=LegacyNativeItemTypeRuneMetadata.Refinary;
        private readonly string root=Path.Combine(Path.GetTempPath(),"wolf-equipment-refinery-"+Guid.NewGuid().ToString("N"));
        internal LegacyNativeRifinery Owner {get;}
        internal RefineryFiles()
        {
            Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root,"Rifinery.txt"),"");
            LegacyNativeServerConfig.DbLocation=root+Path.DirectorySeparatorChar; LegacyNativeItemTypeRuneMetadata.Refinary=new();
            Owner=new LegacyNativeRifinery(); LegacyNativePoolItems.RifineryItems=Owner;
        }
        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation=oldLocation; LegacyNativePoolItems.RifineryItems=oldOwner; LegacyNativeItemTypeRuneMetadata.Refinary=oldCache;
            string exact=Path.GetFullPath(root);
            string prefix=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar+"wolf-equipment-refinery-";
            if(!exact.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Owned refinery path escaped temporary root.");
            if(Directory.Exists(exact))Directory.Delete(exact,true);
        }
    }

    public static IEnumerable<object[]> RefineryCases()
    {
        yield return new object[]{1,"PhysicalDamageDecrease",5u}; yield return new object[]{2,"CriticalStrike",500u};
        yield return new object[]{3,"SkillCStrike",500u}; yield return new object[]{4,"Immunity",500u};
        yield return new object[]{5,"Breakthrough",50u}; yield return new object[]{6,"Counteraction",23u};
        yield return new object[]{7,"Detoxication",5u}; yield return new object[]{8,"Block",203u};
        yield return new object[]{9,"Penetration",500u}; yield return new object[]{10,"MaxHitpoints",5u};
        yield return new object[]{11,"PhysicalDamageIncrease",5u}; yield return new object[]{12,"MagicDamageIncrease",5u};
    }

    [Theory]
    [MemberData(nameof(RefineryCases))]
    public Task RefineryUsesBothRealRowTypesAndPreservesAsymmetricSecondaryBlockCounteraction(int type,string field,uint expected) => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); using var files=new RefineryFiles();
        var row=new LegacyNativeRifinery.Item{ItemID=999,Type=(RefType)type,Type2=(RefType)type,Procent=2,Procent2=3,ForItemPosition=0};
        files.Owner[123]=row; var status=s.Status;
        s.Equipment.IncreaseRifainaryStatus(123); Assert.Same(status,s.Status);
        foreach(var physical in typeof(LegacyNativeMsgStatus).GetFields().Where(info=>info.FieldType==typeof(uint)))
            Assert.Equal(physical.Name==field?expected:0u,(uint)physical.GetValue(status)!);
        Assert.Same(row,files.Owner[123]); Assert.Empty(s.Native.Queue);
    });

    [Fact]
    public Task MissingRefineryLogsExactNativeIdWithoutStatusTouchAndNullTableKeepsFault() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); using var files=new RefineryFiles(); s.Client.Status=null!;
        TextWriter old=Console.Out; using var output=new StringWriter();
        try{Console.SetOut(output);s.Equipment.IncreaseRifainaryStatus(123);Assert.Equal("error refinery id 123"+Environment.NewLine,output.ToString());}
        finally{Console.SetOut(old);}
        files.Owner[123]=new(){Type=(RefType)255,Type2=RefType.None}; s.Equipment.IncreaseRifainaryStatus(123); Assert.Null(s.Client.Status);
        LegacyNativePoolItems.RifineryItems=null; Assert.Throws<NullReferenceException>(()=>s.Equipment.IncreaseRifainaryStatus(123));
    });
}
