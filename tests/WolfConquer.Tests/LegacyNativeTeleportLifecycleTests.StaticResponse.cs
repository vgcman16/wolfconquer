using WolfConquer.Core.World;
namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    private static void StaticReply(Scope scope, uint accepted)
    {
        scope.Packet.InitWriter(); scope.Packet.Write(accepted);
        scope.Packet.Size = scope.Packet.Position; // Actual complete initialized reply extent.
        scope.Packet.Seek(4);
    }

    [Fact]
    public Task MessageBoxStateKeepsNativeImplicitNullCallbacksAndDefaultClock()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Null(scope.Client.Player.MessageOK); Assert.Null(scope.Client.Player.MessageCancel);
            Assert.Equal(default(DateTime), scope.Client.Player.StartMessageBox);
        });
    }

    [Fact]
    public Task TruncatedStaticResponseRetainsCallbacksClockInputAndQueue()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Action<LegacyNativeGameClientRuntime> ok = _ => throw new Exception("must not run");
            Action<LegacyNativeGameClientRuntime> cancel = _ => throw new Exception("must not run");
            scope.Client.Player.Map = 1002;
            var deadline = DateTime.Now.AddDays(1);
            scope.Client.Player.StartMessageBox = deadline;
            scope.Client.Player.MessageOK = ok; scope.Client.Player.MessageCancel = cancel;
            StaticReply(scope, 1);
            scope.Packet.Size--;
            byte[] input = Bytes(scope.Packet);
            Assert.Throws<EndOfStreamException>(() => LegacyNativeMsgStaticMessage.Handler(scope.Client, scope.Packet));
            Assert.Same(ok, scope.Client.Player.MessageOK); Assert.Same(cancel, scope.Client.Player.MessageCancel);
            Assert.Equal(deadline, scope.Client.Player.StartMessageBox);
            Assert.Equal(4, scope.Packet.Position); Assert.Equal(7, scope.Packet.Size);
            Assert.Equal(input, Bytes(scope.Packet)); Assert.Empty(scope.Queue);
        });
    }

    [Fact]
    public Task StaticResponseCallsSourceOkDelegateWithSameClientThenClearsBothSlots()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); int ok = 0, cancel = 0;
            scope.Client.Player.Map = 1002;
            scope.Client.Player.StartMessageBox = DateTime.Now.AddDays(1);
            scope.Client.Player.MessageOK = client => { Assert.Same(scope.Client, client); ok++; };
            scope.Client.Player.MessageCancel = _ => cancel++;
            StaticReply(scope, 1);
            LegacyNativeMsgStaticMessage.Handler(scope.Client, scope.Packet);
            Assert.Equal(1, ok); Assert.Equal(0, cancel);
            Assert.Null(scope.Client.Player.MessageOK); Assert.Null(scope.Client.Player.MessageCancel);
            Assert.Equal(8, scope.Packet.Position); Assert.Empty(scope.Queue);
        });
    }

    [Fact]
    public Task StaticResponseUsesCancelOnlyWhenOkIsNullAndWindowIsStillOpen()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); int cancel = 0;
            scope.Client.Player.Map = 1002; scope.Client.Player.StartMessageBox = DateTime.Now.AddDays(1);
            scope.Client.Player.MessageCancel = client => { Assert.Same(scope.Client, client); cancel++; };
            StaticReply(scope, 1); LegacyNativeMsgStaticMessage.Handler(scope.Client, scope.Packet);
            Assert.Equal(1, cancel); Assert.Null(scope.Client.Player.MessageCancel);
        });
    }

    [Fact]
    public Task ExpiredStaticResponseClearsCallbacksWithoutInvokingEither()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); int calls = 0;
            scope.Client.Player.Map = 1002; scope.Client.Player.StartMessageBox = DateTime.Now.AddDays(-1);
            scope.Client.Player.MessageOK = _ => calls++; scope.Client.Player.MessageCancel = _ => calls++;
            StaticReply(scope, 1); LegacyNativeMsgStaticMessage.Handler(scope.Client, scope.Packet);
            Assert.Equal(0, calls); Assert.Null(scope.Client.Player.MessageOK); Assert.Null(scope.Client.Player.MessageCancel);
        });
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    [InlineData(uint.MaxValue)]
    public Task NonAcceptReplyRetainsCallbacksAndDoesNotDereferenceMissingClient(uint accepted)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); Action<LegacyNativeGameClientRuntime> ok = _ => throw new Exception("must not run");
            Action<LegacyNativeGameClientRuntime> cancel = _ => throw new Exception("must not run");
            var deadline = DateTime.Now.AddDays(1);
            scope.Client.Player.SetMessageBox(ok, cancel, deadline);
            StaticReply(scope, accepted); LegacyNativeMsgStaticMessage.Handler(scope.Client, scope.Packet);
            Assert.Same(ok, scope.Client.Player.MessageOK); Assert.Same(cancel, scope.Client.Player.MessageCancel);
            Assert.Equal(deadline, scope.Client.Player.StartMessageBox);
            StaticReply(scope, accepted); LegacyNativeMsgStaticMessage.Handler(null!, scope.Packet);
            Assert.Equal(8, scope.Packet.Position);
            Assert.Same(ok, scope.Client.Player.MessageOK); Assert.Same(cancel, scope.Client.Player.MessageCancel);
            Assert.Equal(deadline, scope.Client.Player.StartMessageBox); Assert.Empty(scope.Queue);
        });
    }

    [Theory]
    [InlineData(601u)]
    [InlineData(6000u)]
    [InlineData(5040u)]
    [InlineData(6001u)]
    [InlineData(1005u)]
    [InlineData(700u)]
    [InlineData(1858u)]
    [InlineData(1860u)]
    [InlineData(3852u)]
    [InlineData(2068u)]
    [InlineData(1768u)]
    public Task BlockedMapReplyRetainsBothCallbacksAndDoesNotInvokeThem(uint map)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Action<LegacyNativeGameClientRuntime> ok = _ => throw new Exception("must not run");
            Action<LegacyNativeGameClientRuntime> cancel = _ => throw new Exception("must not run");
            var deadline = DateTime.Now.AddDays(1);
            scope.Client.Player.Map = map; scope.Client.Player.StartMessageBox = deadline;
            scope.Client.Player.MessageOK = ok; scope.Client.Player.MessageCancel = cancel;
            StaticReply(scope, 1); LegacyNativeMsgStaticMessage.Handler(scope.Client, scope.Packet);
            Assert.Same(ok, scope.Client.Player.MessageOK); Assert.Same(cancel, scope.Client.Player.MessageCancel);
            Assert.Equal(deadline, scope.Client.Player.StartMessageBox); Assert.Empty(scope.Queue);
        });
    }

    [Fact]
    public Task ThrowingCallbackKeepsTheOriginalExceptionAndCannotReplayTheConsumedPair()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var expected = new InvalidOperationException("source callback fault"); int calls = 0;
            Action<LegacyNativeGameClientRuntime> ok = _ => { calls++; throw expected; };
            Action<LegacyNativeGameClientRuntime> cancel = _ => { };
            var deadline = DateTime.Now.AddDays(1);
            scope.Client.Player.Map = 1002; scope.Client.Player.SetMessageBox(ok, cancel, deadline);
            StaticReply(scope, 1);
            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => LegacyNativeMsgStaticMessage.Handler(scope.Client, scope.Packet)));
            Assert.Null(scope.Client.Player.MessageOK); Assert.Null(scope.Client.Player.MessageCancel);
            StaticReply(scope, 1); LegacyNativeMsgStaticMessage.Handler(scope.Client, scope.Packet);
            Assert.Equal(1, calls); Assert.Equal(deadline, scope.Client.Player.StartMessageBox); Assert.Empty(scope.Queue);
        });
    }

    [Fact]
    public Task CallbackReplacementDuringResponseRemainsInstalledAfterReturn()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Client.Player.Map = 1002; scope.Client.Player.StartMessageBox = DateTime.Now.AddDays(1);
            Action<LegacyNativeGameClientRuntime> replacementOk = _ => throw new Exception("not a second reply");
            Action<LegacyNativeGameClientRuntime> replacementCancel = _ => throw new Exception("not a second reply");
            var deadline = DateTime.Now.AddDays(2);
            scope.Client.Player.MessageOK = client =>
            {
                client.Player.SetMessageBox(replacementOk, replacementCancel, deadline);
            };
            StaticReply(scope, 1); LegacyNativeMsgStaticMessage.Handler(scope.Client, scope.Packet);
            Assert.Same(replacementOk, scope.Client.Player.MessageOK); Assert.Same(replacementCancel, scope.Client.Player.MessageCancel);
            Assert.Equal(deadline, scope.Client.Player.StartMessageBox); Assert.Empty(scope.Queue);
        });
    }
}
