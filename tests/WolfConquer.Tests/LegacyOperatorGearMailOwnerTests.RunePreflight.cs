using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOperatorGearMailOwnerTests
{
    private const uint OperatorPreflightRuneId = 4_020_515;

    [Theory]
    [InlineData(2)]
    [InlineData(25)]
    public Task ExistingOsOperatorMailRefusesMalformedRequiredRuneDataBeforeUidOrSave(int field)
    {
        var row = OperatorPreflightRune();
        Assert.IsType<string[]>(row.Fields)[field] = "malformed-required-rune-value";
        return WithOwnerAsync(async owner =>
        {
            var before = owner.Current;
            var binding = owner.Native.OrdinarySpellBinding;
            var files = owner.Fixture.Files(); var high = owner.HighWater; var sent = owner.Sent.Count;
            var result = await owner.Deliver(owner.Request(Guid.NewGuid()) with
            {
                Items = new[] { new LegacyOperatorGearMailItem(OperatorPreflightRuneId) }
            });
            Assert.Equal(LegacyOperatorGearMailStatus.Rejected, result.Status);
            Assert.Equal("operatorMailNativeCatalogInvalid", result.Reason);
            Assert.Empty(result.ItemUids); Assert.Empty(result.MailIds);
            Assert.Equal(high, owner.HighWater); Assert.Equal(sent, owner.Sent.Count);
            Assert.Same(before, owner.Current); Assert.Same(binding, owner.Native.OrdinarySpellBinding);
            Assert.Empty(owner.Mail()); owner.Fixture.AssertFiles(files);
            Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));
        }, [row]);
    }

    [Fact]
    public Task ExistingOsOperatorMailAcceptsCompleteRuneMetadataAndClaimsTheSameIssuedUid() => WithOwnerAsync(async owner =>
    {
        var high = owner.HighWater; var binding = owner.Native.OrdinarySpellBinding;
        var request = owner.Request(Guid.NewGuid()) with
        {
            Items = new[] { new LegacyOperatorGearMailItem(OperatorPreflightRuneId) }
        };
        var result = await owner.Deliver(request);
        Assert.True(result.Status == LegacyOperatorGearMailStatus.CommittedNotificationCompleted,
            $"Unexpected operator mail outcome: {result.Status}; {result.Reason}");
        var uid = Assert.Single(result.ItemUids); var mailId = Assert.Single(result.MailIds);
        Assert.Equal(high + 1, uid); Assert.Equal(high + 1, owner.HighWater);
        var mail = Assert.Single(owner.Mail()); Assert.Equal(mailId, mail.Id); Assert.Equal(uid, mail.ItemId);
        var saved = Assert.Single(owner.Items().WarehouseItems);
        Assert.Equal(uid, saved.Uid); Assert.Equal(OperatorPreflightRuneId, saved.ItemId);
        Assert.Equal((ushort)1, saved.StackSize); Assert.Equal(1000U, saved.WarehouseId);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(owner.Items()),
            PlayerItemSnapshotLoader.ToLegacyBytes(owner.Current.PlayerItems));
        Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));

        await owner.ClaimAsync(mailId);
        Assert.Equal(OperatorPreflightRuneId, Assert.Single(owner.Items().InventoryItems, row => row.Uid == uid).ItemId);
        Assert.Equal(OperatorPreflightRuneId, owner.Native.Inventory!.ClientItems[uid].ITEM_ID);
        Assert.DoesNotContain(owner.Items().WarehouseItems, row => row.Uid == uid);
        Assert.Equal(0U, Assert.Single(owner.Mail()).ItemId);
        Assert.Equal(high + 1, owner.HighWater); Assert.Same(binding, owner.Native.OrdinarySpellBinding);
        Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));

        var afterClaim = owner.Fixture.Files(); var sent = owner.Sent.Count;
        var repeated = await owner.Deliver(request);
        Assert.Equal(LegacyOperatorGearMailStatus.PreviouslySubmittedOutcomeUnproven, repeated.Status);
        Assert.Empty(repeated.ItemUids); Assert.Empty(repeated.MailIds);
        Assert.Equal(high + 1, owner.HighWater); Assert.Equal(sent, owner.Sent.Count);
        owner.Fixture.AssertFiles(afterClaim);
    }, [OperatorPreflightRune()]);

    private static ItemType OperatorPreflightRune()
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = OperatorPreflightRuneId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "OwnedOperatorRunePreflight";
        fields[ItemType.DurabilityFieldIndex] = "321";
        fields[ItemType.StackSizeFieldIndex] = "1";
        // The actual required Rune class/spell columns are complete valid
        // unsigned zero values. No external resolver or fake owner is supplied.
        return new ItemType(checked((int)OperatorPreflightRuneId), fields[1], fields);
    }
}
