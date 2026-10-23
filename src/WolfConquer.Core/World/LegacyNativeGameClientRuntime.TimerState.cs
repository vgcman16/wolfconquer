namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient65/67 implicit-null physical slots only. The complete ctor
    // still owns TimerSyncRoot=new object() at its native1411 slot.
    public IDisposable[]? TimerSubscriptions;
    public object? TimerSyncRoot;
}
