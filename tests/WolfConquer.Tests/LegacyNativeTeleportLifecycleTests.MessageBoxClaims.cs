using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MessageBoxClaimIsRemovedBeforeOkOrCancelCanReenter(bool cancelOnly) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var reply = IndependentMessageBoxReply(); using var reentrant = IndependentMessageBoxReply();
        scope.Client.Player.Map = 1002;
        int calls = 0, unusedCancel = 0; var deadline = DateTime.Now.AddDays(1);
        Action<LegacyNativeGameClientRuntime> callback = user =>
        {
            Assert.Same(scope.Client, user);
            Assert.Equal(1, ++calls); // A faulty recursive claim fails without unbounded recursion.
            Assert.Null(user.Player.MessageOK); Assert.Null(user.Player.MessageCancel);
            Assert.Equal(deadline, user.Player.StartMessageBox);
            LegacyNativeMsgStaticMessage.Handler(user, reentrant);
        };
        scope.Client.Player.SetMessageBox(cancelOnly ? null : callback,
            cancelOnly ? callback : _ => unusedCancel++, deadline);
        LegacyNativeMsgStaticMessage.Handler(scope.Client, reply);
        Assert.Equal(1, calls); Assert.Equal(0, unusedCancel);
        Assert.Equal(8, reply.Position); Assert.Equal(8, reentrant.Position);
        Assert.Null(scope.Client.Player.MessageOK); Assert.Null(scope.Client.Player.MessageCancel);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ThrowAfterInstallingANewMessageBoxKeepsThatReplacementAndOnlyConsumesTheOldPair() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var first = IndependentMessageBoxReply(); using var second = IndependentMessageBoxReply();
        using var duplicate = IndependentMessageBoxReply(); scope.Client.Player.Map = 1002;
        int oldCalls = 0, newCalls = 0, cancelCalls = 0;
        var expected = new InvalidOperationException("owned callback failed after replacing its box");
        var deadline = DateTime.Now.AddDays(2);
        Action<LegacyNativeGameClientRuntime> replacement = user => { Assert.Same(scope.Client, user); newCalls++; };
        Action<LegacyNativeGameClientRuntime> cancel = _ => cancelCalls++;
        scope.Client.Player.SetMessageBox(user =>
        {
            oldCalls++;
            user.Player.SetMessageBox(replacement, cancel, deadline);
            throw expected;
        }, _ => cancelCalls++, DateTime.Now.AddDays(1));
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => LegacyNativeMsgStaticMessage.Handler(scope.Client, first)));
        Assert.Same(replacement, scope.Client.Player.MessageOK); Assert.Same(cancel, scope.Client.Player.MessageCancel);
        Assert.Equal(deadline, scope.Client.Player.StartMessageBox);
        LegacyNativeMsgStaticMessage.Handler(scope.Client, second);
        LegacyNativeMsgStaticMessage.Handler(scope.Client, duplicate);
        Assert.Equal(1, oldCalls); Assert.Equal(1, newCalls); Assert.Equal(0, cancelCalls);
        Assert.Null(scope.Client.Player.MessageOK); Assert.Null(scope.Client.Player.MessageCancel);
        Assert.Equal(deadline, scope.Client.Player.StartMessageBox); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData("user")]
    [InlineData("player")]
    [InlineData("foreign")]
    public Task AcceptedMessageBoxResponseRejectsMissingOrForeignOwnersWithoutConsumingTheirState(string fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var reply = IndependentMessageBoxReply();
        var original = scope.Client.Player; original.Map = 1002;
        var deadline = DateTime.Now.AddDays(1);
        Action<LegacyNativeGameClientRuntime> ok = _ => throw new Exception("must not run");
        Action<LegacyNativeGameClientRuntime> cancel = _ => throw new Exception("must not run");
        original.SetMessageBox(ok, cancel, deadline);
        var peerQueue = new Queue<byte[]>();
        var peer = fault == "foreign" ? scope.Peer(97, peerQueue) : null;
        if (peer is not null) { peer.Player.Map = 1002; peer.Player.SetMessageBox(ok, cancel, deadline); }
        if (fault == "player") scope.Client.Player = null!;
        else if (peer is not null) scope.Client.Player = peer.Player;
        try
        {
            if (fault == "foreign") Assert.Throws<InvalidOperationException>(() => LegacyNativeMsgStaticMessage.Handler(scope.Client, reply));
            else Assert.Throws<ArgumentNullException>(() => LegacyNativeMsgStaticMessage.Handler(fault == "user" ? null! : scope.Client, reply));
            Assert.Same(ok, original.MessageOK); Assert.Same(cancel, original.MessageCancel); Assert.Equal(deadline, original.StartMessageBox);
            if (peer is not null)
            {
                Assert.Same(ok, peer.Player.MessageOK); Assert.Same(cancel, peer.Player.MessageCancel);
                Assert.Equal(deadline, peer.Player.StartMessageBox);
            }
            Assert.Equal(8, reply.Position); Assert.Empty(scope.Queue); Assert.Empty(peerQueue);
        }
        finally { scope.Client.Player = original; }
    });

    [Fact]
    public Task MissingMessageBoxInputCannotConsumeAnInstalledPair() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var deadline = DateTime.Now.AddDays(1);
        Action<LegacyNativeGameClientRuntime> ok = _ => throw new Exception("must not run");
        Action<LegacyNativeGameClientRuntime> cancel = _ => throw new Exception("must not run");
        scope.Client.Player.SetMessageBox(ok, cancel, deadline);
        Assert.Throws<ArgumentNullException>(() => LegacyNativeMsgStaticMessage.Handler(scope.Client, null!));
        Assert.Same(ok, scope.Client.Player.MessageOK); Assert.Same(cancel, scope.Client.Player.MessageCancel);
        Assert.Equal(deadline, scope.Client.Player.StartMessageBox); Assert.Empty(scope.Queue);
    });

    private static LegacyNativePacket IndependentMessageBoxReply()
    {
        // Independent initialized2320 header and accepted UInt32 body. The
        // component reader starts at4; this does not claim host dispatch proof.
        var packet = new LegacyNativePacket(Convert.FromHexString("0800100901000000"));
        packet.Seek(4);
        return packet;
    }
}
