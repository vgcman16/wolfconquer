using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CombinedOwnedMailboxPreparesMoneyFromItsFinalDeferredProposalAndCannotPayTwice(bool claimAll) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            using var metadata = new NativeLoginBeastMetadataScope(1000);
            var owners = await join.PrepareCombinedAdmissionOwnersAsync();
            var native = await join.StartCombinedNativeAdmissionAsync(owners);
            Assert.NotNull(native.OrdinarySpellBinding);
            Assert.False(native.FullLoading);
            var before = CapturePackageJoin(fixture, join);
            Assert.Equal(before.Owner.Character.Money, native.Player.Money);
            var expectedMoney = checked(before.Owner.Character.Money + (claimAll ? (long)AtomicMailMoney : 0L));
            Exception? observed = null; int observations = 0;
            join.SendObserver = _ =>
            {
                observations++;
                try
                {
                    Assert.Equal(expectedMoney, native.Player.Money);
                    Assert.Equal(join.Current.Character.Money, native.Player.Money);
                }
                catch (Exception error) { observed ??= error; }
            };
            var started = DateTime.Now;

            await join.FrameAsync(PackageJoinRequest(claimAll));

            var ended = DateTime.Now; join.SendObserver = null;
            if (observed is not null) ExceptionDispatchInfo.Capture(observed).Throw();
            Assert.True(observations > 0);
            Assert.Equal(expectedMoney, native.Player.Money);
            Assert.Equal(1031U, native.Player.ClassExperience);
            AssertPackageJoinCommitted(fixture, join, before, claimAll, 10);
            AssertPackageJoinFrames(fixture, join, before, claimAll, started, ended, before.FirstFrame,
                sendClassExperience: false);
            var committed = fixture.PackageFiles(); int replay = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            Assert.Equal(expectedMoney, native.Player.Money);
            Assert.Equal(1031U, native.Player.ClassExperience);
            AssertPackageJoinFiles(fixture, committed); AssertPackageJoinReplay(fixture, claimAll, replay);
        });
}
