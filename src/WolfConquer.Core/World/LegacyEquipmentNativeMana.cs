using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyEquipmentNativeMana
{
    public static uint Item(PlayerItemSnapshotItem item, WorldDataCatalog? data)
    {
        if (data is null) return 0;
        var total = ItemMana(item.ItemId, data);
        if (PurificationInLife(item))
            total = unchecked(total + ItemMana(item.PurificationItemId, data));
        return total;
    }

    public static ushort ClassAndSpirit(CharacterSnapshot character)
    {
        var multiplier = character.Class is >= 13_005 and <= 13_057 or >= 14_005 and <= 14_057
            ? 30 : character.Class switch
            {
                13_002 or 14_002 => 15,
                13_003 or 14_003 => 20,
                13_004 or 14_004 => 25,
                _ => 5,
            };
        return unchecked((ushort)(character.Spirit * multiplier));
    }

    public static uint CombatGear(WorldSession session, WorldDataCatalog? data)
    {
        var family = session.Character.Class / 1_000;
        if (data is null || family is not (2 or 4 or 7 or 13 or 14) || session.CombatGear is not { } gear)
            return 0;
        uint total = 0;
        if (session.Character.Class is >= 7_000 and <= 7_051)
        {
            for (uint collection = 700; collection < 704; collection++)
            {
                var score = 0;
                if (collection != 700 && gear.Items.FirstOrDefault(item => item.ItemID == collection) is { } item)
                {
                    var itemScore = item.ItemID <= int.MaxValue && item.Level <= int.MaxValue &&
                        data.CombatGearByGearTypeAndLevel.TryGetValue(((int)item.ItemID, (int)item.Level), out var row)
                        ? unchecked((uint)row.Score) : 0;
                    score = unchecked(score + (int)(itemScore + item.MasteryScore));
                }
                foreach (var id in gear.BlackJadeBag.BlackJades)
                {
                    if (!TryJade(data, id, out var jade)) continue;
                    var type = Field(jade.Fields, 14);
                    if (collection == 700 ? type == 0 && Field(jade.Fields, 9) % 100 == 7 : type == collection)
                        score = unchecked(score + (int)Field(jade.Fields, 6));
                }
                // Native GetNutBook adds EVERY matching loaded row, including zero-score matches.
                foreach (var row in data.CollectionRewardTiers)
                    if (Field(row.Fields, 1) == collection && (long)score >= Field(row.Fields, 2) &&
                        (long)score <= Field(row.Fields, 3) && Field(row.Fields, 4) == 23)
                        total = unchecked(total + Field(row.Fields, 5));
            }
        }
        var equipped = gear.Items.FirstOrDefault(item => item.Equip == 1 && (family switch
        {
            2 => item.ItemID is >= 201 and <= 203,
            4 => item.ItemID is >= 401 and <= 403,
            7 => item.ItemID is >= 701 and <= 703,
            _ => item.ItemID is >= 1_301 and <= 1_305,
        }));
        if (equipped is not null)
            foreach (var id in equipped.BlackJades)
                if (TryJade(data, id, out var jade))
                {
                    if (Field(jade.Fields, 2) == 23) total = unchecked(total + Field(jade.Fields, 3));
                    if (Field(jade.Fields, 4) == 23) total = unchecked(total + Field(jade.Fields, 5));
                }
        return total;
    }

    public static uint JiangHu(CharacterSnapshot character, WorldDataCatalog? data)
    {
        if (data is null) return 0;
        IReadOnlyList<LegacyJiangHuStageState>? stages = null;
        foreach (var key in new[] { "JiangHu", "Jiang", "JiangHuInfo", "JiangHuState" })
            if (character.Attributes.TryGetValue(key, out var text) &&
                LegacyJiangHuHitpointsCalculator.TryParseLegacyRow(text, out var parsed))
            {
                stages = parsed;
                break;
            }
        if (stages is null) return 0;
        uint total = 0;
        foreach (var stage in stages)
        {
            if (!stage.Active) continue;
            // Native filters UID!=0, not the independent star.Activate field.
            var stars = stage.Stars.Where(star => star.EncodedAttribute != 0).ToArray();
            for (var start = 0; start < stars.Length;)
            {
                var end = start + 1;
                while (end < stars.Length && stars[end].EncodedAttribute % 256 == stars[start].EncodedAttribute % 256)
                    end++;
                if (stars[start].EncodedAttribute % 256 == 15)
                {
                    var count = end - start;
                    var bonus = count switch { 1 => 0, 2 => 10, 3 => 13, 4 => 15, 5 => 18,
                        6 => 21, 7 => 25, 8 => 30, _ => 50 };
                    ushort aligned = 0;
                    for (var at = start; at < end; at++)
                    {
                        var encoded = stars[at].EncodedAttribute;
                        var row = data.JiangHuAttributes.FirstOrDefault(attribute =>
                            attribute.AttributeType == encoded % 256 && attribute.Level == encoded / 256);
                        var power = row is null ? (ushort)0 : unchecked((ushort)row.Value);
                        aligned = unchecked((ushort)(aligned + unchecked((ushort)(power + power * bonus / 100))));
                    }
                    total = unchecked(total + aligned);
                }
                start = end;
            }
        }
        return total;
    }

    private static uint ItemMana(uint id, WorldDataCatalog data) =>
        id <= int.MaxValue && data.ItemsById.TryGetValue((int)id, out var rows) && rows.Count > 0 &&
        rows[0].Fields.Count > 19 &&
        ushort.TryParse(rows[0].Fields[19], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mana)
            ? mana : 0U;

    private static bool PurificationInLife(PlayerItemSnapshotItem item)
    {
        if (item.PurificationDuration == 0) return item.PurificationItemId > 1_000;
        try
        {
            var expires = new TimeSpan(DateTime.FromBinary(item.PurificationAddedOn)
                .AddSeconds(item.PurificationDuration).Ticks);
            return (int)(expires.TotalSeconds - new TimeSpan(DateTime.Now.Ticks).TotalSeconds) > 0;
        }
        catch (ArgumentException) { return false; }
    }

    private static bool TryJade(WorldDataCatalog data, uint id, out DaoqiDictType jade)
    {
        jade = null!;
        return id <= int.MaxValue && data.DaoqiDictTypesById.TryGetValue((int)id, out jade!);
    }

    private static uint Field(IReadOnlyList<string> fields, int index) => fields.Count > index &&
        uint.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
