using ProtoBuf.Meta;
using Opt = WolfConquer.Core.World.LegacyNativeMsgCombatGearOpt;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private static object RunPackedWireOptionScenario(Scope scope)
    {
        var before = new GraphSnapshot();
        // This is the real fresh Default and the correct native repeated UInt32
        // tag/member. Only its wire encoding option conflicts with the golden.
        var owner = RuntimeTypeModel.Default.Add(typeof(Opt), applyDefaultBehaviour: false);
        var field = owner.AddField(6, "U");
        field.IsRequired = true;
        field.IsPacked = true;
        Require(field.FieldNumber == 6 && field.Member.Name == "U" && field.IsRequired && field.IsPacked,
            "Actual correct-tag/member packed native field must be installed before Load.");

        Table.Load(scope.Prefix);

        RequireRejectedFirstLoad(scope, before);
        Require(ReferenceEquals(owner, RuntimeTypeModel.Default[typeof(Opt)]) &&
            owner.GetFields().Length == 1 && ReferenceEquals(field, owner.GetFields()[0]) && field.IsPacked && field.IsRequired,
            "Wire-option refusal must retain the caller's exact conflicting owner/field without resetting or rewriting it.");
        RequireNoProof();
        return new { Field = "U", Tag = 6, Packed = field.IsPacked, Refused = true };
    }
}
