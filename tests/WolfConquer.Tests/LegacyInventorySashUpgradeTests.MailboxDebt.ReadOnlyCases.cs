using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ActualReadOnlyPackageRecoveryOwnerPreservesAllBeforeimagesAndPermitsOnlyExactOriginalReplay(bool allocated, bool preparedSidecar) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            var intent = PrepareDebtIntent(fixture, join, initial, allocated);
            if (preparedSidecar)
            {
                PrepareDebtWork(fixture, join, initial, intent);
                AssertDebtWork(fixture, join, initial, intent, DebtAfterFiles(fixture, initial, intent), completed: false);
            }
            var before = CapturePackageJoin(fixture, join);
            var producer = ActualDebtFrameOwner<LegacyMailboxPackageProducer>(join, "_mailboxPackages");
            Assert.True(join.SessionStore.TryRemove(join.Connection!, out var retired));
            var candidate = LoadDebtRecoveryCandidate(fixture, Assert.IsType<WorldSession>(retired));
            Assert.True(join.SessionStore.TrySetMailboxRecoveryOwner(join.Connection!, candidate, producer,
                out var admitted, out var reason), reason);
            Assert.Same(candidate, admitted);
            Assert.Same(admitted, join.Current);
            Assert.Equal(JsonSerializer.Serialize(candidate), JsonSerializer.Serialize(join.Current));
            AssertDebtPinnedResourcesEqual(initial.Owner, join.Current);
            AssertPackageJoinFiles(fixture, before.Files);
            Assert.Equal(before.FirstFrame, fixture.Sent.Count);
            var readonlyBefore = CapturePackageJoin(fixture, join);

            var wrong = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimMoney, AtomicMailId, []).ToFrameBytes();
            var context = ActualDebtContext(fixture, join);
            await DebtRegisteredAsync(fixture, context, wrong, direct: true);
            AssertDebtRetained(fixture, join, readonlyBefore, intent);
            var started = DateTime.Now;
            var request = PackageJoinRequest(claimAll: true);
            await DebtRegisteredAsync(fixture, context, request, direct: false);
            var committedIntent = AssertPackageJoinAllocation(fixture, join);
            Assert.Equal(intent.Request, committedIntent.Request);
            AssertDebtPackageCommitted(fixture, join, initial, committedIntent);
            AssertPackageJoinFrames(fixture, join, initial, true, started, DateTime.Now, before.FirstFrame);
            Assert.True(producer.TryRequireAccountWritable(fixture.Account, out reason), reason);
            await AssertDebtNormalMoneyAllowedAsync(fixture, join);
        });

    private static WorldSession LoadDebtRecoveryCandidate(Fixture fixture, WorldSession authenticated)
    {
        var loader = new CharacterSnapshotLoader(fixture.Users, fixture.Items, null, null,
            playersCombatGearRoot: fixture.PackageGearRoot, playerQuestsRoot: null);
        // Preserve actual authenticated identity, then construct the same complete
        // raw loader state as the recovery startup path. Never import a previous
        // session's derived physical overlay, normalized HP, or runtime fields.
        return new WorldSession(authenticated.AccountId, authenticated.Username,
            Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(fixture.Account)),
            loader.LoadPlayerItemsByCharacterUid(Fixture.Uid), loader.LoadPlayerSpellsByCharacterUid(Fixture.Uid),
            loader.LoadPlayerProficienciesByCharacterUid(Fixture.Uid),
            CombatGear: loader.LoadCombatGearByCharacterUid(Fixture.Uid), UsersRoot: loader.UsersRoot)
        { NativeGameClient = authenticated.NativeGameClient };
    }

    private static void AssertDebtPinnedResourcesEqual(WorldSession previous, WorldSession raw)
    {
        Assert.Equal(previous.Character.Money, raw.Character.Money);
        Assert.Equal(previous.Character.ConquerPoints, raw.Character.ConquerPoints);
        Assert.Equal(previous.Character.Class, raw.Character.Class);
        Assert.Equal(previous.Character.Attributes["ClassExperience"], raw.Character.Attributes["ClassExperience"]);
        Assert.Equal(JsonSerializer.Serialize(previous.PlayerItems), JsonSerializer.Serialize(raw.PlayerItems));
        Assert.Equal(JsonSerializer.Serialize(previous.CombatGear), JsonSerializer.Serialize(raw.CombatGear));
        Assert.Equal(previous.Character.Attributes.GetValueOrDefault("SecurityPass"), raw.Character.Attributes.GetValueOrDefault("SecurityPass"));
    }

    private static async Task AssertDebtNormalMoneyAllowedAsync(Fixture fixture, PackageJoin join)
    {
        var before = join.Current;
        var first = fixture.Sent.Count;
        var request = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimMoney, 22, []).ToFrameBytes();
        await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), request, direct: true);
        Assert.Equal(before.Character.Money + 37, join.Current.Character.Money);
        Assert.Equal(before.Character.ConquerPoints + 11, join.Current.Character.ConquerPoints);
        Assert.True(fixture.Sent.Count > first);
        Assert.Equal(request, fixture.Sent[^1]);
    }
}
