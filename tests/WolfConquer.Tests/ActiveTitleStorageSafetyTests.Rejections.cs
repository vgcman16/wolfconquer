using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ActiveTitleStorageSafetyTests
{
    public enum SaveFailure { Missing, Locked, UidMismatch }

    [Theory]
    [InlineData(SaveFailure.Missing, LegacyTitleStorageAction.Equip)]
    [InlineData(SaveFailure.Locked, LegacyTitleStorageAction.Equip)]
    [InlineData(SaveFailure.UidMismatch, LegacyTitleStorageAction.Equip)]
    [InlineData(SaveFailure.Missing, LegacyTitleStorageAction.UnEquip)]
    [InlineData(SaveFailure.Locked, LegacyTitleStorageAction.UnEquip)]
    [InlineData(SaveFailure.UidMismatch, LegacyTitleStorageAction.UnEquip)]
    public async Task FailedSaveDoesNotPublishSessionOrPackets(SaveFailure failure, LegacyTitleStorageAction action)
    {
        using var fixture = new Fixture(equipped: action == LegacyTitleStorageAction.UnEquip);
        var accountBytes = fixture.WriteAccountDecoy();
        if (failure == SaveFailure.Missing)
            File.Delete(fixture.SnapshotPath);
        if (failure == SaveFailure.UidMismatch)
            fixture.ReplaceStoredUid(Fixture.CharacterUid + 99, equipped: action == LegacyTitleStorageAction.UnEquip);
        var before = File.Exists(fixture.SnapshotPath) ? File.ReadAllBytes(fixture.SnapshotPath) : null;

        using (var exclusive = failure == SaveFailure.Locked
            ? new FileStream(fixture.SnapshotPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            : null)
        {
            await fixture.DispatchAsync(action);
        }

        fixture.AssertRejected(fixture.Original, before);
        Assert.Equal(accountBytes, fixture.ReadAccountDecoy());
    }

    [Theory]
    [InlineData(LegacyTitleStorageAction.Equip)]
    [InlineData(LegacyTitleStorageAction.UnEquip)]
    public async Task StaleContextCannotOverwriteCurrentSessionOrSave(LegacyTitleStorageAction action)
    {
        using var fixture = new Fixture(equipped: action == LegacyTitleStorageAction.UnEquip);
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        // Equal-value record copy, distinct reference: catches equality-based commit checks.
        var newer = fixture.Original with { };
        Assert.NotSame(fixture.Original, newer);
        Assert.Equal(fixture.Original, newer);
        fixture.Sessions.Set(fixture.Connection, newer);

        await fixture.DispatchAsync(action, context: fixture.Context);

        fixture.AssertRejected(newer, before);
    }

    [Theory]
    [InlineData(LegacyTitleStorageAction.Equip)]
    [InlineData(LegacyTitleStorageAction.UnEquip)]
    public async Task MissingSessionStoreFailsClosed(LegacyTitleStorageAction action)
    {
        using var fixture = new Fixture(equipped: action == LegacyTitleStorageAction.UnEquip);
        var before = File.ReadAllBytes(fixture.SnapshotPath);

        await fixture.DispatchAsync(action, context: fixture.Context with { Sessions = null });

        fixture.AssertRejected(fixture.Original, before);
    }

    [Theory]
    [InlineData(LegacyTitleStorageAction.Equip)]
    [InlineData(LegacyTitleStorageAction.UnEquip)]
    public async Task CancellationBeforeCommitLeavesSaveAndSessionUnchanged(LegacyTitleStorageAction action)
    {
        using var fixture = new Fixture(equipped: action == LegacyTitleStorageAction.UnEquip);
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.DispatchAsync(action, cancellationToken: cancellation.Token));

        fixture.AssertRejected(fixture.Original, before);
    }

    [Fact]
    public async Task UnequipOtherOwnedWingCannotClearTheCurrentlyEquippedWing()
    {
        using var fixture = new Fixture(equipped: true);
        var before = File.ReadAllBytes(fixture.SnapshotPath);

        await fixture.DispatchAsync(LegacyTitleStorageAction.UnEquip, id: 4_002, subtype: 19);

        fixture.AssertRejected(fixture.Original, before);
    }

    [Theory]
    [InlineData(LegacyTitleStorageAction.Equip, 4_002U, WingSubId)]
    [InlineData(LegacyTitleStorageAction.Equip, WingId, 18U)]
    [InlineData(LegacyTitleStorageAction.UnEquip, 4_002U, WingSubId)]
    [InlineData(LegacyTitleStorageAction.UnEquip, WingId, 18U)]
    public async Task MismatchedNestedTitleCannotChangeOrAcknowledgeTheRequest(
        LegacyTitleStorageAction action, uint nestedId, uint nestedSubtype)
    {
        using var fixture = new Fixture(equipped: action == LegacyTitleStorageAction.UnEquip);
        var before = File.ReadAllBytes(fixture.SnapshotPath);

        await fixture.DispatchAsync(action, nestedTitle: new LegacyTitleStorageTitle(nestedId, nestedSubtype));

        fixture.AssertRejected(fixture.Original, before);
    }

    [Fact]
    public async Task BlockedRecoveryOwnerRefusesWithoutSessionFileOrPacketEffects()
    {
        using var fixture = new Fixture();
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        Assert.Same(fixture.Recovery, fixture.Context.TradeRecovery);
        fixture.Recovery.Block("owned-title-test-safety-fence");
        Assert.True(fixture.Recovery.IsBlocked);

        await fixture.DispatchAsync();

        fixture.AssertRejected(fixture.Original, before);
        Assert.True(fixture.Recovery.IsBlocked);
    }
}
