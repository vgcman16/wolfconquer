using System.Globalization;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    // Complete detached data preparation. Publication and protobuf registration
    // belong to the real Table.Load owner; this type never changes global state.
    internal sealed partial class MailboxLoadStage
    {
        internal readonly Dictionary<uint, Dictionary<uint, CQ>> Gear;
        internal readonly Dictionary<uint, LegacyNativeDaoqiDictTypeTable.CQ> Jade;
        internal readonly Dictionary<uint, LegacyNativeDictLotteryTable.CQ> Lottery;
        internal readonly Dictionary<uint, LegacyNativeDictCollectionTable.CQ> Collection;
        internal readonly Dictionary<LegacyNativeCombatGearRankTable.Type, LegacyNativeCombatGearRankTable.Rank> Ranks;
        private readonly LoadInput[] inputs;

        private MailboxLoadStage(Dictionary<uint, Dictionary<uint, CQ>> gear,
            Dictionary<uint, LegacyNativeDaoqiDictTypeTable.CQ> jade,
            Dictionary<uint, LegacyNativeDictLotteryTable.CQ> lottery,
            Dictionary<uint, LegacyNativeDictCollectionTable.CQ> collection,
            Dictionary<LegacyNativeCombatGearRankTable.Type, LegacyNativeCombatGearRankTable.Rank> ranks, LoadInput[] inputs)
        {
            Gear = gear; Jade = jade; Lottery = lottery; Collection = collection; Ranks = ranks;
            this.inputs = inputs.ToArray();
        }

        internal static bool TryCreate(string dbPrefix, string rankFilePath,
            out MailboxLoadStage? stage, out string reason)
        {
            stage = null; reason = "mailboxCombatGearTableLoad:InputInvalid";
            if (string.IsNullOrWhiteSpace(dbPrefix) || string.IsNullOrWhiteSpace(rankFilePath)) return false;
            try
            {
                // Preserve the source raw-prefix convention. In particular, do
                // not repair a missing trailing separator or swap global config.
                string[] paths = [dbPrefix + "daoqi_dict_type.txt", dbPrefix + "dict_lottery.txt",
                    dbPrefix + "dict_collection.txt", dbPrefix + "combat_gear.txt", rankFilePath];
                var captured = new LoadInput[paths.Length];
                long remaining = MaximumInputBytes;
                for (var index = 0; index < paths.Length; index++)
                {
                    captured[index] = ReadInput(paths[index], remaining);
                    remaining -= captured[index].Bytes.Length;
                }
                var lines = captured.Select(ReadLines).ToArray();
                if (lines.Take(4).Sum(rows => (long)rows.Length) + Math.Max(0, lines[4].Length - 1) > MaximumRows)
                    throw new InvalidDataException("CombatGear input row budget exceeded.");
                var culture = CaptureCulture();
                var jade = ReadJade(lines[0], culture);
                var lottery = ReadLottery(lines[1], culture);
                var collection = ReadCollection(lines[2], culture);
                var gear = ReadGear(lines[3], culture);
                var ranks = ReadRanks(lines[4], culture);
                var candidate = new MailboxLoadStage(gear, jade, lottery, collection, ranks, captured);
                if (!candidate.IsCurrentInputs())
                { reason = "mailboxCombatGearTableLoad:InputsChanged"; return false; }
                stage = candidate; reason = string.Empty; return true;
            }
            catch (Exception error) when (IsStageFailure(error))
            { reason = "mailboxCombatGearTableLoad:" + error.GetType().Name; return false; }
        }

        internal bool IsCurrentInputs()
        {
            try
            {
                foreach (var input in inputs)
                    if (!InputCurrent(input)) return false;
                return true;
            }
            catch (Exception error) when (IsStageFailure(error)) { return false; }
        }

        internal static Dictionary<uint, LegacyNativeDaoqiDictTypeTable.CQ> ReadJade(string path)
        {
            var input = ReadInput(path, MaximumInputBytes);
            var result = ReadJade(ReadLines(input), CaptureCulture());
            RequireCurrentInput(input);
            return result;
        }

        internal static Dictionary<uint, LegacyNativeDictLotteryTable.CQ> ReadLottery(string path)
        {
            var input = ReadInput(path, MaximumInputBytes);
            var result = ReadLottery(ReadLines(input), CaptureCulture());
            RequireCurrentInput(input);
            return result;
        }

        internal static Dictionary<uint, LegacyNativeDictCollectionTable.CQ> ReadCollection(string path)
        {
            var input = ReadInput(path, MaximumInputBytes);
            var result = ReadCollection(ReadLines(input), CaptureCulture());
            RequireCurrentInput(input);
            return result;
        }

        internal static Dictionary<LegacyNativeCombatGearRankTable.Type, LegacyNativeCombatGearRankTable.Rank> ReadRanksFile(string path)
        {
            var input = ReadInput(path, MaximumInputBytes);
            var result = ReadRanks(ReadLines(input), CaptureCulture());
            RequireCurrentInput(input);
            return result;
        }

        private static bool IsStageFailure(Exception error) => error is IOException or InvalidDataException or
            UnauthorizedAccessException or ArgumentException or InvalidOperationException or OverflowException or
            FormatException or NotSupportedException or System.Security.SecurityException;

        private static CultureInfo CaptureCulture() => CultureInfo.ReadOnly((CultureInfo)CultureInfo.CurrentCulture.Clone());
    }
}
