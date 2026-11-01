namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        // Pure prerequisites before this query's first live mutation or send.
        // Native query callbacks can change later state; the final Set path
        // repeats the required row lookup instead of granting a stale row.
        internal static bool CanPrepareDerivedSpells(WorldPacketHandlerContext context, WorldSession source, out string reason)
        {
            reason = string.Empty;
            IReadOnlyDictionary<ushort, ushort> origins;
            try
            {
                LegacyDerivedSpellOrigins.RequireRows(source.Character, source.PlayerSpells);
                origins = LegacyDerivedSpellOrigins.Read(source.Character);
            }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException)
            { reason = "derived-origin-input:" + error.GetType().Name; return false; }
            bool Unmarked(ushort id) => source.PlayerSpells.ContainsSpell(id) && !origins.ContainsKey(id);
            var data = context.WorldData;
            if (data is not null && source.Character.Class / 1000 is 2 or 4 or 7 or 13 or 14 &&
                source.Character.Class is >= 7_000 and <= 7_051 && !Unmarked(21_930))
            {
                uint overall = NativePirateRank(data.CombatGearRanks, source.Character.Uid, 42);
                uint weekly = NativePirateRank(data.CombatGearRanks, source.Character.Uid, 40);
                uint rank = weekly is >= 1 and <= 10 ? weekly : overall;
                if (rank is >= 1 and <= 10 &&
                    !LegacyMagicRuntime.TryResolveSpell(data, 21_930, checked((ushort)(11 - rank)), out _))
                { reason = "rank-spell-row-unavailable"; return false; }
            }
            uint venom = 0;
            foreach (var item in LegacyEquipmentQuerySelector.Select(source))
            {
                uint level = item.MythSoul % 10;
                if (item.MythSoul != 0 && item.MythSoul % 1000 - level == 10) venom += level;
                if (venom > byte.MaxValue) { reason = "venom-level-overflow"; return false; }
            }
            if (venom == 0 || Unmarked(20_000) || data is not null &&
                LegacyMagicRuntime.TryResolveSpell(data, 20_000, checked((ushort)venom), out _)) return true;
            reason = "venom-spell-row-unavailable"; return false;
        }

        internal void RefuseUnavailableDerivedSpell()
        {
            Admitted = false;
            Adopt(Current with { Loss = Current.Loss | LegacyEquipmentQueryPublicationLoss.RequiredDerivedSpellUnavailable });
        }
    }
}
