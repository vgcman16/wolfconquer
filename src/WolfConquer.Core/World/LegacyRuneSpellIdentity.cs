namespace WolfConquer.Core.World;

internal enum LegacyRuneMappingSemantics { LegacyDisplay, RawSelf }

// Raw item metadata identifies a passive Rune. Its adjusted display ID remains
// a wire identity and must never be used as a learned-spell ownership test.
internal static class LegacyRuneSpellIdentity
{
    internal static bool IsRawSelf(uint itemId, uint encoded) => encoded == itemId &&
        itemId / 10_000 is >= 401 and <= 403;

    internal static bool IsNoSkill(uint itemId, uint encoded, LegacyRuneMappingSemantics semantics) => encoded == 0 ||
        (semantics == LegacyRuneMappingSemantics.RawSelf ? IsRawSelf(itemId, encoded) :
            LegacyGameItemPacket.CreateRuneSpellOverride(itemId, encoded)?.SpellId == itemId);
}
