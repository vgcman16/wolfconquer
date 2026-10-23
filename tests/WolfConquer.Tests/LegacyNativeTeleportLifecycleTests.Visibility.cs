using WolfConquer.Core.World;
namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    private static LegacyNativeMonsterRole Monster(LegacyNativeGameMap map, uint uid, string name, ushort x = 64, ushort y = 64)
    {
        var family = new LegacyNativeMonsterFamily { ID = uid, Name = name, Mesh = 101, Level = 10, MaxHealth = 100 };
        var monster = new LegacyNativeMonsterRole(family, uid, "", map) { Map = map.ID, X = x, Y = y };
        Assert.True(map.View.EnterMap(monster));
        return monster;
    }

    [Fact]
    public Task SameLocationFindsRealRoleAndReturnsNativeNullOnAbsentMatch()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var map = scope.Map(); Scope.Admit(map, scope.Client);
            var monster = Monster(map, 400001, "Pheasant");
            Assert.True(scope.Client.Player.View.SameLocation(LegacyNativeMapObjectType.Monster, out var found));
            Assert.Same(monster, found);
            map.View.MoveTo(monster, 65, 64); monster.X = 65;
            Assert.False(scope.Client.Player.View.SameLocation(LegacyNativeMapObjectType.Monster, out found)); Assert.Null(found);
            scope.Client.Map = null;
            Assert.False(scope.Client.Player.View.SameLocation(LegacyNativeMapObjectType.Monster, out found)); Assert.Null(found);
        });
    }

    [Fact]
    public Task ViewContainsUsesNativeRawMembershipAndSelfUidNotCanSee()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var map = scope.Map(); Scope.Admit(map, scope.Client);
            var monster = Monster(map, 400001, "Pheasant", 66, 64); monster.DynamicID = 9;
            Assert.False(scope.Client.Player.View.CanSee(monster));
            Assert.True(scope.Client.Player.View.Contains(monster));
            Assert.True(map.View.LeaveMap(monster)); Assert.False(scope.Client.Player.View.Contains(monster));
            scope.Client.Map = null;
            Assert.True(scope.Client.Player.View.Contains(scope.Client.Player));
            Assert.False(scope.Client.Player.View.Contains(monster));
            Assert.Throws<NullReferenceException>(() => scope.Client.Player.View.Contains(null!));
        });
    }

    [Fact]
    public Task MonsterNameQueryUsesRealMonsterBirthAndActualVisibilityFilter()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var map = scope.Map(); Scope.Admit(map, scope.Client);
            var monster = Monster(map, 400001, "Pheasant", 65, 64);
            Assert.True(scope.Client.Player.View.ContainMobInScreen("Pheasant"));
            Assert.False(scope.Client.Player.View.ContainMobInScreen("pheasant"));
            monster.DynamicID = 7; Assert.False(scope.Client.Player.View.ContainMobInScreen("Pheasant"));
            monster.AllowDynamic = true; Assert.True(scope.Client.Player.View.ContainMobInScreen("Pheasant"));
        });
    }

    [Fact]
    public Task AttackableRolesEnumeratesRealMonsterPlayerSobOrderAndFiltersEach()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var map = scope.Map(); Scope.Admit(map, scope.Client);
            var monster = Monster(map, 400001, "Pheasant", 65, 64);
            var hidden = Monster(map, 400002, "Hidden", 65, 65); hidden.DynamicID = 9;
            var peer = scope.Peer(18, new()); Scope.Admit(map, peer, 65, 64);
            var sob = new LegacyNativeSobNpcRuntime { UID = 500001, Map = map.ID, X = 64, Y = 65, ObjType = LegacyNativeMapObjectType.SobNpc };
            Assert.True(map.View.EnterMap(sob));
            var roles = scope.Client.Player.View.AttackableRoles().ToArray();
            Assert.Equal(new ILegacyNativeMapObj[] { monster, peer.Player, sob }, roles);
            Assert.DoesNotContain(hidden, roles); Assert.DoesNotContain(scope.Client.Player, roles);
            var lazy = scope.Client.Player.View.AttackableRoles(); scope.Client.Map = null;
            Assert.Throws<NullReferenceException>(() => lazy.ToArray());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ByteViewBroadcastRunsNativeMutableBufferSealAndRealPeerTransport(bool me)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var map = scope.Map(); Scope.Admit(map, scope.Client);
            var queue = new Queue<byte[]>(); var peer = scope.Peer(18, queue); Scope.Admit(map, peer, 65, 64);
            byte[] bytes = {0,0,0xF2,3,10,11,12,13,0,0,0,0,0,0,0,0};
            scope.Client.Player.View.SendView(bytes, me);
            byte[] expected = {8,0,0xF2,3,10,11,12,13,84,81,83,101,114,118,101,114};
            Assert.Equal(expected, bytes);
            Assert.Equal(expected, Assert.Single(queue));
            if (me) Assert.Equal(expected, Assert.Single(scope.Queue)); else Assert.Empty(scope.Queue);
            Assert.Equal(16, bytes.Length);
        });
    }

    [Theory]
    [InlineData((ushort)1008, true, false)]
    [InlineData((ushort)2007, false, true)]
    [InlineData((ushort)1003, false, false)]
    [InlineData((ushort)2002, false, false)]
    public Task PlayerGenderCallsRealBodyOwnerWithoutBroaderClassification(ushort body, bool boy, bool girl)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Client.Player.Body = body;
            Assert.Equal(boy, scope.Client.Player.IsBoy()); Assert.Equal(girl, scope.Client.Player.IsGirl());
            Assert.Empty(scope.Queue);
        });
    }
}
