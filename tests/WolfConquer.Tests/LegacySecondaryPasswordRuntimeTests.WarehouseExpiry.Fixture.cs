using System.Buffers.Binary;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    private const int ExpiryFutureTimer = 20_991_231;
    private const int ExpiryPastTimer = 20_000_101;

    private static PlayerItemSnapshotItem ExpiryRow(uint uid, int timer, byte locked = 2) => new()
    {
        Uid = uid, ItemId = 1_088_001, Durability = 5, MaximumDurability = 10,
        StackSize = 3, Bound = 1, Locked = locked, UnlockTimer = timer, Inscribed = 1,
        PurificationItemId = 800001, PurificationLevel = 3,
        RefineryEffectId = 724440, RefineryEffectLevel = 2,
        RefineryEffectPercent = 10, RefineryEffectPercent2 = 20,
        PerfectionLevel = 2, PerfectionProgress = 17, OwnerUid = 77,
        OwnerName = "ExpiryOwner", Signature = "preserve expiry metadata",
    };

    private static uint ExpiryTarget(uint uid, LegacyWarehouseAction action) => action switch
    {
        LegacyWarehouseAction.ShowInventorySash => uid,
        LegacyWarehouseAction.ShashShow => 10,
        _ => 8,
    };

    private void SeedWarehouseExpiry(uint uid, LegacyWarehouseAction action, bool malformedLast = false)
    {
        Seed(uid, "0,0,0", itemUid: 11);
        Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(UserPath(uid), uid,
            new Dictionary<string, string> { ["InventorySashCount"] = "2" },
            detailedIoFailures: true, out var reason), reason);
        var original = SashPinSetupItems(deposited: false);
        var foreign = ExpiryRow(50, 0).WithWarehouseId(44);
        PlayerItemSnapshot items;
        if (action == LegacyWarehouseAction.ShashShow)
        {
            var sash = Assert.Single(original.InventoryItems, row => row.Uid == 10);
            items = original with
            {
                InventoryItems = original.InventoryItems.Select(row => ReferenceEquals(row, sash)
                    ? row.WithDepositedItems([ExpiryRow(20, 0), ExpiryRow(21, ExpiryPastTimer)]) : row).ToArray(),
                WarehouseItems = original.WarehouseItems.Append(foreign)
                    .OrderBy(row => row.WarehouseId).ThenBy(row => row.Uid).ToArray(),
            };
        }
        else
        {
            PlayerItemSnapshotItem[] selected =
            [
                ExpiryRow(20, 0).WithItemId(1_100_009).WithDepositedItems([ExpiryRow(30, 0)]),
                ExpiryRow(21, ExpiryPastTimer),
                ExpiryRow(22, ExpiryFutureTimer),
                ExpiryRow(23, 20_261_301, locked: 1), // Not Locked2: calendar is irrelevant.
                ExpiryRow(24, malformedLast ? 20_260_230 : 0, locked: malformedLast ? (byte)2 : (byte)0),
            ];
            var target = ExpiryTarget(uid, action);
            items = original with
            {
                WarehouseItems = original.WarehouseItems.Append(foreign)
                    .Concat(selected.Select(row => row.WithWarehouseId(target)))
                    .OrderBy(row => row.WarehouseId).ThenBy(row => row.Uid).ToArray(),
            };
        }
        var loader = new PlayerItemSnapshotLoader(ItemsRoot);
        loader.SaveByCharacterUid(uid, items); // Setup only, before actual native login.
        var loaded = loader.LoadByCharacterUid(uid);
        Assert.Equal(File.ReadAllBytes(ItemPath(uid)), PlayerItemSnapshotLoader.ToLegacyBytes(loaded));
        Assert.True(LegacyItemSaveGraph.TryCapture(loaded, out _));
    }

    private sealed record ExpirySnapshot(WorldSession Session, string Json, Dictionary<string, byte[]> Files, int Frames);

    private ExpirySnapshot CaptureExpiry(Owner owner, Player player)
    {
        var session = Current(owner, player);
        return new(session, JsonSerializer.Serialize(session), SashPinFiles(), player.Sent.Count);
    }

    private static byte[] ExpiryRequest(uint uid, LegacyWarehouseAction action) =>
        new LegacyWarehousePacket(ExpiryTarget(uid, action), action, 0, 0, []).ToFrameBytes();

    private static PlayerItemSnapshot ExpectedExpiryItems(WorldSession before) => before.PlayerItems with
    {
        WarehouseItems = before.PlayerItems.WarehouseItems.Select(row => row.Uid is 20 or 21
            ? row.WithLocked(0) : row).ToArray(),
    };

    private Dictionary<string, byte[]> ExpectedExpiryFiles(uint uid, ExpirySnapshot before)
    {
        var files = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase);
        var path = Path.GetRelativePath(root, ItemPath(uid));
        var bytes = files[path].ToArray();
        var roots = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        var offset = sizeof(int);
        var changed = new List<uint>();
        for (var index = 0; index < roots; index++)
        {
            var rowUid = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));
            if (rowUid is 20 or 21)
            {
                Assert.Equal((byte)2, bytes[offset + 33]);
                bytes[offset + 33] = 0; // Independent DBItem Locked byte oracle.
                changed.Add(rowUid);
            }
            var children = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 136, sizeof(uint)));
            offset = checked(offset + PlayerItemSnapshotLoader.LegacyDbItemSize * (1 + (int)children));
        }
        Assert.Equal(new uint[] { 20, 21 }, changed.OrderBy(value => value));
        files[path] = bytes;
        return files;
    }

    private void AssertExpiryCommitted(Owner owner, Player player, ExpirySnapshot before, Dictionary<string, byte[]> files)
    {
        var current = Current(owner, player);
        Assert.NotSame(before.Session, current);
        Assert.Equal(before.Session with { PlayerItems = current.PlayerItems }, current);
        Assert.Equal(JsonSerializer.Serialize(ExpectedExpiryItems(before.Session)), JsonSerializer.Serialize(current.PlayerItems));
        Assert.Equal(before.Json, JsonSerializer.Serialize(before.Session));
        AssertSashPinFiles(files);
        Assert.Equal(File.ReadAllBytes(ItemPath(player.Uid)), PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
        Assert.Equal(File.ReadAllBytes(ItemPath(player.Uid)), PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(ItemsRoot).LoadByCharacterUid(player.Uid)));
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, current, new(42)));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private void AssertExpiryReadOnly(Owner owner, Player player, ExpirySnapshot before)
    {
        Assert.Same(before.Session, Current(owner, player));
        Assert.Equal(before.Json, JsonSerializer.Serialize(Current(owner, player)));
        AssertSashPinFiles(before.Files);
    }

    private static byte[][] ExpectedExpiryFrames(WorldSession input, LegacyWarehouseAction action, bool ownServer)
    {
        var target = ExpiryTarget(checked((uint)input.Character.Uid), action);
        if (action == LegacyWarehouseAction.ShashShow)
        {
            var children = Assert.Single(input.PlayerItems.InventoryItems, row => row.Uid == 10)
                .DepositedItems.OrderBy(row => row.Uid).ToArray();
            return [new LegacyWarehousePacket(target, action, 0, 12, children).ToFrameBytes(), ExpiryExtras(children)];
        }
        var capacity = target == input.Character.Uid ? (ushort)2 : (ushort)60;
        var frames = new List<byte[]>();
        foreach (var chunk in input.PlayerItems.WarehouseItems.Where(row => row.WarehouseId == target)
            .OrderBy(row => row.Uid).Chunk(4))
        {
            frames.Add(new LegacyWarehousePacket(target, action, 0, capacity, chunk).ToFrameBytes());
            foreach (var row in chunk.Where(row => row.Locked == 2))
            {
                if (row.Uid is 20 or 21)
                {
                    if (ownServer)
                        frames.Add(LegacyGameItemPacket.FromSnapshotItem(row.WithLocked(0),
                            mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes());
                }
                else
                {
                    Assert.Equal(22U, row.Uid);
                    // Preserve accepted managed future-date display even without
                    // own-server authority; only actual clearing needs that issuer.
                    frames.Add(new LegacyItemLockPacket(row.Uid, LegacyItemLockPacket.UnlockDateType,
                        LegacyItemLockPacket.UnlockDateLockedMode, ExpiryFutureTimer).ToFrameBytes());
                }
            }
            frames.Add(ExpiryExtras(chunk));
        }
        return frames.ToArray();
    }

    private static byte[] ExpiryExtras(IReadOnlyList<PlayerItemSnapshotItem> rows) => new LegacyItemExtraPacket(
        rows.Select(row => new LegacyItemExtraPurification(row.Uid, LegacyItemExtraPacket.StabilizationType,
            800001, 3, 0, 0, 0)).ToArray(),
        rows.Select(row => new LegacyItemExtraRefinery(row.Uid, LegacyItemExtraPacket.PermanentRefineryType,
            301, 2, 10, 20, 0)).ToArray()).ToFrameBytes();

    private static void AssertExpiryFrames(Player player, int first, byte[][] expected)
    {
        var actual = player.Sent.Skip(first).ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }
}
