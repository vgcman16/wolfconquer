namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        private sealed partial class OwnedPhysicalDeathBuilder
        {
            private void CalculateRuneHitpoints()
            {
                metadata.Extend(current);
                var projection = LegacyMaxHitpointsCalculator.CalculateQueryHitpointProjection(current, SourceItems, Data, alternate);
                AddMagic(LegacyPlayerStatusCalculator.NativeMagicRuneStorage(current, Data));
                AddMaximum(projection.RuneStorage);
                var blood = ReadAttributeUInt32(current.Character.Attributes, RuntimeLegacyBloodTideHitpointsAttribute);
                if (HasLegacyStatusFlag(current.Character, LegacyBloodTideFlag)) AddMaximum(blood);
                AddHitpoints(blood); // Source setter is unconditional, including0.
                if (HasLegacyStatusFlag(current.Character, LegacyEquipmentQueryRampageFlag))
                {
                    AddMaximum(120_000);
                    AddHitpoints(Maximum);
                }
                CalculateSacrificeHitpoints();
                if (HasLegacyStatusFlag(current.Character, LegacyFineRain1Flag))
                    AddMaximum(ReadAttributeUInt32(current.Character.Attributes, RuntimeLegacyFineRainPowerAttribute));
                if (HasLegacyStatusFlag(current.Character, LegacyRiseOfTaoismFlag))
                {
                    AddMaximum(50_000);
                    AddHitpoints(50_000);
                    AddMagic(new(13_000, 0, 0, 0));
                }
            }

            private void CalculateSacrificeHitpoints()
            {
                if (!HasLegacyStatusFlag(current.Character, LegacySacrificeFlag) || Data is not { } data ||
                    !TryRune("Sacrifice", out _) || !LegacyMagicRuntime.TryResolveLearnedSpell(current.PlayerSpells,
                        data, LegacySacrificeAttachStatusSpellId, out _, out var spell)) return;
                metadata.RequireRuntimeSpell(spell);
                uint power = 0;
                if (LegacyMagicResourceCostCalculator.TryGetEquippedRuneItem(current.PlayerItems, data.ItemsById,
                        "Sacrifice", alternate, current.Character.Attributes, current.Character.Class, out var rune) &&
                    rune is { ItemId: 4_010_701 }) power = rune.SocketProgress;
                if (power > spell.Damage) power = spell.Damage;
                ulong reduction = (ulong)Maximum * power / 100;
                if (reduction > Maximum) throw InvalidOwnedPhysicalDeath();
                SetMaximum(checked(Maximum - (uint)reduction));
                if (current.Character.MinHitPoints > Maximum) AssignHitpoints(RequireHitpoints(Maximum));
            }
        }
    }
}
