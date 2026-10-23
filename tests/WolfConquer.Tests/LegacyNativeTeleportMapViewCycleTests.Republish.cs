using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportMapViewCycleTests
{
    // Existing genuine GetArray prerequisites, isolated exactly as the retained
    // PlayerMap component tests. This does not replace any missing lifecycle owner.
    private sealed class SpawnData : IDisposable
    {
        private readonly Dictionary<uint, LegacyNativeProfessionTable.Benefit> benefits = LegacyNativeProfessionTable.Benefits;
        private readonly LegacyNativeDbSafeDictionary<LegacyNativePrestigeRanking.Type, LegacyNativePrestigeRanking.Rank> ranks = LegacyNativePrestigeRanking.Ranks;
        private readonly object rankSync = LegacyNativePrestigeRanking.SynRoot;
        private readonly uint count = LegacyNativeInnerPowerTable.Count;
        private readonly LegacyNativeInnerPowerTable.Stage[] stages = LegacyNativeInnerPowerTable.Stages;
        private readonly ConcurrentDictionary<uint, LegacyNativeInnerPowerRuntime> inner = LegacyNativeInnerPowerRuntime.InnerPowerPolle;
        private readonly ConcurrentDictionary<uint, LegacyNativeUnionRuntime> unions = LegacyNativeUnionRuntime.UnionPoll;
        private readonly Dictionary<uint, LegacyNativeHundredWeaponsTable.HundredWeapon> weapons = LegacyNativeHundredWeaponsTable.HundredWeaponsList;
        private readonly bool load = LegacyNativeCombatGearTable.m_Load;

        internal SpawnData(Scope scope)
        {
            LegacyNativeProfessionTable.Benefits = new();
            LegacyNativePrestigeRanking.Ranks = new();
            LegacyNativePrestigeRanking.SynRoot = new();
            LegacyNativePrestigeRanking.Create();
            LegacyNativeInnerPowerTable.Count = 0;
            LegacyNativeInnerPowerTable.Stages = [];
            LegacyNativeInnerPowerRuntime.InnerPowerPolle = new();
            LegacyNativeUnionRuntime.UnionPoll = new();
            LegacyNativeHundredWeaponsTable.HundredWeaponsList = new();
            LegacyNativeCombatGearTable.m_Load = false;
            scope.Client.Equipment = new LegacyNativeEquipItemStorage(scope.Client);
            scope.Client.HundredWeapons = new LegacyNativeHundredWeapons(scope.Client);
            scope.Client.CombatGear = new LegacyNativeMyCombatGear(scope.Client);
            scope.Player.InnerPower = new LegacyNativeInnerPowerRuntime("Inner", scope.Player.UID);
            scope.Player.Nobility = new LegacyNativeNobility(scope.Player.UID, "Noble", 0, 0, 0);
        }

        public void Dispose()
        {
            LegacyNativeProfessionTable.Benefits = benefits;
            LegacyNativePrestigeRanking.Ranks = ranks;
            LegacyNativePrestigeRanking.SynRoot = rankSync;
            LegacyNativeInnerPowerTable.Count = count;
            LegacyNativeInnerPowerTable.Stages = stages;
            LegacyNativeInnerPowerRuntime.InnerPowerPolle = inner;
            LegacyNativeUnionRuntime.UnionPoll = unions;
            LegacyNativeHundredWeaponsTable.HundredWeaponsList = weapons;
            LegacyNativeCombatGearTable.m_Load = load;
        }
    }

    [Fact]
    public Task CompleteCheckScreenColorsConsumesLatchAndSendsExactNativeActionToOwnerOnly() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        var peerQueue = new Queue<byte[]>();
        scope.Peer(map, peerQueue);
        scope.Player.View!.InColorScrenn = true;
        scope.Player.View.CheckScrennColors(scope.Packet);
        Assert.False(scope.Player.View.InColorScrenn);
        Assert.Equal(Convert.FromHexString("1000FE070811480060686800700A780A"), Assert.Single(scope.Queue));
        Assert.Empty(peerQueue);
    });

    [Fact]
    public Task ScreenColorsFalseGuardDoesNotDemandStreamOrOwner() => NativePacketTestThread.Run(() =>
    {
        var view = new LegacyNativeRoleView(null!);
        view.CheckScrennColors(null!);
        Assert.False(view.InColorScrenn);
    });

    [Fact]
    public Task ScreenColorNullStreamFaultOccursAfterLatchWasConsumed() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Player.View!.InColorScrenn = true;
        Assert.Throws<NullReferenceException>(() => scope.Player.View.CheckScrennColors(null!));
        Assert.False(scope.Player.View.InColorScrenn);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ScreenColorNullOwnerFaultOccursAfterLatchButBeforePacketMutation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var view = new LegacyNativeRoleView(null!) { InColorScrenn = true };
        scope.Packet.Seek(9);
        Assert.Throws<NullReferenceException>(() => view.CheckScrennColors(scope.Packet));
        Assert.False(view.InColorScrenn);
        Assert.Equal(9, scope.Packet.Position);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task WholeResendViewUsesActualPlayerGetArrayAndSendsOnlyToRealNearbyPeer() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var spawn = new SpawnData(scope);
        var map = scope.Map();
        var peerQueue = new Queue<byte[]>();
        scope.Peer(map, peerQueue);
        scope.Player.View!.ReSendView(scope.Packet);
        Assert.Empty(scope.Queue);
        var bytes = Assert.Single(peerQueue);
        Assert.Equal((ushort)2227, Opcode(bytes));
        var message = Proto<LegacyNativePlayerRuntime.SpawnPacketProto>(bytes);
        Assert.Equal(scope.Player.UID, message.UID);
        Assert.Equal((ushort)10, message.X);
        Assert.Equal((ushort)10, message.Y);
        Assert.Contains("Wolf", message.Names);
    });
}
