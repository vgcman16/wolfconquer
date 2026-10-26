using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyOrdinaryAdmissionSpellImages
{
    internal static byte[] Project(CharacterSnapshot character, byte[]? before)
    {
        var rows = before is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(before);
        if (LegacyClassOwnedSpellGrantProfile.GetSpellIds(character).Count == 0 || rows.ContainsSpell(1110))
            return before?.ToArray() ?? PlayerSpellSnapshotLoader.ToLegacyBytes(rows);
        // Preserve each original twelve-byte record, including its opaque final
        // byte. Re-encoding modeled rows would silently clear that saved state.
        var old = before ?? new byte[sizeof(int)];
        var output = new byte[checked(old.Length + PlayerSpellSnapshotLoader.LegacySpellSize)];
        if (output.Length > 16 * 1024 * 1024) throw new InvalidDataException("Admission spell image is too large.");
        old.CopyTo(output, 0);
        BinaryPrimitives.WriteInt32LittleEndian(output, checked(rows.Spells.Count + 1));
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(old.Length), LegacyClassOwnedSpellGrantProfile.CycloneSpellId);
        return output;
    }
}
