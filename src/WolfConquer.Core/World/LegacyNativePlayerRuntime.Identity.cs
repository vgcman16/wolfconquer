namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player987: plain saved identity field, no setter side effects.
    public uint SpouseUID = 0u;

    // Native Player1093/1101: plain fields, no setter/send behavior.
    public string Name = "";

    public string Spouse = "None";
}
