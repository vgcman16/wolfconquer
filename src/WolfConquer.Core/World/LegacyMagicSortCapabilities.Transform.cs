namespace WolfConquer.Core.World;

public static partial class LegacyMagicSortCapabilities
{
    private const string TransformRuntimeSeam = "transform-runtime";

    private static bool TryClassifyTransformRuntime(
        LegacyMagicSort sort,
        ushort spellId,
        out LegacyMagicSortCapabilityInfo info)
    {
        if (!IsSupportedTransformRuntimeSpell(sort))
        {
            info = default;
            return false;
        }

        info = CreateInfo(
            sort,
            spellId,
            LegacyMagicSortCapability.StateAndBuffRuntime,
            LegacyMagicModelingStatus.FullyModeled,
            reason:
                "Transform sort 19 models the matching handler's pre-resource Fly/Ride rejection, resource and " +
                "cooldown verification, XPList consumption, TransformInfo-backed lookface/max-HP state, " +
                "caster animation and 300-point progress, strict timed expiry, lethal-hit finish, equipment " +
                "refresh, normal-HP restoration, peer spawn refresh, and finish-transform action.",
            sourceEvidence:
                "Matching DecompiledSource/GameServer/MrRayzo.Game.MsgServer.AttackHandler/Transform.cs and " +
                "DecompiledSource/GameServer/MrRayzo.Role/ClientTransform.cs define gameplay and ordering; " +
                "matching DBServer/TransformInfo.txt defines the available forms. Exact D13 packet builders " +
                "remain wire authority; no Source7770 behavior or opcodes are used.",
            suggestedNextOwner:
                "No next gameplay owner for sort 19; expand only if matching TransformInfo or decompiled behavior changes.",
            nextSeam: TransformRuntimeSeam);
        return true;
    }

    public static bool IsSupportedTransformRuntimeSpell(LegacyMagicSort sort)
    {
        return sort == LegacyMagicSort.Transform;
    }
}
