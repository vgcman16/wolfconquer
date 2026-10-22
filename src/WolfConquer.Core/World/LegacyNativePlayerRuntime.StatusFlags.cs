namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Complete native Player3720..3723 and3736..3749 methods. AddFlag lives in
    // its own partial; RemoveFlag/ClearFlags still need experience/equipment owners.
    internal bool UpdateFlag(LegacyNativeMsgUpdate.Flags Flag, int Secounds, bool SetNewTimer, int MaxTime)
    {
        return BitVector.UpdateFlag((int)Flag, Secounds, SetNewTimer, MaxTime);
    }

    internal bool ContainFlag(LegacyNativeMsgUpdate.Flags Flag)
    {
        return BitVector.ContainFlag((int)Flag);
    }

    internal bool CheckInvokeFlag(LegacyNativeMsgUpdate.Flags Flag, DateTime timer32)
    {
        return BitVector.CheckInvoke((int)Flag, timer32);
    }

    internal void UpdateFlagOffset()
    {
        SendUpdate(BitVector.bits, LegacyNativeMsgUpdate.DataType.StatusFlag, scren: true);
    }
}
