using System.Globalization;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    internal sealed partial class MailboxLoadStage
    {
        private static uint U(string value, CultureInfo culture) => Convert.ToUInt32(value, culture);

        private static string[] Columns(string line, int count)
        {
            var fields = line.Split(new[] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != count) throw new InvalidDataException("CombatGear table row has missing or extra columns.");
            return fields;
        }

        private static void RequireRowBudget(string[] lines)
        {
            if (lines.Length > MaximumRows) throw new InvalidDataException("CombatGear table row budget exceeded.");
        }

        private static Dictionary<uint, LegacyNativeDaoqiDictTypeTable.CQ> ReadJade(string[] lines, CultureInfo culture)
        {
            RequireRowBudget(lines);
            var result = new Dictionary<uint, LegacyNativeDaoqiDictTypeTable.CQ>();
            foreach (var line in lines)
            {
                var f = Columns(line, 15);
                var row = new LegacyNativeDaoqiDictTypeTable.CQ
                {
                    ID = U(f[0], culture), Type = (LegacyNativeDaoqiDictTypeTable.Type)U(f[1], culture),
                    attr_type1 = U(f[2], culture), attr_value1 = U(f[3], culture),
                    attr_type2 = U(f[4], culture), attr_value2 = U(f[5], culture), score = U(f[6], culture),
                    points = U(f[7], culture), rate = U(f[8], culture), TypeRune = U(f[9], culture),
                    Level = U(f[10], culture), Exp = U(f[11], culture), u12 = U(f[12], culture),
                    u13 = U(f[13], culture), TypeCombatGear = U(f[14], culture),
                };
                if (!result.TryAdd(row.ID, row)) throw new InvalidDataException("Duplicate daoqi table ID.");
            }
            return result;
        }

        private static Dictionary<uint, LegacyNativeDictLotteryTable.CQ> ReadLottery(string[] lines, CultureInfo culture)
        {
            RequireRowBudget(lines);
            var result = new Dictionary<uint, LegacyNativeDictLotteryTable.CQ>();
            foreach (var line in lines)
            {
                var f = Columns(line, 6);
                var row = new LegacyNativeDictLotteryTable.CQ { Id = U(f[0], culture), Type = U(f[1], culture),
                    Value1 = U(f[2], culture), Value2 = U(f[3], culture), Value3 = U(f[4], culture), rate = U(f[5], culture) };
                if (!result.TryAdd(row.Id, row)) throw new InvalidDataException("Duplicate lottery table ID.");
            }
            return result;
        }

        private static Dictionary<uint, LegacyNativeDictCollectionTable.CQ> ReadCollection(string[] lines, CultureInfo culture)
        {
            RequireRowBudget(lines);
            var result = new Dictionary<uint, LegacyNativeDictCollectionTable.CQ>();
            foreach (var line in lines)
            {
                var f = Columns(line, 7);
                var row = new LegacyNativeDictCollectionTable.CQ { Id = U(f[0], culture), Type = U(f[1], culture),
                    Min = U(f[2], culture), Mix = U(f[3], culture), attr_type1 = (LegacyNativeDaoqiDictTypeTable.AttrType)U(f[4], culture),
                    attr_value1 = U(f[5], culture), value2 = U(f[6], culture) };
                if (!result.TryAdd(row.Id, row)) throw new InvalidDataException("Duplicate collection table ID.");
            }
            return result;
        }

        private static Dictionary<uint, Dictionary<uint, CQ>> ReadGear(string[] lines, CultureInfo culture)
        {
            RequireRowBudget(lines);
            var result = new Dictionary<uint, Dictionary<uint, CQ>>();
            foreach (var line in lines)
            {
                var f = Columns(line, 19);
                var row = new CQ { ID = U(f[0], culture), Type = U(f[1], culture), Level = U(f[2], culture),
                    Exp = U(f[3], culture), MaxHP = U(f[4], culture), PAttack = U(f[5], culture),
                    PDefense = U(f[6], culture), MDefense = U(f[7], culture), Skills = new uint[10] };
                for (var index = 0; index < row.Skills.Length; index++) row.Skills[index] = U(f[8 + index], culture);
                row.Score = U(f[18], culture);
                // Source first-wins is retained only after every duplicate-row
                // column has been parsed successfully; corrupt tails cannot hide.
                if (!result.TryGetValue(row.Type, out var levels)) result.Add(row.Type, levels = new());
                levels.TryAdd(row.Level, row);
            }
            return result;
        }
    }
}
