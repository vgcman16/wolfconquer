using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Separate source-only negative packet: 16 rows, in addition to the frozen nine.
// All dispatches use the existing registered Fixture and its unchanged lifetime.
public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("store")]
    [InlineData("operations")]
    [InlineData("trades")]
    [InlineData("recovery")]
    [InlineData("unready-recovery")]
    [InlineData("wrong-ready-roots")]
    [InlineData("unregistered")]
    [InlineData("items-root")]
    [InlineData("users-root")]
    public Task RegisteredPhysicalSashMissingActualRequiredOwnerCannotDepositOrPublish(string missing) => RunAsync(async fixture =>
    {
        fixture.SeedTransfer();
        var before = fixture.Capture();
        switch (missing)
        {
            case "store": fixture.SetContext(fixture.Context with { Sessions = null }); break;
            case "operations": fixture.SetContext(fixture.Context with { TradeOperations = null }); break;
            case "trades": fixture.SetContext(fixture.Context with { Trades = null }); break;
            case "recovery": fixture.SetContext(fixture.Context with { TradeRecovery = null }); break;
            case "unready-recovery":
                var unready = new LegacyTradeRecoveryState(fixture.Users, fixture.Items);
                Assert.False(unready.IsReadyFor(fixture.Users, fixture.Items));
                fixture.SetContext(fixture.Context with { TradeRecovery = unready });
                break;
            case "wrong-ready-roots":
                var otherItems = Path.Combine(fixture.Root, "OtherOwnedItems");
                Directory.CreateDirectory(otherItems);
                var otherRecovery = new LegacyTradeRecoveryState(fixture.Users, otherItems);
                Assert.True(otherRecovery.TryEnsureInitialized(out var reason), reason);
                Assert.True(otherRecovery.IsReadyFor(fixture.Users, otherItems));
                Assert.False(otherRecovery.IsReadyFor(fixture.Users, fixture.Items));
                fixture.SetContext(fixture.Context with { TradeRecovery = otherRecovery });
                break;
            case "unregistered": Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out _)); break;
            case "items-root": fixture.SetContext(fixture.Context with { PlayerItemsRoot = null }); break;
            case "users-root":
                var noUsers = fixture.Current with { UsersRoot = null };
                fixture.Sessions.Set(fixture.Connection, noUsers);
                fixture.SetContext(fixture.Context with { Session = noUsers });
                before = fixture.Capture();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(missing));
        }

        await fixture.InvokeTransferAsync();

        if (missing != "unregistered") fixture.AssertUnchanged(before);
        else
        {
            Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
            Assert.Equal(before.Json, JsonSerializer.Serialize(before.Owner));
            fixture.AssertFiles(before.Files);
            Assert.Equal(before.FrameCount, fixture.Sent.Count);
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredPhysicalSashForeignStoreDiskChangeRefusesMoveUntilExactLeafRepair(bool withdraw) => RunAsync(async fixture =>
    {
        fixture.SeedTransfer(deposited: withdraw);
        var originalBytes = File.ReadAllBytes(fixture.ItemPath);
        var current = fixture.Current.PlayerItems;
        var foreign = Assert.Single(current.WarehouseItems);
        // A valid, representable change to a DIFFERENT store. The target UID and
        // its sash are unchanged, so only whole-graph disk admission detects it.
        var staleDisk = current with { WarehouseItems = new[] { foreign.WithStackSize(8) } };
        var staleBytes = PlayerItemSnapshotLoader.ToLegacyBytes(staleDisk);
        File.WriteAllBytes(fixture.ItemPath, staleBytes);
        Assert.Equal(staleBytes, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid)));
        Assert.NotEqual(Convert.ToHexString(originalBytes), Convert.ToHexString(staleBytes));
        var before = fixture.Capture();

        await fixture.InvokeTransferAsync(withdraw);

        fixture.AssertUnchanged(before);
        File.WriteAllBytes(fixture.ItemPath, originalBytes); // Repair only the injected owned leaf after terminal work.
        var repaired = fixture.Capture();
        await fixture.InvokeTransferAsync(withdraw);
        AssertTransferCommitted(fixture, repaired, withdraw);
        AssertTransferFrames(fixture, repaired, withdraw);
    });

    [Theory]
    [InlineData("foreign-uid", false)]
    [InlineData("stale-cp", true)]
    [InlineData("disk-escrow", false)]
    public Task RegisteredPhysicalSashStaleCharacterOwnerWalletOrClaimCannotMoveItems(string fault, bool withdraw) => RunAsync(async fixture =>
    {
        fixture.SeedTransfer(deposited: withdraw);
        var originalBytes = File.ReadAllBytes(fixture.CharacterPath);
        switch (fault)
        {
            case "foreign-uid": WriteSashField(fixture, "UID", "1293601"); break;
            case "stale-cp": WriteSashField(fixture, "ConquerPoints", "151"); break;
            case "disk-escrow":
                var original = fixture.Current;
                var attributes = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["ConquerPoints"] = "125" };
                var pending = LegacyTradeEscrowClaims.WithClaim(original with
                { Character = original.Character with { ConquerPoints = 125, Attributes = attributes } },
                    new LegacyTradeEscrowClaim(Guid.Parse("df9226c0-6ba2-4e5f-bca4-331eddbb553b"), 0, 25));
                // A real isolated disk-only debit/claim AFTER startup readiness.
                // Keep the Store stale; do not reinitialize and reconcile it away.
                Assert.True(LegacyTradeEscrowClaims.TryPersistWallet(original, pending, out var reason), reason);
                var disk = CharacterSnapshotParser.ParseText(File.ReadAllText(fixture.CharacterPath), fixture.CharacterPath);
                Assert.True(LegacyTradeEscrowClaims.TryRead(disk.Attributes, out var claim, out reason), reason);
                Assert.NotNull(claim);
                Assert.Equal(25U, claim.ConquerPoints);
                Assert.Equal(125, disk.ConquerPoints);
                Assert.Same(original, fixture.Current);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        Assert.NotEqual(Convert.ToHexString(originalBytes), Convert.ToHexString(File.ReadAllBytes(fixture.CharacterPath)));
        var before = fixture.Capture();

        await fixture.InvokeTransferAsync(withdraw);

        fixture.AssertUnchanged(before);
        // This restores only the test-created fault, not a live user's escrow.
        File.WriteAllBytes(fixture.CharacterPath, originalBytes);
        var repaired = fixture.Capture();
        await fixture.InvokeTransferAsync(withdraw);
        AssertTransferCommitted(fixture, repaired, withdraw);
        AssertTransferFrames(fixture, repaired, withdraw);
    });

    [Fact]
    public Task RegisteredPhysicalSashMalformedForeignTailRefusesWithoutPartialMoveOrOutputs() => RunAsync(async fixture =>
    {
        fixture.SeedTransfer();
        var healthy = fixture.Current;
        // Deliberate malformed LIVE tail after genuine parsed birth. Never ask
        // the serializer to accept a null item or replace an actual saver.
        var malformedItems = healthy.PlayerItems with
        {
            WarehouseItems = healthy.PlayerItems.WarehouseItems.Concat(new PlayerItemSnapshotItem[] { null! }).ToArray(),
        };
        fixture.Sessions.Set(fixture.Connection, healthy with { PlayerItems = malformedItems });
        var before = fixture.Capture();

        await fixture.InvokeTransferAsync();

        fixture.AssertUnchanged(before);
        fixture.Sessions.Set(fixture.Connection, healthy); // Exact owned corruption repair.
        var repaired = fixture.Capture();
        await fixture.InvokeTransferAsync();
        AssertTransferCommitted(fixture, repaired, withdraw: false);
        AssertTransferFrames(fixture, repaired, withdraw: false);
    });

    [Fact]
    public Task RegisteredPhysicalSashStaleContextUsesActualCurrentStoreWithoutLosingFreshRows() => RunAsync(async fixture =>
    {
        fixture.SeedTransfer();
        var staleContext = fixture.Context;
        var staleOwner = fixture.Current;
        var staleJson = JsonSerializer.Serialize(staleOwner);
        var freshItems = staleOwner.PlayerItems with
        {
            InventoryItems = staleOwner.PlayerItems.InventoryItems
                .Select(item => item.Uid == TransferItemUid ? item.WithStackSize(7) : item).ToArray(),
            WarehouseItems = staleOwner.PlayerItems.WarehouseItems.Select(item => item.WithStackSize(8)).ToArray(),
        };
        fixture.SaveTransferSetup(freshItems, keepStaleContext: true);
        Assert.Same(staleContext.Session, fixture.Context.Session);
        Assert.NotSame(fixture.Context.Session, fixture.Current);
        var before = fixture.Capture();

        await fixture.InvokeTransferAsync();

        AssertTransferCommitted(fixture, before, withdraw: false);
        AssertTransferFrames(fixture, before, withdraw: false);
        var sash = Assert.Single(fixture.Current.PlayerItems.InventoryItems, item => item.Uid == TransferSashUid);
        Assert.Equal((ushort)7, Assert.Single(sash.DepositedItems).StackSize);
        Assert.Equal((ushort)8, Assert.Single(fixture.Current.PlayerItems.WarehouseItems).StackSize);
        Assert.Equal(staleJson, JsonSerializer.Serialize(staleOwner));
    });
}
