namespace WolfConquer.Core.World;

public sealed record LegacyFineRainEquipmentAdmission(
    long OwnerUid,
    int StampMilliseconds = 0)
{
    public static LegacyFineRainEquipmentAdmission ForOwner(WorldSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.FineRainEquipmentAdmission is { } admission &&
            admission.OwnerUid == session.Character.Uid
            ? admission
            : new LegacyFineRainEquipmentAdmission(session.Character.Uid);
    }
}
