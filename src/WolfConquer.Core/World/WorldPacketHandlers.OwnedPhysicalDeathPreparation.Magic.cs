using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        private sealed partial class OwnedPhysicalDeathBuilder
        {
            private void CalculateBeforeFury()
            {
                if (TryRune("witchery", out var witchery) && magic.MagicAttack < 15_000)
                {
                    var addition = witchery switch
                    {
                        1 => 2_000U, 2 => 2_300U, 3 => 2_600U, 4 => 3_000U, 5 => 3_500U,
                        6 => 4_000U, 7 => 4_500U, 8 => 5_000U, 9 => 6_000U, _ => 0U,
                    };
                    magic = magic with { MagicAttack = Math.Min(15_000U, checked(magic.MagicAttack + addition)) };
                }
                if (TryRune("Nature`sChant", out var chant))
                {
                    var percent = chant switch
                    { 1 => 13U, 2 => 14U, 3 => 15U, 4 => 16U, 5 => 17U, 6 => 18U, 7 => 19U, 8 => 22U, 9 => 24U, _ => 0U };
                    magic = magic with { MagicAttack = checked(magic.MagicAttack + magic.MagicAttack / 100 * percent) };
                }
            }

            private void CalculateFuryHitpoints()
            {
                if (!HasLegacyStatusFlag(current.Character, LegacyIronGuardFlag) || !TryRune("FuryStrike", out var fury)) return;
                var addition = fury switch
                {
                    >= 1 and <= 11 => 50_000U, 12 => 51_000U, 13 => 52_000U, 14 => 53_000U,
                    15 => 54_000U, 16 => 55_000U, 17 => 58_000U, 18 => 60_000U, 19 => 62_000U,
                    20 => 65_000U, 21 => 68_000U, 22 => 70_000U, 23 => 75_000U, 24 => 78_000U,
                    25 => 80_000U, 26 => 85_000U, 27 => 100_000U, _ => 0U,
                };
                // This source assignment precedes its maximum addition.
                AddHitpoints(addition);
                AddMaximum(addition);
            }

            private void CalculateBeforeGear()
            {
                var wardrobe = EnumerateLegacyWardrobeItems(current.PlayerItems).ToArray();
                bool mount = wardrobe.Where(item => ItemType.GetLegacyEquipmentPosition(item.ItemId) == 17)
                    .Select(item => item.Uid).Distinct().Count() > 100;
                bool garment = wardrobe.Where(item => ItemType.GetLegacyEquipmentPosition(item.ItemId) == 9)
                    .Select(item => item.Uid).Distinct().Count() > 100;
                magic = magic with
                {
                    MagicAttack = checked(magic.MagicAttack + (mount ? 1_000U : 0U) + (garment ? 600U : 0U) + 1),
                    MagicDamageIncrease = checked(magic.MagicDamageIncrease + magic.ThunderGemValue),
                };
                AddMagic(LegacyPlayerStatusCalculator.NativeMagicBeforeGear(current, Data, prestige));
                AddMagic(NativeMagicInnerPower(current, Data));
            }

            private void CalculateVigorAndAppearance()
            {
                var maximum = LegacyMaxHitpointsCalculator.CalculateQueryMaxVigor(equipment, Data);
                if (maximum < 0) throw InvalidOwnedPhysicalDeath();
                var vigor = checked((ushort)Math.Min(ReadAttributeUInt32(current.Character.Attributes, "Vigor"), (uint)maximum));
                current = current with { Character = WithCharacterAttribute(current.Character, "Vigor", vigor) };
                AppendSelf(new LegacyServerInfoPacket(vigor).ToFrameBytes());
                // Historical output is calculated before holder publication.
                // Keep its full Action validation, then use the data codec;
                // this private death plan separately proves both live owners.
                uint action = LegacyActionState.RequireCurrent(current.Character);
                var appearance = WithCharacterAttribute(current.Character, "Action", action);
                frames.Add(new(OwnedPhysicalDeathAudience.Visible,
                    LegacyPlayerSpawnPacket.FromSnapshot(appearance, SourceItems, alternate, maxLife: Maximum).ToFrameBytes(), current));
            }

            private bool TryRune(string name, out byte level)
            {
                level = 0;
                return Data is { } data && TryGetLegacyEligibleEquippedRuneLevel(current, data, name, out level);
            }
        }
    }
}
