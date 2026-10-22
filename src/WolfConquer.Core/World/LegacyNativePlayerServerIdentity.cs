namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player629: LoadMap1044 assigns ushort(MyServerInfo.ID) before
    // its first ServerVer send. Player birth, Store and copies do not assign it.
    public ushort ServerID = 0;
}
