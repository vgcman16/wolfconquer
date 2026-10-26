namespace WolfConquer.Core.World;

internal static partial class LegacyNativeAttackBase
{
    internal readonly record struct MinimumMonsterDamageInputs(
        LegacyNativePlayerRuntime Player, LegacyNativeEquipItemStorage Equipment,
        ushort Level, uint LeftWeapon, uint RightWeapon)
    {
        internal int MinimumDamage => 1 + Level / 10 + (int)(LeftWeapon % 10) + (int)(RightWeapon % 10);

        internal bool IsCurrent(LegacyNativeGameClientRuntime client) =>
            Player is not null && Equipment is not null &&
            ReferenceEquals(client.Player, Player) && ReferenceEquals(Player.Owner, client) &&
            ReferenceEquals(client.Equipment, Equipment) && Equipment.IsOwnedBy(client) &&
            Player.Level == Level && Equipment.LeftWeapon == LeftWeapon && Equipment.RightWeapon == RightWeapon;
    }

    internal static MinimumMonsterDamageInputs CaptureMinimumMonsterDamageInputs(LegacyNativeGameClientRuntime client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var player = client.Player;
        var equipment = client.Equipment;
        if (player is null || !ReferenceEquals(player.Owner, client) ||
            equipment is null || !equipment.IsOwnedBy(client))
        {
            throw new InvalidDataException(
                "Minimum monster damage requires a current physical player and its actual equipment owner.");
        }

        var inputs = new MinimumMonsterDamageInputs(player, equipment, player.Level, equipment.LeftWeapon, equipment.RightWeapon);
        if (!inputs.IsCurrent(client))
        {
            throw new InvalidDataException("Minimum monster damage inputs changed during capture.");
        }

        return inputs;
    }
}
