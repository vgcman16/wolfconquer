using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using SageFlag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("archive-size-large", false)] [InlineData("archive-size-large", true)]
    [InlineData("archive-size-short", false)] [InlineData("archive-size-short", true)]
    [InlineData("archive-position", false)] [InlineData("archive-position", true)]
    [InlineData("archive-content", false)] [InlineData("archive-content", true)]
    [InlineData("status-size-large", false)] [InlineData("status-size-short", false)]
    [InlineData("status-position", false)] [InlineData("status-content", false)]
    public Task SagePreparedFinalComparerCannotPublishChangedPacketMetadataOrBytes(string drift, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Client.Player; var vector = player.BitVector;
        int key = (int)(existing ? SageFlag.SageSpeedMode : SageFlag.TeamLeader);
        Assert.True(vector.TryAdd(key, 77, false, 7));
        using var pool = new SagePreparedPool();
        var status = existing ? null : pool.Packet(SageBitmap(17).Length);
        if (status is not null) pool.Enqueue(status);
        var archive = pool.Packet(SageArchive(17, 900).Length); pool.Enqueue(archive);
        var comparer = new SagePreparedFinalComparer(pool, existing ? 2 : 1);
        vector.ArrayFlags = new ConcurrentDictionary<int, Vector.Flag>(vector.ArrayFlags, comparer);
        var before = SagePreparedState.Capture(player);
        var changed = drift.StartsWith("status-", StringComparison.Ordinal) ? status! : archive;
        byte[]? preparedBytes = null;
        comparer.OnFinalRead = () =>
        {
            preparedBytes = SagePreparedStorage(changed);
            switch (drift)
            {
                case "archive-size-large": case "status-size-large": changed.Size = 1032; break;
                case "archive-size-short": case "status-size-short": changed.Size--; break;
                case "archive-position": case "status-position": changed.Seek(changed.Capacity + 1); break;
                case "archive-content": case "status-content":
                    Marshal.WriteByte((IntPtr)SagePacketMemory(changed), changed.Capacity - 1, 0xE5); break;
                default: throw new ArgumentOutOfRangeException(nameof(drift));
            }
        };
        comparer.Armed = true;

        Assert.False(scope.Ninja.TryAdmitSageMode(900));

        Assert.True(comparer.ReturnedAfterDrift); Assert.NotNull(preparedBytes);
        before.Unchanged(player); Assert.Empty(scope.Queue); Assert.Empty(pool.Queue);
        Assert.Equal(existing, vector.ArrayFlags.ContainsKey((int)SageFlag.SageSpeedMode));
        if (drift.EndsWith("size-large", StringComparison.Ordinal))
        {
            Assert.Equal(1032, changed.Size); Assert.True(changed.Size > changed.Capacity);
        }
        else if (drift.EndsWith("size-short", StringComparison.Ordinal)) Assert.Equal(changed.Capacity - 1, changed.Size);
        if (drift.EndsWith("content", StringComparison.Ordinal))
        {
            var after = SagePreparedStorage(changed)!;
            Assert.Equal((byte)0xE5, after[^1]);
            Assert.Equal(preparedBytes![..^1], after[..^1]);
        }
        else Assert.Equal(preparedBytes, SagePreparedStorage(changed));
        Assert.Empty(scope.Log);
        for (int index = 0; index < 12; index++) scope.Untaken(index);
    });

    private static unsafe IntPtr SagePacketMemory(WolfConquer.Core.Networking.LegacyNativePacket packet)
        => (IntPtr)packet.Memory;
}
