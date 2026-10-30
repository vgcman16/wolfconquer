using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static PlayerSpellSnapshotEntry? ResolveRecordedRuneSpell(LegacyRuneMetadata metadata,
        LegacyRuneMappingSemantics semantics = LegacyRuneMappingSemantics.RawSelf)
    {
        var display = LegacyGameItemPacket.CreateRuneSpellOverride(metadata.ItemId, metadata.RuneSpellId);
        if (display is null) throw InvalidSpellMutation();
        if (LegacyRuneSpellIdentity.IsNoSkill(metadata.ItemId, metadata.RuneSpellId, semantics))
        {
            if (metadata.IsRuneSkill is not null) throw InvalidSpellMutation();
            return null;
        }
        if (metadata.IsRuneSkill is null) throw InvalidSpellMutation();
        var id = metadata.RuneSpellId / 100;
        if (id is 0 or > ushort.MaxValue) throw InvalidSpellMutation();
        return LegacyRuneSpellOrigins.Granted(checked((ushort)id), checked((byte)(metadata.RuneSpellId % 100)));
    }
}
