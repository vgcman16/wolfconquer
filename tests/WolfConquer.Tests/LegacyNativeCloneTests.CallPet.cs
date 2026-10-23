using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCloneTests
{
    [Theory]
    [InlineData((ushort)0,1)] [InlineData((ushort)3,1)] [InlineData((ushort)4,2)] [InlineData((ushort)5,2)]
    public Task WholeCallPetCloneCaseCreatesNativeCountAndCompleteViewPackets(ushort level,int count)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var map=scope.Map();var peerQueue=new Queue<byte[]>();scope.Peer(map,peerQueue);
        var cast=scope.Spell(12090,level);
        LegacyNativeCallPet.Execute(scope.Client,cast.Attack,scope.Packet,cast.Rows);
        Assert.Equal(count,scope.Player.MyClones.Count);Assert.Equal(700100u+(uint)count,LegacyNativeClone.CounterUID.Count);
        Assert.Equal(count==1?new[]{3}:new[]{3,10003},scope.Player.MyClones.Select(clone=>clone.cloneflag).ToArray());
        Assert.Equal(count==1?new ushort[]{2227,2176,2046,2239}:new ushort[]{2227,2176,2227,2176,2046,2046,2239},scope.Queue.Select(Opcode).ToArray());
        Assert.Equal(count==1?new ushort[]{2227,2046,2239}:new ushort[]{2227,2227,2046,2046,2239},peerQueue.Select(Opcode).ToArray());
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster));
        Assert.Equal(100,scope.Player.Stamina);Assert.Equal((ushort)100,scope.Player.Mana);
    });

    [Fact]
    public Task ExistingCloneToggleRemovesBeforeAnySpellVerificationOrAttackAccess()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();LegacyNativeClone.CreateShadowClone(scope.Player,scope.Packet);
        var held=scope.Player.MyClones;scope.Queue.Clear();scope.Client.Equipment=null;scope.Client.MySpells=null;
        LegacyNativeCallPet.Execute(scope.Client,null!,scope.Packet,null!);
        Assert.Same(held,scope.Player.MyClones);Assert.Empty(held);Assert.Equal(700102u,LegacyNativeClone.CounterUID.Count);
        Assert.Equal(new ushort[]{2046,2046},scope.Queue.Select(Opcode).ToArray());
    });

    [Fact]
    public Task RejectedSpellAndUnrecognizedVerifiedSpellDoNotAcknowledgeCloneBirth()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var cast=scope.Spell(12090,3);cast.Rows[3].UseStamina=200;
        LegacyNativeCallPet.Execute(scope.Client,cast.Attack,scope.Packet,cast.Rows);
        Assert.Empty(scope.Player.MyClones);Assert.Equal(700100u,LegacyNativeClone.CounterUID.Count);Assert.Empty(scope.Queue);
        var unknown=scope.Spell(600,0);LegacyNativeCallPet.Execute(scope.Client,unknown.Attack,scope.Packet,unknown.Rows);
        Assert.Empty(scope.Player.MyClones);Assert.Equal(700100u,LegacyNativeClone.CounterUID.Count);Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData((ushort)4,3u)] [InlineData((ushort)5,9u)]
    public Task FullThundercloudCaseCreatesActualFloorMonsterPetInfoSpawnAndAction(ushort level,uint petFlag)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var map=scope.Map();map.MonstersColletion=new(700);scope.ThunderFamily();
        var cast=scope.Spell(12840,level);DateTime before=DateTime.Now;
        LegacyNativeCallPet.Execute(scope.Client,cast.Attack,scope.Packet,cast.Rows);
        var roles=map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster).ToArray();Assert.Equal(2,roles.Length);Assert.Same(roles[0],roles[1]);
        var role=Assert.IsType<LegacyNativeMonsterRole>(roles[0]);Assert.Equal(400001u,role.UID);Assert.Equal(999996641u,role.Family.ID);
        Assert.True(role.IsFloor);Assert.Equal(980u,role.Mesh);Assert.Equal("Thundercloud",role.Name);Assert.Equal((byte)140,role.Level);
        Assert.Equal(14u,role.HitPoints);Assert.Equal(14,role.Family.MaxHealth);Assert.Equal((byte)1,role.Boss);Assert.Equal(petFlag,role.PetFlag);
        Assert.Equal(1000,role.Family.AttackSpeed);Assert.Equal(13190u,role.Family.SpellId);Assert.Equal(10000,role.StampFloorSecounds);
        Assert.Same(scope.Client,role.OwnerFloor);Assert.Same(LegacyNativePoolMagic.Magic[13190][0],role.DBSpell);
        Assert.InRange(role.FloorStampTimer,before.AddSeconds(1),DateTime.Now.AddSeconds(1));
        Assert.InRange(role.RemoveFloor,before.AddSeconds(25),DateTime.Now.AddSeconds(25));
        Assert.Equal(3843u,role.FloorPacket!.m_ID);Assert.Equal((ushort)2,role.FloorPacket.MaxLife);Assert.Equal(14u,role.FloorPacket.Life);
        Assert.Equal(new ushort[]{2239,2192,2227,2046},scope.Queue.Select(Opcode).ToArray());Assert.Empty(scope.Player.MyClones);
    });

    [Fact]
    public Task DoubleThunderUsesGenuineEquippedRuneAndCreatesTwoDistinctNativePets()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var map=scope.Map();map.MonstersColletion=new(700);scope.ThunderFamily();scope.DoubleThunderRune();
        var cast=scope.Spell(12840,5);int selected=-1;
        for(int seed=1;seed<=256;seed++)
        {
            var control=new Random(seed);control.Next(0,8);control.Next(12,13);control.Next(13,14);
            int x=control.Next(7,17),y=control.Next(8,18);if(x!=12||y!=13){selected=seed;break;}
        }
        Assert.InRange(selected,1,256);LegacyNativePoolRandom.GetRandom=new LegacyNativeSafeRandom(selected);
        LegacyNativeCallPet.Execute(scope.Client,cast.Attack,scope.Packet,cast.Rows);
        var roles=map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster).Cast<LegacyNativeMonsterRole>().ToArray();
        Assert.Equal(4,roles.Length);Assert.Equal(new uint[]{400001,400002},roles.Select(role=>role.UID).Distinct().Order().ToArray());
        Assert.All(roles,role=>{Assert.Equal(9u,role.PetFlag);Assert.Equal("Thundercloud",role.Name);Assert.Same(scope.Client,role.OwnerFloor);});
        Assert.Equal(2,scope.Queue.Count(frame=>Opcode(frame)==2192));Assert.Equal(2,scope.Queue.Count(frame=>Opcode(frame)==2227));
        Assert.Equal(1,scope.Queue.Count(frame=>Opcode(frame)==2239));Assert.Equal(400002u,LegacyNativeMobCollection.GenerateUid.Count);
    });

    [Fact]
    public Task ThundercloudMissingMagicRowFaultsAfterNativeSpawnPrefixWithoutPetSuccessPackets()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var map=scope.Map();map.MonstersColletion=new(700);scope.ThunderFamily();
        LegacyNativePoolMagic.Magic.Clear();var cast=scope.Spell(12840,5);
        Assert.Throws<KeyNotFoundException>(()=>LegacyNativeCallPet.Execute(scope.Client,cast.Attack,scope.Packet,cast.Rows));
        var role=Assert.IsType<LegacyNativeMonsterRole>(Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster)));
        Assert.True(role.IsFloor);Assert.Equal("Thundercloud",role.Name);Assert.Null(role.OwnerFloor);Assert.Null(role.DBSpell);
        Assert.Equal(new ushort[]{2239},scope.Queue.Select(Opcode).ToArray());
    });

    [Fact]
    public Task ThundercloudMissingFamilyKeepsInitialAnimationButNeverInventsMonsterMetadata()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var map=scope.Map();map.MonstersColletion=new(700);var cast=scope.Spell(12840,5);
        LegacyNativeCallPet.Execute(scope.Client,cast.Attack,scope.Packet,cast.Rows);
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster));Assert.Empty(scope.Player.MyClones);
        Assert.Equal(400000u,LegacyNativeMobCollection.GenerateUid.Count);Assert.Equal(new ushort[]{2239},scope.Queue.Select(Opcode).ToArray());
    });

    [Fact]
    public Task ThundercloudMissingCollectionFaultsAfterAnimationWithoutInventingMapMutationOwner()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var map=scope.Map();scope.ThunderFamily();var cast=scope.Spell(12840,5);
        Assert.Null(map.MonstersColletion);
        Assert.Throws<NullReferenceException>(()=>LegacyNativeCallPet.Execute(scope.Client,cast.Attack,scope.Packet,cast.Rows));
        Assert.Null(map.MonstersColletion);Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster));
        Assert.Equal(400000u,LegacyNativeMobCollection.GenerateUid.Count);Assert.Equal(new ushort[]{2239},scope.Queue.Select(Opcode).ToArray());
    });
}
