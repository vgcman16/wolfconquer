using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task ActualPendingMailboxCompletionCanReconnectThroughExplicitCombinedOwnersWithoutAnotherGrant() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            using var metadata = new NativeLoginBeastMetadataScope(1000);
            var text = Encoding.UTF8.GetString(File.ReadAllBytes(fixture.CharacterPath));
            Assert.DoesNotContain("SecurityPass=", text, StringComparison.Ordinal);
            Assert.Contains("[Unrelated]", text, StringComparison.Ordinal);
            text = text.Replace("[Unrelated]", "SecurityPass=111,0,0" + Environment.NewLine + "[Unrelated]", StringComparison.Ordinal);
            File.WriteAllBytes(fixture.CharacterPath, new UTF8Encoding(false).GetBytes(text));
            WriteSashField(fixture, "Online", "0");
            await join.RestartAsync();
            var before = CapturePackageJoin(fixture, join);
            Assert.Equal("111,0,0", before.Owner.Character.Attributes["SecurityPass"]);
            Assert.Equal("0", before.Owner.Character.Attributes["Online"]);
            var intent = PrepareDebtIntent(fixture, join, before, allocated: true);
            var pinned = fixture.PackageFiles();

            var owners = await join.PrepareCombinedAdmissionOwnersAsync();
            Assert.True(owners.Mailbox.HasPendingAccountWork(fixture.Account, out var pending, out var reason), reason);
            Assert.True(pending);
            Assert.False(owners.Mailbox.TryGetCompletedWorkProof(intent.Request.OperationId, out var uncompleted, out _));
            Assert.Null(uncompleted);
            Assert.False(owners.Mailbox.TryRequireAccountWritable(fixture.Account, out _));
            Assert.False(owners.Mailbox.TryRequireCharacterWritable(Fixture.Uid, out _));
            var pendingNative = await join.StartCombinedNativeAdmissionAsync(owners);

            AssertPackageJoinFiles(fixture, pinned);
            Assert.Equal(Fixture.Uid, pendingNative.Player.UID);
            Assert.Equal(10U, pendingNative.Player.Class);
            Assert.Equal(PackageClassExperience, pendingNative.Player.ClassExperience);
            Assert.False(pendingNative.Player.IsCheckedPass);
            Assert.Null(pendingNative.Beasts);
            Assert.Null(pendingNative.HairfaceStorage);
            await AssertNativeDebtPinAsync(fixture, join, supplied: 111, action: 5, value: 257);
            await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), PackageJoinRequest(true), direct: false);
            var committed = AssertPackageJoinAllocation(fixture, join);
            Assert.Equal(intent.Request.OperationId, committed.Request.OperationId);
            Assert.Equal(intent.CopyPayload(), committed.CopyPayload());
            AssertDebtPackageCommitted(fixture, join, before, committed);
            Assert.Equal(1031U, pendingNative.Player.ClassExperience);
            Assert.True(owners.Mailbox.TryGetCompletedWorkProof(committed.Request.OperationId, out var completion, out reason), reason);
            Assert.NotNull(completion);
            Assert.True(completion.MatchesProducerIntent(join.Domain, committed));
            Assert.True(owners.Mailbox.TryRequireAccountWritable(fixture.Account, out reason), reason);
            Assert.True(owners.Mailbox.TryRequireCharacterWritable(Fixture.Uid, out reason), reason);
            var completed = join.Current;
            var completedWork = File.ReadAllBytes(DebtWorkPath(fixture, join, committed.Request.OperationId));
            var uidBytes = File.ReadAllBytes(fixture.PackageUidState);
            var items = File.ReadAllBytes(fixture.ItemPath);
            var mailbox = File.ReadAllBytes(fixture.AtomicMailPath);
            var gear = File.ReadAllBytes(fixture.PackageGearPath);

            owners = await join.PrepareCombinedAdmissionOwnersAsync();
            var first = fixture.Sent.Count;
            var normalNative = await join.StartCombinedNativeAdmissionAsync(owners);

            AssertDebtPinnedResourcesEqual(completed, join.Current);
            Assert.Same(normalNative, join.Current.NativeGameClient);
            Assert.Equal(Fixture.Uid, normalNative.Player.UID);
            Assert.Equal(1U, normalNative.Player.Online);
            Assert.Equal(1031U, normalNative.Player.ClassExperience);
            Assert.NotNull(normalNative.Beasts);
            Assert.NotNull(normalNative.HairfaceStorage);
            Assert.False(normalNative.FullLoading);
            Assert.False(normalNative.Player.IsCheckedPass); // Fresh genuine PIN binding, no reused authorization.
            Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), fixture.Sent[first]);
            Assert.Contains("ordinary admission committed key=", fixture.Log.ToString());
            Assert.Equal(uidBytes, File.ReadAllBytes(fixture.PackageUidState));
            Assert.Equal(items, File.ReadAllBytes(fixture.ItemPath));
            Assert.Equal(mailbox, File.ReadAllBytes(fixture.AtomicMailPath));
            Assert.Equal(gear, File.ReadAllBytes(fixture.PackageGearPath));
            Assert.Equal(completedWork, File.ReadAllBytes(DebtWorkPath(fixture, join, committed.Request.OperationId)));
            // Active means unarchived UID-ledger membership, not unpaid work.
            // This single operation is below the proof-gated pressure threshold.
            Assert.True(join.Registry.TryReadActiveProducerIntents(join.Domain, out var active, out reason), reason);
            AssertUidHistorySnapshot(committed, Assert.Single(active));
            Assert.True(owners.Mailbox.TryReadCompletedIntent(committed.Request.OperationId, out var retained, out reason), reason);
            AssertUidHistorySnapshot(committed, Assert.IsType<LegacyItemUidProducerIntentSnapshot>(retained));
            Assert.True(owners.Mailbox.TryGetCompletedWorkProof(committed.Request.OperationId, out var reloadedCompletion, out reason), reason);
            Assert.NotNull(reloadedCompletion);
            Assert.True(reloadedCompletion.MatchesProducerIntent(join.Domain, committed));
            Assert.True(owners.Mailbox.HasPendingAccountWork(fixture.Account, out pending, out reason), reason);
            Assert.False(pending);
            Assert.True(owners.Mailbox.TryRequireAccountWritable(fixture.Account, out reason), reason);
            Assert.True(owners.Mailbox.TryRequireCharacterWritable(Fixture.Uid, out reason), reason);
            AssertPackageJoinAllocation(fixture, join);
        });

    [Fact]
    public Task CombinedAdmissionRefusesAnActualSameDomainOwnerWithDifferentConfiguredRoutesBeforeBinding() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var owners = await join.PrepareCombinedAdmissionOwnersAsync(wrongOrdinaryRoute: true);
            var files = fixture.PackageFiles();
            Assert.True(owners.Ordinary.IsCurrent(owners.OrdinaryRoots));
            Assert.True(owners.Mailbox.IsCurrent(fixture.Users, fixture.Items, fixture.PackageMailboxRoot, join.Recovery));

            Assert.Throws<ArgumentException>(() => new WorldServerFrameHandler(owners.Loader, owners.Handoff,
                PackageJoinCatalog(), owners.Mailbox, owners.Ordinary, fixture.Log, ConquerProtocolProfile.VisibleDx8D13));

            AssertUnboundCombinedProducer(owners.Mailbox);
            Assert.True(owners.Ordinary.IsCurrent(owners.OrdinaryRoots));
            AssertPackageJoinFiles(fixture, files);
        });

    private static void AssertUnboundCombinedProducer(LegacyMailboxPackageProducer producer)
    {
        Assert.Equal(0, (int)typeof(LegacyMailboxPackageProducer).GetField("storeBound", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(producer)!); // Observe only; never set binding flags or replace owners.
        Assert.Null(typeof(LegacyMailboxPackageProducer).GetField("packetOperations", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(producer));
    }
}
