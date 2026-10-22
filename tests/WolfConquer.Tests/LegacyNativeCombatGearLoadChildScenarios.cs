using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using ProtoBuf;
using ProtoBuf.Meta;
using Collection = WolfConquer.Core.Data.LegacyNativeDictCollectionTable;
using Jade = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using Lottery = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Gear = WolfConquer.Core.World.LegacyNativeMsgCombatGear;
using Dict = WolfConquer.Core.World.LegacyNativeMsgDictLottery;

namespace WolfConquer.Tests;

// Only the measured fresh child invokes this synchronous compiled entry.
internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    public static string Execute(string scenario, string guard)
    {
        Require(!RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Any(), "Genuine Default must start fresh.");
        using var s = new Scope();
        object? details = null;
        switch (scenario)
        {
            case "ColumnsAndDuplicates":
                s.Write("db-combat_gear.txt", "@@  " + string.Join("@@@@  ", Enumerable.Range(1, 19)) + "@@ignored\n" +
                    GearRow(999, 2, 3) + GearRow(17, 2, 4) + GearRow(23, 9, 3));
                Table.Load(s.Prefix);
                Require(!ReferenceEquals(s.OldInfo, Table.Info) && s.OldInfo[999][999].ID == 999, "Old gear graph must survive replacement.");
                Require(Table.Info.Count == 2 && Table.Info[2].Count == 2, "Type/level grouping or duplicate-first semantics.");
                Require(Values(Table.Info[2][3]).SequenceEqual(Enumerable.Range(1, 19).Select(i => (uint)i)), "All19gear columns or first duplicate winner.");
                Require(Table.Info[2][4].ID == 17 && Table.Info[9][3].ID == 23 && !Table.Info.ContainsKey(1), "Type, not rowID, is outer key.");
                var alias = Table.Info[2][3]; alias.Score = 777;
                Require(ReferenceEquals(alias, Table.Info[2][3]) && Table.Info[2][3].Score == 777, "Live row alias lost.");
                Require(Rank.Ranks.Count == 18 && s.Log.ToString() == "", "Successful rank stage or unexpected log.");
                break;
            case "Culture":
                var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                culture.NumberFormat.PositiveSign = "p";
                CultureInfo.CurrentCulture = culture;
                s.Write("db-combat_gear.txt", string.Join("@@", Enumerable.Range(1, 19).Select(i => "p" + i)));
                Table.Load(s.Prefix);
                Require(Values(Table.Info[2][3]).SequenceEqual(Enumerable.Range(1, 19).Select(i => (uint)i)), "Current-culture UInt32 conversion.");
                Require(ReferenceEquals(culture, CultureInfo.CurrentCulture) && s.Log.ToString() == "", "Loader changed culture or logged.");
                break;
            case "RawPrefix":
                string raw = s.Prefix.TrimEnd('-');
                Table.Load(raw);
                Require(Table.Location == raw && !ReferenceEquals(s.OldInfo, Table.Info) && Table.Info.Count == 0, "Raw prefix/Info publication.");
                Require(ReferenceEquals(s.OldJade, Jade.Info) && ReferenceEquals(s.OldLottery, Lottery.Info) &&
                    ReferenceEquals(s.OldCollection, Collection.Info), "Missing raw dependency paths must retain owners.");
                Require(s.Log.ToString().Contains("!File.Exists: " + raw + "daoqi_dict_type.txt", StringComparison.Ordinal) &&
                    s.Log.ToString().Contains(raw + "combat_gear.txt", StringComparison.Ordinal), "Raw paths not logged literally.");
                Require(File.Exists(s.GearFile) && Rank.Ranks.Count == 0, "Normalized path read or rank called after missing gear.");
                break;
            case "MissingGear":
                s.RemoveGear();
                Table.Load(s.Prefix);
                Require(!ReferenceEquals(s.OldInfo, Table.Info) && Table.Info.Count == 0 && Rank.Ranks.Count == 0, "Missing file publication order.");
                Require(s.Log.ToString().Contains("System.IO.FileNotFoundException", StringComparison.Ordinal), "Missing main file must be caught/logged.");
                break;
            case "EmptyGear":
                s.Write("db-combat_gear.txt", "");
                Table.Load(s.Prefix);
                Require(!ReferenceEquals(s.OldInfo, Table.Info) && Table.Info.Count == 0 && Rank.Ranks.Count == 18, "Empty existing file still reaches ranks.");
                Require(s.Log.ToString() == "", "Unexpected empty-file log.");
                break;
            case "SharedGear":
                using (var lease = File.Open(s.GearFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Table.Load(s.Prefix);
                Require(!ReferenceEquals(s.OldInfo, Table.Info) && Table.Info.Count == 0 && Rank.Ranks.Count == 0, "Sharing fault publication order.");
                Require(s.Log.ToString().Contains("System.IO.IOException", StringComparison.Ordinal), "Actual sharing fault not observed.");
                break;
            case "LateBlank":
            case "LateFormat":
            case "LateOverflow":
                string bad = scenario == "LateBlank" ? "" : GearRow(17, 8, 9).TrimEnd('\n');
                if (scenario != "LateBlank") bad = bad[..bad.LastIndexOf("@@", StringComparison.Ordinal)] +
                    (scenario == "LateFormat" ? "@@bad" : "@@4294967296");
                s.Write("db-combat_gear.txt", GearRow() + bad + "\n" + GearRow(23, 9, 4));
                Table.Load(s.Prefix);
                Require(Table.Info.Count == 1 && Table.Info[2].Count == 1 && Table.Info[2][3].ID == 1, "Late parse fault must retain prior rows only.");
                Require(Rank.Ranks.Count == 0, "Rank must not run after main parse fault.");
                string exception = scenario == "LateBlank" ? "IndexOutOfRangeException" : scenario == "LateFormat" ? "FormatException" : "OverflowException";
                Require(s.Log.ToString().Contains(exception, StringComparison.Ordinal), "Expected native parse failure not logged.");
                break;
            case "DependenciesContinue":
                s.Write("db-daoqi_dict_type.txt", string.Join("@@", Enumerable.Range(1, 15)) + "\nbad\n");
                s.Write("db-dict_lottery.txt", "42@@1@@2@@3@@4@@5\n");
                s.Write("db-dict_collection.txt", "bad\n");
                Table.Load(s.Prefix);
                Require(!ReferenceEquals(s.OldJade, Jade.Info) && Jade.Info.Count == 1 && Jade.Info.ContainsKey(1), "Jade partial publication.");
                Require(!ReferenceEquals(s.OldLottery, Lottery.Info) && Lottery.Info[42].Value2 == 3, "Lottery not called after jade caught fault.");
                Require(!ReferenceEquals(s.OldCollection, Collection.Info) && Collection.Info.Count == 0, "Collection partial publication.");
                Require(Table.Info[2][3].ID == 1 && Rank.Ranks.Count == 18, "Dependency fault incorrectly aborted parent.");
                Require(s.Log.ToString().Contains("FormatException", StringComparison.Ordinal), "Dependency faults not logged.");
                break;
            case "RepeatedLoad":
            {
                var originalRow = s.OldInfo[999][999];
                Table.Load(s.Prefix);
                VerifyContracts();
                var first = Table.Info; var firstJade = Jade.Info; var firstLottery = Lottery.Info;
                var firstCollection = Collection.Info; var ranks = Rank.Ranks;
                var rankOwners = ranks.ToDictionary(pair => pair.Key, pair => pair.Value);
                var rankLists = ranks.ToDictionary(pair => pair.Key, pair => pair.Value.RankingList);
                var modelOwners = RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().ToDictionary(owner => owner.Type);
                var firstLevels = first[2];
                var firstRow = first[2][3];
                s.Write("db-combat_gear.txt", GearRow(17, 8, 9));
                s.Write("db-dict_lottery.txt", "42@@1@@2@@3@@4@@5\n");
                s.Log.GetStringBuilder().Clear();
                Table.m_Load = false;
                Table.Load(s.Prefix);
                Require(!ReferenceEquals(first, Table.Info) && !ReferenceEquals(s.OldInfo, Table.Info) &&
                    Table.Info.Count == 1 && Table.Info[8].Count == 1, "Unfrozen repeated registration must still publish the newly parsed Info graph.");
                uint[] replacementValues = [17, 8, 9, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19];
                Require(Values(Table.Info[8][9]).SequenceEqual(replacementValues), "Repeated load must parse all19replacement row fields.");
                Require(first.Count == 1 && ReferenceEquals(firstLevels, first[2]) && firstLevels.Count == 1 &&
                    ReferenceEquals(firstRow, firstLevels[3]) && Values(firstRow).SequenceEqual(Enumerable.Range(1, 19).Select(i => (uint)i)),
                    "Escaped first Info dictionary, level map and row must remain unchanged.");
                Require(s.OldInfo.Count == 1 && s.OldInfo[999].Count == 1 && ReferenceEquals(originalRow, s.OldInfo[999][999]) &&
                    originalRow.ID == 999 && originalRow.Skills is null, "Original escaped sentinel graph must survive both replacements.");
                Require(!ReferenceEquals(firstJade, Jade.Info) && !ReferenceEquals(firstLottery, Lottery.Info) &&
                    !ReferenceEquals(firstCollection, Collection.Info) && Jade.Info.Count == 0 && Collection.Info.Count == 0,
                    "Every dependency must republish before the repeated registration/file parse.");
                Require(firstJade.Count == 0 && firstLottery.Count == 0 && firstCollection.Count == 0 && Lottery.Info.Count == 1 &&
                    Lottery.Info[42].Id == 42 && Lottery.Info[42].Type == 1 && Lottery.Info[42].Value1 == 2 &&
                    Lottery.Info[42].Value2 == 3 && Lottery.Info[42].Value3 == 4 && Lottery.Info[42].rate == 5,
                    "New lottery row and escaped dependency owners must retain exact values.");
                Require(ReferenceEquals(ranks, Rank.Ranks) && ranks.Count == 18 && ranks.All(pair =>
                    ReferenceEquals(rankOwners[pair.Key], pair.Value) && ReferenceEquals(rankLists[pair.Key], pair.Value.RankingList) &&
                    pair.Value.MaxItems == 10 && pair.Value.RankingList.Count == 0), "Rank.Create duplicate must preserve every existing rank and list owner.");
                Require(s.Log.ToString().Contains("System.ArgumentException", StringComparison.Ordinal) &&
                    s.Log.ToString().Contains("Overall_Warrior", StringComparison.Ordinal) &&
                    s.Log.ToString().Contains("LegacyNativeCombatGearRankTable.Create", StringComparison.Ordinal),
                    "Repeated load's caught failure must be the later duplicate rank.Create, not registration.");
                Require(RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Count() == 12 && modelOwners.Count == 12 &&
                    modelOwners.All(pair => ReferenceEquals(pair.Value, RuntimeTypeModel.Default[pair.Key])),
                    "Unfrozen repeated Add must reuse all12actual MetaType owners.");
                VerifyContracts(scenario);
                details = ExpectDeferredDuplicateField(new Gear());
                VerifyContracts(scenario);
                break;
            }
            case "ConflictFirst":
            case "ConflictLate":
            {
                bool conflictLate = scenario == "ConflictLate";
                var oldRow = s.OldInfo[999][999];
                var initialRanks = Rank.Ranks;
                Type affected = conflictLate ? typeof(Dict) : typeof(Gear);
                var conflictMeta = RuntimeTypeModel.Default.Add(affected, false);
                conflictMeta.AddField(1, conflictLate ? "Done" : "UID").IsRequired = true;
                Table.Load(s.Prefix);
                Require(!ReferenceEquals(s.OldInfo, Table.Info) && Table.Info.Count == 1 && Table.Info[2].Count == 1 &&
                    Values(Table.Info[2][3]).SequenceEqual(Enumerable.Range(1, 19).Select(i => (uint)i)),
                    "Unfrozen conflicting field must not stop Info publication or its19column parse.");
                Require(s.OldInfo.Count == 1 && s.OldInfo[999].Count == 1 && ReferenceEquals(oldRow, s.OldInfo[999][999]) &&
                    oldRow.ID == 999 && oldRow.Skills is null, "Conflict load must preserve escaped original Info rows.");
                Require(!ReferenceEquals(s.OldJade, Jade.Info) && !ReferenceEquals(s.OldLottery, Lottery.Info) &&
                    !ReferenceEquals(s.OldCollection, Collection.Info) && Jade.Info.Count == 0 && Lottery.Info.Count == 0 &&
                    Collection.Info.Count == 0 && s.OldJade.Count == 1 && s.OldLottery.Count == 1 && s.OldCollection.Count == 1,
                    "Unfrozen conflict must republish all three dependency caches and retain their escaped graphs.");
                Require(ReferenceEquals(initialRanks, Rank.Ranks) && Rank.Ranks.Count == 18 && Rank.Ranks.All(pair =>
                    pair.Key == pair.Value._Type && pair.Value.MaxItems == 10 && pair.Value.RankingList.Count == 0),
                    "Unfrozen conflict must complete native rank creation on the same rank dictionary.");
                Require(s.Log.ToString() == "", "Unfrozen field conflict is accepted during Load and must not log an eager failure.");
                Require(RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Count() == 12 &&
                    ReferenceEquals(conflictMeta, RuntimeTypeModel.Default[affected]), "Conflicting MetaType owner must be retained among all12types.");
                VerifyContracts(scenario);
                details = ExpectDeferredDuplicateField(conflictLate ? (object)new Dict() : new Gear());
                VerifyContracts(scenario);
                Require(s.Log.ToString() == "", "Deferred serializer fault must not be misrepresented as a caught Load error.");
                break;
            }
            case "FrozenFirst":
                bool late = scenario == "ConflictLate";
                var meta = RuntimeTypeModel.Default.Add(late ? typeof(Dict) : typeof(Gear), false);
                meta.AddField(1, late ? "Done" : "UID").IsRequired = true;
                if (scenario == "FrozenFirst")
                {
                    using var frozen = new MemoryStream();
                    Serializer.Serialize(frozen, (object)new Gear());
                    Require(frozen.Length != 0, "Real pre-registration serializer must run.");
                }
                Table.Load(s.Prefix);
                Require(ReferenceEquals(s.OldInfo, Table.Info) && s.OldInfo[999][999].ID == 999, "Registry failure must retain old Info graph.");
                Require(!ReferenceEquals(s.OldJade, Jade.Info) && !ReferenceEquals(s.OldLottery, Lottery.Info) &&
                    !ReferenceEquals(s.OldCollection, Collection.Info), "All dependencies precede registration.");
                Require(Rank.Ranks.Count == 0 && s.Log.ToString().Length != 0, "Registration failure must be caught before ranks.");
                Require(RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Count() == (late ? 11 : 1), "Partial registration sequence changed.");
                if (late) Require(RuntimeTypeModel.Default[typeof(Gear)].GetFields().Length == 3, "Early real type was not registered before late conflict.");
                break;
            case "RankColumnsAndPrefix":
                string[] rankFields = ["101", "Wolf", "20", "104", "105", "106", "107", "108", "109", "110", "111",
                    "112", "113", "114", "115", "116", "117", "118", "119", "120", "Guild", "122"];
                s.Write("rank-CombatGearRank.txt", "Count=1\n" + string.Join('/', rankFields) + "\n");
                s.Write("db-CombatGearRank.txt", "Count=0\n");
                Table.Load(s.Prefix);
                Require(Rank.Ranks.Count == 18 && Rank.Ranks[Rank.Type.Dragonhowl].RankingList.Count == 1, "Configured rank prefix was not used.");
                var entry = Rank.Ranks[Rank.Type.Dragonhowl].RankingList[101];
                Require(RankValues(entry).SequenceEqual(rankFields), "All22rank columns or raw configured rank prefix.");
                Require(s.Log.ToString() == "", "Unexpected rank-column log.");
                break;
            case "RegisteredWire":
                Table.Load(s.Prefix);
                details = VerifyRegisteredWire();
                break;
            case "SeparatorFault":
                s.Write("db-combat_gear.txt", "1\t2@@" + string.Join("@@", Enumerable.Range(3, 17)));
                Table.Load(s.Prefix);
                Require(Table.Info.Count == 0 && Rank.Ranks.Count == 0 && s.Log.ToString().Contains("FormatException", StringComparison.Ordinal),
                    "Embedded tab must not be treated as native delimiter.");
                break;
            default: throw new InvalidOperationException("Unknown isolated gear-loader scenario.");
        }
        Require(Table.m_Load, "Load sets true before every fallible action and leaves it true.");
        if (scenario != "RawPrefix") Require(Table.Location == s.Prefix, "Raw Location assignment.");
        if (scenario != "FrozenFirst") VerifyContracts(scenario);
        return JsonSerializer.Serialize(new
        {
            Guard = guard, Scenario = scenario, Completed = true,
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            Data = new { Scenario = scenario, Passed = true, Loaded = Table.m_Load, Location = Table.Location,
                RegistryTypes = RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Count(),
                GearGroups = Table.Info.Count, RankFamilies = Rank.Ranks.Count, Log = s.Log.ToString(), Details = details }
        });
    }

    private static string[] RankValues(Rank.Entry e) => new[] { e.UID.ToString(), e.Name, ((uint)e.Type).ToString(), e.TotalPoints.ToString(),
        e.Class.ToString(), e.Level.ToString(), e.Mesh.ToString(), e.HairStyle.ToString(), e.Head.ToString(), e.Garment.ToString(),
        e.LeftWeapon.ToString(), e.LefttWeaponAccessory.ToString(), e.RightWeapon.ToString(), e.RightWeaponAccessory.ToString(),
        e.MountArmor.ToString(), e.Armor.ToString(), e.Wing.ToString(), e.WingPlus.ToString(), e.Title.ToString(), e.Flag.ToString(), e.GuildName, e.WeaponWar.ToString() };
}
