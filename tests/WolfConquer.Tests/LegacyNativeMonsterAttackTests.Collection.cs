using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAttackTests
{
    [Theory]
    [InlineData(false,false,1,3)] [InlineData(false,false,2,6)] [InlineData(true,false,7,1)] [InlineData(false,true,7,1)]
    public Task WholeCollectionAddUsesTrueConstructorCopiesAndMapMembership(bool boss,bool justOne,byte count,int expected) => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var f=Family();f.Boss=boss?(byte)1:(byte)0;f.SpawnCount=count;
        var collection=new LegacyNativeMobCollection(s.Map.ID){LocationSpawn="owned"};var last=collection.Add(f,true,27,justOne);
        Assert.NotNull(last);var roles=s.Map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster).Cast<LegacyNativeMonsterRole>().ToArray();
        Assert.Equal(expected,roles.Length);Assert.Equal((uint)(400000+expected),LegacyNativeMobCollection.GenerateUid.Count);
        Assert.Equal(roles.Max(m=>m.UID),last.UID);Assert.All(roles,m=>{Assert.Same(s.Map,m.GMap);Assert.NotSame(f,m.Family);Assert.Equal(s.Map.ID,m.Map);Assert.Equal(27u,m.DynamicID);Assert.True(m.RemoveOnDead);Assert.True(s.Map.MonsterOnTile(m.X,m.Y));Assert.Equal(m.X,m.RespawnX);Assert.Equal(m.Y,m.RespawnY);Assert.Equal("owned",m.LocationSpawn);});
        Assert.All(roles,m=>Assert.Same(last.Family.ItemGenerator,m.Family.ItemGenerator));
    });

    [Fact]
    public Task FailedPlacementStillConstructsAllMonstersConsumesUidAndRetriesExactlyTenTimes() => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var f=Family();f.SpawnX=f.MaxSpawnX=1;f.SpawnY=f.MaxSpawnY=2;f.SpawnCount=1;s.Map.cells[1,2]=Cell.None;
        var collection=new LegacyNativeMobCollection(s.Map.ID);LegacyNativePoolRandom.GetRandom=new(67);var control=new Random(67);
        var familyControl=new Random(69);LegacyNativeMonsterFamily.Random=new Random(69);
        Assert.Null(collection.Add(f));
        for(int i=0;i<3;i++){familyControl.Next(10,20);control.Next(0,8);for(int attempt=0;attempt<11;attempt++){control.Next(1,1);control.Next(2,2);}}
        Assert.Equal(control.Next(),LegacyNativePoolRandom.GetRandom.Next());Assert.Equal(familyControl.Next(),LegacyNativeMonsterFamily.Random.Next());
        Assert.Equal(400003u,LegacyNativeMobCollection.GenerateUid.Count);Assert.Empty(s.Map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster));
    });

    [Theory]
    [InlineData(1011)] [InlineData(3071)] [InlineData(1770)] [InlineData(1771)] [InlineData(1772)] [InlineData(1773)] [InlineData(1774)] [InlineData(1775)] [InlineData(1777)] [InlineData(1782)] [InlineData(1785)] [InlineData(1786)] [InlineData(1787)] [InlineData(1794)]
    public Task SpecialMapDensityCanProduceZeroWithoutAnyConstruction(int mapId) => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var map=CreateMap(mapId);LegacyNativePoolMaps.ServerMaps!.Add((uint)mapId,map);
        var f=Family();f.SpawnCount=2;f.SpawnX=10;f.MaxSpawnX=11;f.SpawnY=10;f.MaxSpawnY=11;
        Assert.Null(new LegacyNativeMobCollection((uint)mapId).Add(f));Assert.Equal(400000u,LegacyNativeMobCollection.GenerateUid.Count);Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster));
    });

    [Fact]
    public Task MissingMapDoesNotGetSynthesizedAndFaultsAfterGenuineBirthAndLocationDraws() => NativePacketTestThread.Run(() =>
    {
        using var s=new Scope();var collection=new LegacyNativeMobCollection(999);var f=Family();f.SpawnX=f.MaxSpawnX=1;f.SpawnY=f.MaxSpawnY=2;
        Assert.Throws<NullReferenceException>(()=>collection.Add(f,justone:true));Assert.Null(collection.DMap);Assert.Equal(400001u,LegacyNativeMobCollection.GenerateUid.Count);
        Assert.False(LegacyNativePoolMaps.ServerMaps!.TryGetValue(999,out _));
    });
}
