using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ClassicTitleSafetyTests
{
    [Theory]
    [InlineData(LegacyTitleAction.List)]
    [InlineData(LegacyTitleAction.Change)]
    public Task StaleContextCannotOverwriteOrPublishIntoSameUidReplacement(LegacyTitleAction action) => InFixture(async fixture =>
    {
        var replacement = fixture.Original with { Character = fixture.Original.Character with { MapId = 1003 } };
        fixture.Sessions.Set(fixture.Connection, replacement);
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch(action);
        fixture.Reject(replacement, before);
    });

    [Theory]
    [InlineData(LegacyTitleAction.List)]
    [InlineData(LegacyTitleAction.Change)]
    public Task MissingSessionStoreCannotPublishOrPersist(LegacyTitleAction action) => InFixture(async fixture =>
    {
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch(action, context: fixture.Context with { Sessions = null });
        fixture.Reject(fixture.Original, before);
    });

    [Theory]
    [InlineData(LegacyTitleAction.List)]
    [InlineData(LegacyTitleAction.Change)]
    public Task MissingAuthenticatedContextSessionCannotBorrowCurrentStoreOwner(LegacyTitleAction action) => InFixture(async fixture =>
    {
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch(action, context: fixture.Context with { Session = null });
        fixture.Reject(fixture.Original, before);
    });

    [Theory]
    [InlineData(null)]
    [InlineData(".")]
    public Task MissingOrRelativeUsersRootCannotSelectAWriteTarget(string? root) => InFixture(async fixture =>
    {
        var invalid = fixture.Original with { UsersRoot = root };
        fixture.Sessions.Set(fixture.Connection, invalid);
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch(context: fixture.Context with { Session = invalid });
        fixture.Reject(invalid, before);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task InvalidCurrentBindingRejectsBeforePacketOrFileSelection(int kind) => InFixture(async fixture =>
    {
        var invalid = kind switch
        {
            0 => fixture.Original with { AccountId = 0 },
            1 => fixture.Original with { AccountId = Fixture.Uid + 1 },
            2 => fixture.Original with { AccountId = -1, Character = fixture.Original.Character with { Uid = -1 } },
            _ => fixture.Original with { AccountId = (long)uint.MaxValue + 1,
                Character = fixture.Original.Character with { Uid = (long)uint.MaxValue + 1 } },
        };
        fixture.Sessions.Set(fixture.Connection, invalid);
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch(context: fixture.Context with { Session = invalid });
        fixture.Reject(invalid, before);
    });

    [Theory]
    [InlineData("2,5,broken,7", "2")]
    [InlineData("2,256,7", "2")]
    [InlineData("2,-1,7", "2")]
    [InlineData("2,5,7", "256")]
    public Task CorruptSavedOwnerCannotGrantValidPrefixAuthorization(string titles, string selected) => InFixture(async fixture =>
    {
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch();
        fixture.Reject(fixture.Original, before);
    }, titles: titles, selected: selected);

    [Theory]
    [InlineData(LegacyTitleAction.Enqueue, (byte)7)]
    [InlineData(LegacyTitleAction.Dequeue, (byte)7)]
    [InlineData(LegacyTitleAction.Change, (byte)8)]
    public Task UnownedTitleAndClientPublicationActionsCannotMutate(LegacyTitleAction action, byte title) => InFixture(async fixture =>
    {
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch(action, title);
        fixture.Reject(fixture.Original, before);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingOrBlockedRecoveryOwnerRefusesChange(bool blocked) => InFixture(async fixture =>
    {
        if (blocked) fixture.Recovery.Block("owned recovery-required evidence");
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch(context: blocked ? fixture.Context : fixture.Context with { TradeRecovery = null });
        fixture.Reject(fixture.Original, before);
    });

    [Fact]
    public Task PreCanceledRequestAndShortPacketHaveNoPreparedSuccessOrEffects() => InFixture(async fixture =>
    {
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Dispatch(cancellationToken: cancellation.Token));
        await fixture.DispatchBytes(new PacketWriter(LegacyTitlePacket.Opcode, LegacyTitlePacket.PayloadLength - 1).ToArray());
        fixture.Reject(fixture.Original, before);
    });

    [Theory]
    [InlineData("MyTitle=2", "MyTitle=02")]
    [InlineData("Titles=2,5,7", "Titles=7,5,2")]
    [InlineData("MyTitle=2", "; selected absent")]
    [InlineData("Titles=2,5,7", "; owned absent")]
    [InlineData("UID=16909060", "UID=16909061")]
    public Task ExactTitlePresenceTextAndUidBaselineAreRequired(string from, string to) => InFixture(async fixture =>
    {
        fixture.Rewrite(text => text.Replace(from, to, StringComparison.Ordinal));
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch();
        fixture.Reject(fixture.Original, before);
    });

    [Fact]
    public Task AmbiguousOrOversizeDiskStateFailsBeforeCommit() => InFixture(async fixture =>
    {
        fixture.Rewrite(text => text + "\n[Character]\nMyTitle=7\n");
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch();
        fixture.Reject(fixture.Original, before);
        using (var file = new FileStream(fixture.SnapshotPath, FileMode.Open, FileAccess.Write, FileShare.None))
            file.SetLength(CharacterSnapshotFileBaseline.MaximumBytes + 1L);
        await fixture.Dispatch();
        Assert.Same(fixture.Original, fixture.Current());
        Assert.Empty(fixture.Sent);
        Assert.Equal(CharacterSnapshotFileBaseline.MaximumBytes + 1L, new FileInfo(fixture.SnapshotPath).Length);
    });

    [Fact]
    public Task MissingSaveRejectsWithoutCreatingOrPublishing() => InFixture(async fixture =>
    {
        File.Delete(fixture.SnapshotPath);
        await fixture.Dispatch();
        fixture.Reject(fixture.Original, null);
    });

    [WindowsSecondaryPasswordPersistenceFact]
    public Task GenuineReplacementFailureRetainsExactPreimageAndCanRetryAfterLeaseRelease() => InFixture(async fixture =>
    {
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        using (new FileStream(fixture.SnapshotPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await fixture.Dispatch();
            fixture.Reject(fixture.Original, before);
            Assert.Contains("classicTitleWriteFailed:", fixture.Log.ToString());
            Assert.False(fixture.Recovery.IsBlocked);
        }
        await fixture.Dispatch();
        Assert.Equal("7", fixture.ReadPersisted().Attributes["MyTitle"]);
        Assert.Single(fixture.Sent);
    });
}
