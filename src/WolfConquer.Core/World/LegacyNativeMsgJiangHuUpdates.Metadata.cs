using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// Native 2070 metadata only. Inventory/currency/activeness receiver remains held.
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal partial struct LegacyNativeMsgJiangHuUpdates
{
    public enum Action : byte { CreateStar, BuyStrength, TrainingPill }
}
