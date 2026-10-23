namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Actual native physical slots391/993/1065; no constructor-owned birth invented.
    internal LegacyNativeTime32 FineRaneSave;
    internal LegacyNativeClientTransformRuntime? TransformInfo = null;
    public bool IsAsasin = false;

    // Whole native Player1517; actual TransformationID getter/setter already exists.
    public bool OnTransform => TransformationID != 0;
}
