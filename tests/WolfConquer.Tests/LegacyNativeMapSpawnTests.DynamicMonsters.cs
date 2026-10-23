using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapSpawnTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ServerSpawnerUsesActualMapIdentityAndWholeActionSpawnStringEarthAndHpGraph(bool large) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map();
        var family = Family(hp: large ? 70000 : 500); LegacyNativePoolMonsters.MonsterFamilies[111] = family;
        LegacyNativeServerMapPopulation.AddMapMonster(scope.Packet, map, 111, 12, 13, 1, 1, 7,
            RemoveOnDead: true, m_effect: LegacyNativeMsgItemPacket.EffectMonsters.Night, streffect: "owned-effect");
        var role = SingleRole<LegacyNativeMonsterRole>(map, LegacyNativeMapObjectType.Monster);
        Assert.Equal(700u, role.Map); Assert.Equal(700u, role.Family.MapID); Assert.True(role.RemoveOnDead);
        Assert.Same(map, map.MonstersColletion.DMap); Assert.Equal(400001u, role.UID);
        Assert.Equal(Cell.Valid | Cell.Monster, map.cells[12, 13]); Assert.Equal((ushort)12, family.SpawnX);
        ushort[] opcodes = scope.Queue.Select(Opcode).ToArray();
        Assert.Equal(large ? new ushort[] { 2046, 2227, 2036, 2164, 2036, 2176, 2176 } :
            new ushort[] { 2046, 2227, 2036, 2164, 2036 }, opcodes);
        var earth = scope.Queue.Single(frame => Opcode(frame) == 2164);
        Assert.Equal((uint)LegacyNativeMsgItemPacket.EffectMonsters.Night, BitConverter.ToUInt32(earth, 4));
        Assert.Equal((byte)LegacyNativeMsgDropID.Earth, earth[18]);
    });

    [Fact]
    public Task MapSpawnerRetainsShadowedIdColdMissRatherThanRepairingItToThisMap() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(); LegacyNativePoolMonsters.MonsterFamilies[111] = Family();
        map.AddMapMonster(scope.Packet, 111, 12, 13, 1, 1, 1);
        Assert.NotNull(map.MonstersColletion); Assert.Null(map.MonstersColletion.DMap);
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster)); Assert.Empty(scope.Queue);
        Assert.Equal(400000u, LegacyNativeMobCollection.GenerateUid.Count);
    });

    [Fact]
    public Task MapSpawnerCanBindNativeShadowIdMapAndNeverAdoptsThatMapAsThis() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(); var shadow = scope.Map(111, admitPlayer: false);
        LegacyNativePoolMonsters.MonsterFamilies[111] = Family();
        map.AddMapMonster(scope.Packet, 111, 12, 13, 1, 1, 1, streffect: null!);
        var role = SingleRole<LegacyNativeMonsterRole>(shadow, LegacyNativeMapObjectType.Monster);
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster));
        Assert.Same(shadow, map.MonstersColletion.DMap); Assert.Same(map.MonstersColletion, shadow.MonstersColletion);
        Assert.Equal(111u, role.Map); Assert.Empty(scope.Queue); Assert.Same(map, scope.Client.Map);
    });

    [Fact]
    public Task MapSpawnerWithExistingCollectionKeepsFamilyShadowIdButRealRoleMapAndSendsNoNullString() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(); LegacyNativePoolMonsters.MonsterFamilies[111] = Family();
        map.MonstersColletion = new LegacyNativeMobCollection(700);
        map.AddMapMonster(scope.Packet, 111, 12, 13, 1, 1, 1, streffect: null!);
        var role = SingleRole<LegacyNativeMonsterRole>(map, LegacyNativeMapObjectType.Monster);
        Assert.Equal(700u, role.Map); Assert.Equal(111u, role.Family.MapID);
        Assert.Equal(new ushort[] { 2046, 2227 }, scope.Queue.Select(Opcode).ToArray());
    });

    [Fact]
    public Task FailedDynamicSpawnKeepsNativeUidAttemptAndDoesNotEmitSuccessFrames() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(); LegacyNativePoolMonsters.MonsterFamilies[111] = Family();
        map.cells[12, 13] = Cell.None;
        LegacyNativeServerMapPopulation.AddMapMonster(scope.Packet, map, 111, 12, 13, 1, 1, 1);
        Assert.Equal(400001u, LegacyNativeMobCollection.GenerateUid.Count);
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster)); Assert.Empty(scope.Queue);
        LegacyNativeServerMapPopulation.AddMapMonster(scope.Packet, map, 999, 12, 13, 1, 1, 1);
        Assert.Equal(400001u, LegacyNativeMobCollection.GenerateUid.Count); Assert.Empty(scope.Queue);
    });
}
