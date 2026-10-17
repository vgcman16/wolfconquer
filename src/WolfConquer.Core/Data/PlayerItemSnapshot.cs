using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Data;

public sealed record PlayerItemSnapshot(
    IReadOnlyList<PlayerItemSnapshotItem> InventoryItems,
    IReadOnlyList<PlayerItemSnapshotItem> RelicFuseItems,
    IReadOnlyList<PlayerItemSnapshotItem> MythsoulBagItems,
    IReadOnlyList<PlayerItemSnapshotItem> EquipmentItems,
    IReadOnlyList<PlayerItemSnapshotItem> RuneItems,
    IReadOnlyList<PlayerItemSnapshotItem> WardrobeItems)
{
    public IReadOnlyList<PlayerItemSnapshotItem> WarehouseItems { get; init; } = [];

    public PlayerItemSnapshot(
        IReadOnlyList<PlayerItemSnapshotItem> InventoryItems,
        IReadOnlyList<PlayerItemSnapshotItem> RelicFuseItems,
        IReadOnlyList<PlayerItemSnapshotItem> MythsoulBagItems,
        IReadOnlyList<PlayerItemSnapshotItem> EquipmentItems,
        IReadOnlyList<PlayerItemSnapshotItem> RuneItems)
        : this(InventoryItems, RelicFuseItems, MythsoulBagItems, EquipmentItems, RuneItems, [])
    {
    }

    public static PlayerItemSnapshot Empty { get; } = new([], [], [], [], []);
}

public sealed class PlayerItemSnapshotItem
{
    public uint Uid { get; init; }
    public uint ItemId { get; init; }
    public ushort Durability { get; init; }
    public ushort MaximumDurability { get; init; }
    public ushort Position { get; init; }
    public uint SocketProgress { get; init; }
    public byte SocketOne { get; init; }
    public byte SocketTwo { get; init; }
    public uint Effect { get; init; }
    public byte Plus { get; init; }
    public byte Bless { get; init; }
    public byte Bound { get; init; }
    public byte Enchant { get; init; }
    public byte Suspicious { get; init; }
    public byte Locked { get; init; }
    public uint PlusProgress { get; init; }
    public ushort Inscribed { get; init; }
    public uint Activate { get; init; }
    public uint TimeLeftInMinutes { get; init; }
    public ushort StackSize { get; init; }
    public uint WarehouseId { get; init; }
    public uint Color { get; init; }
    public int UnlockTimer { get; init; }
    public uint ItemPoints { get; init; }
    public uint PurificationItemId { get; init; }
    public uint PurificationLevel { get; init; }
    public uint PurificationDuration { get; init; }
    public long PurificationAddedOn { get; init; }
    public uint RefineryEffectId { get; init; }
    public uint RefineryEffectLevel { get; init; }
    public uint RefineryEffectPercent { get; init; }
    public uint RefineryEffectPercent2 { get; init; }
    public uint RefineryEffectDuration { get; init; }
    public long RefineryEffectAddedOn { get; init; }
    public uint AnimaItemId { get; init; }
    public uint RuneExperience { get; init; }
    public uint Attribute1 { get; init; }
    public uint Attribute2 { get; init; }
    public uint Attribute3 { get; init; }
    public uint Attribute4 { get; init; }
    public uint Attribute5 { get; init; }
    public long Expiration { get; init; }
    public uint MythSoul { get; init; }
    public uint MythSoulPoints { get; init; }
    public uint PerfectionLevel { get; set; }
    public uint PerfectionProgress { get; set; }
    public uint OwnerUid { get; set; }
    public string OwnerName { get; set; } = "";
    public string Signature { get; set; } = "";
    public IReadOnlyList<PlayerItemSnapshotItem> DepositedItems { get; init; } = [];

    public PlayerItemSnapshotItem WithPosition(ushort position)
    {
        return Copy(position: position);
    }

    public PlayerItemSnapshotItem WithItemId(uint itemId)
    {
        return Copy(itemId: itemId);
    }

    public PlayerItemSnapshotItem WithDurability(ushort durability)
    {
        return Copy(durability: durability);
    }

    public PlayerItemSnapshotItem WithSocketProgress(uint socketProgress)
    {
        return Copy(socketProgress: socketProgress);
    }

