using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgActivityTasks
{
    public enum Action : byte { InitializeList = 3 }
}

[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgActivityRewardsInfo
{
    public enum Action : byte { InitializeList = 1 }
}
