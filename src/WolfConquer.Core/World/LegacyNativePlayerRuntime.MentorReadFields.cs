namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1151 physical backing and complete getter2655..2658 only.
    // Whole setter2659..2681 remains held with full native RemoveFlag/Team graph.
    private ushort _pkpoints;
    public ushort PKPoints
    {
        get { return _pkpoints; }
    }

    // Native Player1281 plain string field, not an imported association projection.
    public string Signature = "";
}
