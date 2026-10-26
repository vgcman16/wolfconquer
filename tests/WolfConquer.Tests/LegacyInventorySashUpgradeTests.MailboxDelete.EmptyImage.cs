using System.Buffers.Binary;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task ActualD13FrameMailboxDeletionOfLastRowsCommitsCanonicalEmptyImageThenReplayIsSilent() =>
        RunAsync(async fixture =>
        {
            await fixture.EnterAtomicMailFrameAsync(rewardMoney: 0, rewardCp: 0, hasAttachment: false);
            // Empty the other row through its genuine currency action, not direct cache/file edits.
            await fixture.FrameAsync(new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimMoney, 22, []).ToFrameBytes());
            var rows = new LegacyMailboxStore(fixture.Root).Load(Fixture.Uid);
            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.True(row.CanDelete));
            var before = fixture.CaptureTransferFrame();
            var mailPath = Path.GetRelativePath(fixture.Root, fixture.AtomicMailPath);
            Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(before.Files[mailPath]));
            var expected = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase)
            {
                [mailPath] = new byte[4], // Complete managed count-zero mailbox, not an absent/truncated file.
            };
            var request = new LegacyMailboxModePacket(LegacyMailboxModeType.DeleteClaim, 81,
                [AtomicMailId, 22UL, AtomicMailId]).ToFrameBytes();
            var observed = 0;
            fixture.SendObserver = _ => { fixture.AssertFiles(expected); observed++; };

            await fixture.FrameAsync(request);

            fixture.SendObserver = null;
            Assert.Equal(2, observed);
            fixture.AssertFiles(expected);
            var frames = fixture.Sent.Skip(before.FrameCount).ToArray();
            Assert.Equal(2, frames.Length);
            Assert.All(frames, frame => Assert.Equal(request, frame));
            Assert.Empty(new LegacyMailboxStore(fixture.Root).Load(Fixture.Uid));
            var first = fixture.Sent.Count;
            await fixture.FrameAsync(request);
            fixture.AssertFiles(expected);
            Assert.Equal(first, fixture.Sent.Count);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        });
}
