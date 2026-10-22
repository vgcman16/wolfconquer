#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Complete native Role.Instance.HundredWeapons, split only at whole-method boundaries.
// No LoadCharacter/SaveClient join, host binding, packet receiver or owner birth is installed.
internal partial class LegacyNativeHundredWeapons
{
    private LegacyNativeGameClientRuntime Owner;
    public Dictionary<LegacyNativeMagicType.WeaponsType, LegacyNativeHundredWeaponInfo> Objects;
    public List<ushort> TODO_Spells;
    public bool Unlocked;
    public Dictionary<byte, LegacyNativeTime32> TriggerStamp;
    public Dictionary<byte, byte> TriggerSeconds;

    // LINQ's real long Sum remains checked; only the native final narrowing is unchecked.
    public uint TotalScore => unchecked((uint)(Unlocked ? Objects.Values.Sum((LegacyNativeHundredWeaponInfo i) => i.Score) : 0u));

    public uint PerfectionScore => unchecked((Unlocked && Valid()) ?
        (TotalScore / 50 + ((TryGetItem(1) != null) ? (TryGetItem(1).Score - TryGetItem(1).Score * 2 / 100) : 0)) : 0u);

    public LegacyNativeHundredWeapons(LegacyNativeGameClientRuntime _own)
    {
        Owner = _own;
        Objects = new Dictionary<LegacyNativeMagicType.WeaponsType, LegacyNativeHundredWeaponInfo>();
        TODO_Spells = new List<ushort>();
        TriggerSeconds = new Dictionary<byte, byte> { { 2, 0 }, { 3, 0 } };
        TriggerStamp = new Dictionary<byte, LegacyNativeTime32>
        {
            { 2, default(LegacyNativeTime32) },
            { 3, default(LegacyNativeTime32) }
        };
    }

    public bool Valid()
    {
        return LegacyNativeAtributesStatus.IsTrojan(Owner.Player.Class) && Owner.Player.Class % 100 != 0 &&
            (Owner.Player.Level >= 40 || Owner.Player.Reborn > 0);
    }

    public bool Free(int Position)
    {
        return Objects.Values.Count((LegacyNativeHundredWeaponInfo i) => i.BitVector.Contain(Position)) == 0;
    }

    public bool FreeAppearance(byte Position)
    {
        return Objects.Values.Count((LegacyNativeHundredWeaponInfo i) => i.AppearancePosition == Position) == 0;
    }

    public LegacyNativeHundredWeaponInfo TryGetItem(int Position)
    {
        return Objects.Values.Where((LegacyNativeHundredWeaponInfo i) => i.BitVector.Contain(Position)).FirstOrDefault();
    }

    public bool ValidWeapon(LegacyNativeMagicType.WeaponsType subType, byte Level)
    {
        return (subType == LegacyNativeMagicType.WeaponsType.Blade || subType == LegacyNativeMagicType.WeaponsType.Sword ||
            subType == LegacyNativeMagicType.WeaponsType.Club || subType == LegacyNativeMagicType.WeaponsType.Whip ||
            subType == LegacyNativeMagicType.WeaponsType.Dagger || subType == LegacyNativeMagicType.WeaponsType.Hook ||
            subType == LegacyNativeMagicType.WeaponsType.Axe || subType == LegacyNativeMagicType.WeaponsType.Hammer ||
            subType == LegacyNativeMagicType.WeaponsType.Scepter) && Level >= 1 && Level <= 9;
    }

    public bool ContainsWeapon(LegacyNativeMagicType.WeaponsType SubType)
    {
        return Objects.ContainsKey(SubType);
    }
}
