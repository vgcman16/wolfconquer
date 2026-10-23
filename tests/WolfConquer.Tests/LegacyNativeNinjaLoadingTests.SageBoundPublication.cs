using System.Reflection;
using WolfConquer.Core.World;
using SageFlag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Fact]
    public Task SageBoundPublicationCannotCommitAnotherRealTokenEvenWhenPreparedWordsMatch()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var bindings = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
            using var publication = PrepareBoundSageToken(bindings, 60, out var first);
            Assert.True(vector.TryPrepareAdd((int)SageFlag.SageSpeedMode, 60, true, 0, false, out var second));
            Assert.NotSame(first, second);
            Assert.True(vector.TryGetPreparedWords(first, out var firstWords));
            Assert.True(vector.TryGetPreparedWords(second, out var secondWords)); Assert.Equal(firstWords, secondWords);
            var before = SagePreparedState.Capture(player);
            Assert.False(vector.TryCommitSage(second!, bindings, publication));
            before.Unchanged(player); Assert.Empty(scope.Queue); Assert.True(publication.HasPreparedStorage());
            Assert.False(vector.TryGetPreparedWords(second, out var consumed)); Assert.Empty(consumed);
            Assert.True(vector.TryCommitSage(first!, bindings, publication));
            publication.Publish(); scope.Sent(SageBitmap(17), SageArchive(17, 60));
            Assert.Equal(60, vector.ArrayFlags[(int)SageFlag.SageSpeedMode].Secounds);
        }
    });

    [Fact]
    public Task SageBoundPublicationRequiresItsExactBindingCaptureNotAnEquivalentReplacement()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var original = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
            var equivalent = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
            using var publication = PrepareBoundSageToken(original, 60, out var token);
            var before = SagePreparedState.Capture(player);
            Assert.True(original.IsCurrent()); Assert.True(equivalent.IsCurrent());
            Assert.False(vector.TryCommitSage(token!, equivalent, publication));
            before.Unchanged(player); Assert.Empty(scope.Queue); Assert.True(publication.HasPreparedStorage());
            Assert.False(vector.TryCommitSage(token!, original, publication)); // Rejected attempt is consumed.
            before.Unchanged(player);
        }
    });

    [Theory]
    [InlineData(60, 61u)] [InlineData(-1, 0x7FFFFFFFu)] [InlineData(int.MinValue, 0u)]
    public Task SageBoundPublicationRefusesTimerThatDoesNotMatchItsRealStagedRegistration(int seconds, uint archiveTimer)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var bindings = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
            Assert.True(vector.TryPrepareAdd((int)SageFlag.SageSpeedMode, seconds, true, 0, false, out var token));
            using var publication = LegacyNativePreparedSagePublication.Create(bindings, archiveTimer, token);
            var before = SagePreparedState.Capture(player);
            Assert.True(publication.HasPreparedStorage());
            Assert.False(vector.TryCommitSage(token!, bindings, publication));
            before.Unchanged(player); Assert.Empty(scope.Queue); Assert.True(publication.HasPreparedStorage());
        }
    });

    [Theory]
    [InlineData("wrong-flag")] [InlineData("keep-on-death")]
    [InlineData("invoker")] [InlineData("replace-existing")]
    public Task SageBoundPublicationCannotRepurposeGenericTokenWithDifferentSageGrantPolicy(string policy)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        if (policy == "replace-existing") Assert.True(vector.TryAdd((int)SageFlag.SageSpeedMode, 77, false, 7));
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var bindings = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
            int flag = (int)(policy == "wrong-flag" ? SageFlag.TeamLeader : SageFlag.SageSpeedMode);
            Assert.True(vector.TryPrepareAdd(flag, 60, policy != "keep-on-death", policy == "invoker" ? 3 : 0,
                policy == "replace-existing", out var token));
            using var publication = LegacyNativePreparedSagePublication.Create(bindings, 60, token);
            var before = SagePreparedState.Capture(player);
            Assert.False(vector.TryCommitSage(token!, bindings, publication));
            before.Unchanged(player); Assert.Empty(scope.Queue); Assert.True(publication.HasPreparedStorage());
        }
    });

    [Fact]
    public Task SageBoundDuplicateArchiveCannotBeMixedWithAnAbsentGrantToken()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var bindings = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
            Assert.True(vector.TryPrepareAdd((int)SageFlag.SageSpeedMode, 60, true, 0, false, out var token));
            using var publication = LegacyNativePreparedSagePublication.Create(bindings, 60, null);
            var before = SagePreparedState.Capture(player);
            Assert.False(vector.TryCommitSage(token!, bindings, publication));
            before.Unchanged(player); Assert.Empty(scope.Queue); Assert.True(publication.HasPreparedStorage());
            Assert.Equal(11, scope.Available); scope.Untaken(1);
        }
    });

    [Fact]
    public Task SageBoundFactoryRejectsTokenFromAnotherPhysicalVectorBeforeAnyRental()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var original = player.BitVector;
        Vector.PreparedChange? token;
        lock (original.SyncRoot) Assert.True(original.TryPrepareAdd((int)SageFlag.SageSpeedMode, 60, true, 0, false, out token));
        player.BitVector = new Vector(576);
        var bindings = new LegacyNativeSageModeBindings(scope.Ninja, client, player, player.BitVector, player.View);
        var before = SageAdmissionSnapshot.Capture(scope, player);
        lock (player.BitVector.SyncRoot)
            Assert.Throws<InvalidDataException>(() => LegacyNativePreparedSagePublication.Create(bindings, 60, token));
        before.Unchanged(scope, player); Assert.Empty(original.ArrayFlags); Assert.Equal(new uint[18], original.bits);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task SageBoundFactoryRequiresRealVectorGateForNewAndDuplicatePreparation(bool duplicate)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        Vector.PreparedChange? token = null;
        if (duplicate) Assert.True(vector.TryAdd((int)SageFlag.SageSpeedMode, 77, false, 7));
        else lock (vector.SyncRoot) Assert.True(vector.TryPrepareAdd((int)SageFlag.SageSpeedMode, 60, true, 0, false, out token));
        var bindings = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
        var before = SageAdmissionSnapshot.Capture(scope, player);
        Assert.Throws<InvalidDataException>(() => LegacyNativePreparedSagePublication.Create(bindings, 60, token));
        before.Unchanged(scope, player);
        if (!duplicate) lock (vector.SyncRoot) Assert.True(vector.TryGetPreparedWords(token, out _));
    });

    [Fact]
    public Task SageBoundFactoryRejectsRetiredUidWithoutRewritingTheCapturedArchiveOwner()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        var bindings = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
        lock (vector.SyncRoot)
        {
            Assert.True(vector.TryPrepareAdd((int)SageFlag.SageSpeedMode, 60, true, 0, false, out var token));
            player.UID = 99;
            var before = SageAdmissionSnapshot.Capture(scope, player);
            Assert.Throws<InvalidDataException>(() => LegacyNativePreparedSagePublication.Create(bindings, 60, token));
            before.Unchanged(scope, player); Assert.Equal(99u, player.UID);
        }
    });

    [Fact]
    public Task SageBoundFactoryCannotPrepareAnotherPublicationFromConsumedToken()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var bindings = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
            using var publication = PrepareBoundSageToken(bindings, 60, out var token);
            Assert.True(vector.TryCommitSage(token!, bindings, publication));
            var before = SagePreparedState.Capture(player); int available = scope.Available;
            Assert.Throws<InvalidDataException>(() => LegacyNativePreparedSagePublication.Create(bindings, 60, token));
            before.Unchanged(player); Assert.Empty(scope.Queue); Assert.Equal(available, scope.Available); scope.Untaken(2);
            // Helper-only commit control: deliberately no claim that a direct commit delivered packets.
        }
    });

    [Fact]
    public Task SageBoundPreparedWordsMustMatchPrivateTokenEvenWhenActualFrameBytesRemainIntact()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var bindings = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
            using var publication = PrepareBoundSageToken(bindings, 60, out var token);
            var privateWords = Assert.IsType<uint[]>(typeof(LegacyNativePreparedSagePublication)
                .GetField("expectedWords", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(publication));
            privateWords[0] ^= 1u << 6; // Real private evidence corruption, never a production escape/API.
            var before = SagePreparedState.Capture(player);
            Assert.True(publication.HasPreparedStorage());
            Assert.False(vector.TryCommitSage(token!, bindings, publication));
            before.Unchanged(player); Assert.Empty(scope.Queue); Assert.True(publication.HasPreparedStorage());
        }
    });

    [Fact]
    public Task SageBoundFactoryDerivesFullWidthUidFromRealCapturedOwnerForMatchingPlan()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        player.UID = uint.MaxValue;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var bindings = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
            using var publication = PrepareBoundSageToken(bindings, uint.MaxValue, out var token);
            Assert.True(vector.TryCommitSage(token!, bindings, publication));
            publication.Publish(); scope.Sent(SageBitmap(uint.MaxValue), SageArchive(uint.MaxValue, uint.MaxValue));
            Assert.Equal(-1, vector.ArrayFlags[(int)SageFlag.SageSpeedMode].Secounds);
        }
    });
}
