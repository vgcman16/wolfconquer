using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyJiangHuHitpointsContribution(
    uint RawHitpoints,
    uint CappedHitpoints,
    IReadOnlyList<string> MissingInputs);

public sealed record LegacyJiangHuStageState(
    bool Active,
    IReadOnlyList<LegacyJiangHuStarState> Stars);

public sealed record LegacyJiangHuStarState(bool Active, ushort EncodedAttribute);

public static class LegacyJiangHuHitpointsCalculator
{
    private const int MaxLifeAttributeType = 1;
    private const uint MaxLifeCap = 12_150;
    private const int LegacyStageCount = 9;
    private const int LegacyStarsPerStage = 9;

    private static readonly string[] SnapshotAttributeKeys =
    [
        "JiangHu",
        "Jiang",
        "JiangHuInfo",
        "JiangHuState",
    ];

    private static readonly ushort[] AlignmentExtraPoints =
    [
        0,
        10,
        13,
        15,
        18,
        21,
        25,
        30,
        50,
    ];

    public static LegacyJiangHuHitpointsContribution Calculate(
        CharacterSnapshot snapshot,
        WorldDataCatalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var missingInputs = new List<string>();
        if (!TryReadRawState(snapshot, out var rawState))
        {
            missingInputs.Add($"JiangHu.txt row for character {snapshot.Uid}");
        }

        if (catalog is null)
        {
            missingInputs.Add("WorldDataCatalog.JiangHuAttributes");
        }

        if (missingInputs.Count > 0)
        {
            return new LegacyJiangHuHitpointsContribution(0, 0, missingInputs);
        }

        return Calculate(rawState!, catalog!.JiangHuAttributes);
    }

    public static LegacyJiangHuHitpointsContribution Calculate(
        IReadOnlyList<LegacyJiangHuStageState>? stages,
        IReadOnlyList<JiangHuAttribute>? attributes)
    {
        var missingInputs = new List<string>();
        if (stages is null)
        {
            missingInputs.Add("JiangHu stage/star state");
        }

        if (attributes is null)
        {
            missingInputs.Add("WorldDataCatalog.JiangHuAttributes");
        }

        if (missingInputs.Count > 0)
        {
            return new LegacyJiangHuHitpointsContribution(0, 0, missingInputs);
        }

        var attributesByEncodedId = attributes!
            .GroupBy(attribute => ValueToRoll(attribute.AttributeType, attribute.Level))
            .ToDictionary(group => group.Key, group => group.First());

        var rawHitpoints = 0u;
        foreach (var stage in stages!)
        {
            if (!stage.Active)
            {
                continue;
            }

            var stars = stage.Stars
                .Where(star => star.EncodedAttribute != 0)
                .Select(star => DecodeStar(star.EncodedAttribute))
                .ToArray();

            foreach (var run in BuildConsecutiveRuns(stars))
            {
                if (run.Count == 1)
                {
                    rawHitpoints = checked(rawHitpoints + CalculateSingleMaxLife(run[0], attributesByEncodedId));
                    continue;
                }

                rawHitpoints = checked(rawHitpoints + CalculateAlignedMaxLife(run, attributesByEncodedId));
            }
        }

        return new LegacyJiangHuHitpointsContribution(
            rawHitpoints,
            Math.Min(rawHitpoints, MaxLifeCap),
            []);
    }

