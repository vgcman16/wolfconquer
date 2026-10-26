using ProtoBuf.Meta;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    // Every enum below is source [Flags]; numeric passthrough preserves the
    // actual declared values and unknown flag combinations without remapping.
    private static readonly Type[] MailboxWireEnumTypes =
    [
        typeof(LegacyNativeMsgCombatGear.TypeID), typeof(LegacyNativeCombatGear.TypeID),
        typeof(LegacyNativeMsgCombatGearOpt.Types), typeof(LegacyNativeMsgCombatGearSkin.TypeID),
        typeof(LegacyNativeMsgDaoQi.Types), typeof(LegacyNativeMsgAirPowerUp.Types),
    ];
    private static MetaType[]? mailboxOwnedWireEnums;

    private static MetaType[] RegisterMailboxWireEnums()
    {
        var result = new MetaType[MailboxWireEnumTypes.Length];
        for (var index = 0; index < result.Length; index++)
        {
            var owner = RuntimeTypeModel.Default.Add(MailboxWireEnumTypes[index], applyDefaultBehaviour: false);
            owner.EnumPassthru = true;
            result[index] = owner;
        }
        // Actual DTO serialization in RegisterMessages traverses every enum
        // field and freezes these same owners before private provenance is kept.
        return result;
    }

    private static bool MailboxWireEnumsCurrent(MetaType[]? owners)
    {
        if (owners is null || owners.Length != MailboxWireEnumTypes.Length) return false;
        for (var index = 0; index < owners.Length; index++)
        {
            var owner = owners[index];
            if (!ReferenceEquals(owner, FindMailboxMeta(MailboxWireEnumTypes[index])) || !owner.EnumPassthru ||
                owner.GetFields().Length != 0 || owner.BaseType is not null || owner.HasCallbacks || owner.HasSubtypes) return false;
        }
        return true;
    }
}
