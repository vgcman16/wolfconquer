using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using SageFlag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("null", false)] [InlineData("null", true)]
    [InlineData("small", false)] [InlineData("small", true)]
    [InlineData("disposed", false)] [InlineData("disposed", true)]
    [InlineData("second-null", false)] [InlineData("second-small", false)]
    [InlineData("second-disposed", false)] [InlineData("alias", false)]
    public Task SagePreparedRejectsUnavailableOrAliasedRealLoansBeforeFlagOrEitherFrame(string fault, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var player = scope.Client.Player; var vector = player.BitVector;
        if (existing) Assert.True(vector.TryAdd((int)SageFlag.SageSpeedMode, 77, false, 7));
        player.XPBooster = 81; player.XPKiller = 42;
        var before = SagePreparedState.Capture(player);
        using var pool = new SagePreparedPool();
        var first = pool.Packet(fault == "small" ? 1 : 256);
        if (fault == "disposed") first.Dispose();
        pool.Enqueue(fault == "null" ? null : first);
        if (!existing)
        {
            var second = fault == "alias" ? first : pool.Packet(fault == "second-small" ? 1 : 256);
            if (fault == "second-disposed") second.Dispose();
            pool.Enqueue(fault == "second-null" ? null : second);
        }
        var bytes = pool.Packets.Select(SagePreparedStorage).ToArray();

        Assert.False(scope.Ninja.TryAdmitSageMode(uint.MaxValue));

        before.Unchanged(player); Assert.Empty(scope.Queue);
        for (int index = 0; index < pool.Packets.Count; index++)
        {
            Assert.Equal(93, pool.Packets[index].Size);
            Assert.Equal(bytes[index], SagePreparedStorage(pool.Packets[index]));
        }
        // Private loan cursors may be taken/reset; no frame bytes may be copied
        // until BOTH distinct live capacities have passed admission.
        for (int index = 0; index < 12; index++) scope.Untaken(index);
    });

    [Theory]
    [InlineData("", false)] [InlineData("", true)]
    [InlineData("short", false)] [InlineData("short", true)]
    public Task SagePreparedContainsRealSealMaterializationFailureBeforeAnyPoolTake(string shortSeal, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Client.Player;
        if (existing) Assert.True(player.BitVector.TryAdd((int)SageFlag.SageSpeedMode, 77, false, 7));
        var before = SageAdmissionSnapshot.Capture(scope, player);
        SagePreparedSeal.SetValue(null, shortSeal); // Genuine invalid private state, not a serializer callback.

        Assert.False(scope.Ninja.TryAdmitSageMode(uint.MaxValue));

        before.Unchanged(scope, player);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task SagePreparedSendBoundRejectsOversizedNewStatusButDoesNotInventDuplicateArchiveRestriction(bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Client.Player;
        player.BitVector = new LegacyNativeStatusFlagsBigVector32(32768);
        if (existing) Assert.True(player.BitVector.TryAdd((int)SageFlag.SageSpeedMode, 77, false, 7));
        var before = SagePreparedState.Capture(player);

        Assert.Equal(existing, scope.Ninja.TryAdmitSageMode(uint.MaxValue));

        before.Unchanged(player);
        if (existing)
        {
            scope.Sent(SageArchive(17, uint.MaxValue)); Assert.Equal(11, scope.Available);
            AssertSageArchiveBuffer(scope, 0, 17, uint.MaxValue); scope.Untaken(1);
        }
        else
        {
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available);
            for (int index = 0; index < 12; index++) scope.Untaken(index);
        }
    });

    [Theory]
    [InlineData(null, false)] [InlineData(null, true)]
    [InlineData("ABCDEFGH", false)] [InlineData("ABCDEFGH", true)]
    [InlineData("éééééééé", false)] [InlineData("éééééééé", true)]
    public Task SagePreparedExactCapacityKeepsIndependentStatusAndFullWidthArchiveGoldens(string? seal, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Client.Player; var vector = player.BitVector;
        if (existing) Assert.True(vector.TryAdd((int)SageFlag.SageSpeedMode, 77, false, 7));
        var before = SagePreparedState.Capture(player);
        SagePreparedSeal.SetValue(null, seal);
        byte[] status = SagePreparedSealed(SageBitmap(17), seal);
        byte[] archive = SagePreparedSealed(SageArchive(17, uint.MaxValue), seal);
        using var pool = new SagePreparedPool();
        LegacyNativePacket? statusPacket = null;
        if (!existing) { statusPacket = pool.Packet(status.Length); pool.Enqueue(statusPacket); }
        var archivePacket = pool.Packet(archive.Length); pool.Enqueue(archivePacket);

        Assert.True(scope.Ninja.TryAdmitSageMode(uint.MaxValue));

        if (existing) { before.Unchanged(player); scope.Sent(archive); }
        else
        {
            scope.Sent(status, archive);
            var row = vector.ArrayFlags[(int)SageFlag.SageSpeedMode];
            Assert.Equal(-1, row.Secounds); Assert.True(row.RemoveOnDead);
            Assert.Equal(0, row.InvokerSecouds); Assert.Equal(default, row.TimerInvoker);
            Assert.Equal(status, NativeSpellTestFixture.Bytes(statusPacket!));
            Assert.Equal(0, statusPacket!.Position);
        }
        Assert.Equal(archive, NativeSpellTestFixture.Bytes(archivePacket)); Assert.Equal(0, archivePacket.Position);
        Assert.Equal((ushort)(archive.Length - (seal is null ? 0 : 8)), BitConverter.ToUInt16(archive, 0));
        Assert.Equal((ushort)2176, BitConverter.ToUInt16(archive, 2));
        Assert.Empty(pool.Queue); Assert.Empty(scope.Log);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task SageCommittedOwnerQueueFailureRemainsAcceptedAndRealPeerStillReceivesPreparedFrames(bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player;
        if (existing) Assert.True(player.BitVector.TryAdd((int)SageFlag.SageSpeedMode, 77, false, 7));
        var before = SagePreparedState.Capture(player);
        var map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
        client.Map = map; player.Map = 1002; player.X = 20; player.Y = 20;
        var peerFrames = new Queue<byte[]>(); var peer = NativeSpellTestFixture.CreateOwner(peerFrames);
        peer.Player.UID = 99; peer.Player.Map = 1002; peer.Player.X = 21; peer.Player.Y = 20;
        peer.Player.ObjType = LegacyNativeMapObjectType.Player;
        Assert.True(map.View.EnterMap(peer.Player));
        SagePreparedSendQueue.SetValue(client.Socket, null); // Actual transport silently drops, not an accepting provider.
        try
        {
            Assert.True(scope.Ninja.TryAdmitSageMode(900));
            Assert.Empty(scope.Queue);
            if (existing)
            {
                before.Unchanged(player); Assert.Equal(SageArchive(17, 900), Assert.Single(peerFrames));
            }
            else
            {
                Assert.True(player.BitVector.TryContainFlag((int)SageFlag.SageSpeedMode, out bool present)); Assert.True(present);
                Assert.Equal(900, player.BitVector.ArrayFlags[(int)SageFlag.SageSpeedMode].Secounds);
                Assert.Equal(2, peerFrames.Count);
                Assert.Equal(SageBitmap(17), peerFrames.ElementAt(0)); Assert.Equal(SageArchive(17, 900), peerFrames.ElementAt(1));
            }
            Assert.Empty(scope.Log); // Socket.Send contains this real delivery failure; no exception-file fixture is needed.
        }
        finally { SagePreparedSendQueue.SetValue(client.Socket, scope.Queue); }
    });
}
