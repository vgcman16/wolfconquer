namespace WolfConquer.Core.World;

// Whole native Flags200..211; SetPkMode/RestorePkMode are not approximated here.
internal enum LegacyNativePkMode : byte
{
    PK = 0,
    Peace = 1,
    Team = 2,
    Capture = 3,
    Revange = 4,
    Guild = 5,
    Jiang = 6,
    CS = 7,
    Union = 11,
}

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1073, actual Capture initializer, not modern attribute projection.
    internal LegacyNativePkMode PkMode = LegacyNativePkMode.Capture;
}
