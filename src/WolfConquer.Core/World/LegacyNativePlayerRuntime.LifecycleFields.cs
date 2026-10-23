namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1051/1055/1195..1203/1213. Direct fields, no host birth or scheduler.
    internal LegacyNativeMsgUpdate.Flags UseXPSpell;
    public ushort KillCounter;
    public DateTime OnlineTrainingTime = default;
    public DateTime ReceivePointsOnlineTraining = default;
    public DateTime HeavenBlessTime = default;
    public int HeavenBlessing = 0;
    public uint OnlineTrainingPoints = 0u;
    public uint ExpProtection = 0u;
}
