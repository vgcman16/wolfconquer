using System.Reflection;
using ProtoBuf;
using ProtoBuf.Meta;
using Gear = WolfConquer.Core.World.LegacyNativeMsgCombatGear;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private static object RunRegistrationProvenanceScenario(Scope scope, string scenario)
    {
        var before = new GraphSnapshot();
        if (scenario == "RegistrationOwned") return GenuineRegistrarThenLoad(scope, before);
        var gear = RuntimeTypeModel.Default.Add(typeof(Gear), applyDefaultBehaviour: false);
        var item = RuntimeTypeModel.Default.Add(typeof(Gear.Item), applyDefaultBehaviour: false);
        AddForeignRequiredFields(gear, ["Type", "UID", "Items"]);
        AddForeignRequiredFields(item, ["ID", "Level", "Exp", "Equip", "Transform", "Data1", "Data2", "Data3", "Data4", "Data5", "Data6", "MasteryScore"]);
        if (scenario == "RegistrationForeignExactFrozen")
        {
            object message = new Gear { Items = [new()] };
            using var bytes = new MemoryStream();
            Serializer.Serialize(bytes, message);
            Require(Convert.ToHexString(bytes.ToArray()) == "080010001A18080010001800200028003000380040004800500058006000",
                "The actual foreign exact contract must really serialize/freeze with the native literal bytes.");
        }
        else Require(scenario == "RegistrationForeignExact", "Known foreign provenance control required.");
        var foreign = CaptureModelFields();

        Table.Load(scope.Prefix);

        RequireRejectedFirstLoad(scope, before);
        Require(ReferenceEquals(gear, RuntimeTypeModel.Default[typeof(Gear)]) &&
            ReferenceEquals(item, RuntimeTypeModel.Default[typeof(Gear.Item)]), "Foreign exact owner references must remain unchanged after refusal.");
        RequireSameModelFields(foreign);
        RequireNoProof();
        return new { ForeignExact = true, Frozen = scenario.EndsWith("Frozen", StringComparison.Ordinal) };
    }

    private static object GenuineRegistrarThenLoad(Scope scope, GraphSnapshot before)
    {
        var registrar = typeof(Table).GetMethod("RegisterMessages", BindingFlags.Static | BindingFlags.NonPublic);
        Require(registrar is not null, "Actual private production registrar ABI must remain available.");
        registrar!.Invoke(null, null);
        before.RequireUnchanged(); RequireNoProof(); VerifyContracts();
        var owned = CaptureModelFields();
        registrar.Invoke(null, null);
        before.RequireUnchanged(); RequireNoProof(); VerifyContracts(); RequireSameModelFields(owned);
        var bytes = VerifyRegisteredWire();
        before.RequireUnchanged(); RequireNoProof();

        Table.Load(scope.Prefix); RequireSuccessfulLoad(scope);
        var proof = RequireProof();
        var published = new GraphSnapshot();
        registrar.Invoke(null, null);
        published.RequireUnchanged(); RequireSameModelFields(owned);
        Require(proof.IsCurrent() && ReferenceEquals(proof, RequireProof()), "Actual registrar repeat must retain the genuine current graph proof.");
        var first = Table.Info; var firstRow = first[2][3]; var firstSkills = firstRow.Skills;
        scope.Write("db-combat_gear.txt", GearRow(17, 8, 9));
        Table.Load(scope.Prefix); RequireSuccessfulLoad(scope); RequireSameModelFields(owned);
        Require(!proof.IsCurrent() && !ReferenceEquals(proof, RequireProof()) &&
            Table.Info[8][9].ID == 17 && ReferenceEquals(firstRow, first[2][3]) && ReferenceEquals(firstSkills, firstRow.Skills) &&
            Values(firstRow).SequenceEqual(Enumerable.Range(1, 19).Select(i => (uint)i)),
            "Load/reload through the privately retained genuine registrar must preserve prior aliases and replace only complete publications.");
        return bytes;
    }

    private static void AddForeignRequiredFields(MetaType owner, string[] names)
    {
        for (int index = 0; index < names.Length; index++) owner.AddField(index + 1, names[index]).IsRequired = true;
    }

    private sealed record ModelFields(MetaType Owner, (int Tag, string Name, bool Required)[] Fields);

    private static Dictionary<Type, ModelFields> CaptureModelFields() => RuntimeTypeModel.Default.GetTypes().Cast<MetaType>()
        .ToDictionary(owner => owner.Type, owner => new ModelFields(owner, owner.GetFields()
            .Select(field => (field.FieldNumber, field.Member?.Name ?? field.Name ?? "", field.IsRequired)).ToArray()));

    private static void RequireSameModelFields(Dictionary<Type, ModelFields> before)
    {
        var current = RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().ToDictionary(owner => owner.Type);
        Require(current.Count == before.Count && before.All(pair => current.TryGetValue(pair.Key, out var owner) &&
            ReferenceEquals(owner, pair.Value.Owner) && pair.Value.Fields.SequenceEqual(owner.GetFields()
                .Select(field => (field.FieldNumber, field.Member?.Name ?? field.Name ?? "", field.IsRequired)))),
            "Real registration retry/refusal must preserve every existing model owner and exact field sequence.");
    }
}
