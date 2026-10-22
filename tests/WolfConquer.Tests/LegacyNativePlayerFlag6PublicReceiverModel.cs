using System.Buffers;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Test-only declared MODEL ownership/prestate. It does not earn TeamLeader or socket birth.
internal sealed class Flag6PublicReceiverSupplementModel
{
    private readonly TaskCompletionSource _swapped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _swapCount;
    internal static readonly DateTime Origin = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Local);

    internal Flag6PublicReceiverSupplementModel(bool originalVisible)
    {
        OriginalVisible = originalVisible;
        Client = new LegacyNativeGameClientRuntime();
        Original = Client.Player;
        Replacement = new LegacyNativePlayerRuntime(Client);
        if (originalVisible)
            Assert.True(NativeFlag6TestModel.AddFlagFixture(Original, Origin));
        else
            Assert.True(NativeFlag6TestModel.AddFlagFixture(Replacement, Origin.AddSeconds(1)));
        OriginalRow = NativeFlag6TestModel.Current(Original);
        ReplacementRow = NativeFlag6TestModel.Current(Replacement);
        Assert.Equal(originalVisible, NativeFlag6TestModel.Visible(Original));
        Assert.Equal(!originalVisible, NativeFlag6TestModel.Visible(Replacement));
        Assert.NotSame(Original, Replacement);
    }

    internal bool OriginalVisible { get; }
    internal LegacyNativeGameClientRuntime Client { get; }
    internal LegacyNativePlayerRuntime Original { get; }
    internal LegacyNativePlayerRuntime Replacement { get; }
    internal LegacyNativeStatusFlagsBigVector32.Flag? OriginalRow { get; }
    internal LegacyNativeStatusFlagsBigVector32.Flag? ReplacementRow { get; }
    internal int SwapCount => Volatile.Read(ref _swapCount);
    internal Task Swapped => _swapped.Task;
    internal void CaptureCallbackFailure(Exception error) => _swapped.TrySetException(error);

    internal async ValueTask SwapAsync(ConquerTcpConnection connection, Func<WorldSession> currentSession)
    {
        try
        {
            Assert.Equal(0, SwapCount);
            Assert.Same(Client, connection.NativeGameClient);
            Assert.Same(Original, Client.Player);
            var before = currentSession();
            Assert.Same(Client, before.NativeGameClient);
            Assert.Same(Original, before.NativePlayer);
            await Task.Yield(); // A genuine asynchronous send callback, not only a synchronous mutation.
            Assert.Same(Original, Client.Player);
            Client.Player = Replacement; // SAME client, new physical Player; no Store.Set/UID recapture.
            Assert.Equal(1, Interlocked.Increment(ref _swapCount));
            Assert.Same(Replacement, before.NativePlayer); // Even an old record follows current Player.
            Assert.Same(Replacement, currentSession().NativePlayer);
            _swapped.TrySetResult();
        }
        catch (Exception error)
        {
            _swapped.TrySetException(error); // Preserve the callback assertion even if production catches it.
            throw;
        }
    }

    internal void AssertRetained(WorldSession current)
    {
        Assert.True(Swapped.IsCompletedSuccessfully);
        Assert.Equal(1, SwapCount);
        Assert.Same(Client, current.NativeGameClient);
        Assert.Same(Replacement, current.NativePlayer);
        Assert.Same(Replacement, Client.Player);
        Assert.Same(Client, Original.Owner);
        Assert.Same(Client, Replacement.Owner);
        Assert.Same(OriginalRow, NativeFlag6TestModel.Current(Original));
        Assert.Same(ReplacementRow, NativeFlag6TestModel.Current(Replacement));
        Assert.Equal(OriginalVisible, NativeFlag6TestModel.Visible(Original));
        Assert.Equal(!OriginalVisible, NativeFlag6TestModel.Visible(Replacement));
        if (OriginalRow is not null)
        {
            Assert.Equal(Origin, OriginalRow.Timer);
            Assert.Equal(NativeFlag6TestModel.NativeSeconds, OriginalRow.Secounds);
            Assert.False(OriginalRow.RemoveOnDead);
        }
        if (ReplacementRow is not null)
        {
            Assert.Equal(Origin.AddSeconds(1), ReplacementRow.Timer);
            Assert.Equal(NativeFlag6TestModel.NativeSeconds, ReplacementRow.Secounds);
            Assert.False(ReplacementRow.RemoveOnDead);
        }
    }

    internal void AssertVector(byte[] bytes, uint uid, bool dead)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(bytes), out var packet));
        Assert.Equal(uid, packet.Uid);
        var entry = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, entry.Type);
        var expected = new ulong[18];
        expected[0] = (OriginalVisible ? LegacyNativePlayerFlag6Lifecycle.Mask : 0UL) |
            (dead ? 1UL << 5 : 0UL);
        Assert.NotNull(entry.Values);
        Assert.Equal(expected, entry.Values); // All18 words, not only a masked subset.
    }

    internal static bool FirstMaximum(byte[] bytes, uint uid) =>
        LegacyMsgUpdatePacket.TryParse(Frame(bytes), out var update) && update.Uid == uid &&
        update.Updates.Count == 1 && update.Updates[0].Type == LegacyMsgUpdateDataType.MaxHitpoints;

    internal static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
