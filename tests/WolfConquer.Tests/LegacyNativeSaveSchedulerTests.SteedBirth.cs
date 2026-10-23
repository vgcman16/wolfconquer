using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSaveSchedulerTests
{
    [Fact]
    public Task GenuineImplicitRaceBirthRetainsAllFiveTablesAndIndependentMutableRows() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var a = new LegacyNativeMsgSteedRace(); var b = new LegacyNativeMsgSteedRace();
        Assert.Equal(new uint[] { 1950, 2064, 2061, 2063, 2062 }, a.RaceSettings.Select(x => x[0]));
        Assert.All(a.RaceSettings, row => Assert.Equal(18, row.Length));
        Assert.Equal(new uint[] { 1950, 88, 149, 420, 431, 4, 65, 174, 621, 123, 243, 60, 214, 334, 70, 346, 459, 100 }, a.RaceSettings[0]);
        Assert.NotSame(a.RaceSettings, b.RaceSettings); Assert.NotSame(a.RaceSettings[0], b.RaceSettings[0]);
        Assert.False(a.CanJoin); Assert.False(a.IsOn); Assert.Equal((ushort)0, a.GateX); Assert.Equal((ushort)0, a.GateY);
        Assert.Null(Field<LegacyNativeGameMap?>(a, "Map")); Assert.Null(Field<LegacyNativeSobNpcRuntime?>(a, "Gate"));
        Assert.Equal(0, Field<int>(a, "Records")); Assert.Null(LegacyNativeMsgSteedRace.Settings);
        Assert.Equal((ushort)1950, LegacyNativeMsgSteedRace.MAPID); Assert.Equal(0u, LegacyNativeMsgSteedRace.RaceRecord);
    });

    [Theory]
    [InlineData(1950u, true)]
    [InlineData(2064u, true)]
    [InlineData(2063u, true)]
    [InlineData(2062u, true)]
    [InlineData(2061u, true)]
    [InlineData(1951u, false)]
    [InlineData(0u, false)]
    [InlineData(4294967295u, false)]
    public void RaceMapAdmissionUsesOnlyTheFiveNativeIds(uint id, bool expected) =>
        Assert.Equal(expected, new LegacyNativeMsgSteedRace().InSteedRace(id));

    [Fact]
    public Task UnknownCreateIdLeavesStateUntouchedWithoutQueryingAnAbsentMapOwner() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var race = new LegacyNativeMsgSteedRace();
        uint[] prior = { 7, 8, 9 }; LegacyNativeMsgSteedRace.Settings = prior;
        LegacyNativeMsgSteedRace.MAPID = 33;
        Set(race, "isOn", true); Set(race, "GateOpen", false);
        LegacyNativePoolMaps.ServerMaps = null;
        race.Create(123456);
        Assert.True(race.CanJoin); Assert.Same(prior, LegacyNativeMsgSteedRace.Settings);
        Assert.Equal((ushort)33, LegacyNativeMsgSteedRace.MAPID); Assert.Null(Field<LegacyNativeGameMap?>(race, "Map"));
    });

    [Fact]
    public Task RecognizedCreateWithAbsentMapOwnerCommitsSettingsAndMapIdBeforeFault() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var race = new LegacyNativeMsgSteedRace();
        LegacyNativePoolMaps.ServerMaps = null;
        Assert.Throws<NullReferenceException>(() => race.Create(2061));
        Assert.Same(race.RaceSettings[2], LegacyNativeMsgSteedRace.Settings);
        Assert.Equal((ushort)2061, LegacyNativeMsgSteedRace.MAPID);
        Assert.Null(Field<LegacyNativeGameMap?>(race, "Map"));
    });

    [Fact]
    public Task WholeCreateReplacesRealStaticAndSobRolesBuildsGateAndOneHundredPotionRoles() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var map = s.Map(1950, false);
        var old = new LegacyNativeStaticRoleRuntime(20, 20) { Map = 1950 }; Assert.True(map.AddStaticRole(old));
        var sob = new LegacyNativeSobNpcRuntime { UID = 123, X = 8, Y = 8, Map = 1950, ObjType = LegacyNativeMapObjectType.SobNpc };
        Assert.True(map.View!.EnterMap(sob)); map.SetFlagNpc(8, 8);
        var race = new LegacyNativeMsgSteedRace { RaceSettings = new[] { RaceRow() } };
        race.Create(1950);
        Assert.Same(map, Field<LegacyNativeGameMap>(race, "Map"));
        Assert.Same(race.RaceSettings[0], LegacyNativeMsgSteedRace.Settings);
        Assert.DoesNotContain(old, map.View.GetAllMapRoles(LegacyNativeMapObjectType.StaticRole));
        var roles = map.View.GetAllMapRoles(LegacyNativeMapObjectType.StaticRole).Cast<LegacyNativeStaticRoleRuntime>().ToArray();
        Assert.Equal(100, roles.Length); Assert.Equal(100, roles.Select(x => x.UID).Distinct().Count());
        Assert.All(roles, role =>
        {
            Assert.True(role.Viable); Assert.Equal(1950u, role.Map);
            Assert.InRange(role.Mesh, 760u, 770u); Assert.InRange(role.Level, (byte)1, (byte)3);
            Assert.Equal((uint)(role.X * 1000 + role.Y), role.UID);
        });
        var gate = Assert.IsType<LegacyNativeSobNpcRuntime>(Assert.Single(map.View.GetAllMapRoles(LegacyNativeMapObjectType.SobNpc)));
        Assert.Equal(19501u, gate.UID); Assert.Equal((ushort)5, gate.X); Assert.Equal((ushort)5, gate.Y);
        Assert.Equal((ushort)621, (ushort)gate.Mesh); Assert.Equal(" ", gate.Name);
        Assert.Equal(LegacyNativeNpcType.Furniture, gate.Type); Assert.Equal((ushort)1, gate.Sort);
        Assert.Equal((ushort)5, race.GateX); Assert.Equal((ushort)5, race.GateY);
        Assert.False(race.IsOn); Assert.False(race.CanJoin); Assert.Equal(0, Field<int>(race, "Records"));
        Assert.False(map.View.Contain(123, 8, 8));
    });

    [Fact]
    public Task WholeRandomCreateUsesTheActualMutableTableAndRealLazyMapDictionary() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Map(1950, false);
        var race = new LegacyNativeMsgSteedRace { RaceSettings = new[] { RaceRow() } };
        race.Create();
        Assert.Equal(100, Field<LegacyNativeGameMap>(race, "Map").View!.GetAllMapRoles(LegacyNativeMapObjectType.StaticRole).Count());
        Assert.Same(race.RaceSettings[0], LegacyNativeMsgSteedRace.Settings);
    });
}
