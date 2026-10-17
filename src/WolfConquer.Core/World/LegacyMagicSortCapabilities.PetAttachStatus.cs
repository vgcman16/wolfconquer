namespace WolfConquer.Core.World;

public static partial class LegacyMagicSortCapabilities
{
    private const ushort LegacyThundercloudPetAttachStatusSpellId = 12_970;
    private const string PetAttachStatusRuntimeSeam = "thundercloud-pet-attach-status-runtime";

    private static bool TryClassifyPetAttachStatusRuntime(
        LegacyMagicSort sort,
        ushort spellId,
        out LegacyMagicSortCapabilityInfo info)
    {
        if (!IsSupportedPetAttachStatusRuntimeSpell(sort, spellId))
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
                "This exact PetAttachStatus spell models matching Thunderbolt behavior after source resource " +
                "verification: interaction echo first, RevengeTail removal and status publication as an early " +
                "return, otherwise a caster-to-caster animation containing every owned visible floor " +
                "Thundercloud as a zero-damage hit target, duration-backed 500ms pet attack mode, and a " +
                "500-point spell-progress tick.",
            sourceEvidence:
                "Matching DecompiledSource/GameServer/MrRayzo.Game.MsgServer.AttackHandler/PetAttachStatus.cs " +
                "handles only spell 12970 under magic sort 89. Matching DBServer/magictype.txt identifies the " +
                "Thunderbolt row duration as 15 seconds. Exact D13 packet builders remain the local wire authority; " +
                "no Source7770 behavior or opcodes are used.",
            suggestedNextOwner:
                "No next gameplay owner for matching PetAttachStatus spell 12970; keep adjacent sort-89 spell IDs unsupported.",
            nextSeam: PetAttachStatusRuntimeSeam);
        return true;
    }

    public static bool IsSupportedPetAttachStatusRuntimeSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.PetAttachStatus &&
            spellId == LegacyThundercloudPetAttachStatusSpellId;
    }
}
