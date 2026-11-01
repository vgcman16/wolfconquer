using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyPreparedSpellFileWrite? PrepareAttributeDerivedFiles(WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed, LegacyOwnedPhysicalMetadataWitness metadata)
    {
        var desired = new List<LegacyDerivedSpellDesiredRow>();
        var data = context.WorldData;
        if (data is not null && proposed.Character.Class / 1000 is 2 or 4 or 7 or 13 or 14)
        {
            uint overall = LegacyEquipmentQueryExecution.NativePirateRank(data.CombatGearRanks, proposed.Character.Uid, 42);
            uint weekly = LegacyEquipmentQueryExecution.NativePirateRank(data.CombatGearRanks, proposed.Character.Uid, 40);
            uint rank = weekly is >= 1 and <= 10 ? weekly : overall;
            desired.Add(new(21_930, proposed.Character.Class is >= 7_000 and <= 7_051 && rank is >= 1 and <= 10
                ? LegacyDerivedSpellOrigins.Granted(21_930, checked((ushort)(11 - rank))) : null));
        }
        else if (LegacyDerivedSpellOrigins.Read(proposed.Character).ContainsKey(21_930)) desired.Add(new(21_930, null));
        byte venom = 0;
        foreach (var item in LegacyEquipmentQuerySelector.Select(proposed))
        {
            byte level = checked((byte)(item.MythSoul % 10));
            if (item.MythSoul != 0 && item.MythSoul % 1000 - level == 10) venom = checked((byte)(venom + level));
        }
        desired.Add(new(20_000, venom == 0 ? null : LegacyDerivedSpellOrigins.Granted(20_000, venom)));
        var projected = LegacyDerivedSpellReconciliation.Project(proposed.Character, proposed.PlayerSpells, desired);
        if (projected.Changes.Count == 0) return null;
        if (!PreparedAttributeRebuild.SameAttributeSpellRoot(context.PlayerSpellsRoot, binding.SpellsRoot) ||
            !binding.Recovery.IsReadyForCharacterSpells(binding.UsersRoot, binding.SpellsRoot))
            throw new InvalidOperationException("attributeRebuildSpellRouteUnavailable");
        foreach (var row in projected.Changes)
            if (row.After is { } grant)
            {
                metadata.RequireDerivedSpell(row.Id, grant.Level);
                _ = LegacySpellPacket.FromSnapshot(grant).ToFrameBytes();
            }
            else _ = new MsgActionPacket { ObjId = checked((uint)binding.Uid), DwParam = row.Id,
                Type = (MsgActionType)109 }.ToFrameBytes();
        // Capture the actual saved baseline now. The later real private query
        // must issue its own journal writer against the then-current Character.
        return binding.Store.PrepareSpellFileWrite(binding.Connection, expected, binding.Recovery, binding.UsersRoot, binding.SpellsRoot);
    }
}
