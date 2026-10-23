using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;

internal partial class LegacyNativeVendorRuntime
{
    // Whole native Vendor69..86. Clear/publication/fault order is not normalized.
    public void StopVending(LegacyNativePacket stream)
    {
        if (InVending)
        {
            LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
            {
                ObjId = VendorUID,
                Type = 135
            };
            Owner.Player.View.SendView(stream.ActionCreate(pQuery), me: true);
            Items.Clear();
            Owner.Map!.View.LeaveMap((ILegacyNativeMapObj)VendorNpc!);
            InVending = false;
            Owner.MyVendor = null;
        }
    }
}
