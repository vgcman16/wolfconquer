using ProtoBuf;
using ProtoBuf.Meta;
using NativeGear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Gear = WolfConquer.Core.World.LegacyNativeMsgCombatGear;
using Opt = WolfConquer.Core.World.LegacyNativeMsgCombatGearOpt;
using Skin = WolfConquer.Core.World.LegacyNativeMsgCombatGearSkin;
using Dao = WolfConquer.Core.World.LegacyNativeMsgDaoQi;
using Air = WolfConquer.Core.World.LegacyNativeMsgAirPowerUp;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private static object RunEnumWireScenario(Scope scope, string scenario)
    {
        if (scenario == "EnumForeignMapped") return RefuseForeignMappedEnum(scope);
        Table.Load(scope.Prefix); RequireSuccessfulLoad(scope);
        var proof = RequireProof();
        var before = new GraphSnapshot();
        var model = CaptureModelFields();
        Type[] enums = [typeof(Gear.TypeID), typeof(NativeGear.TypeID), typeof(Opt.Types), typeof(Skin.TypeID), typeof(Dao.Types), typeof(Air.Types)];
        var owners = enums.ToDictionary(type => type, type => RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Single(owner => owner.Type == type));
        Require(owners.Count == 6 && owners.All(pair => pair.Key.IsEnum && pair.Key.IsDefined(typeof(FlagsAttribute), inherit: false) &&
            pair.Value.EnumPassthru && pair.Value.GetFields().Length == 0),
            "All six actual associated Flags enum owners must use unmapped numeric passthrough.");
        var literalPayloads = VerifyNonzeroEnumPayloads();
        _ = VerifyRegisteredWire();
        if (scenario == "EnumNonzeroLiterals")
        {
            before.RequireUnchanged(); RequireSameModelFields(model);
            Require(proof.IsCurrent() && ReferenceEquals(proof, RequireProof()), "Nonzero enum serialization must preserve the actual completed-load proof.");
            return literalPayloads;
        }
        Require(scenario is "EnumPassthruMutation" or "EnumMappingMutation", "Known visible enum-option scenario required.");
        var enumOwner = owners[typeof(Opt.Types)];
        Exception? rejected = null;
        try
        {
            if (scenario == "EnumMappingMutation") enumOwner.Add(1337, "UPGRADE_PIRATE");
            else enumOwner.EnumPassthru = false;
        }
        catch (Exception error) { rejected = error; }
        before.RequireUnchanged();
        Require(owners.All(pair => ReferenceEquals(pair.Value, RuntimeTypeModel.Default[pair.Key])),
            "Visible enum-option mutation must not replace any actual enum owner.");
        if (rejected is not null)
        {
            RequireSameModelFields(model);
            Require(enumOwner.EnumPassthru && enumOwner.GetFields().Length == 0 && proof.IsCurrent() && ReferenceEquals(proof, RequireProof()),
                "Rejected enum-option mutation must preserve the same current proof and numeric passthrough.");
            _ = VerifyNonzeroEnumPayloads(); _ = VerifyRegisteredWire();
        }
        else
        {
            Require((scenario == "EnumMappingMutation" ? enumOwner.GetFields().Any(field => field.FieldNumber == 1337) :
                !enumOwner.EnumPassthru) && !proof.IsCurrent(), "Accepted visible enum-option mutation must invalidate the held actual proof.");
            // This is the actual production proof preflight used by Pirate
            // reward capture; an old private owner reference cannot bypass it.
            Require(!Table.TryCaptureMailboxLoadProof(out var available) && available is null,
                "Pirate reward's current-complete-table preflight must refuse the mutated enum contract.");
        }
        return new { MutationRejected = rejected is not null, StaleProofRefused = rejected is null };
    }

    private static object RefuseForeignMappedEnum(Scope scope)
    {
        var before = new GraphSnapshot();
        var owner = RuntimeTypeModel.Default.Add(typeof(Opt.Types), applyDefaultBehaviour: false);
        owner.EnumPassthru = false;
        owner.Add(0, "UPGRADE");
        owner.Add(1337, "UPGRADE_PIRATE");
        var fields = owner.GetFields().OrderBy(field => field.FieldNumber).ToArray();
        Require(!owner.EnumPassthru && fields.Select(field => field.FieldNumber).SequenceEqual(new[] { 0, 1337 }),
            "Actual foreign enum-only remapping must exist before real Load.");
        var model = CaptureModelFields();
        Require(model.Count == 1 && model.ContainsKey(typeof(Opt.Types)), "No native DTO may be registered by this enum-only preseed.");

        Table.Load(scope.Prefix);

        RequireRejectedFirstLoad(scope, before); RequireSameModelFields(model);
        var actual = RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().ToArray();
        var retained = owner.GetFields().OrderBy(field => field.FieldNumber).ToArray();
        Require(actual.Length == 1 && ReferenceEquals(actual[0], owner) && !owner.EnumPassthru &&
            retained.Length == fields.Length && fields.Zip(retained).All(pair => ReferenceEquals(pair.First, pair.Second)),
            "Foreign associated enum must refuse before any DTO is added and preserve its exact original owner/mappings.");
        RequireNoProof();
        return new { EnumOnly = true, WireValues = fields.Select(field => field.FieldNumber).ToArray() };
    }

    private static Dictionary<string, string> VerifyNonzeroEnumPayloads()
    {
        // Literal native tags/required zeros and numeric enum values, written
        // independently of any produced serializer bytes. Pirate1=701 -> BD05.
        (object Value, string Payload)[] controls =
        [
            (new Gear { Type = Gear.TypeID.CrossServer, UID = 17, Items = [new() { ID = NativeGear.TypeID.Pirate1 }] },
                "080A10111A1908BD0510001800200028003000380040004800500058006000"),
            (new Opt { Type = Opt.Types.UPGRADE_PIRATE, ItemID = NativeGear.TypeID.Pirate1, UID = 17,
                U = [17], SelectMaterial = [new()] }, "080B10BD0518002011280030113A0408001000"),
            (new Skin { Type = 1, Weapon = [new() { ID = 701, Type = Skin.TypeID.HolyOmen }] },
                "0801121308BD0510021800200028003000380040004800"),
            (new Dao { Type = Dao.Types.PIRATE_POINTS, PirateExp = 17, Items = [new() { Id = 701 }], Positions = [17] },
                "080B100018002000280030003800400048115000580060006A0308BD057011"),
            (new Air { Type = Air.Types.ADDPOINTS, UserId = 17, Powers = [new() { Id = 7, Points = 19 }] },
                "080510111800221008071000180020002800300038004013"),
        ];
        var payloads = new Dictionary<string, string>();
        foreach (var control in controls)
        {
            string original = PacketState(control.Value);
            using var bytes = new MemoryStream();
            Serializer.Serialize(bytes, control.Value); // Genuine statically-object API and current actual Default.
            string actual = Convert.ToHexString(bytes.ToArray());
            Require(actual == control.Payload, "Nonzero six-enum literal payload mismatch: " + control.Value.GetType().Name);
            Require(original == PacketState(control.Value), "Actual nonzero enum serialization changed the original message values.");
            payloads.Add(control.Value.GetType().Name, actual);
        }
        return payloads;
    }
}
