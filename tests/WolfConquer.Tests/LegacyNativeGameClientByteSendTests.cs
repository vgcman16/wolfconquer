using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using NativeSocket = WolfConquer.Core.Networking.LegacyNativeSecuritySocket;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeGameClientByteSendTests
{
    private static readonly byte[] Unsealed = Convert.FromHexString("000080080102030405060708CCCCCCCCCCCCCCCC");
    private static readonly byte[] Sealed = Convert.FromHexString("0C00800801020304050607085451536572766572");
    private static readonly byte[] Key = Encoding.ASCII.GetBytes("0123456789ABCDEF");
    private static readonly FieldInfo QueueField = typeof(NativeSocket).GetField(
        "SendQueue", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public Task FakeReturnsBeforeMissingSocketAndNullBuffer() => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope();
        var owner = new LegacyNativeGameClientRuntime { Fake = true };

        owner.Send((byte[])null!);

        Assert.Null(owner.Socket);
        Assert.True(owner.Fake);
        logs.AssertNoErrors();
    });

    [Fact]
    public Task DeadSocketReturnsBeforeNullBufferAndUncreatedQueue() => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope();
        var socket = new NativeSocket(null!, null!);
        var owner = new LegacyNativeGameClientRuntime { Socket = socket };

        owner.Send((byte[])null!);

        Assert.False(socket.Alive);
        Assert.Null(QueueField.GetValue(socket));
        Assert.Null(socket.Connection);
        logs.AssertNoErrors();
    });

    [Fact]
    public Task MissingSocketIsCaughtAndLoggedBeforeAnySourceMutation() => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope();
        var owner = new LegacyNativeGameClientRuntime();
        var source = Unsealed.ToArray();

        owner.Send(source);

        Assert.Equal(Unsealed, source);
        Assert.Null(owner.Socket);
        logs.AssertOneError("LegacyNativeGameClientRuntime.Send");
    });

    [Fact]
    public Task LiveNullBufferIsCaughtAndLoggedWithoutQueuePublication() => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope();
        var queue = new Queue<byte[]>();
        var owner = Owner(queue);

        owner.Send((byte[])null!);

        Assert.Empty(queue);
        Assert.True(owner.Socket!.Alive);
        Assert.Null(owner.Socket.Connection);
        logs.AssertOneError("LegacyNativeGameClientRuntime.Send");
    });

    [Theory]
    [InlineData(false, "0C00800801020304050607085451536572766572")]
    [InlineData(true, "0C00800801020304050607085451536572766572")]
    public Task HeaderAndTrailingSealCommitToTheOriginalAfterRealQueueAdmission(
        bool nonzeroHeader, string expectedHex) => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope();
        var queue = new Queue<byte[]>();
        var owner = Owner(queue);
        var source = Unsealed.ToArray();
        if (nonzeroHeader) { source[0] = 0x0C; source[1] = 0; }
        var player = owner.Player;

        owner.Send(source);

        var expected = Convert.FromHexString(expectedHex);
        var queued = Assert.Single(queue);
        Assert.Equal(expected, source);
        Assert.Equal(expected, queued);
        Assert.NotSame(source, queued);
        Assert.Same(player, owner.Player);
        Assert.Same(owner, player.Owner);
        Assert.Null(owner.Socket!.Connection); // MODEL queue, not native Create or host delivery.
        source[4] ^= 0xFF;
        source[^1] ^= 0xFF;
        Assert.Equal(expected, queued);
        logs.AssertNoErrors();
    });

    [Fact]
    public Task ByteSendsEncryptTheSealedBytesAndContinueTheActualNativeCipher() => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope();
        var queue = new Queue<byte[]>();
        var owner = Owner(queue);
        owner.Socket!.Crypto = Cipher();
        var first = Unsealed.ToArray();
        var second = Convert.FromHexString("000080080A0B0C0DCCCCCCCCCCCCCCCC");
        var expectedSecondPlain = Convert.FromHexString("080080080A0B0C0D5451536572766572");
        var control = new LegacyGameServerCipher(Key);
        var expectedFirst = new byte[Sealed.Length];
        var expectedSecond = new byte[expectedSecondPlain.Length];
        control.Encrypt(Sealed, expectedFirst);
        control.Encrypt(expectedSecondPlain, expectedSecond);

        owner.Send(first);
        owner.Send(second);

        Assert.Equal(Sealed, first);
        Assert.Equal(expectedSecondPlain, second);
        var entries = queue.ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal(expectedFirst, entries[0]);
        Assert.Equal(expectedSecond, entries[1]);
        Assert.NotSame(first, entries[0]);
        Assert.NotSame(second, entries[1]);
        Assert.Null(owner.Socket.Connection);
        logs.AssertNoErrors();
    });

    [Fact]
    public Task MissingActualQueueRefusesBeforeSourceMutationOrCipherAdvance() => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope();
        var socket = new NativeSocket(null!, null!) { Alive = true, Crypto = Cipher() };
        var owner = new LegacyNativeGameClientRuntime { Socket = socket };
        var first = Unsealed.ToArray();
        var second = Unsealed.ToArray();
        var control = new LegacyGameServerCipher(Key);
        var expectedFirstQueued = new byte[Sealed.Length];
        control.Encrypt(Sealed, expectedFirstQueued);

        owner.Send(first);

        Assert.Equal(Unsealed, first);
        Assert.Null(QueueField.GetValue(socket));
        var queue = new Queue<byte[]>();
        QueueField.SetValue(socket, queue); // MODEL prestate only; no Create/pump/socket binding.
        owner.Send(second);
        Assert.Equal(Sealed, second);
        Assert.Equal(expectedFirstQueued, Assert.Single(queue));
        Assert.True(socket.Alive);
        Assert.Null(socket.Connection);
        logs.AssertNoErrors();
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public Task ShortBufferRefusalIsLoggedWithoutAnyCallerHeaderPublication(int length) => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope();
        var queue = new Queue<byte[]>();
        var owner = Owner(queue);
        var source = new byte[length];
        var expected = source.ToArray();
        owner.Send(source);

        Assert.Equal(expected, source);
        Assert.Empty(queue);
        Assert.Null(owner.Socket!.Connection);
        logs.AssertOneError("LegacyNativeGameClientRuntime.Send");
    });

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public Task TrailerMustNotOverwriteTheFourByteHeaderOnRejectedTinyBuffers(int length) => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope();
        var queue = new Queue<byte[]>();
        var owner = Owner(queue);
        owner.Socket!.Crypto = Cipher();
        var source = new byte[length];

        owner.Send(source);

        Assert.Equal(new byte[length], source); Assert.Empty(queue);
        var next = Unsealed.ToArray(); owner.Send(next);
        var expected = new byte[Sealed.Length]; new LegacyGameServerCipher(Key).Encrypt(Sealed, expected);
        Assert.Equal(Sealed, next); Assert.Equal(expected, Assert.Single(queue));
        Assert.Null(owner.Socket!.Connection);
        logs.AssertOneError("LegacyNativeGameClientRuntime.Send");
    });

    [Fact]
    public Task HugeBufferReturnsWithoutMutationQueueingOrExceptionCounterAdvance() => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope();
        var queue = new Queue<byte[]>();
        var owner = Owner(queue);
        var source = Enumerable.Repeat((byte)0xCC, 1033).ToArray();
        source[0] = source[1] = 0;
        source[2] = 0x74;
        source[3] = 0x09;
        var before = source.ToArray();

        owner.Send(source);

        Assert.Equal(before, source);
        Assert.Empty(queue);
        Assert.True(owner.Socket!.Alive);
        Assert.Null(owner.Socket.Connection);
        logs.AssertNoErrors(); // Native warning is WriteLine, not WriteException.
    });

    [Fact]
    public Task Actual1024ByteInputHasABoundedAllocationAndCopyControl() => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope();
        var queue = new Queue<byte[]>();
        var owner = Owner(queue);
        var source = Enumerable.Repeat((byte)0xA5, 1024).ToArray();
        source[0] = source[1] = 0;
        var expected = source.ToArray();
        expected[0] = 0xF8;
        expected[1] = 0x03;
        Encoding.ASCII.GetBytes("TQServer").CopyTo(expected, expected.Length - 8);

        owner.Send(source);

        Assert.Equal(expected, source);
        Assert.Equal(expected, Assert.Single(queue));
        Assert.Null(owner.Socket!.Connection);
        logs.AssertNoErrors();
        // The current constructor reserves the actual input length. Separate
        // corrected-only controls cover1025/1032 without executing old unsafe code.
    });

    [Fact]
    public Task PacketConstructorRefusalIsLoggedBeforeCallerBytesOrCipherCanChange() => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope();
        var queue = new Queue<byte[]>();
        var owner = Owner(queue);
        owner.Socket!.Crypto = Cipher();
        var source = Unsealed.ToArray();

        DeepCall(85, () => owner.Send(source));

        Assert.Equal(Unsealed, source);
        Assert.Empty(queue);
        var later = Unsealed.ToArray(); owner.Send(later);
        var expected = new byte[Sealed.Length]; new LegacyGameServerCipher(Key).Encrypt(Sealed, expected);
        Assert.Equal(Sealed, later); Assert.Equal(expected, Assert.Single(queue));
        Assert.Null(owner.Socket!.Connection);
        logs.AssertOneError("frame count excceeded 70");
    });

    private static LegacyNativeGameClientRuntime Owner(Queue<byte[]> queue)
    {
        var socket = new NativeSocket(null!, null!) { Alive = true };
        QueueField.SetValue(socket, queue); // Explicit MODEL of otherwise-uncreated native SendQueue.
        return new LegacyNativeGameClientRuntime { Socket = socket };
    }

    private static LegacyNativeTQCast5 Cipher()
    {
        var cipher = new LegacyNativeTQCast5();
        cipher.GenerateKey(Key);
        return cipher;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void DeepCall(int depth, Action action)
    {
        if (depth == 0) action();
        else DeepCall(depth - 1, action);
        GC.KeepAlive(action);
    }

    private sealed class OwnedLogScope : IDisposable
    {
        private readonly FieldInfo pathField = typeof(LegacyNativeApplication).GetField(
            "startupPath", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? priorPath;
        private readonly LegacyNativeCounter counter;
        private readonly uint before;
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-byte-send-log-" + Guid.NewGuid().ToString("N"));

        internal OwnedLogScope()
        {
            priorPath = pathField.GetValue(null);
            Directory.CreateDirectory(root);
            pathField.SetValue(null, root); // Owned MODEL of actual cached path, never workspace/client logs.
            counter = (LegacyNativeCounter)typeof(LegacyNativeConsole).GetField(
                "ExceptionsCounter", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            before = counter.Count;
        }

        internal void AssertNoErrors()
        {
            Assert.Equal(before, counter.Count);
            Assert.Empty(Directory.GetFiles(root, "*.txt", SearchOption.AllDirectories));
        }

        internal void AssertOneError(string text)
        {
            Assert.Equal(unchecked(before + 1), counter.Count);
            var path = Assert.Single(Directory.GetFiles(root, "*.txt", SearchOption.AllDirectories));
            var log = File.ReadAllText(path);
            Assert.Contains("----Exception message----", log);
            Assert.Contains("----Stack trace----", log);
            Assert.Contains(text, log);
        }

        public void Dispose()
        {
            pathField.SetValue(null, priorPath);
            counter.Set(before);
            var full = Path.GetFullPath(root);
            var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), expectedParent) ||
                !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(full), "^wolf-byte-send-log-[0-9a-f]{32}$"))
                throw new InvalidOperationException("Owned byte-send log cleanup containment failed.");
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }
}
