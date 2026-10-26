using System.Reflection;
using ProtoBuf;
using ProtoBuf.Meta;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Opt = WolfConquer.Core.World.LegacyNativeMsgCombatGearOpt;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private static object RunFrozenRegistrationMutationScenario(Scope scope, string scenario)
    {
        Table.Load(scope.Prefix); RequireSuccessfulLoad(scope);
        var proof = RequireProof();
        _ = VerifyRegisteredWire();
        var before = new GraphSnapshot();
        var model = CaptureModelFields();
        var owner = RuntimeTypeModel.Default[typeof(Opt)];
        Require(scenario is "RegistrationFrozenFactory" or "RegistrationFrozenSurrogate" or "RegistrationFrozenPacked", "Known frozen mutation required.");
        var factory = typeof(LegacyNativeCombatGearLoadChildScenarios).GetMethod(nameof(CreateProvenanceOpt), BindingFlags.Public | BindingFlags.Static);
        Require(factory is not null && factory.ReturnType == typeof(Opt) && factory.GetParameters().Length == 0,
            "A real static no-argument factory with the exact native return type is required.");
        var conversions = typeof(ProvenanceOptSurrogate).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "op_Implicit").ToArray();
        Require(conversions.Any(method => method.ReturnType == typeof(Opt) && method.GetParameters() is [var argument] && argument.ParameterType == typeof(ProvenanceOptSurrogate)) &&
            conversions.Any(method => method.ReturnType == typeof(ProvenanceOptSurrogate) && method.GetParameters() is [var argument] && argument.ParameterType == typeof(Opt)),
            "The actual surrogate must have both legitimate public native conversion signatures.");
        Exception? refusal = null;
        try
        {
            switch (scenario)
            {
                case "RegistrationFrozenFactory":
                    owner.SetFactory(factory!); break;
                case "RegistrationFrozenSurrogate": owner.SetSurrogate(typeof(ProvenanceOptSurrogate)); break;
                case "RegistrationFrozenPacked": owner.GetFields().Single(field => field.FieldNumber == 6).IsPacked = true; break;
                default: throw new InvalidOperationException("Unknown frozen registration mutation control.");
            }
        }
        catch (Exception error) { refusal = error; }
        Require(refusal is not null,
            "Actual serialized/frozen owned native contract accepted mutation; private provenance is insufficient: " + scenario);
        before.RequireUnchanged(); RequireSameModelFields(model);
        Require(proof.IsCurrent() && ReferenceEquals(proof, RequireProof()), "Rejected actual metadata mutation must preserve the exact current load proof.");
        _ = VerifyRegisteredWire();
        RequireSuccessfulLoad(scope);
        return new { Rejected = true, ExceptionType = refusal!.GetType().FullName };
    }

    // Real documented SetFactory argument. It must never be adopted or invoked:
    // the test requires the mutation of the actual frozen owner to be rejected.
    public static Opt CreateProvenanceOpt() => new();

    [ProtoContract]
    public sealed class ProvenanceOptSurrogate
    {
        [ProtoMember(1, IsRequired = true)] public Opt.Types Type;
        [ProtoMember(2, IsRequired = true)] public Gear.TypeID ItemID;
        [ProtoMember(3, IsRequired = true)] public uint Transform;
        [ProtoMember(4, IsRequired = true)] public uint UID;
        [ProtoMember(5, IsRequired = true)] public uint Exp;
        [ProtoMember(6, IsRequired = true)] public uint[]? U;
        [ProtoMember(7, IsRequired = true)] public Opt.Items[]? SelectMaterial;

        public static implicit operator ProvenanceOptSurrogate(Opt value) => new()
        {
            Type = value.Type, ItemID = value.ItemID, Transform = value.Transform, UID = value.UID,
            Exp = value.Exp, U = value.U, SelectMaterial = value.SelectMaterial,
        };

        public static implicit operator Opt(ProvenanceOptSurrogate value) => new()
        {
            Type = value.Type, ItemID = value.ItemID, Transform = value.Transform, UID = value.UID,
            Exp = value.Exp, U = value.U!, SelectMaterial = value.SelectMaterial!,
        };
    }
}
