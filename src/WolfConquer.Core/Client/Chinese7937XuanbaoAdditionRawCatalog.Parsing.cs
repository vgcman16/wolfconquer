using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Core.Client;

public sealed partial class Chinese7937XuanbaoAdditionRawCatalog
{
    public static bool TryParsePacked(int declaredVersion, ReadOnlySpan<byte> packed,
        ReadOnlySpan<byte> expectedPackedSha256, out Chinese7937XuanbaoAdditionRawCatalog? catalog,
        out Chinese7937XuanbaoAdditionRawFailure failure)
    {
        catalog = null;
        if (declaredVersion != 7937) { failure = Chinese7937XuanbaoAdditionRawFailure.UnsupportedVersion; return false; }
        if (packed.Length is <= 0 or > MaximumBytes) { failure = Chinese7937XuanbaoAdditionRawFailure.InputBounds; return false; }
        if (expectedPackedSha256.Length != 32) { failure = Chinese7937XuanbaoAdditionRawFailure.PinRequired; return false; }
        var pin = expectedPackedSha256.ToArray(); var captured = packed.ToArray();
        var packedHash = SHA256.HashData(captured);
        if (!CryptographicOperations.FixedTimeEquals(pin, packedHash))
        { failure = Chinese7937XuanbaoAdditionRawFailure.PinMismatch; return false; }
        // Exactly the existing stateless profile; no key discovery, format
        // fallback or inference from the legacy server's typed column names.
        var decoded = ClientItemCatalogCodec.Decode(captured);
        if (!ClientItemCatalogCodec.Encode(decoded).AsSpan().SequenceEqual(captured))
        { failure = Chinese7937XuanbaoAdditionRawFailure.InverseMismatch; return false; }
        if (!TryReadRows(decoded, out var rows, out failure)) return false;
        var prepared = new Chinese7937XuanbaoAdditionRawCatalog(packedHash, SHA256.HashData(decoded), rows);
        if (!prepared.ReconstructDecodedBytes().AsSpan().SequenceEqual(decoded))
        { failure = Chinese7937XuanbaoAdditionRawFailure.UnsupportedSyntax; return false; }
        catalog = prepared; failure = Chinese7937XuanbaoAdditionRawFailure.None; return true;
    }

    private static bool TryReadRows(byte[] decoded, out Chinese7937XuanbaoAdditionRawRow[] rows,
        out Chinese7937XuanbaoAdditionRawFailure failure)
    {
        rows = []; failure = Chinese7937XuanbaoAdditionRawFailure.UnsupportedSyntax;
        if (decoded.Any(value => value > 0x7f || value == 0) || !decoded.AsSpan().EndsWith("\r\n"u8)) return false;
        var text = Encoding.ASCII.GetString(decoded);
        var lines = text.Split("\r\n", StringSplitOptions.None);
        if (lines.Length - 1 > MaximumRows) { failure = Chinese7937XuanbaoAdditionRawFailure.RowBounds; return false; }
        var captured = new Chinese7937XuanbaoAdditionRawRow[lines.Length - 1];
        HashSet<uint> seen = [];
        for (var index = 0; index < captured.Length; index++)
        {
            var raw = lines[index]; var fields = raw.Split("@@", StringSplitOptions.None);
            if (raw.Length == 0 || fields.Length != 6 || fields[^1].Length != 0) return false;
            var values = new uint[5];
            for (var column = 0; column < values.Length; column++)
            {
                if (fields[column].Length == 0 || !fields[column].All(char.IsAsciiDigit)) return false;
                // UInt32 is an explicit finite offline capture guard, not a
                // claim about an unidentified native Addition column width.
                if (!uint.TryParse(fields[column], NumberStyles.None, CultureInfo.InvariantCulture, out values[column]))
                { failure = Chinese7937XuanbaoAdditionRawFailure.NumericWidth; return false; }
            }
            if (!seen.Add(values[0])) { failure = Chinese7937XuanbaoAdditionRawFailure.DuplicateRowId; return false; }
            captured[index] = new(index + 1, raw, fields, values);
        }
        if (captured.Length == 0) return false;
        rows = captured; failure = Chinese7937XuanbaoAdditionRawFailure.None; return true;
    }
}
