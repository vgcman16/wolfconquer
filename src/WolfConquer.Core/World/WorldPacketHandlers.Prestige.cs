using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{

    private const int LegacyPrestigePointCount = 43;
    private const int LegacyPrestigeRankingLevelFieldIndex = 2;
    private const int LegacyPrestigeRankingPointCountFieldIndex = 19;
    private const int LegacyPrestigeRankingPointsStartIndex = 20;

    [PacketHandler(WorldPacketOpcodes.LegacyUserAbilityScore)]
    private static async ValueTask HandleLegacyUserAbilityScoreAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyUserAbilityScorePacket.TryParse(frame, out var query))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyUserAbilityScore packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyUserAbilityScore {FormatSession(context)} uid={query.Uid}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        LegacyUserAbilityScorePacket? response = null;
        var targetUid = query.Uid == 0 ? CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)) : query.Uid;
        if (targetUid == session.Character.Uid)
        {
            response = CreateLegacyUserAbilityScoreResponse(session, context.WorldData);
        }
        else if (TryCreateLegacyUserAbilityScoreFromPrestigeRank(context.WorldData, targetUid, out var rankingResponse))
        {
            response = rankingResponse;
        }
        else if (TryGetSessionByUid(context, targetUid, out var target))
        {
            response = CreateLegacyUserAbilityScoreResponse(target.Session, context.WorldData);
        }

        if (response is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyUserAbilityScore {FormatSession(context)} uid={targetUid}; target was not found.")
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(response.Value.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyUserAbilityScore {FormatSession(context)} uid={response.Value.Uid} items={response.Value.Items.Count}.")
            .ConfigureAwait(false);
    }

    private static LegacyUserAbilityScorePacket CreateLegacyUserAbilityScoreResponse(
        WorldSession session,
        WorldDataCatalog? worldData)
    {
        return LegacyUserAbilityScorePacket.CreateResponse(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            CheckedUInt32(session.Character.Level, nameof(session.Character.Level)),
            ReadLegacyPrestigePoints(session, worldData));
    }

    private static bool TryCreateLegacyUserAbilityScoreFromPrestigeRank(
        WorldDataCatalog? worldData,
        uint uid,
        out LegacyUserAbilityScorePacket response)
    {
        response = default;

        if (uid > int.MaxValue ||
            worldData is null ||
            !worldData.PrestigeRanksByCharacterId.TryGetValue((int)uid, out var entries) ||
            entries.Count == 0)
        {
            return false;
        }

        var entry = entries[0];
        if (entry.RankingFields.Count <= LegacyPrestigeRankingPointsStartIndex ||
            !TryParseUInt32(entry.RankingFields[LegacyPrestigeRankingLevelFieldIndex], out var level))
        {
            return false;
        }

        var pointsCount = LegacyPrestigePointCount;
        if (entry.RankingFields.Count > LegacyPrestigeRankingPointCountFieldIndex &&
            TryParseUInt32(entry.RankingFields[LegacyPrestigeRankingPointCountFieldIndex], out var parsedCount) &&
            parsedCount > 0)
        {
            pointsCount = checked((int)Math.Min(parsedCount, LegacyPrestigePointCount));
        }

        var points = new uint[LegacyPrestigePointCount];
        for (var index = 0; index < pointsCount; index++)
        {
            var fieldIndex = LegacyPrestigeRankingPointsStartIndex + index;
            if (fieldIndex >= entry.RankingFields.Count)
            {
                break;
            }

            TryParseUInt32(entry.RankingFields[fieldIndex], out points[index]);
        }

        response = LegacyUserAbilityScorePacket.CreateResponse(uid, level, points);
        return true;
    }

    private static IReadOnlyList<uint> ReadLegacyPrestigePoints(
        WorldSession session,
        WorldDataCatalog? worldData)
    {
        var points = new uint[LegacyPrestigePointCount];
        var character = session.Character;
        FillLegacyPrestigePointsFromAvailableSessionData(character, points);
        FillLegacyPrestigePointsFromSessionItems(session.PlayerItems, points);
        FillLegacyPrestigePointsFromWorldData(session, worldData, points);
        ApplyLegacyPrestigePointOverrides(character, points);
        return points;
    }

    private static void FillLegacyPrestigePointsFromAvailableSessionData(
        CharacterSnapshot character,
        uint[] points)
    {
        points[0] = CalculateLegacyJiangHuInnerStrengthPrestigePoints(character);
        points[1] = CalculateLegacyChiPrestigePoints(character);
        points[15] = CalculateLegacySubClassPrestigePoints(character);
        points[4] = CalculateLegacyLevelPrestigePoints(character.Level);
        points[6] = SaturatingUInt32(
            (ulong)Math.Max(0, character.Strength) +
            (ulong)Math.Max(0, character.Spirit) +
            (ulong)Math.Max(0, character.Vitality) +
            (ulong)Math.Max(0, character.Agility),
            multiplier: 5);
        points[7] = SaturatingUInt32((ulong)Math.Max(0, character.Reborn), multiplier: 1_000);
        points[18] = SaturatingUInt32(ReadLegacyNobilityRank(character), multiplier: 1_000);
        points[20] = ReadAttributeUInt32(character.Attributes, "PerfectionRedRune", "RunePerfectionRed");
        points[21] = ReadAttributeUInt32(character.Attributes, "PerfectionBlueRune", "RunePerfectionBlue");
        points[22] = ReadAttributeUInt32(character.Attributes, "PerfectionYellowRune", "RunePerfectionYellow");
        points[37] = CalculateLegacyBeastPrestigePoints(character);
        points[38] = ReadAttributeUInt32(
            character.Attributes,
            "HundredWeaponsPerfectionScore",
            "HundredWeaponPerfectionScore",
            "HundredWeaponsScore");
    }

    private static void FillLegacyPrestigePointsFromSessionItems(
        PlayerItemSnapshot playerItems,
        uint[] points)
    {
        var equipmentPoints = 0UL;
        foreach (var item in playerItems.EquipmentItems)
        {
            equipmentPoints += item.ItemPoints;
        }

        // The legacy CreatePrestigePoints method adds current equipment item points
        // before and after the total-prestige sum, leaving slot 4 doubled.
        points[3] = equipmentPoints > uint.MaxValue / 2
            ? uint.MaxValue
            : (uint)(equipmentPoints * 2);
    }

    private static void FillLegacyPrestigePointsFromWorldData(
        WorldSession session,
        WorldDataCatalog? worldData,
        uint[] points)
    {
        if (worldData is null)
        {
            return;
        }

        points[2] = CalculateLegacyInnerPowerPrestigePoints(session.Character, worldData);
        var wardrobePoints = CalculateLegacyWardrobePrestigePoints(session.PlayerItems, worldData);
        points[16] = wardrobePoints;
        points[17] = wardrobePoints;
    }

    private static void ApplyLegacyPrestigePointOverrides(
        CharacterSnapshot character,
        uint[] points)
    {
        var rawList = ReadAttributeString(
            character.Attributes,
            "PrestigePoints",
            "PrestigePointList",
            "AbilityScorePoints");
        if (rawList.Length > 0)
        {
            var parts = rawList.Split(
                ['/', ',', ';', '|', ' '],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var index = 0; index < Math.Min(parts.Length, points.Length); index++)
            {
                if (TryParseUInt32(parts[index], out var parsed))
                {
                    points[index] = parsed;
                }
            }
        }

        for (var index = 0; index < points.Length; index++)
        {
            var oneBased = index + 1;
            var keys = new[]
            {
                "PrestigePoint" + oneBased.ToString(CultureInfo.InvariantCulture),
                "PrestigePoints" + oneBased.ToString(CultureInfo.InvariantCulture),
                "AbilityScorePoint" + oneBased.ToString(CultureInfo.InvariantCulture),
            };
            var raw = ReadAttributeString(character.Attributes, keys);
            if (raw.Length > 0 && TryParseUInt32(raw, out var parsed))
            {
                points[index] = parsed;
            }
        }
    }

    private static uint CalculateLegacyLevelPrestigePoints(long level)
    {
        var safeLevel = (ulong)Math.Max(0, level);
        if (level < 120)
        {
            return SaturatingUInt32(safeLevel, multiplier: 10);
        }

        if (level < 130)
        {
            return SaturatingUInt32(safeLevel, multiplier: 15);
        }

        if (level < 140)
        {
            return SaturatingUInt32(safeLevel, multiplier: 20);
        }

        return SaturatingUInt32(safeLevel, multiplier: 25);
    }

    private static uint CalculateLegacyBeastPrestigePoints(CharacterSnapshot character)
    {
        if (!ReadAttributeBool(character.Attributes, RuntimeTailedBeastActivatedAttribute, "BeastActivated", "BeastsActivated"))
        {
            return 0;
        }

        var level = ReadAttributeUInt64(
            character.Attributes,
            RuntimeTailedBeastLevelAttribute,
            "BeastLevel",
            "BeastsLevel");
        var points = 300UL;
        for (var index = 0UL; index <= level && index <= 100; index++)
        {
            points += index >= 50 ? 100UL : 200UL;
        }

        return points > uint.MaxValue ? uint.MaxValue : (uint)points;
    }

    private static uint CalculateLegacyJiangHuInnerStrengthPrestigePoints(CharacterSnapshot character)
    {
        var innerStrength = ReadAttributeUInt64(
            character.Attributes,
            "InnerStrengthScore",
            "Inner_Strength",
            "JiangHuInnerStrength");
        var multiplier = innerStrength switch
        {
            > 0 and <= 16_200 => 30UL,
            >= 16_201 and <= 40_500 => 33UL,
            >= 40_501 and <= 60_750 => 36UL,
            >= 60_751 and <= 72_000 => 40UL,
            >= 72_001 and <= 79_200 => 45UL,
            >= 79_201 and <= 80_800 => 50UL,
            >= 80_801 and <= 81_000 => 60UL,
            _ => 0UL,
        };

        var points = (innerStrength / 100) * multiplier;
        return points > uint.MaxValue ? uint.MaxValue : (uint)points;
    }

    private static uint CalculateLegacyChiPrestigePoints(CharacterSnapshot character)
    {
        var total = 0UL;
        foreach (var power in LegacyChiState.FromSnapshot(character).Powers)
        {
            var score = (ulong)CalculateLegacyChiScore(power);
            total += score * 30UL;
            if (ReadAttributeUInt64(
                    character.Attributes,
                    power.Type + "ChiRank",
                    power.Type + "Rank") < 50)
            {
                total += score * 10UL;
            }
        }

        return total > uint.MaxValue ? uint.MaxValue : (uint)total;
    }

    private static uint CalculateLegacySubClassPrestigePoints(CharacterSnapshot character)
    {
        if (!character.Attributes.TryGetValue("SubProfInfo", out var rawState) ||
            !LegacySubClassHitpointsCalculator.TryParseLegacyState(rawState, out var state))
        {
            return 0;
        }

        var total = 0UL;
        foreach (var entry in state.Entries)
        {
            total += ((ulong)entry.Level * 80UL) + ((ulong)entry.Phrase * 20UL);
        }

        return total > uint.MaxValue ? uint.MaxValue : (uint)total;
    }

    private static uint CalculateLegacyInnerPowerPrestigePoints(
        CharacterSnapshot character,
        WorldDataCatalog worldData)
    {
        if (character.Uid is < int.MinValue or > int.MaxValue ||
            !worldData.InnerPowerRanksByCharacterId.TryGetValue((int)character.Uid, out var rows) ||
            rows.Count == 0)
        {
            return 0;
        }

        var fields = rows[0].Fields;
        if (fields.Count < 5 ||
            !TryReadInt32(fields, 3, out var stageCount) ||
            stageCount <= 0)
        {
            return 0;
        }

        var points = 0U;
        var offset = 4;
        for (var stageIndex = 0; stageIndex < stageCount; stageIndex++)
        {
            if (fields.Count <= offset + 2 ||
                !TryReadInt32(fields, offset + 1, out var unlocked) ||
                !TryReadInt32(fields, offset + 2, out var gongCount) ||
                gongCount < 0)
            {
                return points;
            }

            if (unlocked != 0)
            {
                points = points > uint.MaxValue - 100 ? uint.MaxValue : points + 100;
            }

            offset += 3 + (gongCount * 5);
        }

        return points;
    }

    private static uint CalculateLegacyWardrobePrestigePoints(
        PlayerItemSnapshot playerItems,
        WorldDataCatalog worldData)
    {
        var count = 0;
        foreach (var item in EnumerateLegacyWardrobeItems(playerItems))
        {
            if (item.ItemId > int.MaxValue ||
                !worldData.CoatStorageTypesByItemTypeId.TryGetValue((int)item.ItemId, out var coatStorageType) ||
                coatStorageType.Parameters.Count < 1 ||
                coatStorageType.Parameters[0] != 1)
            {
                continue;
            }

            count++;
        }

        var points = 0U;
        foreach (var threshold in new[] { 0, 15, 30, 45, 60 })
        {
            if (count > threshold)
            {
                points += 50;
            }
        }

        return points;
    }



}
