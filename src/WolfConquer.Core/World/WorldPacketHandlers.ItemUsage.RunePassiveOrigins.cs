using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static void RetireLegacyPassiveRuneOrigins(PlayerItemSnapshot before,
        Dictionary<uint, LegacyRuneMetadata> metadata, HashSet<ushort> providers,
        Dictionary<ushort, byte> origins, List<PlayerSpellSnapshotEntry> rows,
        List<LegacyRuneOperationEvent> output, LegacyRuneMappingSemantics semantics)
    {
        if (semantics != LegacyRuneMappingSemantics.RawSelf) return;
        // RequireRows has already proved every marked row is the exact original
        // default grant. The writer's full before-image validation also requires
        // byte11=0; this pure projection never establishes file ownership.
        var oldPassiveGrants = new HashSet<(ushort Id, byte Level)>();
        foreach (var item in before.RuneItems)
            if (IsLegacyRuneEquipSlot(item.ItemId, item.Position) &&
                metadata.TryGetValue(item.ItemId, out var source) &&
                LegacyRuneSpellIdentity.IsRawSelf(source.ItemId, source.RuneSpellId) &&
                !LegacyRuneSpellIdentity.IsNoSkill(source.ItemId, source.RuneSpellId, LegacyRuneMappingSemantics.LegacyDisplay))
                oldPassiveGrants.Add((checked((ushort)(source.RuneSpellId / 100)), checked((byte)(source.RuneSpellId % 100))));
        var retired = new HashSet<ushort>();
        foreach (var (id, level) in origins.ToArray())
        {
            if (providers.Contains(id) || !oldPassiveGrants.Contains((id, level))) continue;
            origins.Remove(id); retired.Add(id);
            output.Add(new(LegacyRuneOperationEventKind.RemoveSpell, 0, id));
        }
        if (retired.Count != 0) rows.RemoveAll(row => retired.Contains(row.Id));
    }
}
