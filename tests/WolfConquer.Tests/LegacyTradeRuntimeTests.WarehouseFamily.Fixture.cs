using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    private (Player Left, Player Right, uint Bank) CreateWarehouseTradePair(LegacyWarehouseAction action)
    {
        var left = AddItemClosurePlayer(1_297_141, 1_000, 50,
            new[] { ItemClosureSash(1_100_009, Item(12)) });
        var right = AddItemClosurePlayer(1_297_142, 2_000, 60, new[] { Item(20) });
        var bank = action is LegacyWarehouseAction.InventorySashDepositItem or
            LegacyWarehouseAction.InventorySashWithdrawItem ? left.Uid : 8U;
        // The own-UID capacity is genuine disk/current character state, not an
        // attribute injected only into the handler's stale context.
        Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(UserPath(left), left.Uid,
            new Dictionary<string, string> { ["InventorySashCount"] = "2" },
            detailedIoFailures: true, out var reason), reason);
        var current = Current(left);
        var character = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(left)), UserPath(left));
        Assert.Equal("2", character.Attributes["InventorySashCount"]);
        sessions.Set(left.Connection, current with { Character = character });
        if (action is LegacyWarehouseAction.WithdrawItem or LegacyWarehouseAction.InventorySashWithdrawItem)
        {
            current = Current(left);
            var seeded = current.PlayerItems with
            {
                WarehouseItems = current.PlayerItems.WarehouseItems.Append(Item(11).WithWarehouseId(bank, 0))
                    .OrderBy(item => item.WarehouseId).ThenBy(item => item.Uid).ToArray(),
            };
            // Setup precedes recovery/trade birth. The registered action below,
            // never this loader, owns all subsequent storage persistence.
            var loader = new PlayerItemSnapshotLoader(ItemsRoot);
            loader.SaveByCharacterUid(left.Uid, seeded);
            var parsed = loader.LoadByCharacterUid(left.Uid);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(seeded), PlayerItemSnapshotLoader.ToLegacyBytes(parsed));
            sessions.Set(left.Connection, current with { PlayerItems = parsed });
        }
        return (left, right, bank);
    }

    private async Task<LegacyTradeParty> ConfirmWarehouseTradeEscrowAsync(Player left, Player right)
    {
        await OpenItemClosureAsync(left, right);
        await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
        await SendItemClosureAsync(right, new(6) { ItemUID = 20 });
        await SendItemClosureAsync(left, new(7) { Money = 300 });
        await SendItemClosureAsync(left, new(9));
        Assert.True(trades.TryGet(left.Connection, out var party));
        Assert.Single(party.Items);
        Assert.Single(party.Other.Items);
        AssertWarehouseTradeEscrow(left, right, party);
        return party;
    }

    private void AssertWarehouseTradeEscrow(Player left, Player right, LegacyTradeParty expected)
    {
        Assert.True(trades.TryGet(left.Connection, out var actual));
        Assert.Same(expected, actual);
        Assert.True(trades.TryGet(right.Connection, out var peer));
        Assert.Same(expected.Other, peer);
        Assert.Same(expected, peer.Other);
        Assert.True(actual.WindowOpen);
        Assert.True(peer.WindowOpen);
        Assert.True(actual.Confirmed);
        Assert.False(peer.Confirmed);
        Assert.True(Current(left).LegacyTradeActive);
        Assert.True(Current(right).LegacyTradeActive);
        Assert.Equal(300L, actual.Money);
        Assert.Equal(0U, actual.ConquerPoints);
        AssertWallet(left, 700, 50);
        AssertWallet(right, 2_000, 60);
        var disk = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(left)), UserPath(left));
        Assert.True(LegacyTradeEscrowClaims.TryRead(Current(left).Character.Attributes, out var liveClaim, out var reason), reason);
        Assert.True(LegacyTradeEscrowClaims.TryRead(disk.Attributes, out var diskClaim, out reason), reason);
        Assert.Equal(new LegacyTradeEscrowClaim(actual.TradeId, 300, 0), liveClaim);
        Assert.Equal(liveClaim, diskClaim);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    private async Task MoveWarehouseTradeItemAsync(Player player, LegacyWarehouseAction action, uint bank, uint itemUid)
    {
        var request = new LegacyWarehousePacket(bank, action, itemUid, 0, []);
        await handlers.HandleAsync(ItemClosureContext(player), ReadFrame(request.ToFrameBytes()));
    }

    private static PlayerItemSnapshot ExpectedWarehouseTradeMove(PlayerItemSnapshot before,
        LegacyWarehouseAction action, uint bank, uint itemUid)
    {
        var withdraw = action is LegacyWarehouseAction.WithdrawItem or LegacyWarehouseAction.InventorySashWithdrawItem;
        var moving = withdraw
            ? Assert.Single(before.WarehouseItems, item => item.Uid == itemUid && item.WarehouseId == bank)
            : Assert.Single(before.InventoryItems, item => item.Uid == itemUid);
        return before with
        {
            InventoryItems = withdraw ? before.InventoryItems.Append(moving.WithWarehouseId(0, 0))
                .OrderBy(item => item.Uid).ToArray() : before.InventoryItems.Where(item => item.Uid != itemUid).ToArray(),
            WarehouseItems = withdraw ? before.WarehouseItems.Where(item => item.Uid != itemUid || item.WarehouseId != bank).ToArray()
                : before.WarehouseItems.Append(moving.WithWarehouseId(bank, 0))
                    .OrderBy(item => item.WarehouseId).ThenBy(item => item.Uid).ToArray(),
        };
    }

    private void AssertWarehouseTradeStorage(Player player, PlayerItemSnapshot expected,
        Dictionary<string, byte[]> beforeFiles)
    {
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
        Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(Current(player).PlayerItems));
        var expectedFiles = new Dictionary<string, byte[]>(beforeFiles, StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetRelativePath(root, ItemPath(player))] = bytes,
        };
        AssertItemClosureFiles(expectedFiles); // Includes both wallets/claims and every untouched store.
        AssertPersisted(player);
    }

    private void AssertWarehouseTradeClaimsCleared(params Player[] players)
    {
        foreach (var player in players)
        {
            var disk = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(player)), UserPath(player));
            Assert.True(LegacyTradeEscrowClaims.TryRead(Current(player).Character.Attributes, out var live, out var reason), reason);
            Assert.True(LegacyTradeEscrowClaims.TryRead(disk.Attributes, out var saved, out reason), reason);
            Assert.Null(live);
            Assert.Null(saved);
        }
    }
}
