using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    private PrestigeOwners CapturePrestigeOwners()
    {
        var player = Player;
        var oldPoints = PrestigePoints;
        var equipment = Equipment;
        var weapons = HundredWeapons;
        var wardrobe = MyWardrobe;
        var rune = Rune;
        var beasts = Beasts;
        var effects = PerfectionStatus;
        var server = LegacyNativeGroupServerList.MyServerInfo;
        if (player is null || !ReferenceEquals(player.Owner, this) || player.UID == 0 ||
            oldPoints is null || oldPoints.Length < 39 || equipment is null || !equipment.IsOwnedBy(this) ||
            weapons is null || !weapons.IsOwnedBy(this) || wardrobe is null || !wardrobe.IsOwnedBy(this) ||
            rune is null || !rune.IsSaveOwnedBy(this) || beasts is null || !ReferenceEquals(beasts.Owner, this) ||
            effects is null || server is null || PrestigeLevel > int.MaxValue)
        {
            throw new InvalidOperationException("Prestige preparation requires its real owners, identity, point slots and effect width.");
        }
        if (wardrobe.Items is null || wardrobe.Items.Length < 2 || wardrobe.Items[1] is null)
            throw new InvalidOperationException("Prestige preparation requires the real mount wardrobe category.");
        var equipmentRows = equipment.CurentEquip;
        if (equipmentRows is null)
            throw new InvalidOperationException("Prestige preparation requires current equipment.");
        var equipmentItems = new LegacyNativeGameItemRuntime[equipmentRows.Length];
        var equipmentPoints = new uint[equipmentRows.Length];
        for (var index = 0; index < equipmentRows.Length; index++)
        {
            var item = equipmentRows[index]
                ?? throw new InvalidOperationException("Prestige preparation found an invalid equipment tail.");
            equipmentItems[index] = item;
            equipmentPoints[index] = item.ItemPoints;
        }
        // This real witness verifies the otherwise private constructor binding and
        // captures the physical beast fields without changing them.
        var beastBefore = new LegacyNativeBeastsRuntime.PrefixBefore(this);
        _ = rune.Count; // Validate real storage; do not turn a missing cache into zero points.
        var optional = new PrestigeOptionalOwners(player.MyChi, player.InnerPower, player.MyJiangHu, player.SubClass);
        if (!IsPrestigeOptionalOwnersCurrent(player, optional, player.UID))
            throw new InvalidOperationException("Prestige preparation requires optional progression owners to match the player UID.");
        var guild = player.MyGuild;
        var member = guild is null ? null : player.MyGuildMember;
        var guildMembers = member is null ? null : guild!.Members;
        if (!IsPrestigeGuildBindingCurrent(guild, guildMembers, member, player.UID))
            throw new InvalidOperationException("Prestige preparation requires the player's registered guild member.");
        return new PrestigeOwners(player, oldPoints, equipment, equipmentRows, equipmentItems, equipmentPoints,
            weapons, wardrobe, rune, beasts, beastBefore, effects, server, optional, guild, guildMembers, member,
            player.UID, player.Class, player.ServerID, server.ID, PrestigeLevel);
    }

    private void RequireCurrentPrestigeOwners(PrestigeOwners owners)
    {
        if (!ReferenceEquals(Player, owners.Player) || !ReferenceEquals(owners.Player.Owner, this) ||
            !ReferenceEquals(PrestigePoints, owners.OldPoints) || !ReferenceEquals(Equipment, owners.Equipment) ||
            !owners.Equipment.IsOwnedBy(this) || !ReferenceEquals(owners.Equipment.CurentEquip, owners.EquipmentRows) ||
            !IsPrestigeEquipmentCurrent(owners) ||
            !ReferenceEquals(HundredWeapons, owners.Weapons) || !owners.Weapons.IsOwnedBy(this) ||
            !ReferenceEquals(MyWardrobe, owners.Wardrobe) || !owners.Wardrobe.IsOwnedBy(this) ||
            !ReferenceEquals(Rune, owners.Rune) || !owners.Rune.IsSaveOwnedBy(this) ||
            !ReferenceEquals(Beasts, owners.Beasts) || !owners.BeastBefore.IsCurrent() ||
            !ReferenceEquals(PerfectionStatus, owners.Effects) ||
            !ReferenceEquals(LegacyNativeGroupServerList.MyServerInfo, owners.Server) ||
            !IsPrestigeOptionalOwnersCurrent(owners.Player, owners.Optional, owners.Uid) ||
            !ReferenceEquals(owners.Player.MyGuild, owners.Guild) ||
            (owners.Guild is not null && !ReferenceEquals(owners.Player.MyGuildMember, owners.GuildMember)) ||
            !IsPrestigeGuildBindingCurrent(owners.Guild, owners.GuildMembers, owners.GuildMember, owners.Uid) ||
            owners.Player.UID != owners.Uid || owners.Player.Class != owners.CharacterClass ||
            owners.Player.ServerID != owners.PlayerServerId || owners.Server.ID != owners.ServerId ||
            PrestigeLevel != owners.PrestigeLevel)
        {
            throw new InvalidOperationException("A real prestige preparation owner or target changed before commitment.");
        }
    }

    private static bool IsPrestigeEquipmentCurrent(PrestigeOwners owners)
    {
        for (var index = 0; index < owners.EquipmentItems.Length; index++)
        {
            if (!ReferenceEquals(owners.EquipmentRows[index], owners.EquipmentItems[index]) ||
                owners.EquipmentItems[index].ItemPoints != owners.EquipmentPoints[index]) return false;
        }
        return true;
    }

    private static bool IsPrestigeOptionalOwnersCurrent(
        LegacyNativePlayerRuntime player, PrestigeOptionalOwners optional, uint uid)
    {
        return ReferenceEquals(player.MyChi, optional.Chi) &&
            ReferenceEquals(player.InnerPower, optional.InnerPower) &&
            ReferenceEquals(player.MyJiangHu, optional.JiangHu) &&
            ReferenceEquals(player.SubClass, optional.SubClass) &&
            (optional.Chi is null || optional.Chi.UID == uid) &&
            (optional.InnerPower is null || optional.InnerPower.UID == uid) &&
            (optional.JiangHu is null || optional.JiangHu.UID == uid);
    }

    private static bool IsPrestigeGuildBindingCurrent(
        LegacyNativeGuildRuntime? guild,
        ConcurrentDictionary<uint, LegacyNativeGuildRuntime.Member>? members,
        LegacyNativeGuildRuntime.Member? member,
        uint uid)
    {
        return member is null || guild is not null && members is not null &&
            ReferenceEquals(guild.Members, members) && member.UID == uid &&
            members.TryGetValue(uid, out var current) && ReferenceEquals(current, member);
    }

    private sealed record PrestigeOwners(
        LegacyNativePlayerRuntime Player,
        uint[] OldPoints,
        LegacyNativeEquipItemStorage Equipment,
        LegacyNativeGameItemRuntime[] EquipmentRows,
        LegacyNativeGameItemRuntime[] EquipmentItems,
        uint[] EquipmentPoints,
        LegacyNativeHundredWeapons Weapons,
        LegacyNativeWardrobeRuntime Wardrobe,
        LegacyNativeRuneItemStorage Rune,
        LegacyNativeBeastsRuntime Beasts,
        LegacyNativeBeastsRuntime.PrefixBefore BeastBefore,
        LegacyNativePerfectionEffect Effects,
        LegacyNativeGroupServerList.Server Server,
        PrestigeOptionalOwners Optional,
        LegacyNativeGuildRuntime? Guild,
        ConcurrentDictionary<uint, LegacyNativeGuildRuntime.Member>? GuildMembers,
        LegacyNativeGuildRuntime.Member? GuildMember,
        uint Uid,
        uint CharacterClass,
        uint PlayerServerId,
        uint ServerId,
        uint PrestigeLevel);

    private sealed record PrestigeOptionalOwners(
        LegacyNativeChiRuntime? Chi,
        LegacyNativeInnerPowerRuntime? InnerPower,
        LegacyNativeJiangHu? JiangHu,
        LegacyNativeSubClassRuntime? SubClass);
}