    public PlayerItemSnapshotItem WithRuneExperience(uint runeExperience)
    {
        return Copy(runeExperience: runeExperience);
    }

    public PlayerItemSnapshotItem WithStackSize(ushort stackSize)
    {
        return Copy(stackSize: stackSize);
    }

    public PlayerItemSnapshotItem WithBound(byte bound)
    {
        return Copy(bound: bound);
    }

    public PlayerItemSnapshotItem WithColor(uint color)
    {
        return Copy(color: color);
    }

    public PlayerItemSnapshotItem WithLocked(byte locked)
    {
        return Copy(locked: locked);
    }

    public PlayerItemSnapshotItem WithAnimaItemId(uint animaItemId)
    {
        return Copy(animaItemId: animaItemId);
    }

    public PlayerItemSnapshotItem WithWarehouseId(uint warehouseId, ushort? position = null)
    {
        return Copy(warehouseId: warehouseId, position: position);
    }

    public PlayerItemSnapshotItem WithDepositedItems(IReadOnlyList<PlayerItemSnapshotItem> depositedItems)
    {
        ArgumentNullException.ThrowIfNull(depositedItems);
        return Copy(depositedItems: depositedItems);
    }

    private PlayerItemSnapshotItem Copy(
        uint? itemId = null,
        ushort? position = null,
        ushort? durability = null,
        uint? socketProgress = null,
        uint? runeExperience = null,
        ushort? stackSize = null,
        byte? bound = null,
        uint? color = null,
        byte? locked = null,
        uint? animaItemId = null,
        uint? warehouseId = null,
        IReadOnlyList<PlayerItemSnapshotItem>? depositedItems = null)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = Uid,
            ItemId = itemId ?? ItemId,
            Durability = durability ?? Durability,
            MaximumDurability = MaximumDurability,
            Position = position ?? Position,
            SocketProgress = socketProgress ?? SocketProgress,
            SocketOne = SocketOne,
            SocketTwo = SocketTwo,
            Effect = Effect,
            Plus = Plus,
            Bless = Bless,
            Bound = bound ?? Bound,
            Enchant = Enchant,
            Suspicious = Suspicious,
            Locked = locked ?? Locked,
            PlusProgress = PlusProgress,
            Inscribed = Inscribed,
            Activate = Activate,
            TimeLeftInMinutes = TimeLeftInMinutes,
            StackSize = stackSize ?? StackSize,
            WarehouseId = warehouseId ?? WarehouseId,
            Color = color ?? Color,
            UnlockTimer = UnlockTimer,
            ItemPoints = ItemPoints,
            PurificationItemId = PurificationItemId,
            PurificationLevel = PurificationLevel,
            PurificationDuration = PurificationDuration,
            PurificationAddedOn = PurificationAddedOn,
            RefineryEffectId = RefineryEffectId,
            RefineryEffectLevel = RefineryEffectLevel,
            RefineryEffectPercent = RefineryEffectPercent,
            RefineryEffectPercent2 = RefineryEffectPercent2,
            RefineryEffectDuration = RefineryEffectDuration,
            RefineryEffectAddedOn = RefineryEffectAddedOn,
            AnimaItemId = animaItemId ?? AnimaItemId,
            RuneExperience = runeExperience ?? RuneExperience,
            Attribute1 = Attribute1,
            Attribute2 = Attribute2,
            Attribute3 = Attribute3,
            Attribute4 = Attribute4,
            Attribute5 = Attribute5,
            Expiration = Expiration,
            MythSoul = MythSoul,
            MythSoulPoints = MythSoulPoints,
            PerfectionLevel = PerfectionLevel,
            PerfectionProgress = PerfectionProgress,
            OwnerUid = OwnerUid,
            OwnerName = OwnerName,
            Signature = Signature,
            DepositedItems = depositedItems ?? DepositedItems,
        };
    }
}

public sealed class PlayerItemSnapshotLoader
{
    public const int LegacyDbItemSize = 184;
    public const int LegacyPerfectionSize = 80;

