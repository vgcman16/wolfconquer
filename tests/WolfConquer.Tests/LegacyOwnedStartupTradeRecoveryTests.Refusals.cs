using System.Globalization;
using System.Security.Cryptography;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyOwnedStartupTradeRecoveryFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupTradeRecoveryTests
{
    [Theory]
    [InlineData("duplicate-transferred-uid")]
    [InlineData("unknown-uid")]
    [InlineData("malformed-raw-tail")]
    public Task AnUnrelatedPhysicalItemFileCannotBorrowEitherCapturedParticipantExemption(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(3);
        await f.CloseAsync();
        var uid = fault == "unknown-uid" ? LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1 :
            fault == "duplicate-transferred-uid" ? Fixture.LeftItemUid : Fixture.LeftItemUid + 1_000;
        var raw = PlayerItemSnapshotLoader.ToLegacyBytes(new(
            [new() { Uid = uid, ItemId = Fixture.ItemId, StackSize = 1, Durability = 10, MaximumDurability = 10 }], [], [], [], []));
        if (fault == "malformed-raw-tail") raw = raw.Concat(new byte[] { 0x71 }).ToArray();
        File.WriteAllBytes(Path.Combine(f.Data.Items, (Fixture.RightUid + 1).ToString(CultureInfo.InvariantCulture) + ".bin"), raw);
        f.AssertRefused();
    });

    [Theory]
    [InlineData("missing-character")]
    [InlineData("misbound-character")]
    [InlineData("changed-wallet")]
    [InlineData("missing-items")]
    [InlineData("changed-items")]
    public Task MissingOrForeignPhysicalParticipantImagesCannotBeOverwrittenByARealPairWal(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(0);
        await f.CloseAsync();
        var character = f.CharacterPath(Fixture.RightUid);
        var items = f.ItemsPath(Fixture.RightUid);
        switch (fault)
        {
            case "missing-character": File.Delete(character); break;
            case "misbound-character": File.WriteAllBytes(character, Fixture.Replace(File.ReadAllBytes(character),
                new Dictionary<string, string> { [$"UID={Fixture.RightUid}\r\n"] = $"UID={Fixture.LeftUid}\r\n" })); break;
            case "changed-wallet": File.WriteAllBytes(character, Fixture.Replace(File.ReadAllBytes(character),
                new Dictionary<string, string> { ["Money=1300\r\n"] = "Money=1299\r\n" })); break;
            case "missing-items": File.Delete(items); break;
            case "changed-items": File.WriteAllBytes(items, PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty)); break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        f.AssertRefused();
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task AForeignStageForEitherOwnerOrKindRemainsEvidenceAfterRefusal(int ordinal) => WithFixture(async f =>
    {
        await f.StageAsync(0);
        await f.CloseAsync();
        var target = f.OrderedTargets[ordinal];
        var path = target + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(path, f.After[target]);
        Assert.NotEqual(f.StagePath(target), path);
        f.AssertRefused();
    });

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)]
    public Task EveryKnownStageRequiresAnExactBoundedPrefixOfItsOwnActualAfterImage(int ordinal, bool oversized) =>
        WithFixture(async f =>
        {
            await f.StopAfterStageFlushedAsync(4);
            await f.CloseAsync();
            var path = f.StagePath(f.OrderedTargets[ordinal]);
            var bytes = File.ReadAllBytes(path);
            var changed = oversized ? bytes.Concat(new byte[] { 0x71 }).ToArray() : bytes.ToArray();
            if (!oversized) changed[0] ^= 0xff;
            File.WriteAllBytes(path, changed);
            f.AssertRefused();
        });

    [Theory]
    [InlineData("malformed-pending")]
    [InlineData("foreign-pending-entry")]
    [InlineData("foreign-users-stage-directory")]
    [InlineData("foreign-items-stage-directory")]
    public Task ChangedOrUnrelatedRecoveryEvidenceDoesNotBecomePermissionToCleanItUp(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(0);
        await f.CloseAsync();
        switch (fault)
        {
            case "malformed-pending": File.WriteAllText(f.PendingPath, "{\"Version\":1,"); break;
            case "foreign-pending-entry": File.WriteAllText(Path.Combine(Path.GetDirectoryName(f.PendingPath)!, "foreign.json"), "{}"); break;
            case "foreign-users-stage-directory": Directory.CreateDirectory(f.CharacterPath(Fixture.LeftUid) +
                ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp"); break;
            case "foreign-items-stage-directory": Directory.CreateDirectory(f.ItemsPath(Fixture.RightUid) +
                ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp"); break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        f.AssertRefused();
    });

    [Theory]
    [InlineData("domain")]
    [InlineData("evidence")]
    [InlineData("high-water")]
    [InlineData("items-root")]
    public Task ActualConfiguredNamespaceIdentityIsRequiredForBothParticipants(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(3);
        await f.CloseAsync();
        var options = f.Data.Options();
        var changed = fault switch
        {
            "domain" => options with { ExpectedDomain = Guid.NewGuid() },
            "evidence" => options with { HistoricalEvidence = options.HistoricalEvidence + " changed" },
            "high-water" => options with { HistoricalHighWater = options.HistoricalHighWater + 1 },
            "items-root" => options with { ItemsRoot = Path.Combine(f.Data.Root, "OtherItems") },
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        f.AssertRefused(changed);
    });

    [Fact]
    public Task TheActualExistingLeaseMustBeExclusivelyHeldBeforeARealTradeWalCanRecover() => WithFixture(async f =>
    {
        await f.StageAsync(3);
        await f.CloseAsync();
        using (new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            f.AssertRefused(leaseAvailable: false);
        f.AssertLeaseAvailable();
        f.Open(); f.AssertRecoveredImages();
    });

    [Fact]
    public Task MissingExplicitConfigurationLeavesTheActualExchangeJournalAndAllImagesUntouched() => WithFixture(async f =>
    {
        await f.StageAsync(0);
        await f.CloseAsync();
        var before = f.Data.Files();
        var success = LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, null, out var opened, out var reason);
        if (opened is not null) f.Own(opened);
        Assert.False(success, reason); Assert.Null(opened); Assert.NotEmpty(reason);
        f.Data.AssertFiles(before); f.AssertLeaseAvailable();
    });

    [Fact]
    public Task GenuineRetainedProducerWorkCannotBeInferredCompletedFromARealExchangeWal() => WithFixture(async f =>
    {
        var roots = f.Data.Options().Roots;
        var inputs = LegacyTradeJournal.CaptureOrdinaryAdmissionInputs(roots, Fixture.LeftUid);
        var hashes = inputs.ToDictionary(pair => pair.Key, pair => pair.Value is null ? null : SHA256.HashData(pair.Value));
        var recipe = new LegacyOrdinaryAdmissionRecipe(Fixture.LeftUid, roots, hashes,
            SHA256.HashData(inputs[LegacyTradeJournalFileKind.Character]!),
            SHA256.HashData(inputs[LegacyTradeJournalFileKind.Spells]!),
            SHA256.HashData(inputs[LegacyTradeJournalFileKind.Proficiencies]!), [new(Fixture.ItemId, 3, 10, 10)]);
        var payload = recipe.Encode();
        Assert.Equal(payload, LegacyOrdinaryAdmissionRecipe.Decode(payload, roots).Encode());
        var digest = SHA256.HashData(payload);
        var request = new LegacyItemUidReservationRequest(new Guid(digest.AsSpan(0, 16)),
            LegacyOrdinaryAdmissionUidOwner.Writer, Fixture.LeftUid, Fixture.LeftUid,
            Convert.ToHexString(digest).ToLowerInvariant(), 1);
        Assert.True(f.Runtime.Ordinary.Registry.TryPrepareProducerIntent(f.Data.ExpectedDomain, request, payload,
            out var prepared, out var reason), reason);
        Assert.Null(Assert.IsType<LegacyItemUidProducerIntentSnapshot>(prepared).Allocation);
        f.RegistryState = File.ReadAllBytes(f.Data.State);
        await f.StageAsync(0);
        await f.CloseAsync();
        f.AssertRefused();
    });
}
