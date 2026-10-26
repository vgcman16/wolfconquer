using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task RegisteredWarehousePresenceMissingEligibleBankEmitsNoHeader() => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseFamily(LegacyWarehouseAction.DepositItem);
        await AssertPresenceShowAsync(fixture, present: false);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredWarehousePresenceLastWithdrawalRetainsEmptyOrdinaryOrOwnUidBank(bool ownUid) =>
        RunAsync(async fixture =>
        {
            await CreateEmptyPresenceBankAsync(fixture, ownUid);
            await AssertPresenceShowAsync(fixture, present: true, ownUid: ownUid);
            await AssertPresenceShowAsync(fixture, present: true, ownUid: ownUid); // Presence is not an IsShow cache.
        });

    [WindowsTradeJournalFailureFact]
    public Task RegisteredWarehousePresenceRefusedDepositCannotMintEmptyBankAndSameOwnerRetries() => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseFamily(LegacyWarehouseAction.DepositItem);
        var before = fixture.Capture();
        fixture.HoldTransferItemFile();
        await fixture.InvokeWarehouseFamilyAsync(LegacyWarehouseAction.DepositItem);
        fixture.ReleaseFiles(); // Actual task completed before releasing its owned sharing fault.
        fixture.AssertUnchanged(before);
        await AssertPresenceShowAsync(fixture, present: false);
        await CommitPresenceTransferAsync(fixture, LegacyWarehouseAction.DepositItem);
        await CommitPresenceTransferAsync(fixture, LegacyWarehouseAction.WithdrawItem);
        await AssertPresenceShowAsync(fixture, present: true);
    });

    [Fact]
    public Task RegisteredWarehousePresenceSurvivesUnrelatedSameGenerationSessionCopy() => RunAsync(async fixture =>
    {
        await CreateEmptyPresenceBankAsync(fixture);
        var original = fixture.Current;
        var updated = original with { PendingNobilityDonationConquerPoints = 17 };
        fixture.Sessions.Set(fixture.Connection, updated);
        Assert.Same(updated, fixture.Current);
        Assert.NotSame(updated, fixture.Context.Session); // Dispatch must select the actual Store.
        await AssertPresenceShowAsync(fixture, present: true);
    });

    [Fact]
    public Task RegisteredWarehousePresenceRemoveAndRebindExactImmutableRecordCannotReviveRetiredGeneration() => RunAsync(async fixture =>
    {
        await CreateEmptyPresenceBankAsync(fixture);
        await AssertPresenceShowAsync(fixture, present: true);
        var retired = fixture.Current;
        Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out var removed));
        Assert.Same(retired, removed);
        fixture.Sessions.Set(fixture.Connection, retired);
        Assert.Same(retired, fixture.Current);
        await AssertPresenceShowAsync(fixture, present: false);
    });

    [Fact]
    public Task RegisteredWarehousePresenceCannotCrossPhysicalConnectionsWithSameImmutableRecord() => RunAsync(async source =>
    {
        await CreateEmptyPresenceBankAsync(source);
        await AssertPresenceShowAsync(source, present: true);
        var sourceFiles = source.Files();
        await RunAsync(async foreign =>
        {
            foreign.Seed();
            Assert.NotSame(source.Connection, foreign.Connection);
            var before = foreign.Capture();
            source.Sessions.Set(foreign.Connection, source.Current);
            try
            {
                Assert.True(source.Sessions.TryGet(foreign.Connection, out var sharedRecord));
                Assert.Same(source.Current, sharedRecord);
                // Same Store and immutable record, different actual connection and generation.
                await foreign.InvokePresenceWithContextAsync(source.Context with
                { Connection = foreign.Connection, Session = sharedRecord });
                Assert.True(source.Sessions.TryGet(foreign.Connection, out var afterShow));
                Assert.Same(sharedRecord, afterShow);
                foreign.AssertUnchanged(before);
            }
            finally { source.Sessions.TryRemove(foreign.Connection, out _); }
        });
        source.AssertFiles(sourceFiles);
        await AssertPresenceShowAsync(source, present: true);
    });

    [Fact]
    public Task RegisteredWarehousePresenceRealFileReloadAndNewSessionDoesNotPersistEmptyBankKey() => RunAsync(async fixture =>
    {
        await CreateEmptyPresenceBankAsync(fixture);
        await AssertPresenceShowAsync(fixture, present: true);
        var retired = fixture.Current;
        var files = fixture.Files();
        var character = CharacterSnapshotParser.ParseText(File.ReadAllText(fixture.CharacterPath), fixture.CharacterPath);
        var items = new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid);
        Assert.Equal(File.ReadAllBytes(fixture.ItemPath), PlayerItemSnapshotLoader.ToLegacyBytes(items));
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(fixture.Sessions, fixture.Connection,
            Fixture.Uid, out var generation));
        // Component lifecycle: actual parsers plus a NEW session constructor,
        // published over the SAME holder, never a record copy or marker assignment.
        // This isolates fresh-session defaults and is not public login proof.
        var reloaded = new WorldSession(retired.AccountId, retired.Username, character, items,
            retired.PlayerSpells, retired.PlayerProficiencies, UsersRoot: fixture.Users);
        fixture.Sessions.Set(fixture.Connection, reloaded);
        Assert.NotSame(retired, fixture.Current);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(fixture.Sessions, fixture.Connection,
            Fixture.Uid, out var afterReloadGeneration));
        Assert.Same(generation, afterReloadGeneration);
        fixture.AssertFiles(files);
        await AssertPresenceShowAsync(fixture, present: false);
    });

    [Fact]
    public Task RegisteredWarehousePresenceMalformedForeignStoreTailRejectsEmptyHeaderWithoutConsumingPresence() => RunAsync(async fixture =>
    {
        await CreateEmptyPresenceBankAsync(fixture);
        var healthy = fixture.Current;
        fixture.Sessions.Set(fixture.Connection, healthy with
        {
            PlayerItems = healthy.PlayerItems with
            { RuneItems = healthy.PlayerItems.RuneItems.Concat(new PlayerItemSnapshotItem[] { null! }).ToArray() },
        });
        var before = fixture.Capture();
        await fixture.InvokeWarehouseShowAsync(LegacyWarehouseAction.Show);
        fixture.AssertUnchanged(before);
        fixture.Sessions.Set(fixture.Connection, healthy); // Restore only the in-memory malformed-tail fault.
        await AssertPresenceShowAsync(fixture, present: true);
    });

    [Fact]
    public Task RegisteredWarehousePresenceReturnedBankLastWithdrawalRetainsRealEmptyKey() => RunAsync(async fixture =>
    {
        const uint returnedBank = 65_535;
        fixture.SeedWarehouseFamily(LegacyWarehouseAction.WithdrawItem, storedBank: returnedBank);
        Assert.Single(fixture.Current.PlayerItems.WarehouseItems, row => row.WarehouseId == returnedBank);
        await CommitPresenceTransferAsync(fixture, LegacyWarehouseAction.WithdrawItem, returnedBank);
        await AssertPresenceShowAsync(fixture, present: true, target: returnedBank);
    });
}
