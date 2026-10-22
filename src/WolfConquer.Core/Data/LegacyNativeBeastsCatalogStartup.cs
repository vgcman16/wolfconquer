using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Explicit JSON/raw compatible-row bridge, not native text-file Load or Beast owner birth.
internal static class LegacyNativeBeastsCatalogStartup
{
    internal static void AddRows(IReadOnlyList<BeastAttribute> rows)
    {
        foreach (var row in rows)
        {
            if (row is null || row.Parameters is null || row.Fields is null)
                throw new FormatException("Beast startup row has missing raw/typed metadata.");

            var parsed = BeastParser.ParseRow(row.SourceFile, row.LineNumber, row.RawLine);
            if (row.Level != parsed.Level ||
                !row.Parameters.SequenceEqual(parsed.Parameters) ||
                !row.Fields.SequenceEqual(parsed.Fields, StringComparer.Ordinal))
                throw new FormatException("Beast startup raw/typed row mismatch.");

            var native = LegacyNativeBeastsAtrribute.ParseRow(row.RawLine);
            uint[] nativeFields =
            [
                native.RequiredPoints, native.HPAdd, native.PAttack, native.MAttack,
                native.PDefense, native.MDefense, native.FinalPDmgDealt, native.FinalMDmgDealt,
                native.FinalPDmgReceived, native.FinalMDmgReceived,
            ];
            if (native.Level != parsed.Level || native.YellowRuneSlots != parsed.Parameters[15])
                throw new FormatException("Beast startup native byte mapping mismatch.");
            for (var i = 0; i < nativeFields.Length; i++)
                if ((long)nativeFields[i] != parsed.Parameters[i])
                    throw new FormatException("Beast startup native unsigned mapping mismatch.");

            LegacyNativeBeastsAtrribute.Attributes.Add(native.Level, native);
        }
    }
}
