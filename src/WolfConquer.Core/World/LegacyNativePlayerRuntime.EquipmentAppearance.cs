namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Genuine plain native fields omitted from earlier appearance storage; no spawn/loader adoption.
    public uint SpecialGarment = 0u; // Player801
    public uint HeadId = 0u; // Player1221
    public uint LeftWeaponId = 0u;
    public uint RightWeaponId = 0u;
    public uint SteedId = 0u;
    public ushort ColorArmor = 0;
    public ushort ColorShield = 0;
    public ushort ColorHelment = 0;
    public uint SteedPlus = 0u;
    public uint SteedColor = 0u;
    public uint HeadSoul = 0u;
    public uint ArmorSoul = 0u;
    public uint LeftWeapsonSoul = 0u;
    public uint RightWeapsonSoul = 0u;
    public uint WingProgress = 0u;
    public uint Bottle = 0u; // Player1263

    // Complete native Player4810..4816. Existing genuine appearance fields are reused.
    public void ClearItemsSpawn()
    {
        HeadId = (GarmentId = (WingId = (WingProgress = (ArmorId = (LeftWeaponId = (RightWeaponId = (LeftWeaponAccessoryId = (RightWeaponAccessoryId = (SteedId = (MountArmorId = (Bottle = 0u)))))))))));
        ColorArmor = (ColorShield = (ColorHelment = 0));
        SteedPlus = (SteedColor = (WingPlus = 0));
        HeadSoul = (ArmorSoul = (LeftWeapsonSoul = (RightWeapsonSoul = 0u)));
    }
}
