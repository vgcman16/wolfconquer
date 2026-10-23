using System.Globalization;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    private static bool TryParseBook(string? line, out Item[]? rows, out string reason)
    {
        rows = null;
        if (!TryReadSavedBookHeader(line, out var fields, out var count, out reason)) return false;
        return TryMaterializeSavedBookRows(fields!, count, out rows, out reason);
    }

    private static bool TryReadSavedBookHeader(string? line, out string[]? fields,
        out uint count, out string reason)
    {
        fields = null;
        count = 0;
        reason = "exchangeShopSavedStateInvalid";
        if (line is null || line.Length == 0) return false;
        if (line.Length > MaximumSavedCharacters)
        { reason = "exchangeShopSavedStateTooLarge"; return false; }
        fields = line.Split('/');
        int length = fields.Length;
        if (fields[^1].Length == 0) length--;
        if (length == 0 || !ReadSavedUInt(fields[0], out count)) return false;
        if (count > MaximumSavedRecords)
        { reason = "exchangeShopSavedRecordLimit"; return false; }
        if (length != 1 + ((int)count * 3)) return false;
        reason = string.Empty;
        return true;
    }

    private static bool TryMaterializeSavedBookRows(string[] fields, uint count,
        out Item[]? rows, out string reason)
    {
        rows = null;
        reason = "exchangeShopSavedStateInvalid";
        var staged = new Item[(int)count];
        var ids = new HashSet<uint>();
        for (int index = 0; index < staged.Length; index++)
        {
            int offset = 1 + (index * 3);
            if (!ReadSavedUInt(fields[offset], out uint shop) ||
                !ReadSavedUInt(fields[offset + 1], out uint goods) ||
                !ReadSavedUInt(fields[offset + 2], out uint times)) return false;
            if (!ids.Add(goods)) { reason = "exchangeShopSavedDuplicateRecord"; return false; }
            staged[index] = new Item { shopid = shop, goods_rec_id = goods, times = times };
        }
        rows = staged;
        reason = string.Empty;
        return true;
    }

    private static bool ReadSavedUInt(string text, out uint value) =>
        uint.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
}
