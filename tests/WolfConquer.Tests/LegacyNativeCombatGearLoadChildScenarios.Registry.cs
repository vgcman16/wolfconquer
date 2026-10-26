using ProtoBuf;
using ProtoBuf.Meta;
using Collection = WolfConquer.Core.Data.LegacyNativeDictCollectionTable;
using Jade = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using Lottery = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Gear = WolfConquer.Core.World.LegacyNativeMsgCombatGear;
using Dict = WolfConquer.Core.World.LegacyNativeMsgDictLottery;
using Ninja = WolfConquer.Core.World.LegacyNativeMsgNinja;
using Show = WolfConquer.Core.World.LegacyNativeMsgRankMemberShow.MsgRankMemberShowProto;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private static object? RunRegistryScenario(Scope scope, string scenario)
    {
        if (scenario == "RepeatedLoad") return RepeatedCompleteLoad(scope);
        if (scenario == "UnrelatedNinja")
        {
            foreach (object message in new object[] { new Ninja.MsgNinjaUser(), new Ninja.MsgNinjaItem { Items = [new()] }, new Show { Items = [new()] } })
            {
                using var bytes = new MemoryStream();
                Serializer.Serialize(bytes, message);
                Require(bytes.Length != 0, "Actual attributed Ninja contracts must be prepared by the real serializer.");
            }
            var ninjaOwners = RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().ToDictionary(owner => owner.Type);
            Table.Load(scope.Prefix);
            RequireSuccessfulLoad(scope);
            Require(ninjaOwners.All(pair => ReferenceEquals(pair.Value, RuntimeTypeModel.Default[pair.Key])),
                "Complete Pirate load must preserve every preexisting genuine Ninja model owner.");
            return VerifyRegisteredWire();
        }
        bool late = scenario is "ConflictLate" or "FrozenLate";
        bool frozen = scenario is "FrozenFirst" or "FrozenLate";
        Type affected = late ? typeof(Dict) : typeof(Gear);
        var meta = RuntimeTypeModel.Default.Add(affected, false);
        meta.AddField(1, frozen ? "Type" : late ? "Done" : "UID").IsRequired = true;
        if (frozen)
        {
            using var bytes = new MemoryStream();
            Serializer.Serialize(bytes, late ? (object)new Dict() : new Gear());
            Require(bytes.Length != 0, "Real incomplete pre-registration serializer must run before Load.");
        }
        var before = new GraphSnapshot();
        Table.Load(scope.Prefix);
        RequireRejectedFirstLoad(scope, before);
        Require(ReferenceEquals(meta, RuntimeTypeModel.Default[affected]) && meta.GetFields().Length == 1 &&
            meta.GetFields()[0].FieldNumber == 1 && meta.GetFields()[0].Member.Name == (frozen ? "Type" : late ? "Done" : "UID"),
            "Refusal must not reset or rewrite the caller's genuine conflicting/frozen metadata.");
        // A late frozen owner can leave irreversible earlier real registrations.
        // Neither a partial registry nor retrying Load is evidence of readiness.
        var afterFirst = RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().ToDictionary(owner => owner.Type,
            owner => (Owner: owner, Fields: owner.GetFields().Select(field => (field.FieldNumber, field.Member.Name, field.IsRequired)).ToArray()));
        Table.Load(scope.Prefix);
        before.RequireUnchanged(); RequireNoProof();
        Require(RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Count() == afterFirst.Count &&
            afterFirst.All(pair => ReferenceEquals(pair.Value.Owner, RuntimeTypeModel.Default[pair.Key]) &&
            pair.Value.Fields.SequenceEqual(RuntimeTypeModel.Default[pair.Key].GetFields()
                .Select(field => (field.FieldNumber, field.Member.Name, field.IsRequired)))),
            "Failed model retry must preserve already-created actual fields without duplicate registration.");
        return new { AffectedType = affected.FullName, Frozen = frozen, ActualTypes = afterFirst.Count };
    }

    private static object RepeatedCompleteLoad(Scope scope)
    {
        var originalRow = scope.OldInfo[999][999];
        Table.Load(scope.Prefix); RequireSuccessfulLoad(scope);
        var firstProof = RequireProof();
        var first = Table.Info; var firstJade = Jade.Info; var firstLottery = Lottery.Info;
        var firstCollection = Collection.Info; var firstRanks = Rank.Ranks;
        var firstLevels = first[2]; var firstRow = first[2][3];
        var rankOwners = firstRanks.ToDictionary(pair => pair.Key, pair => pair.Value);
        var rankLists = firstRanks.ToDictionary(pair => pair.Key, pair => pair.Value.RankingList);
        var modelOwners = RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().ToDictionary(owner => owner.Type);
        scope.Write("db-combat_gear.txt", GearRow(17, 8, 9));
        scope.Write("db-dict_lottery.txt", "42@@1@@2@@3@@4@@5\n");
        Table.Load(scope.Prefix); RequireSuccessfulLoad(scope);
        var secondProof = RequireProof();
        Require(!firstProof.IsCurrent() && !ReferenceEquals(firstProof, secondProof), "Successful reload must replace the prior graph proof.");
        Require(!ReferenceEquals(first, Table.Info) && Table.Info.Count == 1 && Table.Info[8].Count == 1 &&
            Values(Table.Info[8][9]).SequenceEqual(new uint[] { 17, 8, 9, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 }),
            "Repeated full load must atomically adopt the complete replacement19column graph.");
        Require(first.Count == 1 && ReferenceEquals(firstLevels, first[2]) && ReferenceEquals(firstRow, firstLevels[3]) &&
            Values(firstRow).SequenceEqual(Enumerable.Range(1, 19).Select(i => (uint)i)) &&
            ReferenceEquals(originalRow, scope.OldInfo[999][999]) && originalRow.ID == 999 && originalRow.Skills is null,
            "Both escaped prior gear graphs must remain unchanged.");
        Require(!ReferenceEquals(firstJade, Jade.Info) && !ReferenceEquals(firstLottery, Lottery.Info) &&
            !ReferenceEquals(firstCollection, Collection.Info) && !ReferenceEquals(firstRanks, Rank.Ranks) &&
            firstJade.Count == 0 && firstLottery.Count == 0 && firstCollection.Count == 0 &&
            Lottery.Info.Count == 1 && Lottery.Info[42].Id == 42 && Lottery.Info[42].Type == 1 &&
            Lottery.Info[42].Value1 == 2 && Lottery.Info[42].Value2 == 3 && Lottery.Info[42].Value3 == 4 && Lottery.Info[42].rate == 5,
            "Repeated load must publish all dependency families together and preserve escaped prior dictionaries.");
        Require(firstRanks.Count == 18 && firstRanks.All(pair => ReferenceEquals(rankOwners[pair.Key], pair.Value) &&
            ReferenceEquals(rankLists[pair.Key], pair.Value.RankingList) && pair.Value.RankingList.Count == 0 && pair.Value.MaxItems == 10),
            "Reload must preserve all escaped prior rank owners/lists.");
        Require(modelOwners.All(pair => ReferenceEquals(pair.Value, RuntimeTypeModel.Default[pair.Key])),
            "Repeated exact registration must reuse actual MetaType owners.");
        return VerifyRegisteredWire();
    }
}
