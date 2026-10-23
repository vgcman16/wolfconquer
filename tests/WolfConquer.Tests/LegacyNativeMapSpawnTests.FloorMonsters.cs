using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapSpawnTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task WholeFloorSpawnerUsesRealMonsterReferenceTwiceAndExactFloorMetadata(bool throughMap) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(); var family = Family(1);
        LegacyNativePoolMonsters.MonsterFamilies[1] = family;
        if (throughMap) map.MonstersColletion = new LegacyNativeMobCollection(700);
        var spell = new LegacyNativeMagicType.Magic(); DateTime before = DateTime.Now.AddSeconds(7);
        bool result = throughMap ? map.AddFloor(scope.Packet, 940, 12, 13, 8, spell, scope.Client, 61, 17, Name: "OwnedFloor") :
            LegacyNativeServerMapPopulation.AddFloor(scope.Packet, map, 940, 12, 13, 8, spell, scope.Client, 61, 17, Name: "OwnedFloor");
        Assert.True(result);
        var roles = map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster).ToArray();
        Assert.Equal(2, roles.Length); Assert.Same(roles[0], roles[1]); var role = Assert.IsType<LegacyNativeMonsterRole>(roles[0]);
        Assert.True(role.IsFloor); Assert.Equal(940u, role.Family.ID); Assert.Equal(throughMap ? 940u : 700u, role.Family.MapID);
        Assert.Equal(25u, role.HitPoints); Assert.Equal(25, role.Family.MaxHealth); Assert.Equal(LegacyNativeMonsterSettings.Lottus, role.Family.Settings);
        Assert.Same(spell, role.DBSpell); Assert.Same(scope.Client, role.OwnerFloor); Assert.Equal((ushort)8, role.SpellLevel);
        Assert.InRange(role.FloorStampTimer, before, DateTime.Now.AddSeconds(7));
        var packet = role.FloorPacket!; Assert.Equal(role.UID, packet.m_UID); Assert.Equal(940u, packet.m_ID);
        Assert.Equal((ushort)12, packet.m_X); Assert.Equal((ushort)13, packet.m_Y); Assert.Equal((ushort)25, packet.MaxLife); Assert.Equal(25u, packet.Life);
        Assert.Equal(LegacyNativeMsgDropID.Effect, packet.DropType); Assert.Equal((byte)13, packet.m_Color); Assert.Equal((byte)2, packet.FlowerType);
        Assert.Equal(17u, packet.ItemOwnerUID); Assert.Equal(61u, packet.GuildID); Assert.Equal("OwnedFloor", packet.Name);
        Assert.Equal(LegacyNativeRoleCore.TqTimer(role.FloorStampTimer), packet.Timer);
        Assert.Equal(Cell.Valid | Cell.Monster, map.cells[12, 13]); Assert.Equal((ushort)2164, Opcode(Assert.Single(scope.Queue)));
        Assert.Equal(1u, family.ID); Assert.Equal(500, family.MaxHealth);
    });

    [Fact]
    public Task FloorSpawnerPreservesColdMapShadowMissMissingFamilyAndFailedTileBoundaries() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(); var spell = new LegacyNativeMagicType.Magic();
        LegacyNativePoolMonsters.MonsterFamilies[1] = Family(1);
        Assert.False(map.AddFloor(scope.Packet, 940, 12, 13, 1, spell, scope.Client, 0, 17));
        Assert.Null(map.MonstersColletion.DMap); Assert.Equal(400000u, LegacyNativeMobCollection.GenerateUid.Count);
        map.MonstersColletion = new LegacyNativeMobCollection(700); LegacyNativePoolMonsters.MonsterFamilies.Clear();
        Assert.False(map.AddFloor(scope.Packet, 940, 12, 13, 1, spell, scope.Client, 0, 17));
        LegacyNativePoolMonsters.MonsterFamilies[1] = Family(1); map.cells[12, 13] = Cell.None;
        Assert.False(map.AddFloor(scope.Packet, 940, 12, 13, 1, spell, scope.Client, 0, 17));
        Assert.Equal(400001u, LegacyNativeMobCollection.GenerateUid.Count);
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster)); Assert.Empty(scope.Queue);
    });
}
