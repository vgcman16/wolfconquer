using System.Globalization;
using System.Security.Cryptography;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupItemRecoveryTests
{
    [Fact]
    public Task ACompetingActualRegistryLeaseRefusesBeforeRecoveryAndReleaseAllowsTheSameWal() => WithFixture(scope =>
    {
        var acquiredOwner = LegacyItemUidRegistry.TryOpen(scope.Data.RegistryOptions(), scope.Data.ExpectedDomain,
            out var acquired, out var reason);
        if (acquired is not null) scope.Own(acquired);
        Assert.True(acquiredOwner, reason);
        var owner = Assert.IsType<LegacyItemUidRegistry>(acquired);
        scope.Stage(0);
        scope.AssertRefused(leaseAvailable: false);
        owner.Dispose();
        scope.AssertLeaseAvailable();
        scope.Open();
        scope.AssertRecoveredImages();
    });

    [Theory]
    [InlineData("domain")]
    [InlineData("evidence")]
    [InlineData("floor")]
    public Task ExplicitNamespaceIdentityMismatchCannotRollForwardOrRepairTheLedger(string mismatch) => WithFixture(scope =>
    {
        scope.Stage(1);
        var configured = scope.Data.Options();
        var options = mismatch switch
        {
            "domain" => configured with { ExpectedDomain = Guid.NewGuid() },
            "evidence" => configured with { HistoricalEvidence = "different owned evidence" },
            "floor" => configured with { HistoricalHighWater = configured.HistoricalHighWater + 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
        };
        scope.AssertRefused(options);
    });

    [Theory]
    [InlineData("missing-state")]
    [InlineData("corrupt-state")]
    [InlineData("uid-pending")]
    [InlineData("archive-stage")]
    [InlineData("foreign-archive-row")]
    public Task InvalidOrUnresolvedRegistryStatePreservesBothJournalsAndTargetBytes(string fault) => WithFixture(scope =>
    {
        scope.Stage(1);
        switch (fault)
        {
            case "missing-state": File.Delete(scope.Data.State); break;
            case "corrupt-state": File.WriteAllText(scope.Data.State, "{"); break;
            case "uid-pending": File.WriteAllBytes(Path.Combine(scope.Data.Registry, "pending.json"), scope.RegistryState); break;
            case "archive-stage": File.WriteAllText(Path.Combine(scope.Data.Registry, "archive.pending.json"), "{}"); break;
            case "foreign-archive-row":
                var history = Path.Combine(scope.Data.Registry, "history");
                Directory.CreateDirectory(history);
                File.WriteAllText(Path.Combine(history, Guid.NewGuid().ToString("N") + ".json"), "{}");
                break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        scope.AssertRefused();
    });

    [Theory]
    [InlineData("missing-character")]
    [InlineData("changed-character")]
    [InlineData("missing-items")]
    [InlineData("changed-items")]
    public Task MissingOrForeignTargetPreimageCannotBeInferredAsCompletedOrOverwritten(string fault) => WithFixture(scope =>
    {
        scope.Stage(0);
        switch (fault)
        {
            case "missing-character": File.Delete(scope.CharacterPath); break;
            case "changed-character": File.WriteAllBytes(scope.CharacterPath,
                Replace(scope.BeforeCharacter, "Money=777\r\n", "Money=776\r\n")); break;
            case "missing-items": File.Delete(scope.ItemsPath); break;
            case "changed-items": File.WriteAllBytes(scope.ItemsPath,
                PlayerItemSnapshotLoader.ToLegacyBytes(Snapshot(ItemUid, 6))); break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        scope.AssertRefused();
    });

    [Theory]
    [InlineData("uid-birth-below-high-water")]
    [InlineData("deposited-uid-birth-below-high-water")]
    [InlineData("uid-above-high-water")]
    [InlineData("malformed-raw-tail")]
    [InlineData("character-uid-changed")]
    public Task GenuineWalWithUnsupportedAfterImageRemainsUntouched(string fault) => WithFixture(scope =>
    {
        var afterItems = fault switch
        {
            "uid-birth-below-high-water" => PlayerItemSnapshotLoader.ToLegacyBytes(Snapshot(ItemUid, 10, includeBirth: true)),
            "deposited-uid-birth-below-high-water" => PlayerItemSnapshotLoader.ToLegacyBytes(new(
                [Item(ItemUid, 10).WithDepositedItems([Item(ItemUid + 1, 10)])], [], [], [], [])),
            "uid-above-high-water" => PlayerItemSnapshotLoader.ToLegacyBytes(Snapshot(scope.HighWater + 1, 10)),
            "malformed-raw-tail" => scope.AfterItems.Concat(new byte[] { 0x71 }).ToArray(),
            _ => scope.AfterItems,
        };
        var afterCharacter = fault == "character-uid-changed"
            ? Replace(scope.AfterCharacter, $"UID={Uid}\r\n", $"UID={Uid + 1}\r\n") : scope.AfterCharacter;
        scope.Stage(0, afterItems, afterCharacter);
        scope.AssertRefused();
    });

    [Theory]
    [InlineData("duplicate-uid")]
    [InlineData("unregistered-uid")]
    [InlineData("malformed-raw-tail")]
    public Task WholeNamespacePrescanMustPassBeforeAnySupportedJournalTargetIsWritten(string fault) => WithFixture(scope =>
    {
        scope.Stage(0);
        var unrelated = Path.Combine(scope.Data.Items, (Uid + 1).ToString(CultureInfo.InvariantCulture) + ".bin");
        var uid = fault == "unregistered-uid" ? scope.HighWater + 1 : fault == "duplicate-uid" ? ItemUid : ItemUid + 1;
        var raw = PlayerItemSnapshotLoader.ToLegacyBytes(Snapshot(uid, 5));
        if (fault == "malformed-raw-tail") raw = raw.Concat(new byte[] { 0x22 }).ToArray();
        File.WriteAllBytes(unrelated, raw);
        scope.AssertRefused();
    });

    [Theory]
    [InlineData("before-uid-above-high-water")]
    [InlineData("before-character-uid-mismatch")]
    public Task InvalidRecordedBeforeImageRemainsFencedEvenWhenEveryCurrentTargetAlreadyMatchesAfter(string fault) => WithFixture(scope =>
    {
        if (fault == "before-uid-above-high-water")
        {
            scope.BeforeItems = PlayerItemSnapshotLoader.ToLegacyBytes(new(
                [Item(ItemUid, 5), Item(scope.HighWater + 1, 5)], [], [], [], []));
            File.WriteAllBytes(scope.ItemsPath, scope.BeforeItems);
        }
        else
        {
            scope.BeforeCharacter = Replace(scope.BeforeCharacter, $"UID={Uid}\r\n", $"UID={Uid + 1}\r\n");
            File.WriteAllBytes(scope.CharacterPath, scope.BeforeCharacter);
        }
        scope.Stage(2);
        Assert.Equal(scope.AfterCharacter, File.ReadAllBytes(scope.CharacterPath));
        Assert.Equal(scope.AfterItems, File.ReadAllBytes(scope.ItemsPath));
        scope.AssertRefused();
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task ForeignTransactionStageFilenameCannotBorrowTheCapturedWalCustody(bool character) => WithFixture(scope =>
    {
        scope.Stage(0);
        var target = character ? scope.CharacterPath : scope.ItemsPath;
        var unknown = target + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(unknown, character ? scope.AfterCharacter : scope.AfterItems);
        Assert.NotEqual(scope.StagePath(character), unknown);
        scope.AssertRefused();
    });

    [Theory]
    [InlineData(true, "nonprefix")]
    [InlineData(false, "nonprefix")]
    [InlineData(true, "oversized")]
    [InlineData(false, "oversized")]
    public Task TamperedKnownStageIsNotDeletedOrRewrittenEvenWithTheRightTransactionFilename(bool character, string fault) =>
        WithFixture(scope =>
        {
            scope.StopAfterStageFlushed(2);
            var path = scope.StagePath(character);
            var actual = File.ReadAllBytes(path);
            var changed = fault == "oversized" ? actual.Concat(new byte[] { 0x71 }).ToArray() : actual.ToArray();
            if (fault == "nonprefix") changed[0] ^= 0xff;
            File.WriteAllBytes(path, changed);
            Assert.NotEqual(actual, changed);

            scope.AssertRefused();
        });

    [Fact]
    public Task MalformedPendingRecordIsNotDeletedOrReplacedByAValidLookingCurrentImage() => WithFixture(scope =>
    {
        scope.Stage(2);
        File.WriteAllText(scope.PendingPath, "{\"Version\":2,");
        scope.AssertRefused();
    });

    [Fact]
    public Task MissingExplicitConfigurationCannotUseTheClosedRecoveryAuthority() => WithFixture(scope =>
    {
        scope.Stage(0);
        var before = scope.Data.Files();
        var success = LegacyOwnedAdmissionRuntime.TryOpen(scope.Data.Loader, null, out var opened, out var reason);
        if (opened is not null) scope.OwnAsync(opened);
        Assert.False(success, reason);
        Assert.Null(opened);
        Assert.NotEmpty(reason);
        scope.Data.AssertFiles(before);
        scope.AssertLeaseAvailable();
    });

    [Fact]
    public Task GenericRegistryOpenAndBootstrapRetainTheirPendingJournalRefusals() => WithFixture(scope =>
    {
        scope.Stage(0);
        var before = scope.Data.Files();
        var success = LegacyItemUidRegistry.TryOpen(scope.Data.RegistryOptions(), scope.Data.ExpectedDomain, out var opened, out var reason);
        if (opened is not null) scope.Own(opened);
        Assert.False(success, reason);
        Assert.Null(opened);
        Assert.NotEmpty(reason);
        var bootstrapSuccess = LegacyItemUidRegistry.TryBootstrap(scope.Data.RegistryOptions(), out var bootstrapped, out reason);
        if (bootstrapped is not null) scope.Own(bootstrapped);
        Assert.False(bootstrapSuccess, reason);
        Assert.Null(bootstrapped);
        Assert.NotEmpty(reason);
        scope.Data.AssertFiles(before);
        scope.AssertLeaseAvailable();
    });

    [Fact]
    public Task GenuineRetainedOrdinaryProducerIntentCannotBeReconciledThroughSingleOwnerGameWalRecovery() => WithFixture(scope =>
    {
        var opened = LegacyItemUidRegistry.TryOpen(scope.Data.RegistryOptions(), scope.Data.ExpectedDomain,
            out var acquired, out var reason);
        if (acquired is not null) scope.Own(acquired);
        Assert.True(opened, reason);
        var owner = Assert.IsType<LegacyItemUidRegistry>(acquired);
        var roots = scope.Data.Options().Roots;
        var inputs = LegacyTradeJournal.CaptureOrdinaryAdmissionInputs(roots, Uid);
        var hashes = inputs.ToDictionary(pair => pair.Key, pair => pair.Value is null ? null : SHA256.HashData(pair.Value));
        var recipe = new LegacyOrdinaryAdmissionRecipe(Uid, roots, hashes, SHA256.HashData(scope.AfterCharacter),
            SHA256.HashData(inputs[LegacyTradeJournalFileKind.Spells]!),
            SHA256.HashData(inputs[LegacyTradeJournalFileKind.Proficiencies]!),
            [new(ItemId, 3, 10, 10)]);
        var payload = recipe.Encode();
        Assert.Equal(payload, LegacyOrdinaryAdmissionRecipe.Decode(payload, roots).Encode());
        var digest = SHA256.HashData(payload);
        var request = new LegacyItemUidReservationRequest(new Guid(digest.AsSpan(0, 16)),
            LegacyOrdinaryAdmissionUidOwner.Writer, Uid, Uid, Convert.ToHexString(digest).ToLowerInvariant(), 1);
        Assert.True(owner.TryPrepareProducerIntent(scope.Data.ExpectedDomain, request, payload,
            out var prepared, out reason), reason);
        Assert.Null(Assert.IsType<LegacyItemUidProducerIntentSnapshot>(prepared).Allocation);
        Assert.NotEqual(scope.RegistryState, File.ReadAllBytes(scope.Data.State));
        Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor, scope.HighWater);
        owner.Dispose();
        scope.Stage(0);

        scope.AssertRefused();
    });

    [Fact]
    public Task SharedItemsConfiguredDomainWithItsOwnGenuinePendingWalCannotBorrowAnotherDomainsRecovery() => WithFixture(scope =>
    {
        scope.Stage(0);
        var otherUsers = Path.Combine(scope.Data.Root, "OtherUsers");
        Directory.CreateDirectory(otherUsers);
        var otherUid = Uid + 1;
        var otherCharacter = Path.Combine(otherUsers, otherUid.ToString(CultureInfo.InvariantCulture) + ".ini");
        var otherItems = Path.Combine(scope.Data.Items, otherUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var beforeCharacter = Replace(scope.BeforeCharacter, $"UID={Uid}\r\n", $"UID={otherUid}\r\n");
        var afterCharacter = Replace(scope.AfterCharacter, $"UID={Uid}\r\n", $"UID={otherUid}\r\n");
        var beforeItems = PlayerItemSnapshotLoader.ToLegacyBytes(Snapshot(ItemUid + 1, 5));
        var afterItems = PlayerItemSnapshotLoader.ToLegacyBytes(Snapshot(ItemUid + 1, 10));
        File.WriteAllBytes(otherCharacter, beforeCharacter);
        File.WriteAllBytes(otherItems, beforeItems);
        using var interruption = LegacyJournalInterruption.StopAfter(0);
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(otherUsers, scope.Data.Items,
            [new(LegacyTradeJournalFileKind.Character, otherUid, beforeCharacter, afterCharacter),
             new(LegacyTradeJournalFileKind.Items, otherUid, beforeItems, afterItems)], interruption, out var reason));
        Assert.Contains(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        Assert.True(interruption.PendingObserved);
        Assert.True(LegacyTradeJournal.HasPending(otherUsers));
        // Both genuine journals exist before either retained configured binding.
        var ownRoots = scope.Data.Options().Roots;
        LegacyOrdinaryAdmissionMutationFence.BindConfiguredDomain(ownRoots);
        LegacyOrdinaryAdmissionMutationFence.BindConfiguredDomain(ownRoots with { Users = otherUsers });

        scope.AssertRefused();

        Assert.True(LegacyTradeJournal.HasPending(otherUsers));
        Assert.Equal(beforeCharacter, File.ReadAllBytes(otherCharacter));
        Assert.Equal(beforeItems, File.ReadAllBytes(otherItems));
    });

    [Fact]
    public Task VersionOnePairWithUnrelatedCharacterAndItemChangesRemainsOutsideTradeStartupCustody() => WithFixture(scope =>
    {
        const long otherAccount = Uid + 1;
        var user = Path.Combine(scope.Data.Users, otherAccount.ToString(CultureInfo.InvariantCulture) + ".ini");
        var items = Path.Combine(scope.Data.Items, otherAccount.ToString(CultureInfo.InvariantCulture) + ".bin");
        var character = Replace(scope.BeforeCharacter, $"UID={Uid}\r\n", $"UID={otherAccount}\r\n");
        var raw = PlayerItemSnapshotLoader.ToLegacyBytes(Snapshot(ItemUid + 1, 5));
        File.WriteAllBytes(user, character);
        File.WriteAllBytes(items, raw);
        var changes = scope.Changes().Concat(new LegacyTradeJournalChange[]
        {
            new(LegacyTradeJournalFileKind.Character, otherAccount, character, character),
            new(LegacyTradeJournalFileKind.Items, otherAccount, raw, raw),
        }).ToArray();
        using var interruption = LegacyJournalInterruption.StopAfter(0);
        Assert.False(LegacyTradeJournal.TryCommit(scope.Data.Users, scope.Data.Items, changes, interruption, out var reason));
        Assert.Contains(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        Assert.True(interruption.PendingObserved);
        scope.AssertRefused();
    });

    [Fact]
    public Task NullableItemsBeforeImageCannotCreateAnItemFileThroughTheClosedRecoveryRoute() => WithFixture(scope =>
    {
        File.Delete(scope.ItemsPath);
        var changes = scope.Changes().Select(change => change.Kind == LegacyTradeJournalFileKind.Items
            ? change with { Before = null } : change).ToArray();
        using var interruption = LegacyJournalInterruption.StopAfter(0);
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(scope.Data.Users, scope.Data.Items, changes, interruption, out var reason));
        Assert.Contains(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        Assert.True(interruption.PendingObserved);
        scope.AssertRefused();
        Assert.False(File.Exists(scope.ItemsPath));
    });
}
