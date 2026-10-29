using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial record LegacyMailboxModePacket
{
    private static bool HasKnownItemExtent(LegacyMailboxModeType type, uint count,
        ReadOnlySpan<byte> itemRegion, ConquerProtocolProfile profile)
    {
        if (count > (uint)(itemRegion.Length / sizeof(ulong))) return false;
        // Count is now bounded by the complete captured frame; this product fits int.
        var trailer = itemRegion[(checked((int)count) * sizeof(ulong))..];
        if (trailer.IsEmpty) return true; // Existing canonical form in either profile.
        if (profile != ConquerProtocolProfile.VisibleDx8D13) return false;
        var expected = type switch
        {
            LegacyMailboxModeType.Content when count == 0 => 8,
            LegacyMailboxModeType.ClaimAll => 7,
            _ => 0,
        };
        if (expected == 0 || trailer.Length != expected) return false;
        foreach (var value in trailer) if (value != 0) return false;
        return true;
    }
}
