namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    private readonly record struct BookRow(uint Key, Item Row, uint ShopId, uint GoodsId, uint Times);
    private sealed record BookState(Dictionary<uint, Item> Source, BookRow[] Rows);

    private bool TryCaptureBook(out BookState? book, out string reason)
    {
        book = null;
        reason = "exchangeShopBookUnavailable";
        var source = Items;
        if (source is null) return false;
        if (source.Count > MaximumSavedRecords) { reason = "exchangeShopBookLimit"; return false; }
        var entries = source.ToArray();
        if (entries.Length > MaximumSavedRecords) { reason = "exchangeShopBookLimit"; return false; }
        var rows = new BookRow[entries.Length];
        var aliases = new HashSet<Item>(ReferenceEqualityComparer.Instance);
        reason = "exchangeShopBookInvalid";
        for (int index = 0; index < entries.Length; index++)
        {
            var pair = entries[index];
            var row = pair.Value;
            if (row is null || pair.Key != row.goods_rec_id || !aliases.Add(row)) return false;
            rows[index] = new(pair.Key, row, row.shopid, row.goods_rec_id, row.times);
        }
        if (!ReferenceEquals(source, Items)) { reason = "exchangeShopStateChanged"; return false; }
        book = new(source, rows);
        reason = string.Empty;
        return true;
    }

    private bool BookIsCurrent(BookState book)
    {
        if (!ReferenceEquals(Items, book.Source) || book.Source.Count != book.Rows.Length) return false;
        foreach (var before in book.Rows)
            if (!book.Source.TryGetValue(before.Key, out var row) || !ReferenceEquals(row, before.Row) ||
                row.shopid != before.ShopId || row.goods_rec_id != before.GoodsId || row.times != before.Times)
                return false;
        return true;
    }

    private static Dictionary<uint, Item> CopyBook(BookState book)
    {
        var staged = new Dictionary<uint, Item>(book.Source.Comparer);
        foreach (var row in book.Rows) staged.Add(row.Key, row.Row);
        return staged;
    }

    private static void ReportRefusal(string operation, string reason)
    {
        try { Console.WriteLine($"ExchangeShop {operation} refused: {reason}"); }
        catch { /* A diagnostic failure cannot alter an already classified result. */ }
    }

    private static string PreparationFailure(Exception error) =>
        "exchangeShopMaterializationFailed:" + error.GetType().Name;
}
