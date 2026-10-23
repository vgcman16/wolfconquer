using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ClassicTitleSafetyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ListStopsRemainingPreparedFramesWhenSourceRetiresAfterFirstSend(int kind) => InFixture(async fixture =>
    {
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        fixture.AfterSend = () =>
        {
            if (kind == 0) fixture.Sessions.Set(fixture.Connection, fixture.Original with { });
            if (kind == 1) fixture.Sessions.TryRemove(fixture.Connection, out _);
            if (kind == 2)
            {
                fixture.Sessions.TryRemove(fixture.Connection, out _);
                fixture.Sessions.Set(fixture.Connection, fixture.Original);
            }
            if (kind == 3) fixture.Sessions.Set(fixture.Connection, fixture.Original with
            { AccountId = Fixture.Uid + 1, Character = fixture.Original.Character with { Uid = Fixture.Uid + 1 } });
        };
        await fixture.Dispatch(LegacyTitleAction.List);
        Assert.Single(fixture.Sent);
        Assert.Equal((byte)2, fixture.Sent[0][8]);
        Assert.Equal(before, File.ReadAllBytes(fixture.SnapshotPath));
    });

    [Fact]
    public Task AckObserverReplacementCannotBeOverwrittenOrRefundDurablyCommittedTitle() => InFixture(async fixture =>
    {
        fixture.VerifyCommittedBeforeSend = true;
        WorldSession? replacement = null;
        fixture.AfterSend = () =>
        {
            replacement = fixture.Current() with { Character = fixture.Current().Character with { MapId = 1003 } };
            fixture.Sessions.Set(fixture.Connection, replacement);
        };
        await fixture.Dispatch();
        Assert.Single(fixture.Sent);
        Assert.Same(replacement, fixture.Current());
        Assert.Equal("7", fixture.ReadPersisted().Attributes["MyTitle"]);
    });

    [Fact]
    public Task CapturedIdentityCannotCommitAfterRemovalAndReinsertionOfExactSameSessionObject() => InFixture(fixture =>
    {
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(fixture.Sessions, fixture.Connection,
            Fixture.Uid, out var retiredIdentity));
        Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out _));
        fixture.Sessions.Set(fixture.Connection, fixture.Original);
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        var args = new object?[] { fixture.Context, fixture.Original, Fixture.WithSelection(fixture.Original, "7"),
            retiredIdentity, CancellationToken.None, null };
        // Invoke the real commit owner with a genuine retired identity; no writer
        // callback or alternate session gate is substituted.
        var method = typeof(WorldPacketHandlers).GetMethod("TryCommitClassicTitle", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.False((bool)method.Invoke(null, args)!);
        Assert.Equal("sessionIdentityChanged", args[5]);
        fixture.Reject(fixture.Original, before);
        return Task.CompletedTask;
    });

    [Fact]
    public Task PreparedAcknowledgementAndCanceledCommitLeaveFileSessionAndOutputUnchanged() => InFixture(fixture =>
    {
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(fixture.Sessions, fixture.Connection,
            Fixture.Uid, out var identity));
        var acknowledgement = new LegacyTitlePacket((uint)Fixture.Uid, 7, LegacyTitleAction.Change).ToFrameBytes();
        Assert.Equal(new byte[] { 0x0A, 0, 0x68, 8, 4, 3, 2, 1, 7, 3 }, acknowledgement);
        var prepared = Fixture.WithSelection(fixture.Original, "7");
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var args = new object?[] { fixture.Context, fixture.Original, prepared, identity, cancellation.Token, null };
        var method = typeof(WorldPacketHandlers).GetMethod("TryCommitClassicTitle", BindingFlags.Static | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, args));
        Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException);
        fixture.Reject(fixture.Original, before);
        return Task.CompletedTask;
    });
}
