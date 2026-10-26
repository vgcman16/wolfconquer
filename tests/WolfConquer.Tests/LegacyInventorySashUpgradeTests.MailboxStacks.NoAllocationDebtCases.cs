using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task RegisteredD13NoAllocationPreparedWorkFencesOrdinaryWritersUntilFreshReadOnlyOriginalReplay() =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var expected = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems));
            var prepared = PrepareStackNoAllocationWork(fixture, join, before, expected);
            var pending = StackPendingFiles(fixture, join, before, prepared);
            var first = fixture.Sent.Count;
            var producer = join.ReadDebtProducer();
            Assert.True(producer.HasPendingAccountWork(fixture.Account, out var debt, out var reason), reason);
            Assert.True(debt);
            Assert.False(producer.TryRequireAccountWritable(fixture.Account, out _));
            var fields = new Dictionary<string, string>(join.Current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["Money"] = (join.Current.Character.Money + 1).ToString(CultureInfo.InvariantCulture) };
            var character = join.Current.Character with { Money = join.Current.Character.Money + 1, Attributes = fields };
            Assert.False(join.SessionStore.TryCompareAndSet(join.Connection!, join.Current, join.Current with { Character = character }));
            Assert.False(CharacterSnapshotScalarPersistence.TryPersist(fixture.Users, character, out _, out _));
            Assert.Throws<IOException>(() => new PlayerItemSnapshotLoader(fixture.Items).SaveByCharacterUid(Fixture.Uid, expected));
            var mailbox = ActualDebtFrameOwner<LegacyMailboxStore>(join, "_mailboxes");
            var cache = JsonSerializer.Serialize(mailbox.List(Fixture.Uid));
            Assert.Throws<IOException>(() => mailbox.Add(Fixture.Uid, fixture.AtomicMailSeed.WithClaimedMoney()));
            Assert.Equal(cache, JsonSerializer.Serialize(mailbox.List(Fixture.Uid)));
            Assert.Throws<IOException>(() => mailbox.Save(Fixture.Uid));
            await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join),
                new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimMoney, 22, []).ToFrameBytes(), direct: true);
            await join.FrameAsync(new MsgActionPacket { ObjId = Fixture.Uid, Type = MsgActionType.SetLocation }.ToFrameBytes());
            Assert.Equal(before.Json, JsonSerializer.Serialize(join.Current));
            AssertPackageJoinFiles(fixture, pending);
            Assert.Equal(first, fixture.Sent.Count);
            AssertNoPackageJoinIntents(join);

            await join.RestartAsync();
            AssertPackageJoinFiles(fixture, pending);
            var admitted = join.Current;
            var readonlyJson = JsonSerializer.Serialize(admitted);
            first = fixture.Sent.Count;
            await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), PackageJoinRequest(true), direct: true);
            Assert.Same(admitted, join.Current);
            Assert.Equal(readonlyJson, JsonSerializer.Serialize(join.Current));
            AssertPackageJoinFiles(fixture, pending);
            Assert.Equal(first, fixture.Sent.Count);
            await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), PackageJoinRequest(false), direct: false);
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, 0);
            Assert.True(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out reason), reason);
            await AssertStackReplayRetainsAsync(fixture, join, false);
            await AssertDebtNormalMoneyAllowedAsync(fixture, join);
        });

    [Fact]
    public Task RegisteredD13NoAllocationCrashAfterRealJointCommitReconcilesCompletedWorkWithoutSecondCredit() =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var expected = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems));
            var prepared = PrepareStackNoAllocationWork(fixture, join, before, expected);
            Assert.False(prepared.Work.TryCommit(LegacyMailboxPackageInterruption.AfterJointCommit,
                out var proof, out var durable, out var reason));
            Assert.Null(proof);
            Assert.True(durable);
            Assert.Equal("mailboxClaimRecoveryRequired:packageCompletionInterrupted", reason);
            var after = new Dictionary<string, byte[]>(prepared.After, StringComparer.OrdinalIgnoreCase);
            IncludeStackWorkFile(fixture, join, before, after, 0, completed: false);
            AssertPackageJoinFiles(fixture, after);
            AssertNoPackageJoinIntents(join);
            Assert.Equal(before.Json, JsonSerializer.Serialize(join.Current));
            Assert.False(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out _));
            var first = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(false));
            Assert.Equal(first, fixture.Sent.Count);
            AssertPackageJoinFiles(fixture, after);
            await join.RestartAsync();
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, 0);
            Assert.True(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out reason), reason);
            await AssertStackReplayRetainsAsync(fixture, join, false);
            await AssertDebtNormalMoneyAllowedAsync(fixture, join);
        });

    private static Dictionary<string, byte[]> StackPendingFiles(Fixture fixture, PackageJoin join,
        PackageJoinBefore before, StackPreparedWork prepared)
    {
        var after = new Dictionary<string, byte[]>(prepared.After, StringComparer.OrdinalIgnoreCase);
        var row = IncludeStackWorkFile(fixture, join, before, after, 0, completed: false);
        Assert.Equal(prepared.Recipe.OperationId.ToString("N"), row.Operation);
        Assert.Equal(prepared.Recipe.CopyPayload(), Convert.FromBase64String(row.PayloadBase64!));
        var path = DebtWorkPath(fixture, join, prepared.Recipe.OperationId);
        var files = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase)
        { [Path.GetRelativePath(fixture.Root, path)] = File.ReadAllBytes(path) };
        AssertPackageJoinFiles(fixture, files);
        return files;
    }
}
