using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyGmRuntimeTests
{
    [Theory]
    [InlineData("basic-trojan", 9)]
    [InlineData("socket-materials", 6)]
    [InlineData("consumables", 3)]
    public Task RegisteredChatDeliversRealMailAndRetryNeverRemints(string kit, int count) => WithGmAsync(async owner =>
    {
        var operation = Guid.NewGuid();
        var initialHigh = owner.HighWater;
        var native = owner.Native;
        var binding = native.OrdinarySpellBinding;
        var command = $"@wolf kit {kit} {operation:N}";
        await CommandAsync(owner, command);
        Assert.Contains(Messages(owner), text => text.Contains("status=CommittedNotificationCompleted", StringComparison.Ordinal));
        var mail = owner.Mail();
        Assert.Equal(count, mail.Length);
        Assert.Equal(count, mail.Select(row => row.Id).Distinct().Count());
        Assert.Equal(count, mail.Select(row => row.ItemId).Distinct().Count());
        Assert.All(mail, row => Assert.True(row.ItemId > initialHigh));
        Assert.Equal(initialHigh + (uint)count, owner.HighWater);
        var saved = owner.Items();
        Assert.Equal(count, saved.WarehouseItems.Count);
        Assert.Equal(mail.Select(row => row.ItemId).Order(), saved.WarehouseItems.Select(row => row.Uid).Order());
        Assert.All(saved.WarehouseItems, row => Assert.Equal(1000U, row.WarehouseId));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(saved),
            PlayerItemSnapshotLoader.ToLegacyBytes(owner.Current.PlayerItems));
        Assert.Same(native, owner.Current.NativeGameClient);
        Assert.Same(binding, native.OrdinarySpellBinding);
        Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));

        var files = owner.Fixture.Files();
        owner.Sent.Clear();
        await CommandAsync(owner, command);
        Assert.Contains(Messages(owner), text => text.Contains("PreviouslySubmittedOutcomeUnproven", StringComparison.Ordinal));
        Assert.Equal(initialHigh + (uint)count, owner.HighWater);
        owner.Fixture.AssertFiles(files);

        owner.Sent.Clear();
        await CommandAsync(owner, "@wolf inspect mail");
        Assert.Contains(Messages(owner), text => text.Contains($"Cached live mails: {count}", StringComparison.Ordinal));
        owner.Fixture.AssertFiles(files);
    });

    [Fact]
    public Task MissingKitCatalogDataRefusesBeforeReservationOrMail() => WithGmAsync(async owner =>
    {
        var files = owner.Fixture.Files();
        var high = owner.HighWater;
        await CommandAsync(owner, $"@wolf kit basic-trojan {Guid.NewGuid():N}");
        AssertRefusal(owner);
        Assert.Equal(high, owner.HighWater);
        Assert.Empty(owner.Mail());
        owner.Fixture.AssertFiles(files);
    }, completeCatalog: false);

    [Fact]
    public Task RealMailClaimMovesTheExactGmIssuedAttachmentWithoutRelog() => WithGmAsync(async owner =>
    {
        await CommandAsync(owner, $"@wolf kit consumables {Guid.NewGuid():N}");
        var mail = owner.Mail();
        Assert.Equal(3, mail.Length);
        var attachment = mail[0];
        var high = owner.HighWater;
        await owner.ClaimAsync(attachment.Id);
        Assert.Contains(owner.Items().InventoryItems, row => row.Uid == attachment.ItemId);
        Assert.DoesNotContain(owner.Items().WarehouseItems, row => row.Uid == attachment.ItemId);
        Assert.Contains(owner.Current.PlayerItems.InventoryItems, row => row.Uid == attachment.ItemId);
        Assert.Equal(0U, Assert.Single(owner.Mail(), row => row.Id == attachment.Id).ItemId);
        Assert.Equal(high, owner.HighWater);
        Assert.Same(owner.Native, owner.Current.NativeGameClient);
        Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));
    });
}