    public static bool TryParseLegacyRow(string rawRow, out IReadOnlyList<LegacyJiangHuStageState> stages)
    {
        stages = [];
        if (string.IsNullOrWhiteSpace(rawRow))
        {
            return false;
        }

        var fields = rawRow.Split('/', StringSplitOptions.None);
        var offset = 9;
        var parsedStages = new List<LegacyJiangHuStageState>(LegacyStageCount);

        for (var stageIndex = 0; stageIndex < LegacyStageCount; stageIndex++)
        {
            if (!TryReadByte(fields, offset++, out var active))
            {
                return false;
            }

            var stars = new List<LegacyJiangHuStarState>(LegacyStarsPerStage);
            for (var starIndex = 0; starIndex < LegacyStarsPerStage; starIndex++)
            {
                if (!TryReadByte(fields, offset++, out var starActive) ||
                    !TryReadUInt16(fields, offset++, out var encoded))
                {
                    return false;
                }

                if (stageIndex == LegacyStageCount - 1 &&
                    GetValueType(encoded) == 2)
                {
                    encoded = ValueToRoll(MaxLifeAttributeType, GetValueLevel(encoded));
                }

                stars.Add(new LegacyJiangHuStarState(starActive == 1, encoded));
            }

            parsedStages.Add(new LegacyJiangHuStageState(active == 1, stars));
        }

        stages = parsedStages;
        return true;
    }

    private static uint CalculateSingleMaxLife(
        DecodedStar star,
        IReadOnlyDictionary<ushort, JiangHuAttribute> attributesByEncodedId)
    {
        if (star.Type != MaxLifeAttributeType ||
            !attributesByEncodedId.TryGetValue(star.EncodedAttribute, out var attribute) ||
            attribute.Value <= 0)
        {
            return 0;
        }

        return checked((uint)attribute.Value);
    }

    private static uint CalculateAlignedMaxLife(
        IReadOnlyList<DecodedStar> stars,
        IReadOnlyDictionary<ushort, JiangHuAttribute> attributesByEncodedId)
    {
        if (stars.Count == 0 ||
            stars[0].Type != MaxLifeAttributeType)
        {
            return 0;
        }

        var bonusPercent = AlignmentExtraPoints[Math.Min(AlignmentExtraPoints.Length - 1, stars.Count - 1)];
        var total = 0u;
        foreach (var star in stars)
        {
            if (!attributesByEncodedId.TryGetValue(star.EncodedAttribute, out var attribute) ||
                attribute.Value <= 0)
            {
                continue;
            }

            var power = checked((uint)attribute.Value);
            total = checked(total + power + (power * bonusPercent / 100));
        }

        return total;
    }

    private static IReadOnlyList<IReadOnlyList<DecodedStar>> BuildConsecutiveRuns(IReadOnlyList<DecodedStar> stars)
    {
        var runs = new List<IReadOnlyList<DecodedStar>>();
        var index = 0;
        while (index < stars.Count)
        {
            var start = index;
            index++;
            while (index < stars.Count && stars[index].Type == stars[start].Type)
            {
                index++;
            }

            runs.Add(stars.Skip(start).Take(index - start).ToArray());
        }

        return runs;
    }

    private static DecodedStar DecodeStar(ushort encodedAttribute)
    {
        return new DecodedStar(
            encodedAttribute,
            GetValueType(encodedAttribute),
            GetValueLevel(encodedAttribute));
    }

    private static bool TryReadRawState(CharacterSnapshot snapshot, out IReadOnlyList<LegacyJiangHuStageState> stages)
    {
        foreach (var key in SnapshotAttributeKeys)
        {
            if (snapshot.Attributes.TryGetValue(key, out var raw) &&
                TryParseLegacyRow(raw, out stages))
            {
                return true;
            }
        }

        stages = [];
        return false;
    }

    private static ushort ValueToRoll(int type, int level)
    {
        return checked((ushort)(type + (level * 256)));
    }

    private static byte GetValueLevel(ushort value)
    {
        return (byte)((value - (value % 256)) / 256);
    }

    private static byte GetValueType(ushort value)
    {
        return (byte)(value % 256);
    }

    private static bool TryReadByte(IReadOnlyList<string> fields, int index, out byte value)
    {
        value = 0;
        return fields.Count > index &&
            byte.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUInt16(IReadOnlyList<string> fields, int index, out ushort value)
    {
        value = 0;
        return fields.Count > index &&
            ushort.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private sealed record DecodedStar(ushort EncodedAttribute, byte Type, byte Level);
}
