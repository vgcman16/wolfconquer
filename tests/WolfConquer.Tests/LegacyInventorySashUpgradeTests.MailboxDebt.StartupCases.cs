using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FreshActualFrameAdmitsOnlyReadOnlyPackageRecoveryWithoutStartupSaveOrSetLocationEffects(bool preparedSidecar) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            var intent = PrepareDebtIntent(fixture, join, initial);
            if (preparedSidecar) PrepareDebtWork(fixture, join, initial, intent);
            var before = fixture.PackageFiles();
            var raw = LoadDebtRecoveryCandidate(fixture, initial.Owner);
            var first = fixture.Sent.Count;
            await join.RestartAsync(); // Actual fresh handoff and the helper's actual following SetLocation request.
            AssertPackageJoinFiles(fixture, before);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(raw), System.Text.Json.JsonSerializer.Serialize(join.Current));
            AssertDebtPinnedResourcesEqual(initial.Owner, join.Current);
            var metadata = fixture.Sent.Skip(first).ToArray();
            Assert.Contains(metadata, bytes => bytes.AsSpan().SequenceEqual(
                WorldSnapshotResponsePacket.Build(WorldSnapshotWireStatus.Success, fixture.Account, join.Current.Character)));
            Assert.DoesNotContain(metadata, bytes => MsgActionPacket.TryParse(ReadFrame(bytes), out var action) && action.Type == MsgActionType.SetLocation);
            var producer = ActualDebtFrameOwner<LegacyMailboxPackageProducer>(join, "_mailboxPackages");
            Assert.True(producer.HasPendingAccountWork(fixture.Account, out var pending, out var reason), reason);
            Assert.True(pending);
            Assert.False(producer.TryRequireAccountWritable(fixture.Account, out _));
            var readonlyBefore = CapturePackageJoin(fixture, join);
            var ordinary = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimMoney, AtomicMailId, []).ToFrameBytes();
            await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), ordinary, direct: true);
            AssertDebtRetained(fixture, join, readonlyBefore, intent);
            var replayStart = DateTime.Now;
            await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), PackageJoinRequest(claimAll: true), direct: false);
            AssertDebtPackageCommitted(fixture, join, initial, intent);
            AssertPackageJoinFrames(fixture, join, initial, true, replayStart, DateTime.Now, readonlyBefore.FirstFrame);
        });

    [Theory]
    [InlineData("money")]
    [InlineData("class-experience")]
    [InlineData("items")]
    [InlineData("gear")]
    public Task GenuineReadOnlyAdmissionRejectsNormalizedOrChangedLoadedCandidateBeforePublishingAnyOwner(string fault) =>
        RunPackageJoinAsync((fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            var intent = PrepareDebtIntent(fixture, join, initial);
            var before = fixture.PackageFiles();
            var first = fixture.Sent.Count;
            var producer = ActualDebtFrameOwner<LegacyMailboxPackageProducer>(join, "_mailboxPackages");
            Assert.True(join.SessionStore.TryRemove(join.Connection!, out var retired));
            var valid = LoadDebtRecoveryCandidate(fixture, Assert.IsType<WorldSession>(retired));
            var candidate = fault switch
            {
                "money" => valid with { Character = valid.Character with { Money = valid.Character.Money + 1 } },
                "class-experience" => valid with { Character = valid.Character with
                    { Attributes = new Dictionary<string, string>(valid.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                        { ["ClassExperience"] = (PackageClassExperience + 1).ToString(CultureInfo.InvariantCulture) } } },
                "items" => valid with { PlayerItems = valid.PlayerItems with
                    { InventoryItems = valid.PlayerItems.InventoryItems.Select(item => item.WithStackSize(2)).ToArray() } },
                "gear" => valid with { CombatGear = valid.CombatGear! with { MasteryPoints = valid.CombatGear!.MasteryPoints + 1 } },
                _ => throw new ArgumentOutOfRangeException(nameof(fault)),
            };
            Assert.False(join.SessionStore.TrySetMailboxRecoveryOwner(join.Connection!, candidate, producer, out var rejected, out _));
            Assert.Null(rejected);
            Assert.False(join.SessionStore.TryGet(join.Connection!, out _));
            AssertPackageJoinFiles(fixture, before);
            Assert.Equal(first, fixture.Sent.Count);
            Assert.True(join.SessionStore.TrySetMailboxRecoveryOwner(join.Connection!, valid, producer, out var admitted, out var reason), reason);
            Assert.Same(valid, admitted);
            AssertPackageJoinFiles(fixture, before);
            Assert.Equal(first, fixture.Sent.Count);
            Assert.True(join.Registry.TryReadProducerIntent(join.Domain, intent.Request.OperationId, out var retained, out reason), reason);
            Assert.Equal(intent.CopyPayload(), retained!.CopyPayload());
            return Task.CompletedTask;
        });
}
