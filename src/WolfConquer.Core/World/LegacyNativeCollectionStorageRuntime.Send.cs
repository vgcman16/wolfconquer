namespace WolfConquer.Core.World;

internal partial class LegacyNativeCollectionStorageRuntime
{
    public void Load()
    {
        LegacyNativeMsgCollectionStorage msgCollectionStorage = new LegacyNativeMsgCollectionStorage();
        msgCollectionStorage.Type = LegacyNativeMsgCollectionStorage.Types.STORAGE_LOAD;
        msgCollectionStorage.Identity = user.Player.CollectionID;
        msgCollectionStorage.items = new List<LegacyNativeMsgCollectionStorage.item>();
        foreach (Scrap value in Scraps.Values)
        {
            msgCollectionStorage.pScrap.Add(new LegacyNativeMsgCollectionStorage.Scrap
            {
                Type = value.Type,
                Amount = value.Amount
            });
        }
        foreach (Item value2 in Items.Values)
        {
            msgCollectionStorage.items.Add(new LegacyNativeMsgCollectionStorage.item
            {
                Activate = value2.Activate,
                Amount = value2.Amount,
                RemainingTime = LegacyNativeMsgCollectionStorage.Timestamp(value2.RemainingTime),
                Type = value2.Type,
                F5 = value2.F5
            });
        }
        user.Send(msgCollectionStorage);
    }
}
