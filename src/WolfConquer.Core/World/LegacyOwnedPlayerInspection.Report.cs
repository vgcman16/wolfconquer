namespace WolfConquer.Core.World;

public sealed record LegacyOwnedPlayerCharacter(long Uid, long Map, long X, long Y, long Body,
    long Class, long Level, long Gold, long ConquerPoints,
    long? Hitpoints = null, long? MinMana = null, long? MaxMana = null);

public sealed record LegacyOwnedPlayerItem(string Container, uint Uid, uint ItemId, ushort Position,
    uint WarehouseId, byte Plus, ushort StackSize, ushort Durability, ushort MaximumDurability,
    byte SocketOne, byte SocketTwo, byte Bound, byte Locked);

public sealed record LegacyOwnedPlayerMail(uint MailId, uint AttachedItemUid, bool AttachmentPresentInInbox,
    ulong Gold, uint ConquerPoints, uint PackageFlag);

public sealed record LegacyOwnedPlayerFileEvidence(string Kind, bool Present, long Length,
    DateTime? CreationUtc, DateTime? LastWriteUtc, bool BytesAndMetadataUnchanged, string? ContentSha256 = null);

public sealed record LegacyOwnedPlayerInspectionReport(int SchemaVersion, Guid Domain, uint AccountId,
    string? ProtocolProfile, string ProfileEvidence, LegacyOwnedPlayerCharacter Character,
    int InventoryCount, int EquipmentCount, int WarehouseCount, int PhysicalItemCount,
    int SpellCount, int ProficiencyCount, int ActiveMailCount,
    LegacyOwnedPlayerItem[] Items, LegacyOwnedPlayerMail[] Mails, bool SummariesTruncated,
    DateTimeOffset StartedUtc, DateTimeOffset FinishedUtc,
    string StabilityBound, bool LiveStoreSynchronized, LegacyOwnedPlayerFileEvidence[] Files);
