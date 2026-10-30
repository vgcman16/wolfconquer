using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CombinedOwnedMailboxPublishesItsFinalCpTupleBeforeRepliesAndCannotClaimTwice(bool claimAll) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            using var metadata = new NativeLoginBeastMetadataScope(1000);
            var owners = await join.PrepareCombinedAdmissionOwnersAsync();
            var native = await join.StartCombinedNativeAdmissionAsync(owners);
            Assert.NotNull(native.OrdinarySpellBinding); Assert.False(native.FullLoading);
            var before = CapturePackageJoin(fixture, join);
            var expected = (checked((uint)(before.Owner.Character.ConquerPoints + (claimAll ? AtomicMailCp : 0L))),
                checked((int)before.Owner.Character.BoundConquerPoints), checked((ulong)before.Owner.Character.BankConquerPoints));
            Assert.Equal((checked((uint)before.Owner.Character.ConquerPoints), checked((int)before.Owner.Character.BoundConquerPoints),
                checked((ulong)before.Owner.Character.BankConquerPoints)), CombinedNativeCps(native));
            Exception? observed = null; int replies = 0;
            join.SendObserver = sentFrame =>
            {
                replies++;
                try
                {
                    Assert.Equal(expected, CombinedNativeCps(native));
                    Assert.Equal(checked((long)expected.Item1), join.Current.Character.ConquerPoints);
                    Assert.Equal(checked((long)expected.Item2), join.Current.Character.BoundConquerPoints);
                    Assert.Equal(checked((long)expected.Item3), join.Current.Character.BankConquerPoints);
                }
                catch (Exception error) { observed ??= error; }
            };
            var started = DateTime.Now;

            await join.FrameAsync(PackageJoinRequest(claimAll));

            var ended = DateTime.Now; join.SendObserver = null;
            if (observed is not null) ExceptionDispatchInfo.Capture(observed).Throw();
            Assert.True(replies > 0); Assert.Equal(expected, CombinedNativeCps(native));
            Assert.Equal(1031U, native.Player.ClassExperience); Assert.False(native.FullLoading);
            AssertPackageJoinCommitted(fixture, join, before, claimAll, 10);
            AssertColdCpClaimFrames(fixture, join, before, claimAll, started, ended);
            var committed = fixture.PackageFiles(); int replay = fixture.Sent.Count;

            await join.FrameAsync(PackageJoinRequest(claimAll));

            Assert.Equal(expected, CombinedNativeCps(native)); Assert.Equal(1031U, native.Player.ClassExperience);
            AssertPackageJoinFiles(fixture, committed); AssertPackageJoinReplay(fixture, claimAll, replay);
        });

    private static (uint, int, ulong) CombinedNativeCps(LegacyNativeGameClientRuntime native) =>
        (native.Player.ConquerPoints, native.Player.BoundConquerPoints, native.ConquerPointsSave);

    private static void AssertColdCpClaimFrames(Fixture fixture, PackageJoin join, PackageJoinBefore before,
        bool claimAll, DateTime started, DateTime ended)
    {
        var intent = AssertPackageJoinAllocation(fixture, join);
        var items = ExpectedPackageJoinItems(before, intent, claimAll);
        var expected = new List<byte[]>();
        if (claimAll)
        {
            expected.Add(LegacyMsgMessagePacket.SystemTalk($"STR_PICK_MONEY@@{AtomicMailMoney}@@").ToFrameBytes());
            expected.Add(new LegacyMsgUpdatePacket(Fixture.Uid,
                [new(LegacyMsgUpdateDataType.Money, checked((ulong)before.Owner.Character.Money + AtomicMailMoney))]).ToFrameBytes());
            expected.Add(LegacyMsgMessagePacket.SystemTalk($"You received {AtomicMailCp} ConquerPoints.").ToFrameBytes());
            expected.Add(new LegacyMsgUpdatePacket(Fixture.Uid,
                [new(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)before.Owner.Character.ConquerPoints + AtomicMailCp))]).ToFrameBytes());
        }
        // This actual fresh native client is not fully loaded: ClassExperience
        // commits physically but its existing conditional update is absent.
        for (var index = 0; index < 6; index++) expected.Add(PackageJoinGeneratedFrame(items, intent, index));
        if (claimAll) expected.Add(LegacyGameItemPacket.FromSnapshotItem(
            Assert.Single(items.InventoryItems, row => row.Uid == AtomicMailItemUid)).ToFrameBytes());
        var actual = fixture.Sent.Skip(before.FirstFrame).ToArray();
        Assert.Equal(expected.Count + (claimAll ? 2 : 1), actual.Length);
        for (var index = 0; index < expected.Count; index++) Assert.Equal(expected[index], actual[index]);
        if (claimAll)
        {
            Assert.True(LegacyMailListPacket.TryParse(ReadFrame(actual[^2]),
                ConquerProtocolProfile.VisibleDx8D13, out var refresh));
            Assert.Equal(LegacyMailListType.ClaimAllUpdate, refresh.Type); Assert.Equal(1, refresh.Count);
            var row = Assert.Single(refresh.Mails);
            var cleared = fixture.AtomicMailSeed.WithClearedFlag().WithClaimedMoney().WithClearedItemId();
            var earliest = LegacyMailListPacket.FromMailboxEntry(cleared, started);
            var latest = LegacyMailListPacket.FromMailboxEntry(cleared, ended);
            Assert.Equal(earliest with { ExpirationDate = row.ExpirationDate }, row);
            Assert.InRange(row.ExpirationDate, latest.ExpirationDate, earliest.ExpirationDate);
        }
        Assert.Equal(PackageJoinRequest(claimAll), actual[^1]);
    }
}
