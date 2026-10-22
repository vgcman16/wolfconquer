using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using SageFlag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SageModeNullBinFaultDistinguishesNewFlagPublicationFromDuplicateArchiveAdmission(bool existing) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var vector = scope.Client.Player.BitVector;
            if (existing) Assert.True(vector.TryAdd((int)SageFlag.SageSpeedMode, 77, false, 7));
            var priorRow = existing ? vector.ArrayFlags[(int)SageFlag.SageSpeedMode] : null;
            scope.UseNullBin();
            try { Assert.Throws<NullReferenceException>(() => scope.Ninja.SageMode(10)); }
            finally { scope.RestoreOwnedBin(); }
            var row = vector.ArrayFlags[(int)SageFlag.SageSpeedMode];
            if (existing) Assert.Same(priorRow, row);
            Assert.Equal(existing ? 77 : 10, row.Secounds);
            Assert.Equal(existing ? 7 : 0, row.InvokerSecouds);
            Assert.Equal(!existing, row.RemoveOnDead);
            Assert.True(vector.Contain((int)SageFlag.SageSpeedMode));
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
        });

    [Theory]
    [InlineData("user")]
    [InlineData("player")]
    [InlineData("vector")]
    public Task SageModeMissingOwnerFaultsBeforeEitherPacketTake(string missing) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            if (missing == "user") scope.Ninja.user = null!;
            if (missing == "player") scope.Client.Player = null!;
            if (missing == "vector") scope.Client.Player.BitVector = null!;
            Assert.Throws<NullReferenceException>(() => scope.Ninja.SageMode(10));
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SageModeNewFlagViewFaultRetainsRegistrationAndOwnerBitmapButPreventsArchive(bool missingMapView) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            if (missingMapView) scope.Client.Map = new LegacyNativeGameMap(72, 72, 1002);
            else scope.Client.Player.View = null!;
            Assert.Throws<NullReferenceException>(() => scope.Ninja.SageMode(10));
            var row = scope.Client.Player.BitVector.ArrayFlags[(int)SageFlag.SageSpeedMode];
            Assert.Equal(10, row.Secounds); Assert.True(row.RemoveOnDead);
            scope.Sent(SageBitmap(17)); scope.Serialized(0, SageBitmap(17));
            Assert.Equal(11, scope.Available); scope.Untaken(1);
            if (missingMapView) Assert.Null(scope.Client.Map!.View);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SageModeDuplicateViewFaultOccursAfterOwnerArchiveSendWithNoBitmapOrRenewal(bool missingMapView) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var vector = scope.Client.Player.BitVector;
            Assert.True(vector.TryAdd((int)SageFlag.SageSpeedMode, 77, false, 7));
            var row = vector.ArrayFlags[(int)SageFlag.SageSpeedMode];
            DateTime timer = row.Timer;
            if (missingMapView) scope.Client.Map = new LegacyNativeGameMap(72, 72, 1002);
            else scope.Client.Player.View = null!;
            try
            {
                Assert.Throws<NullReferenceException>(() => scope.Ninja.SageMode(20));
                scope.Sent(SageArchive(17, 20)); AssertSageArchiveBuffer(scope, 0, 17, 20);
                Assert.Same(row, vector.ArrayFlags[(int)SageFlag.SageSpeedMode]);
                Assert.Equal(timer, row.Timer); Assert.Equal(77, row.Secounds);
                Assert.False(row.RemoveOnDead); Assert.Equal(7, row.InvokerSecouds);
                Assert.Equal(11, scope.Available); scope.Untaken(1);
                if (missingMapView) Assert.Null(scope.Client.Map!.View);
            }
            finally { LegacyNativePacketRecycle.Reuse(scope.Used(0)); }
        });

    [Fact]
    public Task SageModeBrokenBitmapFaultLeavesTheRealRegistrationPrefixBeforeAnyUpdateOrPacketTake() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var vector = scope.Client.Player.BitVector;
            vector.bits = null!;
            var before = DateTime.Now;
            Assert.Throws<NullReferenceException>(() => scope.Ninja.SageMode(10));
            var after = DateTime.Now;
            var row = vector.ArrayFlags[(int)SageFlag.SageSpeedMode];
            Assert.Equal(10, row.Secounds); Assert.True(row.RemoveOnDead);
            Assert.InRange(row.Timer, before, after); Assert.Null(vector.bits);
            Assert.Empty(vector.GetFlags()); // TryAdd faults before publishing its Update=true cache bit.
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
        });
}
