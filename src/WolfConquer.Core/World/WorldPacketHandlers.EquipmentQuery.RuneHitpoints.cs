namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyEquipmentQueryRampageFlag = 0xD6;

    private sealed partial class LegacyEquipmentQueryExecution
    {
        public async ValueTask<bool> RunRuneHitpointsAsync()
        {
            if (!Refresh()) return false;
            var projection = LegacyMaxHitpointsCalculator.CalculateQueryHitpointProjection(
                Session, SourceItems, Context.WorldData, CapturedAlternate);
            if (!NativeMagicRuneStorage() || !AddMaximum(projection.RuneStorage)) return false;

            var blood = ReadAttributeUInt32(Session.Character.Attributes, RuntimeLegacyBloodTideHitpointsAttribute);
            if (HasLegacyStatusFlag(Session.Character, LegacyBloodTideFlag) && !AddMaximum(blood))
                return false;
            // Equip.AddRuneAtributes1782 is UNCONDITIONAL, including +=0 and retained field after flag removal.
            if (!await AddHitpointsAsync(blood).ConfigureAwait(false)) return false;

            if (HasLegacyStatusFlag(Session.Character, LegacyEquipmentQueryRampageFlag))
            {
                if (!AddMaximum(120_000) ||
                    !await AddHitpointsAsync(Maximum).ConfigureAwait(false))
                    return false;
            }

            if (!await ApplySacrificeHitpointsAsync().ConfigureAwait(false)) return false;
            if (!Refresh()) return false;
            if (HasLegacyStatusFlag(Session.Character, LegacyFineRain1Flag) &&
                !AddMaximum(ReadAttributeUInt32(Session.Character.Attributes, RuntimeLegacyFineRainPowerAttribute)))
                return false;

            if (HasLegacyStatusFlag(Session.Character, LegacyRiseOfTaoismFlag))
            {
                if (!AddMaximum(50_000) ||
                    !await AddHitpointsAsync(50_000).ConfigureAwait(false) ||
                    !AddNativeMagic(new LegacyNativeMagicQueryFields(13_000, 0, 0, 0)))
                    return false;
            }

            return Refresh();
        }

        private async ValueTask<bool> ApplySacrificeHitpointsAsync()
        {
            if (!Refresh()) return false;
            if (!HasLegacyStatusFlag(Session.Character, LegacySacrificeFlag) ||
                Context.WorldData is not { } data ||
                !TryGetLegacyEligibleEquippedRuneLevel(Session, data, "Sacrifice", out _) ||
                !LegacyMagicRuntime.TryResolveLearnedSpell(
                    Session.PlayerSpells, data, LegacySacrificeAttachStatusSpellId, out _, out var spell))
                return true;

            uint power = 0;
            if (LegacyMagicResourceCostCalculator.TryGetEquippedRuneItem(
                    Session.PlayerItems, data.ItemsById, "Sacrifice", Session.AlternateEquipmentActive,
                    Session.Character.Attributes, Session.Character.Class, out var rune) &&
                rune is { ItemId: 4_010_701 })
                power = rune.SocketProgress;
            if (power > spell.Damage) power = spell.Damage;
            var reduction = unchecked(Maximum * power) / 100;
            if (!SetMaximum(unchecked(Maximum - reduction))) return false;
            if (Session.Character.MinHitPoints > Maximum &&
                !await AssignAsync(unchecked((int)Maximum)).ConfigureAwait(false))
                return false;
            // Source also adds the reduction-derived physical-flat Status value.
            // This new HP phase owner does not claim that separate cached field.
            return Refresh();
        }
    }
}
