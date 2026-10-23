using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using SageFlag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(60u)]
    [InlineData(0x7FFFFFFFu)]
    [InlineData(0x80000000u)]
    [InlineData(uint.MaxValue)]
    public Task SageModeRegistersRealTimedDeadRemovedFlagThenSendsBitmapAndFullWidthArchive(uint timer) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var vector = scope.Client.Player.BitVector;
            scope.Client.Rune = null; // SageSpeedMode is not an XP-flag Rune lookup.
            scope.Client.Map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
            scope.Client.Player.Map = 1002; scope.Client.Player.X = 20; scope.Client.Player.Y = 20;
            scope.Client.Player.XPBooster = 77; scope.Client.Player.XPKiller = 66;
            var before = DateTime.Now;
            try
            {
                scope.Ninja.SageMode(timer);
                var after = DateTime.Now;
                var row = vector.ArrayFlags[(int)SageFlag.SageSpeedMode];
                Assert.Same(vector, scope.Client.Player.BitVector);
                Assert.True(scope.Client.Player.ContainFlag(SageFlag.SageSpeedMode));
                Assert.True(vector.Contain((int)SageFlag.SageSpeedMode));
                Assert.Equal((int)SageFlag.SageSpeedMode, row.Key);
                Assert.Equal(unchecked((int)timer), row.Secounds);
                Assert.InRange(row.Timer, before, after);
                Assert.True(row.RemoveOnDead); Assert.Equal(0, row.InvokerSecouds);
                Assert.Equal(default, row.TimerInvoker);
                Assert.Same(row, Assert.Single(vector.GetFlags()));
                DateTime expiry = row.Timer.AddSeconds(unchecked((int)timer));
                Assert.False(row.Expire(expiry.AddTicks(-1))); Assert.True(row.Expire(expiry));
                Assert.Equal(unchecked((int)timer) <= 0, vector.InLife((int)SageFlag.SageSpeedMode, row.Timer));
                Assert.Equal((byte)77, scope.Client.Player.XPBooster); Assert.Equal((byte)66, scope.Client.Player.XPKiller);
                scope.Sent(SageBitmap(17), SageArchive(17, timer));
                scope.Serialized(0, SageBitmap(17));
                AssertSageArchiveBuffer(scope, 1, 17, timer);
                Assert.Equal(10, scope.Available); scope.Untaken(2);
                vector.GetClear();
                Assert.False(vector.ContainFlag((int)SageFlag.SageSpeedMode));
                Assert.False(vector.Contain((int)SageFlag.SageSpeedMode));
                Assert.Empty(vector.GetFlags());
                Assert.Equal(2, scope.Queue.Count); Assert.Empty(scope.Log);
            }
            finally
            {
                // Idempotent fixture reset of the exact packet; SageMode now disposes
                // its real archive rental synchronously.
                LegacyNativePacketRecycle.Reuse(scope.Used(1));
            }
        });

    [Fact]
    public Task SageModeDuplicateCallsKeepOriginalRegistrationButAlwaysSendEachNewArchiveTimer() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            try
            {
                scope.Ninja.SageMode(10);
                var vector = scope.Client.Player.BitVector;
                var row = vector.ArrayFlags[(int)SageFlag.SageSpeedMode];
                DateTime originalTimer = row.Timer;
                var cached = vector.GetFlags();
                // Coherent duplicate prestate; malformed visibility is a separate refusal control.
                scope.Ninja.SageMode(20);
                scope.Ninja.SageMode(20); // No equality guard: an identical timer still sends.
                Assert.Same(row, vector.ArrayFlags[(int)SageFlag.SageSpeedMode]);
                Assert.Equal(originalTimer, row.Timer); Assert.Equal(10, row.Secounds);
                Assert.True(row.RemoveOnDead); Assert.True(vector.Contain((int)SageFlag.SageSpeedMode));
                var current = vector.GetFlags(); Assert.NotSame(cached, current);
                Assert.Same(row, Assert.Single(cached)); Assert.Same(row, Assert.Single(current));
                scope.Sent(SageBitmap(17), SageArchive(17, 10), SageArchive(17, 20), SageArchive(17, 20));
                scope.Serialized(0, SageBitmap(17));
                AssertSageArchiveBuffer(scope, 1, 17, 10);
                AssertSageArchiveBuffer(scope, 2, 17, 20);
                AssertSageArchiveBuffer(scope, 3, 17, 20);
                Assert.Equal(8, scope.Available); scope.Untaken(4);
            }
            finally
            {
                foreach (int index in new[] { 1, 2, 3 }) LegacyNativePacketRecycle.Reuse(scope.Used(index));
            }
        });

    [Fact]
    public Task SageModeTargetsItsLiveUserPlayerRatherThanTheReceivingFixtureClientOrMyNinja() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var foreignQueue = new Queue<byte[]>();
            var foreign = NativeSpellTestFixture.CreateOwner(foreignQueue);
            foreign.Player.UID = 999;
            foreign.MyNinja = null; foreign.Rune = null;
            scope.Ninja.user = foreign;
            try
            {
                scope.Ninja.SageMode(30);
                Assert.True(foreign.Player.ContainFlag(SageFlag.SageSpeedMode));
                Assert.False(scope.Client.Player.ContainFlag(SageFlag.SageSpeedMode));
                Assert.Equal(SageBitmap(999), foreignQueue.ElementAt(0));
                Assert.Equal(SageArchive(999, 30), foreignQueue.ElementAt(1));
                Assert.Equal(2, foreignQueue.Count); Assert.Empty(scope.Queue);
                Assert.Same(foreign, scope.Ninja.user);
                scope.Serialized(0, SageBitmap(999)); AssertSageArchiveBuffer(scope, 1, 999, 30);
                Assert.Equal(10, scope.Available);
            }
            finally { LegacyNativePacketRecycle.Reuse(scope.Used(1)); }
        });

    [Fact]
    public Task SageModeFakeRecipientStillRegistersAndSerializesBothActualUpdates() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Client.Fake = true; scope.Client.Socket = null!;
            try
            {
                scope.Ninja.SageMode(uint.MaxValue);
                var row = scope.Client.Player.BitVector.ArrayFlags[(int)SageFlag.SageSpeedMode];
                Assert.Equal(-1, row.Secounds); Assert.True(row.RemoveOnDead);
                Assert.Empty(scope.Queue); Assert.Equal(10, scope.Available);
                scope.Serialized(0, SageBitmap(17)); AssertSageArchiveBuffer(scope, 1, 17, uint.MaxValue);
                Assert.Empty(scope.Log);
            }
            finally { LegacyNativePacketRecycle.Reuse(scope.Used(1)); }
        });

    private static byte[] SageBitmap(uint uid)
    {
        var payload = new List<byte>(); Field(payload, 1, uid);
        var nested = new List<byte>(); Field(nested, 1, 25);
        for (int word = 0; word < 18; word++)
            Field(nested, 3, word == 8 ? 131072u : 0u); // Native SageSpeedMode273: word8, bit17.
        Varint(payload, 18); Varint(payload, (ulong)nested.Count); payload.AddRange(nested);
        return Frame(2176, payload);
    }

    private static byte[] SageArchive(uint uid, uint timer)
    {
        var payload = new List<byte>(); Field(payload, 1, uid);
        var nested = new List<byte>(); Field(nested, 1, 120);
        Field(nested, 4, 273);
        if (timer != 0) Field(nested, 5, timer);
        Varint(payload, 18); Varint(payload, (ulong)nested.Count); payload.AddRange(nested);
        return Frame(2176, payload);
    }

    private static void AssertSageArchiveBuffer(Scope scope, int index, uint uid, uint timer)
    {
        byte[] expected = SageArchive(uid, timer);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Used(index)));
        Assert.Equal(expected.Length, scope.Used(index).Size);
        Assert.Equal(0, scope.Used(index).Position); // Real using rental was synchronously returned.
    }
}
