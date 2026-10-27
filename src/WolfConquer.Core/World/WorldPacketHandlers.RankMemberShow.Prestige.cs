using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyRankMemberShowModeShow = 0;
    private const uint LegacyRankMemberShowPrestigeChampionType = 1;

    private static bool HasCurrentLegacyPrestigeQuerySession(WorldPacketHandlerContext context)
    {
        return context.Sessions is { } sessions &&
            sessions.TryGet(context.Connection, out var current) &&
            ReferenceEquals(current, context.Session);
    }

    private static bool TryPrepareLegacyPrestigeChampionResponse(
        WorldDataCatalog? worldData,
        out byte[] responseBytes,
        out int responseItemCount,
        out string? rejection)
    {
        responseBytes = [];
        responseItemCount = 0;
        if (worldData is null)
        {
            rejection = "required prestige catalog is unavailable";
            return false;
        }

        if (worldData.PrestigeRanks is not { } prestigeRows ||
            worldData.Manifest?.Counts is not { } counts ||
            !counts.TryGetValue("prestigeRanks", out var expectedCount) ||
            expectedCount < 0 || expectedCount != prestigeRows.Count)
        {
            rejection = "required prestige catalog count is missing or inconsistent";
            return false;
        }

        if (!PrestigeRankingSnapshot.TryCreate(prestigeRows, out var snapshot, out rejection))
        {
            rejection = "required prestige catalog is invalid: " + rejection;
            return false;
        }

        var champion = snapshot.BestOfTheWorld;
        var response = champion is null
            ? LegacyRankMemberShowPacket.EmptyResponse(LegacyRankMemberShowModeShow)
            : new LegacyRankMemberShowPacket(
                LegacyRankMemberShowModeShow,
                [CreateLegacyPrestigeChampionEntry(champion)]);
        try
        {
            // Prepare the complete frame before publishing anything to the connection.
            responseBytes = response.ToFrameBytes();
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            rejection = "required prestige catalog cannot be encoded: " + exception.GetType().Name;
            return false;
        }

        responseItemCount = response.Items.Count;
        rejection = null;
        return true;
    }

    private static LegacyRankMemberShowEntry CreateLegacyPrestigeChampionEntry(PrestigeRankingEntry champion)
    {
        return new LegacyRankMemberShowEntry(
            Type: LegacyRankMemberShowPrestigeChampionType,
            Rank: 1,
            EntityUid: champion.UID,
            champion.Name,
            champion.GuildName,
            champion.Mesh,
            champion.HairStyle,
            champion.Head,
            champion.Garment,
            champion.LeftWeapon,
            champion.LeftWeaponAccessory,
            champion.RightWeapon,
            champion.RightWeaponAccessory,
            champion.MountArmor,
            champion.Armor,
            champion.Wing,
            champion.WingPlus,
            champion.Title,
            champion.Flag,
            Profession: 0,
            WeaponWar: 0);
    }
}
