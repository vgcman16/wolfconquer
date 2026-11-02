using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        private sealed partial class OwnedPhysicalDeathBuilder
        {
            private void CalculateEquipmentQuery()
            {
                var admission = LegacyEquipmentRebuildAdmission.ApplyCurrent(current,
                    LegacyEquipmentRebuildReason.PlayerDeath, context.LegacyClock ?? LegacySignedMillisecondClock.System);
                admitted = admission.Admitted;
                current = current with
                {
                    FineRainEquipmentAdmission = admission.Session.FineRainEquipmentAdmission,
                    PhysicalStatusOverlay = admitted ? current.PhysicalStatusOverlay : admission.Session.PhysicalStatusOverlay,
                };
                querySource = current;
                metadata.Extend(current);
                if (!admitted)
                {
                    AppendSelf(LegacyMsgMessagePacket.SystemTalk("You Shoud wait 6 seconds.").ToFrameBytes());
                    return;
                }
                equipment = LegacyEquipmentQuerySelector.Select(querySource).ToArray();
                alternate = querySource.AlternateEquipmentActive;
                AppendSelf(LegacyShowEquipmentPacket.CreateEquipmentRefresh(SourceItems, alternate).ToFrameBytes());
                CalculateStatsPrefix();
                CalculateRuneHitpoints();
                var fields = CalculateLegacyRuntimeStatusFields(current, Data);
                fields[LegacyPlayerStatusPacket.MagicAttackIndex] = magic.MagicAttack;
                fields[LegacyPlayerStatusPacket.MagicPercentIndex] = magic.MagicPercent;
                fields[LegacyPlayerStatusPacket.SkillCStrikeIndex] = magic.SkillCStrike;
                fields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex] = magic.MagicDamageIncrease;
                var queryStatus = new LegacyPlayerStatusPacket(fields);
                queryCombat = CombatFields.From(queryStatus);
                AppendSelf(queryStatus.ToFrameBytes());
                AssignHitpoints(RequireHitpoints(Math.Min(current.Character.MinHitPoints, (long)Maximum)));
                if (LegacyTransformState.TryGetActiveMaxHitpoints(current.Character, out var transform))
                {
                    AssignHitpoints(RequireHitpoints(transform));
                    SetMaximum(transform);
                }
                AppendHitpointPair();
                AppendSelf(LegacyUserTotalRefineLevPacket.FromEquipmentPerfection(Uid, current.PlayerItems).ToFrameBytes());
                PrepareWindWalkerPostlude();
            }

            private void CalculateStatsPrefix()
            {
                metadata.RequireNativeQueryInputs(current);
                var rawSource = current with
                {
                    PlayerItems = SourceItems, AlternateEquipmentActive = alternate,
                    PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(current) with
                        { Breakthrough = null, Counteraction = null },
                };
                var raw = CalculateLegacyRuntimeStatusFields(rawSource, Data);
                current = current with
                {
                    PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(current) with
                    {
                        Breakthrough = raw[LegacyPlayerStatusPacket.BreakthroughIndex],
                        Counteraction = raw[LegacyPlayerStatusPacket.CounteractionIndex],
                        RidingCropItemId = 0, RightWeaponItemEffect = 0, RingItemEffect = 0, MaxMana = 0,
                    },
                };
                SetMaximum(0);
                magic = new(checked((uint)current.Character.Spirit), 0, 0, 0);
                prestige = 0;
                foreach (var item in equipment)
                {
                    prestige = checked(prestige + item.PerfectionLevel);
                    if (item.Durability == 0) continue;
                    CalculateDurableItem(item);
                }
                CalculateBeforeFury();
                CalculateFuryHitpoints();
                var projection = LegacyMaxHitpointsCalculator.CalculateQueryHitpointProjection(current, SourceItems, Data, alternate);
                AddMaximum(projection.OtherBeforeVip);
                if (prestige > int.MaxValue) throw InvalidOwnedPhysicalDeath();
                perfection = LegacyNativePerfectionSnapshot.FromPrestige(prestige, Rune);
                current = current with
                {
                    PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(current) with
                        { PerfectionToxinEraser = perfection.ToxinEraser },
                };
                CalculateBeforeGear();
                CalculateDerivedSpells();
                metadata.Extend(current);
                AddMaximum(LegacyMaxHitpointsCalculator.CalculateAttributeHitpoints(current.Character.Class,
                    current.Character.Strength, current.Character.Agility, current.Character.Vitality, current.Character.Spirit));
                AddMana(LegacyEquipmentNativeMana.ClassAndSpirit(current.Character));
                if (ReadAttributeUInt32(current.Character.Attributes, "VipLevel") == 7)
                {
                    AddHitpoints(3_000);
                    AddMaximum(3_000);
                    perfection = perfection with { LuckyStrike = perfection.LuckyStrike + 3 };
                }
                CalculateVigorAndAppearance();
                projection = LegacyMaxHitpointsCalculator.CalculateQueryHitpointProjection(current, SourceItems, Data, alternate);
                AddMaximum(projection.SubClass);
                AddMagic(NativeMagicSubClass(current, Data));
                if (!HasLegacyPlayerMagicInterserverAttribute(current)) jiangHu = NativeMagicJiangHu(current, Data);
                AddMagic(jiangHu);
                var mana = HasLegacyPlayerMagicInterserverAttribute(current)
                    ? LegacyPhysicalStatusOverlay.ForOwner(current).JiangHuMaxMana ?? 0
                    : LegacyEquipmentNativeMana.JiangHu(current.Character, Data);
                current = current with
                {
                    PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(current) with { JiangHuMaxMana = mana },
                };
                AddMana(mana);
            }

            private void CalculateDurableItem(PlayerItemSnapshotItem item)
            {
                var slot = item.Position % 50;
                var overlay = LegacyPhysicalStatusOverlay.ForOwner(current);
                overlay = slot switch
                {
                    18 => overlay with { RidingCropItemId = item.ItemId },
                    4 => overlay with { RightWeaponItemEffect = item.Effect },
                    6 => overlay with { RingItemEffect = item.Effect },
                    2 => overlay with { NecklaceItemEffect = item.Effect },
                    _ => overlay,
                };
                current = current with { PhysicalStatusOverlay = overlay };
                magic = slot switch
                {
                    4 => magic with { RightWeaponItemId = item.ItemId },
                    5 => magic with { LeftWeaponItemId = item.ItemId },
                    _ => magic,
                };
                magic = LegacyPlayerStatusCalculator.NativeMagicItemGems(magic, item);
                var circle = Rune("Circle of Life");
                if (circle is >= 1 and <= 9)
                {
                    var addition = LegacyMaxHitpointsCalculator.CalculateQueryCircleEnchantHitpoints(item, circle);
                    AddMaximum(addition);
                    AddHitpoints(addition);
                }
                AddMagic(LegacyPlayerStatusCalculator.NativeMagicItemAfterCircle(item, Data));
                AddMaximum(LegacyMaxHitpointsCalculator.CalculateQueryItemHitpoints(item, Data, alternate));
                AddMana(LegacyEquipmentNativeMana.Item(item, Data));
                // Relic lucky is discarded by the later complete perfection
                // rebuild. No intermediate mutable cache is published here.
            }
        }
    }
}
