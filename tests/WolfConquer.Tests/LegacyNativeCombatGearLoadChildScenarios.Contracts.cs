using ProtoBuf;
using ProtoBuf.Meta;
using Gear = WolfConquer.Core.World.LegacyNativeMsgCombatGear;
using Opt = WolfConquer.Core.World.LegacyNativeMsgCombatGearOpt;
using Skin = WolfConquer.Core.World.LegacyNativeMsgCombatGearSkin;
using Dao = WolfConquer.Core.World.LegacyNativeMsgDaoQi;
using Air = WolfConquer.Core.World.LegacyNativeMsgAirPowerUp;
using Dict = WolfConquer.Core.World.LegacyNativeMsgDictLottery;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private static void VerifyContracts()
    {
        (Type Type, string[] Names)[] expected =
        [
            (typeof(Gear), ["Type", "UID", "Items"]),
            (typeof(Gear.Item), ["ID", "Level", "Exp", "Equip", "Transform", "Data1", "Data2", "Data3", "Data4", "Data5", "Data6", "MasteryScore"]),
            (typeof(Opt), ["Type", "ItemID", "Transform", "UID", "Exp", "U", "SelectMaterial"]),
            (typeof(Opt.Items), ["UID", "Size"]),
            (typeof(Skin), ["Type", "Weapon"]),
            (typeof(Skin.WeaponInfo), ["ID", "Type", "Data1", "Data2", "Data3", "Data4", "Data5", "Data6", "Data7"]),
            (typeof(Dao), ["Type", "ItemID", "TaoistReiki", "LOTTERY", "Data5", "Data6", "MasteryPoints", "Data8", "PirateExp", "Data10", "Position", "Done", "Items", "Positions"]),
            (typeof(Dao.Item), ["Id"]),
            (typeof(Air), ["Type", "UserId", "PowerType", "Powers"]),
            (typeof(Air.Power), ["Id", "SenseLevel", "SenseAipPower", "ArmedLevel", "ArmedAipPower", "OverLoadLevel", "OverLoadAipPower", "Points"]),
            (typeof(Dict), ["Type", "Done", "Items2"]),
            (typeof(Dict.Lottery), ["Type", "Exp"])
        ];
        var actual = RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().ToArray();
        var expectedTypes = expected.Select(contract => contract.Type).ToHashSet();
        Require(expectedTypes.Count == 12 && expected.Sum(type => type.Names.Length) == 67,
            "Twelve actual message types and67fields required.");
        Type[] unrelatedNinja = [typeof(WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaUser),
            typeof(WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaItem),
            typeof(WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaItem.Item),
            typeof(WolfConquer.Core.World.LegacyNativeMsgRankMemberShow.MsgRankMemberShowProto),
            typeof(WolfConquer.Core.World.LegacyNativeMsgRankMemberShow.MsgRankMemberShowProto.Entry)];
        // Real attributed Ninja contracts may already coexist; their owners are not Pirate fields.
        Require(actual.Where(type => !expectedTypes.Contains(type.Type)).All(type => type.Type.IsEnum || unrelatedNinja.Contains(type.Type)),
            "Unexpected non-enum model registration.");
        int observedFields = 0;
        foreach (var contract in expected)
        {
            var meta = actual.Single(type => type.Type == contract.Type);
            var wanted = contract.Names.Select((name, index) => (Tag: index + 1, Name: name, Required: true)).ToList();
            var expectedFields = wanted.OrderBy(field => field.Tag).ThenBy(field => field.Name, StringComparer.Ordinal).ToArray();
            var fields = meta.GetFields().OrderBy(field => field.FieldNumber)
                .ThenBy(field => field.Member.Name, StringComparer.Ordinal).ToArray();
            Require(fields.Length == expectedFields.Length, "Actual registered field count for " + contract.Type.FullName);
            observedFields += fields.Length;
            for (int i = 0; i < fields.Length; i++)
                Require(fields[i].FieldNumber == expectedFields[i].Tag && fields[i].Member.Name == expectedFields[i].Name &&
                    fields[i].IsRequired == expectedFields[i].Required,
                    "Actual required tag/member/multiplicity mismatch: " + contract.Type.FullName + ":" + expectedFields[i].Tag);
        }
        Require(observedFields == 67, "Exact full native registry field total; repeated registration must not append duplicates.");
    }

    private static Dictionary<string, string> VerifyRegisteredWire()
    {
        VerifyContracts();
        (object Value, string Payload)[] controls =
        [
            (new Gear { Items = [new()] }, "080010001A18080010001800200028003000380040004800500058006000"),
            (new Opt { U = [0, 17, uint.MaxValue], SelectMaterial = [new()] }, "080010001800200028003000301130FFFFFFFF0F3A0408001000"),
            (new Skin { Weapon = [new()] }, "08001212080010001800200028003000380040004800"),
            (new Dao { Items = [new()], Positions = [0, 17, uint.MaxValue] }, "0800100018002000280030003800400048005000580060006A0208007000701170FFFFFFFF0F"),
            (new Air { Powers = [new()] }, "080010001800221008001000180020002800300038004000"),
            (new Dict { Items2 = [new()] }, "080010001A0408001000")
        ];
        var payloads = new Dictionary<string, string>();
        foreach (var control in controls)
        {
            using var first = new MemoryStream();
            Serializer.Serialize(first, control.Value); // Actual object-typed serializer and genuine Default.
            string payload = Convert.ToHexString(first.ToArray());
            Require(payload == control.Payload, "Registered literal payload: " + control.Value.GetType().Name);
            first.Position = 0;
            object restored = RuntimeTypeModel.Default.Deserialize(first, null, control.Value.GetType());
            Require(restored.GetType() == control.Value.GetType(), "Actual internal round-trip type mismatch.");
            using var second = new MemoryStream();
            Serializer.Serialize(second, restored);
            Require(second.ToArray().SequenceEqual(first.ToArray()), "Registered object round-trip bytes.");
            payloads.Add(control.Value.GetType().Name, payload);
        }
        VerifyContracts();
        return payloads;
    }
}
