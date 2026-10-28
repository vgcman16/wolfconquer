using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOperatorGearMailOwnerTests
{
    [Fact]
    public Task DeliveryAndClaimUseTheSameGenuineAdmittedClientAndPersistBothTransitions() => WithOwnerAsync(async owner =>
    {
        var high = owner.HighWater;
        var request = owner.Request(Guid.NewGuid());
        var result = await owner.Deliver(request);
        Assert.True(result.Status == LegacyOperatorGearMailStatus.CommittedNotificationCompleted, result.Reason);
        var mailId = Assert.Single(result.MailIds);
        var itemUid = Assert.Single(result.ItemUids);
        Assert.Equal(high + 1, itemUid);
        Assert.Equal(itemUid, owner.HighWater);
        var mail = Assert.Single(owner.Mail());
        Assert.Equal(mailId, mail.Id);
        Assert.Equal(itemUid, mail.ItemId);
        var attachment = Assert.Single(owner.Items().WarehouseItems, row => row.Uid == itemUid);
        Assert.Equal(GearId, attachment.ItemId);
        Assert.Equal(1000U, attachment.WarehouseId);
        Assert.Same(owner.Native, owner.Current.NativeGameClient);
        Assert.Contains(owner.Current.PlayerItems.WarehouseItems, row => row.Uid == itemUid && row.WarehouseId == 1000);
        Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));

        var files = owner.Fixture.Files();
        var frames = owner.Sent.Count;
        var repeated = await owner.Deliver(request);
        Assert.Equal(LegacyOperatorGearMailStatus.PreviouslySubmittedOutcomeUnproven, repeated.Status);
        Assert.Empty(repeated.MailIds);
        Assert.Empty(repeated.ItemUids);
        Assert.Equal(frames, owner.Sent.Count);
        owner.Fixture.AssertFiles(files);

        await owner.ClaimAsync(mailId);
        Assert.True(owner.Sent.Count > frames);
        Assert.Contains(owner.Items().InventoryItems, row => row.Uid == itemUid && row.ItemId == GearId && row.WarehouseId == 0);
        Assert.DoesNotContain(owner.Items().WarehouseItems, row => row.Uid == itemUid);
        Assert.Equal(0U, Assert.Single(owner.Mail()).ItemId);
        Assert.Contains(owner.Current.PlayerItems.InventoryItems, row => row.Uid == itemUid && row.ItemId == GearId);
        Assert.Same(owner.Native, owner.Current.NativeGameClient);
        Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));
    });

    [Theory]
    [InlineData("catalog")]
    [InlineData("empty")]
    [InlineData("too-many")]
    [InlineData("domain")]
    public Task InvalidRequestsRefuseBeforeAnyUidOrFileChange(string kind) => WithOwnerAsync(async owner =>
    {
        var request = owner.Request(Guid.NewGuid());
        request = kind switch
        {
            "catalog" => request with { Items = new[] { new LegacyOperatorGearMailItem(999999999) } },
            "empty" => request with { Items = Array.Empty<LegacyOperatorGearMailItem>() },
            "too-many" => request with { Items = Enumerable.Repeat(new LegacyOperatorGearMailItem(GearId), 33).ToArray() },
            _ => request with { ExpectedDomain = Guid.NewGuid() },
        };
        var before = owner.Fixture.Files();
        var high = owner.HighWater;
        var frames = owner.Sent.Count;
        var current = owner.Current;
        var result = await owner.Deliver(request);
        Assert.Equal(LegacyOperatorGearMailStatus.Rejected, result.Status);
        Assert.Empty(result.ItemUids);
        Assert.Empty(result.MailIds);
        Assert.Equal(high, owner.HighWater);
        Assert.Equal(frames, owner.Sent.Count);
        Assert.Same(current, owner.Current);
        owner.Fixture.AssertFiles(before);
    });

    [Fact]
    public Task ClosedTargetRefusesBeforeUidAllocation() => WithOwnerAsync(async owner =>
    {
        await owner.CloseAsync();
        var before = owner.Fixture.Files();
        var high = owner.HighWater;
        var frames = owner.Sent.Count;
        var result = await owner.Deliver(owner.Request(Guid.NewGuid()));
        Assert.Equal(LegacyOperatorGearMailStatus.Rejected, result.Status);
        Assert.Empty(result.ItemUids);
        Assert.Equal(high, owner.HighWater);
        Assert.Equal(frames, owner.Sent.Count);
        owner.Fixture.AssertFiles(before);
    });
    [Fact]
    public Task JournalMailboxReplacementFailureNeverReportsDeliverySuccess() => WithOwnerAsync(async owner =>
    {
        if (!OperatingSystem.IsWindows()) return; // This control uses Windows delete-sharing semantics.
        var mailPath = Path.Combine(owner.Fixture.Mailbox, Uid + ".bin");
        var mailBefore = File.ReadAllBytes(mailPath);
        var characterBefore = File.ReadAllBytes(owner.Fixture.UserPath);
        var itemPath = Path.Combine(owner.Fixture.Items, Uid + ".bin");
        var itemBefore = File.ReadAllBytes(itemPath);
        var high = owner.HighWater;
        var frames = owner.Sent.Count;
        LegacyOperatorGearMailResult result;
        using (var lockFile = new FileStream(mailPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            result = await owner.Deliver(owner.Request(Guid.NewGuid()));
        Assert.Contains(result.Status, new[] { LegacyOperatorGearMailStatus.Rejected, LegacyOperatorGearMailStatus.RecoveryRequired });
        Assert.Contains("tradeJournal", result.Reason, StringComparison.Ordinal);
        Assert.Empty(result.MailIds);
        Assert.Empty(result.ItemUids);
        Assert.Equal(frames, owner.Sent.Count);
        Assert.Equal(high + 1, owner.HighWater); // Actual durable reservation preceded this journal failure.
        Assert.Equal(mailBefore, File.ReadAllBytes(mailPath));
        if (!LegacyTradeJournal.HasPending(owner.Fixture.Users))
        {
            Assert.Equal(characterBefore, File.ReadAllBytes(owner.Fixture.UserPath));
            Assert.Equal(itemBefore, File.ReadAllBytes(itemPath));
        }
        else Assert.Equal(LegacyOperatorGearMailStatus.RecoveryRequired, result.Status);
    });
}
