namespace WolfConquer.Core.World;

// Native metadata only. Economic 2082 receiver remains held with Inventory/equipment graph.
internal partial class LegacyNativeMsgJiangHuInfo
{
    public enum JiangMode : byte
    {
        IconBar = 0, CreateJiang = 1, InfoStauts = 7, UpdateTalent = 5, ViewJiang = 9,
        RestoreStar = 10, UpdateStar = 11, OpenStage = 12, UpdateDateTime = 13,
        SetName = 14, ProtectionPill = 16,
    }
}
