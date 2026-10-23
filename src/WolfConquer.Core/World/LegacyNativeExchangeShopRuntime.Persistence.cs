using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    // Engineering persisted-book bounds, not wire/shop catalog constants.
    internal const int MaximumSavedRecords = 4096;
    internal const int MaximumSavedCharacters = 1024 * 1024;

    public override string ToString()
    {
        lock (bookSyncRoot)
        {
            if (!TryCaptureBook(out var book, out var reason)) throw new InvalidDataException(reason);
            var line = new LegacyNativeDbWriteLine('/').Add(book!.Rows.Length);
            foreach (var row in book.Rows) line.Add(row.ShopId).Add(row.GoodsId).Add(row.Times);
            string saved = line.Close();
            if (saved.Length > MaximumSavedCharacters) throw new InvalidDataException("exchangeShopSavedStateTooLarge");
            return saved;
        }
    }

    public void Load(string? Line)
    {
        if (!TryLoad(Line, out var reason)) throw new FormatException("Invalid ExchangeShop saved state: " + reason);
    }

    public bool TryLoad(string? Line, out string reason)
    {
        lock (bookSyncRoot)
        {
            try
            {
                if (!TryCaptureBook(out var book, out reason) || !TryParseBook(Line, out var incoming, out reason)) return false;
                if (incoming!.Length == 0) { reason = string.Empty; return true; }
                if (incoming.Length > MaximumSavedRecords - book!.Rows.Length)
                { reason = "exchangeShopBookLimit"; return false; }
                var staged = CopyBook(book!);
                foreach (var row in incoming)
                {
                    if (!staged.TryAdd(row.goods_rec_id, row))
                    { reason = "exchangeShopSavedDuplicateRecord"; return false; }
                }
                if (!BookIsCurrent(book!)) { reason = "exchangeShopStateChanged"; return false; }
                Items = staged;
                reason = string.Empty;
                return true;
            }
            catch (Exception error) { reason = PreparationFailure(error); return false; }
        }
    }
}
