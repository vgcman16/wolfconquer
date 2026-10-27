namespace WolfConquer.Core.Data;

/// <summary>Appearance captured in a saved prestige row, without live player ownership.</summary>
public sealed record PrestigeRankingAppearance(
    uint Mesh,
    uint HairStyle,
    uint Head,
    uint Garment,
    uint LeftWeapon,
    uint LeftWeaponAccessory,
    uint RightWeapon,
    uint RightWeaponAccessory,
    uint MountArmor,
    uint Armor,
    uint Wing,
    uint WingPlus,
    uint Title,
    uint Flag,
    string GuildName);
