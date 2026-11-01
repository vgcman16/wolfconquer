namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Genuine plain native Player1121/1123/1143 storage, all implicit zero.
    // No Level/Class setter, loader or host caller is installed by these fields.
    public uint FirstClass;

    public uint SecoundeClass;

    private ulong experience;
    public ulong Experience
    {
        get => experience;
        set
        {
            RequireUnboundActorScalarMutation();
            lock (Owner.SpellAdmissionSyncRoot)
            lock (SpellAdmissionSyncRoot)
            {
                RequireUnboundActorScalarMutation();
                experience = value;
            }
        }
    }
}
