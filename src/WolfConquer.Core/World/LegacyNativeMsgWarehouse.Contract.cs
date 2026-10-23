namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgWarehouse
{
    public enum DepositActionID : ushort
    {
        Show=2560,DepositItem=2561,WithdrawItem=2562,
        Show_WH_House=5120,DepositItem_WH_House=5121,WithdrawItem_WH_House=5122,
        ShashShow=7680,ShashDepositItem=7681,ShashWithdrawItem=7682,
        ShowInventorySash=10240,InventorySashDepositItem=10241,InventorySashWithdrawItem=10242
    }
}
