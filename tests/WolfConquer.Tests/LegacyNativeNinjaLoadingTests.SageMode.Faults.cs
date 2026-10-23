using WolfConquer.Core.World;
using SageFlag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task SageModeMissingPoolCannotPublishNewRegistrationOrDuplicateArchive(bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Client.Player; var vector = player.BitVector;
        if (existing) Assert.True(vector.TryAdd((int)SageFlag.SageSpeedMode, 77, false, 7));
        var before = SageAdmissionSnapshot.Capture(scope, player);
        scope.UseNullBin();
        try
        {
            // Both absent and duplicate grants contain preparation failure before
            // registration or success output; no propagated pool fault is required.
            Assert.False(scope.Ninja.TryAdmitSageMode(10));
        }
        finally { scope.RestoreOwnedBin(); }
        before.Unchanged(scope, player);
        Assert.Equal(existing, vector.ArrayFlags.ContainsKey((int)SageFlag.SageSpeedMode));
    });

    [Theory]
    [InlineData("user")] [InlineData("player")] [InlineData("vector")]
    public Task SageModeMissingRequiredOwnerRefusesBeforeEitherPacketTake(string missing)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Client.Player;
        if (missing == "user") scope.Ninja.user = null!;
        if (missing == "player") scope.Client.Player = null!;
        if (missing == "vector") player.BitVector = null!;
        var before = SageAdmissionSnapshot.Capture(scope, player);
        scope.Ninja.SageMode(10);
        before.Unchanged(scope, player);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task SageModeNewFlagRejectsUnavailableViewBeforeRegistrationOrArchive(bool missingMapView)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Client.Player;
        if (missingMapView) scope.Client.Map = new LegacyNativeGameMap(72, 72, 1002);
        else player.View = null!;
        var before = SageAdmissionSnapshot.Capture(scope, player);
        scope.Ninja.SageMode(10);
        before.Unchanged(scope, player);
        Assert.Empty(player.BitVector.ArrayFlags);
        if (missingMapView) Assert.Null(scope.Client.Map!.View);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task SageModeDuplicateRejectsUnavailableViewWithoutArchiveOrRenewal(bool missingMapView)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Client.Player; var vector = player.BitVector;
        Assert.True(vector.TryAdd((int)SageFlag.SageSpeedMode, 77, false, 7));
        if (missingMapView) scope.Client.Map = new LegacyNativeGameMap(72, 72, 1002);
        else player.View = null!;
        var before = SageAdmissionSnapshot.Capture(scope, player);
        scope.Ninja.SageMode(20);
        before.Unchanged(scope, player);
        Assert.Same(before.Rows[0].Row, vector.ArrayFlags[(int)SageFlag.SageSpeedMode]);
        if (missingMapView) Assert.Null(scope.Client.Map!.View);
    });

    [Fact]
    public Task SageModeUnavailableBitmapRefusesWithoutRegistrationOrArchivePrefix()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Client.Player; var vector = player.BitVector;
        vector.bits = null!;
        var before = SageAdmissionSnapshot.Capture(scope, player);
        scope.Ninja.SageMode(10);
        before.Unchanged(scope, player);
        Assert.Empty(vector.ArrayFlags); Assert.Null(vector.bits); Assert.Empty(vector.GetFlags());
    });
}
