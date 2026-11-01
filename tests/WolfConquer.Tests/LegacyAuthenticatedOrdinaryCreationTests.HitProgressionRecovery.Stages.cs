using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    private const uint HitActorAccount = DeathTargetAccount + 1;

    internal sealed partial class CreationFixture
    {
        internal async Task ConnectHitRecoveryActorAsync(uint account)
        {
            RuneConnection = NewConnection(account);
            await SendOnAsync(RuneConnection, new LegacyMsgLoginGamePacket(account).ToFrameBytes());
            var actor = CurrentFor(RuneConnection);
            Assert.Equal((long)account, actor.AccountId);
            Assert.Equal((long)RuneActor, actor.Character.Uid);
            Assert.NotEqual(actor.AccountId, actor.Character.Uid);
            Assert.True(Store.IsBoundSpellOwnerCurrent(actor.NativeGameClient!.OrdinarySpellBinding!));
            Frames(account).Clear();
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public Task ActualDifferentAccountActorHitRecoversBothParticipants(int boundary) =>
        RunHitRecoveryAsync(false, async (f, target) =>
        {
            var actor = f.CurrentFor(f.RuneConnection!);
            Assert.Equal((long)HitActorAccount, actor.AccountId);
            Assert.Equal((long)CreationFixture.RuneActor, actor.Character.Uid);
            var capture = await CaptureHitRecoveryAsync(f, target, false, boundary);
            Assert.Equal(HitActorAccount, capture.ActorAccount);
            await AssertHitRecoveryRestartsAsync(capture);
        }, actorAccount: HitActorAccount);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task EarnedHitOwnTruncatedStageRecoversOrCorruptPrefixRefuses(bool corrupt) =>
        RunHitRecoveryAsync(false, async (f, target) =>
        {
            var capture = await CaptureHitRecoveryAsync(f, target, false, 1, flushed: true);
            string path = capture.Targets[0];
            string stage = path + ".managed-trade-" + capture.Transaction + ".tmp";
            string key = Path.GetRelativePath(f.Data.Root, path);
            byte[] after = capture.After[key];
            Assert.True(after.Length > 2);
            Assert.Equal(after, File.ReadAllBytes(stage));
            await f.CloseAsync();
            // Declared interrupted-write input, applied only to the actual
            // issuer's own stage after the real owner has closed.
            var prefix = after[..(after.Length / 2)];
            if (corrupt) prefix[^1] ^= 1;
            File.WriteAllBytes(stage, prefix);
            if (!corrupt)
            {
                await AssertHitRecoveryRestartsAsync(capture, alreadyClosed: true);
                return;
            }

            var files = f.Data.Files();
            int output = f.Sent.Count + f.Frames(capture.ActorAccount).Count + f.Frames(DeathTargetAccount).Count;
            for (int restart = 0; restart < 2; restart++)
            {
                Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason), reason);
                Assert.Null(opened); Assert.False(string.IsNullOrWhiteSpace(reason));
                f.Data.AssertFiles(files); AssertHistoricalFiles(capture.History, HistoryFiles(f));
                Assert.Equal(capture.Ledger, File.ReadAllBytes(f.Data.State));
                Assert.Equal(output, f.Sent.Count + f.Frames(capture.ActorAccount).Count + f.Frames(DeathTargetAccount).Count);
                using var lease = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
        });
}
