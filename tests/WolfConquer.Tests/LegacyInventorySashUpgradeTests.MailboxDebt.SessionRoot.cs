using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("whitespace")]
    [InlineData("nul")]
    [InlineData("mismatched")]
    [InlineData("relative")]
    public Task InvalidRecoveryRootCannotBindFreshGenuinePendingMailboxProducerOrPublishSession(string fault) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            var intent = PrepareDebtIntent(fixture, join, initial);
            var owners = await join.PrepareCombinedAdmissionOwnersAsync();
            var producer = owners.Mailbox;
            var valid = LoadDebtRecoveryCandidate(fixture, initial.Owner);
            var relative = Path.GetRelativePath(Environment.CurrentDirectory, fixture.Users);
            if (Path.IsPathFullyQualified(relative)) relative = Path.Combine(".", "Users");
            string? root = fault switch
            {
                "null" => null,
                "empty" => string.Empty,
                "whitespace" => " \t\r\n",
                "nul" => "invalid\0root",
                "mismatched" => Path.Combine(fixture.Root, "UnrelatedUsers"),
                "relative" => relative,
                _ => throw new ArgumentOutOfRangeException(nameof(fault)),
            };
            if (fault == "relative") Assert.False(Path.IsPathFullyQualified(root!));
            var files = fixture.PackageFiles();
            var firstFrame = fixture.Sent.Count;
            var changes = 0;
            var store = new WorldSessionStore((_, _, _) => changes++);
            var connection = new ConquerTcpConnection(null, null);
            Assert.True(producer.TryReconcileRetainedWork(out var reason), reason);
            Assert.True(producer.HasPendingAccountWork(fixture.Account, out var pending, out reason), reason);
            Assert.True(pending);

            Assert.False(store.TrySetMailboxRecoveryOwner(connection, valid with { UsersRoot = root }, producer,
                out var rejected, out reason));

            Assert.Equal("sessionStorageRootInvalid", reason);
            Assert.Null(rejected);
            Assert.Null(store.MailboxPackages);
            Assert.False(store.TryGet(connection, out _));
            Assert.Empty(store.Snapshot());
            Assert.Equal(0, changes);
            // The real producer's reconciliation API rejects a bound owner.
            // Success here proves the rejected candidate did not consume that lifecycle.
            Assert.True(producer.TryReconcileRetainedWork(out reason), reason);
            AssertPackageJoinFiles(fixture, files);
            Assert.Equal(firstFrame, fixture.Sent.Count);

            Assert.True(store.TrySetMailboxRecoveryOwner(connection, valid, producer, out var admitted, out reason), reason);
            Assert.Same(valid, admitted);
            Assert.Same(producer, store.MailboxPackages);
            Assert.True(store.TryGet(connection, out var current));
            Assert.Same(valid, current);
            Assert.False(producer.TryReconcileRetainedWork(out reason));
            Assert.Equal("mailboxClaimRecoveryRequired:packageFreshOwnerRequired", reason);
            Assert.False(store.TryCompareAndSet(connection, valid,
                valid with { Character = valid.Character with { Money = valid.Character.Money + 1 } }));
            Assert.True(store.TryGet(connection, out current));
            Assert.Same(valid, current);
            Assert.Equal(0, changes);
            Assert.True(join.Registry.TryReadProducerIntent(join.Domain, intent.Request.OperationId, out var retained, out reason), reason);
            Assert.Equal(intent.Request, retained!.Request);
            Assert.Equal(intent.CopyPayload(), retained.CopyPayload());
            AssertPackageJoinFiles(fixture, files);
            Assert.Equal(firstFrame, fixture.Sent.Count);
        });
}
