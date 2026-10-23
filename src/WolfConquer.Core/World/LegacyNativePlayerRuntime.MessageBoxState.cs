namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1003/1005/1007; these delegates are source-defined message-box state.
    public Action<LegacyNativeGameClientRuntime>? MessageOK;
    public Action<LegacyNativeGameClientRuntime>? MessageCancel;
    public DateTime StartMessageBox = default(DateTime);
}