    private const int UidOffset = 0;
    private const int ItemIdOffset = 4;
    private const int DurabilityOffset = 8;
    private const int MaximumDurabilityOffset = 10;
    private const int PositionOffset = 12;
    private const int SocketProgressOffset = 16;
    private const int SocketOneOffset = 20;
    private const int SocketTwoOffset = 21;
    private const int EffectOffset = 24;
    private const int PlusOffset = 28;
    private const int BlessOffset = 29;
    private const int BoundOffset = 30;
    private const int EnchantOffset = 31;
    private const int SuspiciousOffset = 32;
    private const int LockedOffset = 33;
    private const int PlusProgressOffset = 36;
    private const int InscribedOffset = 40;
    private const int ActivateOffset = 44;
    private const int TimeLeftInMinutesOffset = 48;
    private const int StackSizeOffset = 52;
    private const int WarehouseIdOffset = 56;
    private const int ColorOffset = 60;
    private const int UnlockTimerOffset = 64;
    private const int ItemPointsOffset = 68;
    private const int PurificationItemIdOffset = 84;
    private const int PurificationLevelOffset = 88;
    private const int PurificationDurationOffset = 92;
    private const int PurificationAddedOnOffset = 96;
    private const int RefineryEffectIdOffset = 104;
    private const int RefineryEffectLevelOffset = 108;
    private const int RefineryEffectPercentOffset = 112;
    private const int RefineryEffectPercent2Offset = 116;
    private const int RefineryEffectDurationOffset = 120;
    private const int RefineryEffectAddedOnOffset = 128;
    private const int DepositeCountOffset = 136;
    private const int AnimaItemIdOffset = 140;
    private const int RuneExperienceOffset = 144;
    private const int Attribute1Offset = 148;
    private const int Attribute2Offset = 152;
    private const int Attribute3Offset = 156;
    private const int Attribute4Offset = 160;
    private const int Attribute5Offset = 164;
    private const int ExpirationOffset = 168;
    private const int MythSoulOffset = 176;
    private const int MythSoulPointsOffset = 180;

    private const int PerfectionItemUidOffset = 0;
    private const int PerfectionLevelOffset = 4;
    private const int PerfectionProgressOffset = 8;
    private const int PerfectionOwnerUidOffset = 12;
    private const int PerfectionOwnerNameOffset = 16;
    private const int PerfectionSignatureOffset = 48;
    private const int PerfectionTextLength = 32;

    private readonly string _playerItemsRoot;

