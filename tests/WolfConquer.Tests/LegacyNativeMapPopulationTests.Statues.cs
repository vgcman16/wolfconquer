using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapPopulationTests
{
    [Fact]
    public Task CachedStaticStatueCopiesOwnedRawBytesBeforeTouchingLiveUserOrStatusState() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); byte[] cached = [9, 8, 7, 6, 5];
        var statue = new LegacyNativeStatue { Static = true, StatuePacket = cached };
        var sob = new LegacyNativeSobNpcRuntime(statue) { BitVector = null! };
        scope.Packet.Seek(17);
        Assert.Same(scope.Packet, sob.GetArray(scope.Packet, false, false));
        Assert.Equal(cached, Bytes(scope.Packet)); Assert.Equal(0, scope.Packet.Position);
        Assert.Same(cached, statue.StatuePacket); Assert.Null(statue.user);
    });

    [Fact]
    public Task LiveStatueMissingUserFaultsBeforePacketCursorMutationOrCachePublication() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var statue = new LegacyNativeStatue();
        var sob = new LegacyNativeSobNpcRuntime(statue); scope.Packet.Seek(17);
        Assert.Throws<NullReferenceException>(() => sob.GetArray(scope.Packet, false, false));
        Assert.Equal(17, scope.Packet.Position); Assert.Null(statue.StatuePacket);
    });

    [Theory]
    [InlineData(false, "En")] [InlineData(false, "Ar")] [InlineData(true, "En")]
    public Task WholeLiveStatueSpawnUsesPlayerIdentitySobHpZeroFlagsAndActualCachePolicy(bool isStatic, string language) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.Equipment = new(scope.Client);
        scope.Player.Language = language; scope.Player.ClanName = "OwnedPack"; scope.Player.FlowerRank = 7;
        scope.Player.RealUID = 99; scope.Player.CountryID = 44; scope.Client.Status.MaxHitpoints = 1234;
        var statue = new LegacyNativeStatue { user = scope.Client, Static = isStatic, Action = 230 };
        var sob = new LegacyNativeSobNpcRuntime(statue)
        {
            UID = 500, X = 21, Y = 22, HitPoints = 9000, MaxHitPoints = 10000,
            Name = "EnglishStatue", Name2 = "AlternateStatue"
        };
        sob.BitVector.Add(1);
        scope.Packet.InitWriter(); sob.GetArray(scope.Packet, false, false);
        byte[] first = Bytes(scope.Packet); var fields = Scalars(first);
        Assert.Equal((ushort)2227, Opcode(first)); Assert.Equal(17UL, Assert.Single(fields[2]));
        Assert.Equal(18UL, Assert.Single(fields[21])); Assert.Equal(21UL, Assert.Single(fields[27]));
        Assert.Equal(22UL, Assert.Single(fields[28])); Assert.Equal(isStatic ? 250UL : 230UL, Assert.Single(fields[31]));
        Assert.Equal(10007UL, Assert.Single(fields[42])); Assert.Equal(1234UL, Assert.Single(fields[23]));
        Assert.Equal(18, fields[6].Count); Assert.All(fields[6], flag => Assert.Equal(0UL, flag));
        Assert.Contains(language == "En" ? "EnglishStatue" : "AlternateStatue", System.Text.Encoding.UTF8.GetString(first));
        Assert.Equal(isStatic, statue.StatuePacket != null);
        sob.X = 31; scope.Packet.InitWriter(); sob.GetArray(scope.Packet, true, true);
        if (isStatic) Assert.Equal(first, Bytes(scope.Packet));
        else Assert.Equal(31UL, Assert.Single(Scalars(Bytes(scope.Packet))[27]));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task GenuineStatueProducerUsesRealSpatialRoleAndNativeUserStateWithoutSaving(bool isStatic) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(); scope.Client.Equipment = new(scope.Client);
        scope.Client.Status.MaxHitpoints = 200; Set(scope.Player, "_minhitpoints", 30);
        LegacyNativeStatue.CreateStatue(scope.Client, 12, 13, 230, 999, isStatic);
        var sob = Assert.IsType<LegacyNativeSobNpcRuntime>(Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.SobNpc)));
        Assert.Equal(105176u, sob.UID); Assert.Equal(300, sob.HitPoints); Assert.Equal(2000, sob.MaxHitPoints);
        Assert.Same(scope.Client, sob.statue!.user); Assert.Equal(0, sob.statue.HitPotion); Assert.Equal(230, sob.statue.Action);
        Assert.True(LegacyNativeStatue.ContainStatue(map, 12, 13)); Assert.False(LegacyNativeStatue.ContainStatue(map, 13, 12));
        Assert.Equal(isStatic, LegacyNativeStatue.StaticStatue != null);
        Assert.Equal(isStatic, LegacyNativeStatue.StaticSobNpc != null);
        Assert.Single(scope.Queue); Assert.Equal((ushort)2227, Opcode(Assert.Single(scope.Queue)));
        if (isStatic)
        {
            Assert.Same(sob, LegacyNativeStatue.StaticSobNpc); Assert.Same(sob.statue, LegacyNativeStatue.StaticStatue);
            Assert.Equal(LegacyNativePlayerMapFlags.ConquerAction.Sit, scope.Player.Action);
            Assert.Equal(LegacyNativePlayerMapFlags.ConquerAngle.South, scope.Player.Angle);
        }
    });

    [Fact]
    public Task WholeStatueSaveAndLoadRoundTripOnlyOwnedPacketAndMapReferences() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(admitPlayer: false);
        var statue = new LegacyNativeStatue { Static = true, StatuePacket = [9, 8, 7, 6, 5] };
        var sob = new LegacyNativeSobNpcRuntime(statue)
        { UID = 51, X = 12, Y = 13, Map = 700, MaxHitPoints = 1000, HitPoints = -7, ObjType = LegacyNativeMapObjectType.SobNpc };
        LegacyNativeStatue.StaticStatue = statue; LegacyNativeStatue.StaticSobNpc = sob;
        LegacyNativeStatue.Save();
        string[] lines = File.ReadAllLines(Path.Combine(scope.Db, "StaticStatue.txt"));
        Assert.Equal("Count=1", lines[0]); Assert.Equal("5/9/8/7/6/5/51/12/13/700/1000/-7/", lines[1]);
        LegacyNativeStatue.StaticStatue = null; LegacyNativeStatue.StaticSobNpc = null;
        LegacyNativeStatue.Load();
        var restored = LegacyNativeStatue.StaticSobNpc!;
        Assert.NotSame(sob, restored); Assert.Same(LegacyNativeStatue.StaticStatue, restored.statue);
        Assert.Equal(statue.StatuePacket, restored.statue!.StatuePacket); Assert.True(restored.statue.Static);
        Assert.Null(restored.statue.user); Assert.Equal(-7, restored.HitPoints); Assert.Equal(1000, restored.MaxHitPoints);
        Assert.Same(restored, Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.SobNpc)));
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task StatueSaveWithoutCompleteNativeStaticStateDoesNotCreateAnEmptySave() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        LegacyNativeStatue.Save(); Assert.False(File.Exists(Path.Combine(scope.Db, "StaticStatue.txt")));
        LegacyNativeStatue.StaticStatue = new() { Static = true };
        LegacyNativeStatue.Save(); Assert.False(File.Exists(Path.Combine(scope.Db, "StaticStatue.txt")));
    });
}
