namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    public void GetRandomEx(uint shopid, uint Count)
    {
        if (!TryGetRandomEx(shopid, Count, out var reason)) ReportRefusal(nameof(GetRandomEx), reason);
    }

    public bool TryGetRandomEx(uint shopid, uint Count, out string reason)
    {
        lock (bookSyncRoot)
        {
            try
            {
                if (!TryCaptureBook(out var book, out reason)) return false;
                return TryRefresh(book!, shopid, Count, resetAll: false, out reason);
            }
            catch (Exception error) { reason = PreparationFailure(error); return false; }
        }
    }

    public void Reset()
    {
        if (!TryReset(out var reason)) ReportRefusal(nameof(Reset), reason);
    }

    public bool TryReset(out string reason)
    {
        lock (bookSyncRoot)
        {
            try
            {
                if (!TryCaptureBook(out var book, out reason)) return false;
                return TryRefresh(book!, 168u, 8u, resetAll: true, out reason);
            }
            catch (Exception error) { reason = PreparationFailure(error); return false; }
        }
    }

    public void Loading()
    {
        if (!TryLoading(out var reason)) ReportRefusal(nameof(Loading), reason);
    }

    public bool TryLoading(out string reason)
    {
        lock (bookSyncRoot)
        {
            try
            {
                if (!TryCaptureBook(out var book, out reason)) return false;
                // Retain v2's physical goods168 (NOT shop/key) single-scan gate.
                bool hasPhysicalGoods168 = false;
                foreach (var row in book!.Rows) hasPhysicalGoods168 |= row.GoodsId == 168;
                if (hasPhysicalGoods168) { reason = string.Empty; return true; }
                return TryRefresh(book, 168u, 8u, resetAll: false, out reason);
            }
            catch (Exception error) { reason = PreparationFailure(error); return false; }
        }
    }

    private bool TryRefresh(BookState book, uint shopid, uint count, bool resetAll, out string reason)
    {
        if (!TryCaptureExCatalog(out var catalog, out reason)) return false;
        if (HasForeignExCollision(book, catalog!))
        { reason = "exchangeShopForeignStockCollision"; return false; }
        var candidates = catalog!.Rows.Where(row => row.ShopId == shopid).ToArray();
        if (count > (uint)candidates.Length)
        { reason = "exchangeShopInsufficientCandidates"; return false; }
        var staged = PrepareRetainedRefreshStock(book, candidates, resetAll, out int removed);
        if ((long)book.Rows.Length - removed + count > MaximumSavedRecords)
        { reason = "exchangeShopBookLimit"; return false; }
        if (!TryPrepareRefreshSelection(staged, candidates, count, out reason)) return false;
        if (!BookIsCurrent(book) || !ExCatalogIsCurrent(catalog))
        { reason = "exchangeShopStateChanged"; return false; }
        // A valid zero count intentionally retires target IDs without RNG. A genuine
        // no-change zero request leaves the existing dictionary reference intact.
        if (removed != 0 || count != 0) Items = staged;
        reason = string.Empty;
        return true;
    }

    private static Dictionary<uint, Item> PrepareRetainedRefreshStock(BookState book,
        ExGoodsRow[] candidates, bool resetAll, out int removed)
    {
        var retiredIds = new HashSet<uint>(candidates.Select(row => row.Id));
        var staged = new Dictionary<uint, Item>(book.Source.Comparer);
        removed = 0;
        foreach (var existing in book.Rows)
        {
            if (resetAll || retiredIds.Contains(existing.GoodsId)) { removed++; continue; }
            staged.Add(existing.Key, existing.Row);
        }
        return staged;
    }

    private static bool TryPrepareRefreshSelection(Dictionary<uint, Item> staged, ExGoodsRow[] candidates,
        uint count, out string reason)
    {
        reason = string.Empty;
        if (count == 0) return true;
        var random = LegacyNativePoolRandom.GetRandom;
        if (random is null) { reason = "exchangeShopRandomUnavailable"; return false; }
        // Uniform ordered sampling without replacement, like the valid native
        // rejection loop, but exactly count bounded draws. Weight was never used.
        for (int selected = 0; selected < (int)count; selected++)
        {
            int index = random.Next(selected, candidates.Length);
            (candidates[selected], candidates[index]) = (candidates[index], candidates[selected]);
            var goods = candidates[selected];
            staged.Add(goods.Id, new Item { shopid = goods.ShopId, goods_rec_id = goods.Id, times = 0u });
        }
        return true;
    }
}
