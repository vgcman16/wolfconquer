using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Native InnerPower.UpdateStatus cached aggregate, consumed at Equip.AddInnerPowerAtributes.
    private static LegacyNativeMagicQueryFields NativeMagicInnerPower(WorldSession current, WorldDataCatalog? data)
    {
        if (data is null || current.Character.Uid > int.MaxValue ||
            !data.InnerPowerRanksByCharacterId.TryGetValue((int)current.Character.Uid, out var rows) ||
            rows.Count == 0 || !TryReadLegacyInnerPowerState(rows[0], out var state, out _)) return default;
        uint attack = 0, critical = 0, increase = 0;
        void Add(int kind, uint value)
        {
            unchecked { if (kind == 3) attack += value; else if (kind == 11) critical += value; else if (kind == 7) increase += value; }
        }
        foreach (var stage in state.Stages)
        {
            if (!stage.Unlocked) break;
            if (!data.NeiGongSecretsById.TryGetValue(stage.Id, out var secret)) continue;
            for (var index = 0; index < stage.Gongs.Count; index++)
            {
                var gong = stage.Gongs[index];
                if (!gong.Unlocked || gong.Level == 0) break;
                if (!data.NeiGongTypeBySecretIdAndSlot.TryGetValue((stage.Id, index + 1), out var type) ||
                    type.MaxLevel == 0) continue;
                for (var attr = 0; attr < type.AttributeTypes.Count && attr < type.AttributeValues.Count; attr++)
                    Add(type.AttributeTypes[attr], unchecked((uint)type.AttributeValues[attr]) /
                        unchecked((uint)type.MaxLevel) * gong.Level);
            }
            if (stage.Gongs.All(gong => gong.Complete))
                for (var attr = 0; attr < secret.AttributeTypes.Count && attr < secret.AttributeValues.Count; attr++)
                    Add(secret.AttributeTypes[attr], unchecked((uint)secret.AttributeValues[attr]));
        }
        return new(attack, 0, critical, increase);
    }

    private static LegacyNativeMagicQueryFields NativeMagicSubClass(WorldSession current, WorldDataCatalog? data)
    {
        if (data is null || !current.Character.Attributes.TryGetValue("SubProfInfo", out var raw) ||
            !LegacySubClassHitpointsCalculator.TryParseLegacyState(raw, out var state)) return default;
        uint attack = 0, critical = 0;
        foreach (var entry in state.Entries)
        {
            if (entry.Id is not (2 or 6)) continue;
            var section = entry.Phrase == 0 ? entry.Id : entry.Id * 10 + entry.Phrase;
            var row = data.SubProfessions.FirstOrDefault(row => row.Id == section);
            if (row is null) continue;
            var first = row.Intro.Split(';')[0].Split('+');
            if (first.Length < 2 || !uint.TryParse(first[1].Split('%')[0].Trim(),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) continue;
            unchecked { if (entry.Id == 6) attack += value; else critical += value * 100; }
        }
        return new(attack, 0, critical, 0);
    }

    private static LegacyNativeMagicQueryFields NativeMagicJiangHu(WorldSession current, WorldDataCatalog? data)
    {
        if (data is null) return default;
        IReadOnlyList<LegacyJiangHuStageState>? stages = null;
        foreach (var key in new[] { "JiangHu", "Jiang", "JiangHuInfo", "JiangHuState" })
            if (current.Character.Attributes.TryGetValue(key, out var raw) &&
                LegacyJiangHuHitpointsCalculator.TryParseLegacyRow(raw, out var parsed))
            { stages = parsed; break; }
        if (stages is null) return default;
        var table = data.JiangHuAttributes.GroupBy(row => row.AttributeType + row.Level * 256)
            .ToDictionary(group => group.Key, group => group.First());
        uint attack = 0, critical = 0, increase = 0;
        void Add(int kind, ushort value)
        {
            unchecked { if (kind == 3) attack += value; else if (kind == 11) critical += value; else if (kind == 7) increase += value; }
        }
        var alignment = new uint[] { 0, 10, 13, 15, 18, 21, 25, 30, 50 };
        foreach (var stage in stages)
        {
            if (!stage.Active) continue;
            var stars = stage.Stars.Where(star => star.EncodedAttribute != 0).ToArray();
            var index = 0;
            while (index < stars.Length)
            {
                var start = index++;
                var kind = stars[start].EncodedAttribute % 256;
                while (index < stars.Length && stars[index].EncodedAttribute % 256 == kind) index++;
                var count = index - start;
                ushort power = 0;
                for (var at = start; at < index; at++)
                    if (table.TryGetValue(stars[at].EncodedAttribute, out var row))
                    {
                        var scalar = unchecked((ushort)row.Value);
                        power = unchecked((ushort)(power + scalar + scalar * alignment[count - 1] / 100));
                    }
                Add(kind, power); // Native aligned group accumulator is ushort before ComputeStatus.
            }
        }
        return new(attack, 0, Math.Min(critical, 5_400), increase);
    }
}
