namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient277: physical nullable field. Native DataCore's owner birth
    // remains a separate phase; ordinary client construction does not initialize it.
    internal LegacyNativeProficiencyStorage? MyProfs;
}
