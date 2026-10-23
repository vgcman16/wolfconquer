using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1157/1159 and2683..2695. No clamp, expiry enforcement or send
    // is part of the property; UpdateVip is the separate genuine publication owner.
    public DateTime ExpireVip = default(DateTime);

    private byte _viplevel;

    public byte VipLevel
    {
        get
        {
            return _viplevel;
        }
        set
        {
            _viplevel = value;
        }
    }

    // Whole native Player4751..4767. Sends scalar level BEFORE the status mask.
    internal void UpdateVip(LegacyNativePacket stream)
    {
        SendUpdate(stream, VipLevel, LegacyNativeMsgUpdate.DataType.VIPLevel);
        if (VipLevel >= 6)
        {
            Owner.Send(stream.VipStatusCreate(LegacyNativeMsgVipStatus.VipFlags.FullVip));
        }
        else if (VipLevel >= 1)
        {
            Owner.Send(stream.VipStatusCreate(LegacyNativeMsgVipStatus.VipFlags.VipLevelOne));
        }
        else
        {
            Owner.Send(stream.VipStatusCreate(LegacyNativeMsgVipStatus.VipFlags.None));
        }
    }
}
