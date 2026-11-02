using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // DATA only: movement's two representations must agree before either is
    // edited. This does not issue a native flag or a movement publication.
    private sealed class MovementStatusAttributes
    {
        private readonly ulong[] words;
        private readonly string? statusFlags, bitVector;

        private MovementStatusAttributes(ulong[] words, string? statusFlags, string? bitVector)
        { this.words = words; this.statusFlags = statusFlags; this.bitVector = bitVector; }

        internal static MovementStatusAttributes Read(Dictionary<string, string> attributes)
        {
            bool hasStatus = LegacyCharacterAttributeMap.TryRead(attributes, "StatusFlags", out var status);
            bool hasVector = LegacyCharacterAttributeMap.TryRead(attributes, "BitVector", out var vector);
            var statusWords = hasStatus ? Parse(status) : null;
            var vectorWords = hasVector ? Parse(vector) : null;
            if (statusWords is not null && vectorWords is not null &&
                !statusWords.AsSpan().SequenceEqual(vectorWords)) throw Invalid();
            return new(statusWords ?? vectorWords ?? new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength],
                hasStatus ? status : null, hasVector ? vector : null);
        }

        internal bool Has(int flag) => flag >= 0 && flag / 32 < words.Length &&
            (words[flag / 32] & (1UL << (flag % 32))) != 0;

        internal void ClearMovementFlags(Dictionary<string, string> attributes)
        {
            // Preserve each valid representation's spelling and unrelated
            // formatting. Both aliases were checked before any DATA edit.
            if (statusFlags is not null)
                LegacyCharacterAttributeMap.Set(attributes, "StatusFlags", Clear(statusFlags));
            if (bitVector is not null)
                LegacyCharacterAttributeMap.Set(attributes, "BitVector", Clear(bitVector));
        }

        private string Clear(string raw)
        {
            var parts = raw.Split('/');
            foreach (int flag in LegacyMovementClearedFlags)
                if (Has(flag))
                {
                    int index = flag / 32;
                    ulong word = ulong.Parse(parts[index].Trim(), NumberStyles.None, CultureInfo.InvariantCulture);
                    parts[index] = (word & ~(1UL << (flag % 32))).ToString(CultureInfo.InvariantCulture);
                }
            return string.Join('/', parts);
        }

        private static ulong[] Parse(string raw)
        {
            if (raw is null) throw Invalid();
            var result = new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength];
            if (string.IsNullOrWhiteSpace(raw)) return result;
            var parts = raw.Split('/', StringSplitOptions.TrimEntries);
            int count = parts.Length;
            if (parts[^1].Length == 0) count--;
            if (count > result.Length) throw Invalid();
            for (int index = 0; index < count; index++)
                if (!ulong.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out result[index]))
                    throw Invalid();
            return result;
        }

        private static InvalidDataException Invalid() => new("movementStatusAliasesMalformedOrConflicting");
    }

    private static bool HasMovementActivePick(Dictionary<string, string> attributes)
    {
        if (!LegacyCharacterAttributeMap.TryRead(attributes, "ActivePick", out var raw)) return false;
        return raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value != 0;
    }
}
