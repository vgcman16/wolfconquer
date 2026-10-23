using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Parent intake adds only `partial` to the existing genuine sealed declaration.
internal sealed partial class LegacyNativeWarehouseRuntime
{
    public static bool IsWarehouse(LegacyNativeNpcID ID)
    {
        return ID==LegacyNativeNpcID.WHTwin || ID==LegacyNativeNpcID.wHPheonix || ID==LegacyNativeNpcID.WHMarket ||
            ID==LegacyNativeNpcID.WHBird || ID==LegacyNativeNpcID.WHDesert || ID==LegacyNativeNpcID.WHApe ||
            ID==LegacyNativeNpcID.PokerWarehouseman || ID==LegacyNativeNpcID.WHStone || ID==(LegacyNativeNpcID)65535u;
    }

    // Whole native Warehouse.Show. The initial IsShow mutation and all full
    // batching/extra/lock branches are retained, including mutation-before-fault.
    public void Show(uint NpcID, LegacyNativeMsgWarehouse.DepositActionID Action, LegacyNativePacket stream)
    {
        if (!ClientItems.ContainsKey(NpcID) || IsShow.Contains(NpcID)) return;
        IsShow.Add(NpcID);
        Dictionary<int,List<LegacyNativeGameItemRuntime>> dictionary = new Dictionary<int,List<LegacyNativeGameItemRuntime>>();
        dictionary.Add(0,new List<LegacyNativeGameItemRuntime>());
        int num=0;
        LegacyNativeGameItemRuntime[] array=ClientItems[NpcID].Values.ToArray();
        for (uint num2=0u;num2<array.Length;num2++)
        {
            if (num2%4==0 && num2!=0)
            {
                num++; dictionary.Add(num,new List<LegacyNativeGameItemRuntime>());
            }
            dictionary[num].Add(array[num2]);
        }
        foreach (List<LegacyNativeGameItemRuntime> value in dictionary.Values)
        {
            LegacyNativeItemExtra msgItemExtra=new LegacyNativeItemExtra();
            stream.WarehouseCreate(NpcID,Action,0u,(NpcID==User!.Player.UID)?User.Player.InventorySashCount:WHMaxSpace(),value.Count);
            foreach (LegacyNativeGameItemRuntime item in value)
            {
                stream.AddItemWarehouse(item);
                if (item.Refinary.InLife)
                {
                    item.Refinary.Typ=LegacyNativeItemExtra.Typing.RefinaryAdding;
                    if (item.Refinary.EffectDuration==0) item.Refinary.Typ=LegacyNativeItemExtra.Typing.PermanentRefinery;
                    msgItemExtra.Refinerys.Add(item.Refinary);
                }
                if (item.Purification.InLife)
                {
                    item.Purification.Typ=LegacyNativeItemExtra.Typing.PurificationAdding;
                    msgItemExtra.Purifications.Add(item.Purification);
                }
            }
            User!.Send(stream.FinalizeWarehouse());
            foreach (LegacyNativeGameItemRuntime item2 in value) item2.SendItemLocked(User,stream);
            if (msgItemExtra.Refinerys.Count!=0 || msgItemExtra.Purifications.Count!=0)
                User.Send(msgItemExtra.CreateArray(stream));
        }
    }
}
