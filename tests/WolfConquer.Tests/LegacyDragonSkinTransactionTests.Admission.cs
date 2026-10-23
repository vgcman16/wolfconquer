using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDragonSkinTransactionTests
{
    [Theory]
    [InlineData(2, "uninitialized")]
    [InlineData(3, "uninitialized")]
    [InlineData(2, "failed")]
    [InlineData(3, "failed")]
    [InlineData(2, "null-roots")]
    [InlineData(3, "null-roots")]
    [InlineData(2, "null-items")]
    [InlineData(3, "null-items")]
    [InlineData(2, "wrong-users")]
    [InlineData(3, "wrong-users")]
    [InlineData(2, "wrong-items")]
    [InlineData(3, "wrong-items")]
    public Task RegisteredMutationRequiresGenuinelyReadyRecoveryForItsExactDomain(int type, string mode) => RunAsync(async f =>
    {
        f.Seed(type == 2 ? "0/0/" : "1/1/1/", [Item(9, 720_001), Item(10, Mapped(1))]);
        var before = Capture(f.Current);
        byte[]? evidence = null;
        var pending = Path.Combine(f.Users, ".managed-trade-journal", "pending.json");
        switch (mode)
        {
            case "uninitialized": f.Recovery = new(f.Users, f.Items); break;
            case "failed":
                Directory.CreateDirectory(Path.GetDirectoryName(pending)!);
                File.WriteAllText(pending, "{owned malformed pending record}");
                evidence = File.ReadAllBytes(pending);
                f.Recovery = new(f.Users, f.Items);
                Assert.False(f.Recovery.TryEnsureInitialized(out _));
                break;
            default:
                var users = mode == "null-roots" ? null : mode == "wrong-users" ? Path.Combine(f.Root, "OtherUsers") : f.Users;
                var items = mode is "null-roots" or "null-items" ? null : mode == "wrong-items" ? Path.Combine(f.Root, "OtherItems") : f.Items;
                if (users is not null) Directory.CreateDirectory(users);
                if (items is not null) Directory.CreateDirectory(items);
                f.Recovery = new(users, items);
                Assert.True(f.Recovery.TryEnsureInitialized(out var reason), reason);
                break;
        }
        await f.InvokeAsync((LegacyDragonSkinType)type);
        Assert.Same(before.Session, f.Current); AssertModelUnchanged(before); Assert.Empty(f.Sent);
        Assert.Equal(f.CharacterBefore, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(f.ItemsBefore, File.ReadAllBytes(f.ItemPath));
        Assert.Contains("dragonSkinRecoveryOrOperationOwnerUnavailable", f.Log.ToString());
        if (evidence is not null)
        {
            Assert.Equal(evidence, File.ReadAllBytes(pending));
            Assert.False(f.Recovery.TryEnsureInitialized(out _));
        }
        else Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [Theory]
    [InlineData("store")]
    [InlineData("barrier")]
    [InlineData("recovery")]
    public Task MissingRequiredOwnerCannotTurnIntoUnpersistedMutation(string missing) => RunAsync(async f =>
    {
        f.Seed("0/0/", [Item(10, Mapped(1))]);
        var before = Capture(f.Current);
        await f.InvokeAsync(LegacyDragonSkinType.Unlock, includeStore: missing != "store",
            includeBarrier: missing != "barrier", includeRecovery: missing != "recovery");
        AssertRejected(f, before, missing == "store" ? "dragonSkinCurrentOwnerUnavailable" : "dragonSkinRecoveryOrOperationOwnerUnavailable");
    });

    [Theory]
    [InlineData("same-uid")]
    [InlineData("different-uid")]
    [InlineData("removed")]
    public Task StaleSuppliedSessionCannotGrantOrOverwriteTheRegisteredOwner(string mode) => RunAsync(async f =>
    {
        f.Seed("0/0/", [Item(10, Mapped(1))]);
        var original = Capture(f.Original);
        var replacement = f.Original with { Character = f.Original.Character with { Money = 77, Uid = mode == "different-uid" ? Uid + 1 : Uid } };
        if (mode == "removed") Assert.True(f.Sessions.TryRemove(f.Connection, out _));
        else f.Sessions.Set(f.Connection, replacement);
        var replacementImage = Capture(replacement);
        await f.InvokeAsync(LegacyDragonSkinType.Unlock, supplied: f.Original);
        if (mode == "removed") Assert.False(f.Sessions.TryGet(f.Connection, out _)); else Assert.Same(replacement, f.Current);
        AssertModelUnchanged(original); AssertModelUnchanged(replacementImage); AssertFilesUnchanged(f);
        Assert.Empty(f.Sent); Assert.Contains("dragonSkinCurrentOwnerUnavailable", f.Log.ToString());
    });

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public Task OwnerRetiredWhileQueuedForExclusiveAdmissionCannotCommitStalePreparation(int type) => RunAsync(async f =>
    {
        f.Seed(type == 2 ? "0/0/" : "1/1/1/", [Item(10, Mapped(1))]);
        var original = Capture(f.Original);
        using var budget = new CancellationTokenSource(Bound);
        var held = await f.Barrier.EnterSharedAsync(budget.Token);
        f.KeepLease(held);
        var actual = f.InvokeAsync((LegacyDragonSkinType)type);
        Assert.False(actual.IsCompleted);
        var replacement = f.Original with { Character = f.Original.Character with { Money = 77 } };
        var before = Capture(replacement);
        f.Sessions.Set(f.Connection, replacement);
        held.Dispose();
        await actual;
        AssertRejected(f, before, "dragonSkinCurrentOwnerChanged");
        AssertModelUnchanged(original);
    });

    [Theory]
    [InlineData("0/2/1/1/")]
    [InlineData("0/1/")]
    [InlineData("0/-1/")]
    [InlineData("0/2/1//")]
    [InlineData("3/1/1/")]
    [InlineData("0/1/0/")]
    public Task MalformedCompleteEntitlementNeverAuthorizesInventoryMutation(string state) => RunAsync(async f =>
    {
        f.Seed(state, [Item(10, Mapped(1))]);
        var before = Capture(f.Current);
        await f.InvokeAsync(LegacyDragonSkinType.Unlock);
        AssertRejected(f, before, "dragonSkinStateInvalid");
    });

    [Theory]
    [InlineData("inventory-position")]
    [InlineData("equipment-tail")]
    [InlineData("rune-tail")]
    [InlineData("warehouse-tail")]
    [InlineData("duplicate-tail")]
    [InlineData("null-tail")]
    [InlineData("deep-deposit")]
    [InlineData("perfection-text")]
    [InlineData("perfection-nul")]
    public Task FullPhysicalGraphCorruptionCannotHideAfterAValidCostRow(string fault) => RunAsync(async f =>
    {
        f.Seed("0/0/", [Item(10, Mapped(1))]);
        var items = f.Original.PlayerItems;
        switch (fault)
        {
            case "inventory-position": items = items with { InventoryItems = [Item(10, Mapped(1)).WithPosition(4)] }; break;
            case "equipment-tail": items = items with { EquipmentItems = [Item(11, 410_339)] }; break;
            case "rune-tail": items = items with { RuneItems = [Item(11, 410_339)] }; break;
            case "warehouse-tail": items = items with { WarehouseItems = [Item(11, 410_339)] }; break;
            case "duplicate-tail": items = items with { EquipmentItems = [Item(10, 410_339).WithPosition(4)] }; break;
            case "null-tail": items = items with { RelicFuseItems = [null!] }; break;
            case "deep-deposit": items = items with { InventoryItems = [Item(10, Mapped(1)), Item(11, 720_001).WithDepositedItems(
                [Item(12, 720_002).WithDepositedItems([Item(13, 720_003)])])] }; break;
            default:
                var malformed = Item(11, 720_001);
                malformed.OwnerName = fault == "perfection-nul" ? "owned\0tail" : new string('x', 33);
                items = items with { InventoryItems = [items.InventoryItems[0], malformed] };
                break;
        }
        var current = f.Original with { PlayerItems = items };
        f.Sessions.Set(f.Connection, current);
        var before = Capture(current);
        await f.InvokeAsync(LegacyDragonSkinType.Unlock);
        AssertRejected(f, before, "dragonSkinInventoryInvalid");
    });
}
