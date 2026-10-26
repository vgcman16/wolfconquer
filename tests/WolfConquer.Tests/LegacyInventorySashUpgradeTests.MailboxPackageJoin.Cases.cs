using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualD13ExplicitPackageJoinCommitsFourRealImagesBeforeFirstOutputAndDuplicateCannotPayAgain(bool claimAll) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var observed = 0;
            join.SendObserver = _ =>
            {
                AssertPackageJoinCommitted(fixture, join, before, claimAll, 10);
                observed++;
            };
            var started = DateTime.Now;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            var ended = DateTime.Now;
            join.SendObserver = null;
            Assert.True(observed > 0);
            AssertPackageJoinCommitted(fixture, join, before, claimAll, 10);
            AssertPackageJoinFrames(fixture, join, before, claimAll, started, ended, before.FirstFrame);
            var committed = fixture.PackageFiles();
            var firstReplay = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            AssertPackageJoinFiles(fixture, committed);
            AssertPackageJoinCommitted(fixture, join, before, claimAll, 10);
            AssertPackageJoinReplay(fixture, claimAll, firstReplay);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualD13ExplicitPackageJoinReconnectRetainsOneReceiptAndNeverReallocatesOrPaysAgain(bool claimAll) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            await join.FrameAsync(PackageJoinRequest(claimAll));
            AssertPackageJoinCommitted(fixture, join, before, claimAll, 10);
            var committed = fixture.PackageFiles();
            var operation = AssertPackageJoinAllocation(fixture, join).Request.OperationId;
            var retired = join.Current;
            await join.RestartAsync();
            Assert.NotSame(retired, join.Current);
            AssertPackageJoinFiles(fixture, committed);
            Assert.Equal(operation, AssertPackageJoinAllocation(fixture, join).Request.OperationId);
            var firstReplay = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            AssertPackageJoinCommitted(fixture, join, before, claimAll, 10);
            AssertPackageJoinFiles(fixture, committed);
            AssertPackageJoinReplay(fixture, claimAll, firstReplay);
        });

    [Fact]
    public Task ActualD13ExplicitPackageJoinFirstSendFailureRetainsFullCommitAndReplayCannotPayAgain() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var attempts = 0;
            const string message = "owned package postcommit send fault";
            join.SendObserver = _ =>
            {
                AssertPackageJoinCommitted(fixture, join, before, claimAll: false, job: 10);
                attempts++;
                throw new IOException(message);
            };
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            join.SendObserver = null;
            Assert.Equal(1, attempts);
            Assert.Equal(before.FirstFrame + 1, fixture.Sent.Count);
            Assert.Contains(message, fixture.Log.ToString());
            AssertPackageJoinCommitted(fixture, join, before, claimAll: false, job: 10);
            var committed = fixture.PackageFiles();
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            Assert.Equal(before.FirstFrame + 1, fixture.Sent.Count);
            AssertPackageJoinFiles(fixture, committed);
            AssertPackageJoinCommitted(fixture, join, before, claimAll: false, job: 10);
        });

    private static byte[] PackageJoinRequest(bool claimAll) => claimAll
        ? Fixture.AtomicMailRequest(claimAll: true)
        : MailboxPackageRequest();

    private static void AssertPackageJoinReplay(Fixture fixture, bool claimAll, int firstFrame)
    {
        var frames = fixture.Sent.Skip(firstFrame).ToArray();
        if (!claimAll) { Assert.Empty(frames); return; }
        Assert.Equal(2, frames.Length);
        Assert.True(LegacyMailListPacket.TryParse(ReadFrame(frames[0]),
            ConquerProtocolProfile.VisibleDx8D13, out var refresh));
        Assert.Equal(LegacyMailListType.ClaimAllUpdate, refresh.Type);
        var mail = Assert.Single(refresh.Mails);
        Assert.Equal(0UL, mail.Flag);
        Assert.Equal(PackageJoinRequest(claimAll: true), frames[1]);
    }
}
