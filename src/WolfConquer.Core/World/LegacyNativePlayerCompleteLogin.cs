namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1039: birth starts false. Data132's completed startup-send
    // producer owns the write; session copies, adoption and PIN state do not.
    public bool CompleteLogin = false;
}
