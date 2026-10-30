using System.Globalization;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyGmRuntimeTests
{
    private const uint GmCatalogConsumableId = 1_000_001;
    private const string GmCatalogCapability = "mail.catalogitem.self";

    [Fact]
    public Task CatalogOnlyGrantDeliversOneRealAttachmentClaimsAndRetriesWithoutReminting() =>
        WithCatalogItemGmAsync(async owner =>
        {
            var operation = Guid.NewGuid(); var high = owner.HighWater;
            var original = owner.Current; var binding = owner.Native.OrdinarySpellBinding;
            var command = $"@wolf item {GmCatalogConsumableId} 7 {operation:N}";
            await CommandAsync(owner, command);
            Assert.Contains(Messages(owner), text => text.Contains("status=CommittedNotificationCompleted", StringComparison.Ordinal));
            var mail = Assert.Single(owner.Mail());
            Assert.Equal(high + 1, owner.HighWater); Assert.Equal(high + 1, mail.ItemId);
            var saved = Assert.Single(owner.Items().WarehouseItems);
            Assert.Equal(mail.ItemId, saved.Uid); Assert.Equal(GmCatalogConsumableId, saved.ItemId);
            Assert.Equal((ushort)7, saved.StackSize); Assert.Equal(1000U, saved.WarehouseId);
            Assert.DoesNotContain(owner.Current.PlayerItems.InventoryItems, row => row.Uid == saved.Uid);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(owner.Items()),
                PlayerItemSnapshotLoader.ToLegacyBytes(owner.Current.PlayerItems));
            Assert.Same(binding, owner.Native.OrdinarySpellBinding); Assert.Same(owner.Native, owner.Current.NativeGameClient);
            Assert.Equal(original.Character.Money, owner.Current.Character.Money);
            Assert.Equal(original.Character.ConquerPoints, owner.Current.Character.ConquerPoints);
            Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));

            var delivered = owner.Fixture.Files(); owner.Sent.Clear();
            await CommandAsync(owner, command);
            Assert.Contains(Messages(owner), text => text.Contains("PreviouslySubmittedOutcomeUnproven", StringComparison.Ordinal));
            Assert.Equal(high + 1, owner.HighWater); owner.Fixture.AssertFiles(delivered);

            owner.Sent.Clear(); await owner.ClaimAsync(mail.Id);
            var claimed = Assert.Single(owner.Items().InventoryItems, row => row.Uid == saved.Uid);
            Assert.Equal(GmCatalogConsumableId, claimed.ItemId); Assert.Equal((ushort)7, claimed.StackSize);
            Assert.Equal(0U, claimed.WarehouseId);
            Assert.DoesNotContain(owner.Items().WarehouseItems, row => row.Uid == saved.Uid);
            Assert.Equal(0U, Assert.Single(owner.Mail()).ItemId);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(owner.Items()),
                PlayerItemSnapshotLoader.ToLegacyBytes(owner.Current.PlayerItems));
            Assert.Equal(high + 1, owner.HighWater); Assert.Same(binding, owner.Native.OrdinarySpellBinding);
            Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));

            var afterClaim = owner.Fixture.Files(); owner.Sent.Clear();
            await CommandAsync(owner, command);
            Assert.Contains(Messages(owner), text => text.Contains("PreviouslySubmittedOutcomeUnproven", StringComparison.Ordinal));
            Assert.Equal(high + 1, owner.HighWater); owner.Fixture.AssertFiles(afterClaim);
        }, GmCatalogItem(stackLimit: 7));

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public Task ActualCatalogStackBoundsRoundtripThroughMailAndClaim(int stack) =>
        WithCatalogItemGmAsync(async owner =>
        {
            var high = owner.HighWater;
            await CommandAsync(owner, $"@wolf item {GmCatalogConsumableId} {stack} {Guid.NewGuid():N}");
            Assert.Contains(Messages(owner), text => text.Contains("status=CommittedNotificationCompleted", StringComparison.Ordinal));
            var mail = Assert.Single(owner.Mail());
            Assert.Equal((ushort)stack, Assert.Single(owner.Items().WarehouseItems).StackSize);
            await owner.ClaimAsync(mail.Id);
            Assert.Equal((ushort)stack, Assert.Single(owner.Items().InventoryItems, row => row.Uid == mail.ItemId).StackSize);
            Assert.Equal(high + 1, owner.HighWater); Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));
        }, GmCatalogItem(stackLimit: ushort.MaxValue));

    [Fact]
    public Task ReusingAnOperationForDifferentCatalogPayloadCannotAllocateAgain() =>
        WithCatalogItemGmAsync(async owner =>
        {
            var operation = Guid.NewGuid();
            await CommandAsync(owner, $"@wolf item {GmCatalogConsumableId} 7 {operation:N}");
            Assert.Contains(Messages(owner), text => text.Contains("status=CommittedNotificationCompleted", StringComparison.Ordinal));
            var high = owner.HighWater; var files = owner.Fixture.Files(); owner.Sent.Clear();
            await CommandAsync(owner, $"@wolf item {GmCatalogConsumableId} 6 {operation:N}");
            Assert.Contains(Messages(owner), text => text.Contains("status=Rejected", StringComparison.Ordinal));
            Assert.Equal(high, owner.HighWater); owner.Fixture.AssertFiles(files);
            Assert.Single(owner.Mail()); Assert.Single(owner.Items().WarehouseItems);
        }, GmCatalogItem(stackLimit: 7));

    [Theory]
    [InlineData("inspect.self")]
    [InlineData("mail.testkit.self")]
    [InlineData("both")]
    public Task ExistingCapabilitiesNeverGrantCatalogItemMail(string capability) => WithGmAsync(async owner =>
    {
        await AssertCatalogRefusesUnchangedAsync(owner, $"@wolf item {OperatorGearMailFixture.GearId} 1 {Guid.NewGuid():N}");
        owner.Sent.Clear(); await CommandAsync(owner, "@wolf help");
        Assert.DoesNotContain(Messages(owner), text => text.StartsWith("@wolf item", StringComparison.Ordinal));
    }, capability == "both" ? ["inspect.self", "mail.testkit.self"] : [capability]);

    [Theory]
    [InlineData("@wolf inspect")]
    [InlineData("@wolf kit consumables 0a0649ee2f714d2da050fd4dc226baf9")]
    public Task CatalogOnlyGrantDoesNotGrantInspectionOrKitMail(string command) => WithGmAsync(async owner =>
    {
        await AssertCatalogRefusesUnchangedAsync(owner, command);
    }, [GmCatalogCapability]);

    [Fact]
    public Task CatalogOnlyHelpAdvertisesOnlyItsExplicitMailCommand() => WithGmAsync(async owner =>
    {
        var before = owner.Fixture.Files(); var high = owner.HighWater;
        await CommandAsync(owner, "@wolf help");
        var messages = Messages(owner);
        Assert.Contains(messages, text => text.StartsWith("@wolf item", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, text => text.StartsWith("@wolf kit", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, text => text.StartsWith("@wolf inspect", StringComparison.Ordinal));
        Assert.All(messages, text => { Assert.InRange(text.Length, 1, 180); Assert.All(text, ch => Assert.InRange(ch, ' ', '~')); });
        Assert.InRange(messages.Length, 1, 12); Assert.Equal(high, owner.HighWater); owner.Fixture.AssertFiles(before);
    }, [GmCatalogCapability]);

    [Theory]
    [InlineData("9999999", "1")]
    [InlineData("4294967295", "1")]
    [InlineData("410339", "2")]
    [InlineData("410339", "0")]
    [InlineData("410339", "65536")]
    [InlineData("0", "1")]
    public Task UnknownCatalogOrInvalidItemAndStackRefuseBeforeReservation(string id, string stack) =>
        WithGmAsync(owner => AssertCatalogRefusesUnchangedAsync(owner,
            $"@wolf item {id} {stack} {Guid.NewGuid():N}"), [GmCatalogCapability]);

    [Theory]
    [InlineData("stack")]
    [InlineData("timed")]
    [InlineData("durability")]
    [InlineData("rune-class")]
    [InlineData("rune-spell")]
    public Task UnsupportedRequiredCatalogDataRefusesBeforeAllocation(string fault)
    {
        var row = fault.StartsWith("rune-", StringComparison.Ordinal) ? GmCatalogItem(id: 4_020_515) : GmCatalogItem(stackLimit: 7);
        var fields = Assert.IsType<string[]>(row.Fields);
        if (fault == "timed") fields[ItemType.TimeFieldIndex] = "1";
        if (fault == "durability") fields[ItemType.DurabilityFieldIndex] = "0";
        if (fault == "rune-class") fields[ItemType.RequiredClassFieldIndex] = "invalid-class";
        if (fault == "rune-spell") fields[ItemType.RuneSpellIdFieldIndex] = "invalid-spell";
        return WithCatalogItemGmAsync(owner => AssertCatalogRefusesUnchangedAsync(owner,
            $"@wolf item {row.Id} {(fault == "stack" ? 8 : 1)} {Guid.NewGuid():N}"), row);
    }

    [Fact]
    public Task AmbiguousActualCatalogItemIdRefusesBeforeAllocation() => WithCatalogItemGmAsync(owner =>
        AssertCatalogRefusesUnchangedAsync(owner,
            $"@wolf item {GmCatalogConsumableId} 1 {Guid.NewGuid():N}"),
        GmCatalogItem(), GmCatalogItem());

    [Fact]
    public Task CompleteRuneCatalogMetadataSupportsRealMailAndClaim() => WithCatalogItemGmAsync(async owner =>
    {
        const uint rune = 4_020_515;
        var high = owner.HighWater;
        await CommandAsync(owner, $"@wolf item {rune} 1 {Guid.NewGuid():N}");
        Assert.Contains(Messages(owner), text => text.Contains("status=CommittedNotificationCompleted", StringComparison.Ordinal));
        var mail = Assert.Single(owner.Mail());
        Assert.Equal(high + 1, mail.ItemId); Assert.Equal(high + 1, owner.HighWater);
        Assert.Equal(rune, Assert.Single(owner.Items().WarehouseItems).ItemId);
        await owner.ClaimAsync(mail.Id);
        Assert.Equal(rune, Assert.Single(owner.Items().InventoryItems, row => row.Uid == mail.ItemId).ItemId);
        Assert.Equal(rune, owner.Native.Inventory!.ClientItems[mail.ItemId].ITEM_ID);
        Assert.Equal(high + 1, owner.HighWater); Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));
    }, GmCatalogItem(id: 4_020_515));

    private static async Task AssertCatalogRefusesUnchangedAsync(OperatorGearMailFixture owner, string command)
    {
        var files = owner.Fixture.Files(); var high = owner.HighWater; var current = owner.Current;
        await CommandAsync(owner, command);
        AssertRefusal(owner); Assert.Single(owner.SentSnapshot());
        Assert.Equal(high, owner.HighWater); Assert.Same(current, owner.Current);
        Assert.Empty(owner.Mail()); owner.Fixture.AssertFiles(files);
        Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));
        Assert.DoesNotContain("Frame exception", owner.Log.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static ItemType GmCatalogItem(ushort stackLimit = 1, uint id = GmCatalogConsumableId)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "OwnedGmCatalogFixture";
        fields[ItemType.DurabilityFieldIndex] = "321";
        fields[ItemType.StackSizeFieldIndex] = stackLimit.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)id), fields[1], fields);
    }

    private static async Task WithCatalogItemGmAsync(Func<OperatorGearMailFixture, Task> test, params ItemType[] rows)
    {
        OperatorGearMailFixture? owner = null; Exception? primary = null;
        try
        {
            owner = new OperatorGearMailFixture(); owner.Initialize(rows, [GmCatalogCapability]);
            await owner.LoginAsync(); owner.Sent.Clear(); await test(owner);
        }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (owner is not null)
        {
            owner.RetainEvidence = primary is not null;
            try { await owner.DisposeAsync(); } catch (Exception error) { cleanup = error; }
        }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }
}
