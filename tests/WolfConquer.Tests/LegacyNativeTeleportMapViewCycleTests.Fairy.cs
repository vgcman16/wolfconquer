using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportMapViewCycleTests
{
    [Fact]
    public void WholeFairyHandlerRetainsTheNative2336PacketAssociationWithoutActivatingDispatch()
    {
        var method = typeof(LegacyNativeMsgTransformFairy).GetMethod("Handler")!;
        var association = Assert.Single(method.GetCustomAttributesData(),
            attribute => attribute.AttributeType == typeof(LegacyNativePacketAttribute));
        var argument = Assert.Single(association.ConstructorArguments);
        Assert.Equal(typeof(ushort), argument.ArgumentType);
        Assert.Equal((ushort)2336, argument.Value);
        Assert.Empty(association.NamedArguments);
    }

    [Theory]
    [InlineData(0u, 0u, 0u)]
    [InlineData(1u, 33u, 17u)]
    [InlineData(2u, 44u, uint.MaxValue)]
    [InlineData(uint.MaxValue, uint.MaxValue, 0u)]
    public Task FairyWriterAndReaderRetainAllRawUIntWordsAndIgnoreUnknownReadWord(uint action, uint fairy, uint uid) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Packet.TransformFairyCreate((LegacyNativeMsgTransformFairy.Action)action, fairy, uid);
        Assert.Equal(FairyFrame(action, fairy, uid), NativeSpellTestFixture.Bytes(scope.Packet));
        scope.Input(action, fairy, uid, 0xAABBCCDD);
        scope.Packet.GetTransformFairy(out var readAction, out var readFairy, out var readUid);
        Assert.Equal(action, (uint)readAction);
        Assert.Equal(fairy, readFairy);
        Assert.Equal(uid, readUid);
        Assert.Equal(20, scope.Packet.Position);
    });

    [Theory]
    [InlineData(0u)]
    [InlineData(33u)]
    [InlineData(uint.MaxValue)]
    public Task CompleteFairyActivationBroadcastsRealFrameAndReplacesSpoofedUidWithPlayer(uint fairy) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        var peerQueue = new Queue<byte[]>();
        scope.Peer(map, peerQueue);
        Assert.False(scope.Player.OnFairy);
        scope.Input(1, fairy, 999, 777);
        LegacyNativeMsgTransformFairy.Handler(scope.Client, scope.Packet);
        Assert.True(scope.Player.OnFairy);
        Assert.Equal(LegacyNativeMsgTransformFairy.Action.Active, scope.Player.FairySpawn.Mode);
        Assert.Equal(fairy, scope.Player.FairySpawn.FairyType);
        Assert.Equal(scope.Player.UID, scope.Player.FairySpawn.UID);
        Assert.Equal(0u, scope.Player.FairySpawn.UnKnow);
        byte[] expected = FairyFrame(1, fairy, scope.Player.UID);
        Assert.Equal(expected, Assert.Single(scope.Queue));
        Assert.Equal(expected, Assert.Single(peerQueue));
    });

    [Fact]
    public Task ReactivationFirstBroadcastsOldFairyDeactivationThenPublishesNewState() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        var peerQueue = new Queue<byte[]>();
        scope.Peer(map, peerQueue);
        scope.Player.OnFairy = true;
        scope.Player.FairySpawn = new() { Mode = LegacyNativeMsgTransformFairy.Action.Active, FairyType = 88, UID = 77, UnKnow = 66 };
        scope.Input(1, 99, 555);
        LegacyNativeMsgTransformFairy.Handler(scope.Client, scope.Packet);
        byte[][] expected = [FairyFrame(2, 88, 77), FairyFrame(1, 99, scope.Player.UID)];
        Assert.Equal(expected, scope.Queue.ToArray());
        Assert.Equal(expected, peerQueue.ToArray());
        Assert.True(scope.Player.OnFairy);
        Assert.Equal(99u, scope.Player.FairySpawn.FairyType);
        Assert.Equal(scope.Player.UID, scope.Player.FairySpawn.UID);
        Assert.Equal(0u, scope.Player.FairySpawn.UnKnow);
    });

    [Fact]
    public Task CompleteDeactivationUsesIncomingTypeButOwnerUidThenClearsWholeStruct() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Map();
        scope.Player.OnFairy = true;
        scope.Player.FairySpawn = new() { Mode = LegacyNativeMsgTransformFairy.Action.Active, FairyType = 88, UID = 77, UnKnow = 66 };
        scope.Input(2, 99, 555);
        LegacyNativeMsgTransformFairy.Handler(scope.Client, scope.Packet);
        Assert.False(scope.Player.OnFairy);
        Assert.Equal(FairyFrame(2, 99, scope.Player.UID), Assert.Single(scope.Queue));
        Assert.Equal(default, scope.Player.FairySpawn.Mode);
        Assert.Equal(0u, scope.Player.FairySpawn.FairyType);
        Assert.Equal(0u, scope.Player.FairySpawn.UID);
        Assert.Equal(0u, scope.Player.FairySpawn.UnKnow);
    });

    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    [InlineData(uint.MaxValue)]
    public Task UnknownActionConsumesAllWordsWithoutTouchingFairyOrView(uint action) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Player.View = null!;
        scope.Player.OnFairy = true;
        scope.Player.FairySpawn = new() { Mode = LegacyNativeMsgTransformFairy.Action.Active, FairyType = 88, UID = 77, UnKnow = 66 };
        scope.Input(action, 99, 555);
        LegacyNativeMsgTransformFairy.Handler(scope.Client, scope.Packet);
        Assert.True(scope.Player.OnFairy);
        Assert.Equal(88u, scope.Player.FairySpawn.FairyType);
        Assert.Equal(77u, scope.Player.FairySpawn.UID);
        Assert.Equal(66u, scope.Player.FairySpawn.UnKnow);
        Assert.Equal(20, scope.Packet.Position);
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActivationViewFaultRetainsSourceMutationPrefixRatherThanFakeSuccessfulState(bool alreadyActive) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Player.OnFairy = alreadyActive;
        scope.Player.FairySpawn = new() { Mode = LegacyNativeMsgTransformFairy.Action.Active, FairyType = 88, UID = 77, UnKnow = 66 };
        scope.Player.View = null!;
        scope.Input(1, 99, 555);
        Assert.Throws<NullReferenceException>(() => LegacyNativeMsgTransformFairy.Handler(scope.Client, scope.Packet));
        Assert.True(scope.Player.OnFairy);
        Assert.Equal(alreadyActive ? LegacyNativeMsgTransformFairy.Action.Dezactive : LegacyNativeMsgTransformFairy.Action.Active,
            scope.Player.FairySpawn.Mode);
        Assert.Equal(88u, scope.Player.FairySpawn.FairyType);
        Assert.Equal(77u, scope.Player.FairySpawn.UID);
        Assert.Equal(66u, scope.Player.FairySpawn.UnKnow);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task DeactivationViewFaultClearsOnFlagButRetainsStructUntilRealSendCompletes() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Player.OnFairy = true;
        scope.Player.FairySpawn = new() { Mode = LegacyNativeMsgTransformFairy.Action.Active, FairyType = 88, UID = 77 };
        scope.Player.View = null!;
        scope.Input(2, 99, 555);
        Assert.Throws<NullReferenceException>(() => LegacyNativeMsgTransformFairy.Handler(scope.Client, scope.Packet));
        Assert.False(scope.Player.OnFairy);
        Assert.Equal(LegacyNativeMsgTransformFairy.Action.Active, scope.Player.FairySpawn.Mode);
        Assert.Equal(88u, scope.Player.FairySpawn.FairyType);
        Assert.Equal(77u, scope.Player.FairySpawn.UID);
        Assert.Empty(scope.Queue);
    });
}
