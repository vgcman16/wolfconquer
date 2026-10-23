namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    // These counters do not debit currency, grant inventory, authorize a purchase or
    // make the held purchase receiver transactional. They join the same book gate.
    public bool AddItemEx(uint shopid, uint goods_rec_id, uint times)
    {
        lock (bookSyncRoot)
        {
            try
            {
                if (!TryPrepareExCounterUpdate(shopid, goods_rec_id, times, out var book, out var catalog, out var staged)) return false;
                if (!BookIsCurrent(book!) || !ExCatalogIsCurrent(catalog!)) return false;
                if (staged is not null) Items = staged;
                return true;
            }
            catch (Exception error) { ReportRefusal(nameof(AddItemEx), PreparationFailure(error)); return false; }
        }
    }

    public bool AddItem(uint shopid, uint goods_rec_id, uint times)
    {
        lock (bookSyncRoot)
        {
            try
            {
                if (!TryPrepareGoodsCounterUpdate(shopid, goods_rec_id, times, out var book, out var catalog, out var staged)) return false;
                if (!BookIsCurrent(book!) || !GoodsCatalogIsCurrent(catalog!)) return false;
                if (staged is not null) Items = staged;
                return true;
            }
            catch (Exception error) { ReportRefusal(nameof(AddItem), PreparationFailure(error)); return false; }
        }
    }

    private bool TryPrepareExCounterUpdate(uint shopid, uint goodsId, uint times, out BookState? book,
        out ExCatalogState? catalog, out Dictionary<uint, Item>? staged)
    {
        catalog = null;
        staged = null;
        if (!TryCaptureBook(out book, out _) || !TryCaptureExCatalog(out catalog, out _) ||
            HasForeignExCollision(book!, catalog!)) return false;
        var goods = catalog!.Rows.FirstOrDefault(row => row.Id == goodsId);
        if (goods.Row is null || goods.ShopId != shopid ||
            !TryPrepareCounter(book!, shopid, goodsId, times, goods.Amount, out staged)) return false;
        return true;
    }

    private bool TryPrepareGoodsCounterUpdate(uint shopid, uint goodsId, uint times, out BookState? book,
        out GoodsCatalogState? catalog, out Dictionary<uint, Item>? staged)
    {
        catalog = null;
        staged = null;
        if (!TryCaptureBook(out book, out _) || !TryCaptureGoodsCatalog(out catalog)) return false;
        var goods = catalog!.Rows.FirstOrDefault(row => row.Id == goodsId);
        if (goods.Row is null || goods.ShopId != shopid ||
            !TryPrepareCounter(book!, shopid, goodsId, times, goods.Amount, out staged)) return false;
        return true;
    }

    private static bool TryPrepareCounter(BookState book, uint shopid, uint goodsId, uint increment,
        uint maximum, out Dictionary<uint, Item>? staged)
    {
        staged = null;
        var previous = book.Rows.FirstOrDefault(row => row.GoodsId == goodsId);
        if (previous.Row is not null && previous.ShopId != shopid) return false;
        ulong total = (ulong)previous.Times + increment;
        if (total > uint.MaxValue || (maximum != 0 && total > maximum)) return false;
        if (previous.Row is not null && increment == 0) return true;
        if (previous.Row is null && book.Rows.Length >= MaximumSavedRecords) return false;
        staged = CopyBook(book);
        staged[goodsId] = new Item { shopid = shopid, goods_rec_id = goodsId, times = (uint)total };
        return true;
    }
}
