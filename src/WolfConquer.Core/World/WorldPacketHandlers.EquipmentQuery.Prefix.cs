using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        public PlayerItemSnapshot SourceItems => Session.PlayerItems with
        {
            EquipmentItems = CapturedEquipment,
        };

        public async ValueTask<bool> RunStatsPrefixAsync()
        {
            // Equip.UpdateStats consumes captured MyGear AFTER AppendItems.
            // Earned overlays do not enter its raw BT/CA rebuild projection.
            if (!Refresh()) return false;
            if (!LegacyNativePerfectionClient.BeginQuery(Context.Sessions, Context.Connection,
                    Request.Scope.OwnerUid, Request.Scope.OwnerNativeClientIdentity)) return false;
            var rawSource = Session with
            {
                PlayerItems = SourceItems,
                AlternateEquipmentActive = CapturedAlternate,
                PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(Session) with
                    { Breakthrough = null, Counteraction = null },
            };
            var rawFields = CalculateLegacyRuntimeStatusFields(rawSource, Context.WorldData);
            if (!Commit(session => session with
                {
                    PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(session) with
                    {
                        Breakthrough = rawFields[LegacyPlayerStatusPacket.BreakthroughIndex],
                        Counteraction = rawFields[LegacyPlayerStatusPacket.CounteractionIndex],
                        RidingCropItemId = 0, // Native Equip.UpdateStats619, BEFORE any item or Circle publication.
                        RightWeaponItemEffect = 0, // Native616. NecklaceEffect is NOT reset.
                        RingItemEffect = 0, // Native615.
                        MaxMana = 0, // Native new MsgStatus629; NOT current Character.MaxMana.
                    },
                    EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(Request.Scope.OwnerUid, 0),
                }))
                return false;

            if (!ResetNativeMagicQuery()) return false; // Source new Status629/Spirit633/Gems634.
            foreach (var item in CapturedEquipment)
            {
                if (!Refresh()) return false;
                if (!LegacyNativePerfectionClient.AddCapturedItem(Context.Sessions, Context.Connection,
                        Request.Scope.OwnerUid, item.PerfectionLevel,
                        Request.Scope.OwnerNativeClientIdentity)) return false; // Native653: CURRENT shared field.
                if (item.Durability == 0) continue;
                if (item.Position % 50 == 18 && !Commit(session => session with
                    {
                        PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(session) with
                            { RidingCropItemId = item.ItemId },
                    })) return false; // Native785–789 before this item's Circle828, even before item DB lookup.
                var effectSlot = item.Position % 50;
                if (effectSlot is 4 or 6 or 2 && !Commit(session => session with
                    {
                        PhysicalStatusOverlay = effectSlot switch
                        {
                            4 => LegacyPhysicalStatusOverlay.ForOwner(session) with { RightWeaponItemEffect = item.Effect },
                            6 => LegacyPhysicalStatusOverlay.ForOwner(session) with { RingItemEffect = item.Effect },
                            _ => LegacyPhysicalStatusOverlay.ForOwner(session) with { NecklaceItemEffect = item.Effect },
                        },
                    })) return false; // Native808/813/817 before this item's Circle828.
                if (!NativeMagicItemBeforeCircle(item)) return false; // Source AddGem819-820.
                if (Context.WorldData is { } data &&
                    TryGetLegacyEligibleEquippedRuneLevel(Session, data, "Circle of Life", out var circle) &&
                    circle is >= 1 and <= 9)
                {
                    var addition = LegacyMaxHitpointsCalculator.CalculateQueryCircleEnchantHitpoints(item, circle);
                    // Source Circle adds Max BEFORE HP, including enchant-derived0.
                    if (!AddMaximum(addition) || !await AddHitpointsAsync(addition).ConfigureAwait(false))
                        return false;
                }

                if (!NativeMagicItemAfterCircle(item)) return false; // Source item statuses AFTER Circle setter.
                if (!AddMaximum(LegacyMaxHitpointsCalculator.CalculateQueryItemHitpoints(
                        item, Context.WorldData, CapturedAlternate)))
                    return false;
                if (!AddManaMaximum(LegacyEquipmentNativeMana.Item(item, Context.WorldData))) return false;
                if (!CaptureNativePerfectionRelicLucky(item)) return false;
            }

            if (!Refresh()) return false;
            if (!NativeMagicBeforeFury()) return false; // Current Witchery/NatureChant precede Fury HP await.
            if (Context.WorldData is { } worldData &&
                HasLegacyStatusFlag(Session.Character, LegacyIronGuardFlag) &&
                TryGetLegacyEligibleEquippedRuneLevel(Session, worldData, "FuryStrike", out var fury))
            {
                var addition = fury switch
                {
                    >= 1 and <= 11 => 50_000U, 12 => 51_000U, 13 => 52_000U, 14 => 53_000U,
                    15 => 54_000U, 16 => 55_000U, 17 => 58_000U, 18 => 60_000U, 19 => 62_000U,
                    20 => 65_000U, 21 => 68_000U, 22 => 70_000U, 23 => 75_000U, 24 => 78_000U,
                    25 => 80_000U, 26 => 85_000U, 27 => 100_000U, _ => 0U,
                };
                // Source setter precedes its Max addition (even unsupported level0).
                if (!await AddHitpointsAsync(addition).ConfigureAwait(false) || !AddMaximum(addition))
                    return false;
            }

            if (!Refresh()) return false;
            var projection = LegacyMaxHitpointsCalculator.CalculateQueryHitpointProjection(
                Session, SourceItems, Context.WorldData, CapturedAlternate);
            if (!AddMaximum(projection.OtherBeforeVip)) return false;
            if (!RebuildNativePerfectionCache()) return false;
            if (!NativeMagicBeforeGear()) return false;
            if (!await RunNativeDerivedSpellsAsync().ConfigureAwait(false)) return false;
            if (!Refresh() || !AddMaximum(LegacyMaxHitpointsCalculator.CalculateAttributeHitpoints(
                    Session.Character.Class, Session.Character.Strength, Session.Character.Agility,
                    Session.Character.Vitality, Session.Character.Spirit)))
                return false; // Native1554: CURRENT attributes, before current Class/Spirit Mana1555.
            if (!Refresh() || !AddManaMaximum(LegacyEquipmentNativeMana.ClassAndSpirit(Session.Character)))
                return false; // Native1555 reads CURRENT Spirit/Class after both derived-spell awaits.
            if (ReadAttributeUInt32(Session.Character.Attributes, "VipLevel") == 7)
            {
                if (!await AddHitpointsAsync(3_000).ConfigureAwait(false) || !AddMaximum(3_000))
                    return false;
                if (!Refresh()) return false;
                if (!LegacyNativePerfectionClient.AddLucky(Context.Sessions, Context.Connection,
                        Request.Scope.OwnerUid, 3, Request.Scope.OwnerNativeClientIdentity)) return false;
            }

            if (!await RunVigorAndCreateSpawnPrefixAsync().ConfigureAwait(false))
                return false;
            if (!Refresh()) return false;
            projection = LegacyMaxHitpointsCalculator.CalculateQueryHitpointProjection(
                Session, SourceItems, Context.WorldData, CapturedAlternate);
            if (!AddMaximum(projection.SubClass)) return false;
            return NativeMagicLateProfiles() && AddJiangHuManaMaximum(); // Native1577–1582.
        }
    }
}