    public PlayerItemSnapshotLoader(string playerItemsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerItemsRoot);
        _playerItemsRoot = playerItemsRoot;
    }

    public string PlayerItemsRoot => _playerItemsRoot;

    public PlayerItemSnapshot LoadByCharacterUid(long characterUid)
    {
        var path = GetCharacterSnapshotPath(characterUid);
        return SnapshotFileLockCoordinator.ExecuteLocked(
            path,
            () =>
            {
                var bytes = PlayerItemSnapshotFilePersistence.ReadAllBytesIfExists(path);
                return bytes is null
                    ? PlayerItemSnapshot.Empty
                    : Parse(bytes, Path.GetFileName(path));
            });
    }

    public void SaveByCharacterUid(long characterUid, PlayerItemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var path = GetCharacterSnapshotPath(characterUid);
        var bytes = ToLegacyBytes(snapshot);
        SnapshotFileLockCoordinator.ExecuteLocked(
            path,
            () =>
            {
                PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(path, bytes);
                return true;
            });
    }

    public static byte[] ToLegacyBytes(PlayerItemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var topLevelItems = EnumerateTopLevelItems(snapshot).ToArray();
        var depositedItemCount = topLevelItems.Sum(item => item.DepositedItems.Count);
        var perfectionItems = EnumerateWithDeposits(topLevelItems)
            .Where(HasPerfectionRecord)
            .GroupBy(item => item.Uid)
            .Select(group => group.First())
            .ToArray();

        var bytes = new byte[checked(
            sizeof(int) +
            ((topLevelItems.Length + depositedItemCount) * LegacyDbItemSize) +
            sizeof(int) +
            (perfectionItems.Length * LegacyPerfectionSize))];
        var offset = 0;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)), topLevelItems.Length);
        offset += sizeof(int);

        foreach (var item in topLevelItems)
        {
            WriteDbItem(bytes.AsSpan(offset, LegacyDbItemSize), item);
            offset += LegacyDbItemSize;

            foreach (var depositedItem in item.DepositedItems)
            {
                WriteDbItem(bytes.AsSpan(offset, LegacyDbItemSize), depositedItem);
                offset += LegacyDbItemSize;
            }
        }

        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)), perfectionItems.Length);
        offset += sizeof(int);

        foreach (var item in perfectionItems)
        {
            WritePerfection(bytes.AsSpan(offset, LegacyPerfectionSize), item);
            offset += LegacyPerfectionSize;
        }

        return bytes;
    }

    public static PlayerItemSnapshot Parse(ReadOnlySpan<byte> bytes, string sourceFile = "")
    {
        if (bytes.Length < sizeof(int))
        {
            throw new FormatException($"Player item snapshot '{sourceFile}' is missing the item count.");
        }

        var itemCount = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (itemCount < 0)
        {
            throw new FormatException($"Player item snapshot '{sourceFile}' has negative item count {itemCount}.");
        }

        var offset = sizeof(int);
        var inventory = new List<PlayerItemSnapshotItem>();
        var relicFuse = new List<PlayerItemSnapshotItem>();
        var mythsoul = new List<PlayerItemSnapshotItem>();
        var equipment = new List<PlayerItemSnapshotItem>();
        var runes = new List<PlayerItemSnapshotItem>();
        var wardrobe = new List<PlayerItemSnapshotItem>();
        var warehouse = new List<PlayerItemSnapshotItem>();
        var visibleByUid = new Dictionary<uint, PlayerItemSnapshotItem>();

        for (var i = 0; i < itemCount; i++)
        {
            EnsureAvailable(bytes, offset, LegacyDbItemSize, sourceFile);
            var item = ReadDbItem(bytes.Slice(offset, LegacyDbItemSize));
            offset += LegacyDbItemSize;

            var depositCount = BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.Slice(offset - LegacyDbItemSize + DepositeCountOffset, sizeof(uint)));
            var depositedItems = new List<PlayerItemSnapshotItem>();
            for (var depositIndex = 0; depositIndex < depositCount; depositIndex++)
            {
                EnsureAvailable(bytes, offset, LegacyDbItemSize, sourceFile);
                var depositedItem = ReadDbItem(bytes.Slice(offset, LegacyDbItemSize));
                offset += LegacyDbItemSize;
                depositedItems.Add(depositedItem);
                visibleByUid.TryAdd(depositedItem.Uid, depositedItem);
            }

            if (depositedItems.Count > 0)
            {
                item = item.WithDepositedItems(
                    depositedItems.OrderBy(depositedItem => depositedItem.Uid).ToArray());
            }

            AddVisibleItem(item, inventory, relicFuse, mythsoul, equipment, runes, wardrobe, warehouse, visibleByUid);
        }

        EnsureAvailable(bytes, offset, sizeof(int), sourceFile);
        var perfectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, sizeof(int)));
        if (perfectionCount < 0)
        {
            throw new FormatException(
                $"Player item snapshot '{sourceFile}' has negative perfection count {perfectionCount}.");
        }

        offset += sizeof(int);
        for (var i = 0; i < perfectionCount; i++)
        {
            EnsureAvailable(bytes, offset, LegacyPerfectionSize, sourceFile);
            ApplyPerfection(bytes.Slice(offset, LegacyPerfectionSize), visibleByUid);
            offset += LegacyPerfectionSize;
        }

        if (offset != bytes.Length)
        {
            throw new FormatException(
                $"Player item snapshot '{sourceFile}' has {bytes.Length - offset} trailing bytes.");
        }

        return new PlayerItemSnapshot(
            inventory.ToArray(),
            relicFuse.ToArray(),
            mythsoul.ToArray(),
            equipment.OrderBy(item => item.Position).ThenBy(item => item.Uid).ToArray(),
            runes
                .OrderBy(item => item.Position)
                .ThenBy(item => IsLegacyRuneBagPosition(item.Position) ? 0u : item.Uid)
                .ToArray(),
            wardrobe.ToArray())
        {
            WarehouseItems = warehouse.OrderBy(item => item.WarehouseId).ThenBy(item => item.Uid).ToArray(),
        };
    }

    private static PlayerItemSnapshotItem ReadDbItem(ReadOnlySpan<byte> item)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = ReadUInt32(item, UidOffset),
            ItemId = ReadUInt32(item, ItemIdOffset),
            Durability = ReadUInt16(item, DurabilityOffset),
            MaximumDurability = ReadUInt16(item, MaximumDurabilityOffset),
            Position = ReadUInt16(item, PositionOffset),
            SocketProgress = ReadUInt32(item, SocketProgressOffset),
            SocketOne = ReadByteEnum(item, SocketOneOffset),
            SocketTwo = ReadByteEnum(item, SocketTwoOffset),
            Effect = ReadUInt32(item, EffectOffset),
            Plus = item[PlusOffset],
            Bless = item[BlessOffset],
            Bound = item[BoundOffset],
            Enchant = item[EnchantOffset],
            Suspicious = item[SuspiciousOffset],
            Locked = item[LockedOffset],
            PlusProgress = ReadUInt32(item, PlusProgressOffset),
            Inscribed = ReadUInt16(item, InscribedOffset),
            Activate = ReadUInt32(item, ActivateOffset),
            TimeLeftInMinutes = ReadUInt32(item, TimeLeftInMinutesOffset),
            StackSize = ReadUInt16(item, StackSizeOffset),
            WarehouseId = ReadUInt32(item, WarehouseIdOffset),
            Color = ReadUInt32(item, ColorOffset),
            UnlockTimer = BinaryPrimitives.ReadInt32LittleEndian(item.Slice(UnlockTimerOffset, sizeof(int))),
            ItemPoints = ReadUInt32(item, ItemPointsOffset),
            PurificationItemId = ReadUInt32(item, PurificationItemIdOffset),
            PurificationLevel = ReadUInt32(item, PurificationLevelOffset),
            PurificationDuration = ReadUInt32(item, PurificationDurationOffset),
            PurificationAddedOn = BinaryPrimitives.ReadInt64LittleEndian(item.Slice(PurificationAddedOnOffset, sizeof(long))),
            RefineryEffectId = ReadUInt32(item, RefineryEffectIdOffset),
            RefineryEffectLevel = ReadUInt32(item, RefineryEffectLevelOffset),
            RefineryEffectPercent = ReadUInt32(item, RefineryEffectPercentOffset),
            RefineryEffectPercent2 = ReadUInt32(item, RefineryEffectPercent2Offset),
            RefineryEffectDuration = ReadUInt32(item, RefineryEffectDurationOffset),
            RefineryEffectAddedOn = BinaryPrimitives.ReadInt64LittleEndian(item.Slice(RefineryEffectAddedOnOffset, sizeof(long))),
            AnimaItemId = ReadUInt32(item, AnimaItemIdOffset),
            RuneExperience = ReadUInt32(item, RuneExperienceOffset),
            Attribute1 = ReadUInt32(item, Attribute1Offset),
            Attribute2 = ReadUInt32(item, Attribute2Offset),
            Attribute3 = ReadUInt32(item, Attribute3Offset),
            Attribute4 = ReadUInt32(item, Attribute4Offset),
            Attribute5 = ReadUInt32(item, Attribute5Offset),
            Expiration = BinaryPrimitives.ReadInt64LittleEndian(item.Slice(ExpirationOffset, sizeof(long))),
            MythSoul = ReadUInt32(item, MythSoulOffset),
            MythSoulPoints = ReadUInt32(item, MythSoulPointsOffset),
        };
    }

    private static IEnumerable<PlayerItemSnapshotItem> EnumerateTopLevelItems(PlayerItemSnapshot snapshot)
    {
        foreach (var item in snapshot.InventoryItems)
        {
            yield return item;
        }

        foreach (var item in snapshot.RelicFuseItems)
        {
            yield return item;
        }

        foreach (var item in snapshot.MythsoulBagItems)
        {
            yield return item;
        }

        foreach (var item in snapshot.EquipmentItems)
        {
            yield return item;
        }

        foreach (var item in snapshot.RuneItems)
        {
            yield return item;
        }

        foreach (var item in snapshot.WardrobeItems)
        {
            yield return item;
        }

        foreach (var item in snapshot.WarehouseItems)
        {
            yield return item;
        }
    }

    private static IEnumerable<PlayerItemSnapshotItem> EnumerateWithDeposits(
        IEnumerable<PlayerItemSnapshotItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var depositedItem in item.DepositedItems)
            {
                yield return depositedItem;
            }
        }
    }

    private static bool HasPerfectionRecord(PlayerItemSnapshotItem item)
    {
        return item.PerfectionLevel != 0 ||
            item.PerfectionProgress != 0 ||
            item.OwnerUid != 0 ||
            item.OwnerName.Length != 0 ||
            item.Signature.Length != 0;
    }

    private static void WriteDbItem(Span<byte> destination, PlayerItemSnapshotItem item)
    {
        destination.Clear();
        WriteUInt32(destination, UidOffset, item.Uid);
        WriteUInt32(destination, ItemIdOffset, item.ItemId);
        WriteUInt16(destination, DurabilityOffset, item.Durability);
        WriteUInt16(destination, MaximumDurabilityOffset, item.MaximumDurability);
        WriteUInt16(destination, PositionOffset, item.Position);
        WriteUInt32(destination, SocketProgressOffset, item.SocketProgress);
        destination[SocketOneOffset] = item.SocketOne;
        destination[SocketTwoOffset] = item.SocketTwo;
        WriteUInt32(destination, EffectOffset, item.Effect);
        destination[PlusOffset] = item.Plus;
        destination[BlessOffset] = item.Bless;
        destination[BoundOffset] = item.Bound;
        destination[EnchantOffset] = item.Enchant;
        destination[SuspiciousOffset] = item.Suspicious;
        destination[LockedOffset] = item.Locked;
        WriteUInt32(destination, PlusProgressOffset, item.PlusProgress);
        WriteUInt16(destination, InscribedOffset, item.Inscribed);
        WriteUInt32(destination, ActivateOffset, item.Activate);
        WriteUInt32(destination, TimeLeftInMinutesOffset, item.TimeLeftInMinutes);
        WriteUInt16(destination, StackSizeOffset, item.StackSize);
        WriteUInt32(destination, WarehouseIdOffset, item.WarehouseId);
        WriteUInt32(destination, ColorOffset, item.Color);
        WriteInt32(destination, UnlockTimerOffset, item.UnlockTimer);
        WriteUInt32(destination, ItemPointsOffset, item.ItemPoints);
        WriteUInt32(destination, PurificationItemIdOffset, item.PurificationItemId);
        WriteUInt32(destination, PurificationLevelOffset, item.PurificationLevel);
        WriteUInt32(destination, PurificationDurationOffset, item.PurificationDuration);
        WriteInt64(destination, PurificationAddedOnOffset, item.PurificationAddedOn);
        WriteUInt32(destination, RefineryEffectIdOffset, item.RefineryEffectId);
        WriteUInt32(destination, RefineryEffectLevelOffset, item.RefineryEffectLevel);
        WriteUInt32(destination, RefineryEffectPercentOffset, item.RefineryEffectPercent);
        WriteUInt32(destination, RefineryEffectPercent2Offset, item.RefineryEffectPercent2);
        WriteUInt32(destination, RefineryEffectDurationOffset, item.RefineryEffectDuration);
        WriteInt64(destination, RefineryEffectAddedOnOffset, item.RefineryEffectAddedOn);
        WriteUInt32(destination, DepositeCountOffset, checked((uint)item.DepositedItems.Count));
        WriteUInt32(destination, AnimaItemIdOffset, item.AnimaItemId);
        WriteUInt32(destination, RuneExperienceOffset, item.RuneExperience);
        WriteUInt32(destination, Attribute1Offset, item.Attribute1);
        WriteUInt32(destination, Attribute2Offset, item.Attribute2);
        WriteUInt32(destination, Attribute3Offset, item.Attribute3);
        WriteUInt32(destination, Attribute4Offset, item.Attribute4);
        WriteUInt32(destination, Attribute5Offset, item.Attribute5);
        WriteInt64(destination, ExpirationOffset, item.Expiration);
        WriteUInt32(destination, MythSoulOffset, item.MythSoul);
        WriteUInt32(destination, MythSoulPointsOffset, item.MythSoulPoints);
    }

    private static void WritePerfection(Span<byte> destination, PlayerItemSnapshotItem item)
    {
        destination.Clear();
        WriteUInt32(destination, PerfectionItemUidOffset, item.Uid);
        WriteUInt32(destination, PerfectionLevelOffset, item.PerfectionLevel);
        WriteUInt32(destination, PerfectionProgressOffset, item.PerfectionProgress);
        WriteUInt32(destination, PerfectionOwnerUidOffset, item.OwnerUid);
        WriteFixedUtf8(destination.Slice(PerfectionOwnerNameOffset, PerfectionTextLength), item.OwnerName);
        WriteFixedUtf8(destination.Slice(PerfectionSignatureOffset, PerfectionTextLength), item.Signature);
    }

    private static void AddVisibleItem(
        PlayerItemSnapshotItem item,
        List<PlayerItemSnapshotItem> inventory,
        List<PlayerItemSnapshotItem> relicFuse,
        List<PlayerItemSnapshotItem> mythsoul,
        List<PlayerItemSnapshotItem> equipment,
        List<PlayerItemSnapshotItem> runes,
        List<PlayerItemSnapshotItem> wardrobe,
        List<PlayerItemSnapshotItem> warehouse,
        Dictionary<uint, PlayerItemSnapshotItem> visibleByUid)
    {
        if (item.WarehouseId != 0)
        {
            if (item.WarehouseId == 100)
            {
                if (item.Position == 0)
                {
                    Add(item, wardrobe, visibleByUid);
                }
                else if (item.Position <= 59)
                {
                    Add(item, equipment, visibleByUid);
                }
            }
            else
            {
                Add(item, warehouse, visibleByUid);
            }

            return;
        }

        if (item.Position == 217)
        {
            Add(item, mythsoul, visibleByUid);
        }
        else if (item.Position >= 213 && item.Position <= 215)
        {
            Add(item, relicFuse, visibleByUid);
        }
        else if (item.Position > 0 && item.Position <= 63)
        {
            Add(item, equipment, visibleByUid);
        }
        else if (item.Position == 0)
        {
            Add(item, inventory, visibleByUid);
        }
        else if (item.Position is 211 or 212 || item.Position >= 101 && item.Position <= 135)
        {
            Add(item, runes, visibleByUid);
        }
    }

    private static void Add(
        PlayerItemSnapshotItem item,
        List<PlayerItemSnapshotItem> target,
        Dictionary<uint, PlayerItemSnapshotItem> visibleByUid)
    {
        if (visibleByUid.ContainsKey(item.Uid))
        {
            return;
        }

        target.Add(item);
        visibleByUid[item.Uid] = item;
    }

    private static bool IsLegacyRuneBagPosition(ushort position)
    {
        return position is 211 or 212;
    }

    private static void ApplyPerfection(
        ReadOnlySpan<byte> perfection,
        IReadOnlyDictionary<uint, PlayerItemSnapshotItem> visibleByUid)
    {
        var itemUid = ReadUInt32(perfection, PerfectionItemUidOffset);
        if (!visibleByUid.TryGetValue(itemUid, out var item))
        {
            return;
        }

        item.PerfectionLevel = ReadUInt32(perfection, PerfectionLevelOffset);
        item.PerfectionProgress = ReadUInt32(perfection, PerfectionProgressOffset);
        item.OwnerUid = ReadUInt32(perfection, PerfectionOwnerUidOffset);
        item.OwnerName = ReadFixedUtf8(perfection, PerfectionOwnerNameOffset, PerfectionTextLength);
        item.Signature = ReadFixedUtf8(perfection, PerfectionSignatureOffset, PerfectionTextLength);
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> value, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(value.Slice(offset, sizeof(ushort)));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> value, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(value.Slice(offset, sizeof(uint)));
    }

    private static void WriteUInt16(Span<byte> destination, int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), value);
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }

    private static void WriteInt32(Span<byte> destination, int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, sizeof(int)), value);
    }

    private static void WriteInt64(Span<byte> destination, int offset, long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(offset, sizeof(long)), value);
    }

    private static byte ReadByteEnum(ReadOnlySpan<byte> value, int offset)
    {
        return value[offset];
    }

    private static string ReadFixedUtf8(ReadOnlySpan<byte> value, int offset, int length)
    {
        var bytes = value.Slice(offset, length);
        var nullIndex = bytes.IndexOf((byte)0);
        if (nullIndex >= 0)
        {
            bytes = bytes[..nullIndex];
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static void WriteFixedUtf8(Span<byte> destination, string value)
    {
        if (value.Length == 0)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }

    private string GetCharacterSnapshotPath(long characterUid)
    {
        return Path.Combine(
            _playerItemsRoot,
            characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
    }

    private static void EnsureAvailable(
        ReadOnlySpan<byte> bytes,
        int offset,
        int length,
        string sourceFile)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new FormatException($"Player item snapshot '{sourceFile}' is truncated.");
        }
    }
}
