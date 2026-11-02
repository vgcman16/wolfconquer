namespace WolfConquer.Core.World;

// Immutable retained outcome DATA, never a producer, owner, entropy source or
// UID reservation. Recovery validates it against the actual journal evidence.
internal sealed class LegacyRelicFuseRollData
{
    private LegacyRelicFuseRollData(uint firstUid, uint secondUid, uint itemId, byte bound,
        ushort durability, uint attribute1, uint attribute2, uint attribute3, uint attribute4, uint attribute5)
    {
        FirstUid = firstUid; SecondUid = secondUid; ItemId = itemId; Bound = bound;
        Durability = durability; Attribute1 = attribute1; Attribute2 = attribute2;
        Attribute3 = attribute3; Attribute4 = attribute4; Attribute5 = attribute5;
    }

    internal uint FirstUid { get; }
    internal uint SecondUid { get; }
    internal uint ItemId { get; }
    internal byte Bound { get; }
    internal ushort Durability { get; }
    internal uint Attribute1 { get; }
    internal uint Attribute2 { get; }
    internal uint Attribute3 { get; }
    internal uint Attribute4 { get; }
    internal uint Attribute5 { get; }

    // Pure structural reconstruction for the real evidence/journal consumer.
    // Passing this factory does not establish that a random draw ever occurred.
    internal static bool TryFromRecorded(uint firstUid, uint secondUid, uint itemId, byte bound,
        ushort durability, uint attribute1, uint attribute2, uint attribute3, uint attribute4, uint attribute5,
        out LegacyRelicFuseRollData? result)
    {
        result = null;
        if (firstUid == 0 || secondUid == 0 || firstUid == secondUid ||
            itemId is < 4_100_001 or > 4_100_005 || bound > 1 ||
            durability is < 3_000 or > 10_000 || durability % 100 != 0 ||
            !ValidWord(attribute1) || !ValidWord(attribute2) || !ValidWord(attribute3) ||
            !ValidWord(attribute4) || !ValidWord(attribute5)) return false;
        result = new(firstUid, secondUid, itemId, bound, durability,
            attribute1, attribute2, attribute3, attribute4, attribute5);
        return true;
    }

    private static bool ValidWord(uint raw)
    {
        var type = raw % 100;
        var marker = raw % 1000 - type;
        return type is >= 1 and <= 14 && raw / 1000 <= ushort.MaxValue && marker is 0 or 100 &&
            (type != (uint)LegacyChiAttributeType.LukyStrike || raw == 100_114);
    }
}
