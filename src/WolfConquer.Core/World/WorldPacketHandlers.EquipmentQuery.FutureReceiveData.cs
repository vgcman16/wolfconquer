using System.Collections.ObjectModel;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Future calculation DATA only. No epoch, native row, query completion,
    // binding, file permit or receive permission is issued by this projection.
    // A real later query must independently produce and match these values.
    internal sealed class FutureEquipmentReceiveData
    {
        private static readonly string[] QueryRuneNames =
        [
            "Circle of Life", "FuryStrike", "witchery", "Nature`sChant", "Sacrifice",
            "Sanctity", "FireBlast", "Serenity", "DrainingTouchBooster",
            "BloodSpawnBooster", "FreeSoulBooster", "EnormousCrusher",
        ];

        internal WorldSession Session { get; }
        internal int QueryHitpoints { get; }
        internal uint MaximumHitpoints { get; }
        internal uint MaximumMana { get; }
        internal LegacyNativeMagicQueryFields Magic { get; }
        internal LegacyNativeMagicQueryFields JiangHu { get; }
        internal LegacyNativePerfectionSnapshot Perfection { get; }
        internal uint Prestige { get; }
        internal LegacyDerivedSpellReconciliation.Model Derived { get; }
        internal bool RunesEligible { get; }
        // Only names actually read by the query/perfection pipeline. Absence
        // differs from a selected level-zero row; this is not a worn-rune owner.
        internal IReadOnlyDictionary<string, byte> EligibleRunes { get; }

        private FutureEquipmentReceiveData(WorldSession session, LegacyEquipmentQueryHitpointReplay hp,
            EquipmentQueryMagicData magic, LegacyNativePerfectionSnapshot perfection,
            LegacyDerivedSpellReconciliation.Model derived, bool eligible, Dictionary<string, byte> runes)
        {
            Session = session; QueryHitpoints = hp.Current; MaximumHitpoints = hp.Maximum;
            MaximumMana = magic.Mana; Magic = magic.Fields; JiangHu = magic.JiangHu;
            Prestige = magic.Prestige; Perfection = perfection; Derived = derived;
            RunesEligible = eligible; EligibleRunes = new ReadOnlyDictionary<string, byte>(runes);
        }

        internal static FutureEquipmentReceiveData Project(WorldSession source, WorldDataCatalog catalog,
            ILegacySignedMillisecondClock clock, LegacyOwnedPhysicalMetadataWitness.AdmissionQueryData metadata,
            ConquerProtocolProfile profile)
        {
            ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(clock); ArgumentNullException.ThrowIfNull(metadata);
            if (profile != ConquerProtocolProfile.VisibleDx8D13 || !metadata.IsCurrentFor(source, catalog) ||
                HasLegacyPlayerMagicInterserverAttribute(source))
                throw Invalid("futureQueryInputUnavailable");
            var admission = LegacyEquipmentRebuildAdmission.ApplyCurrent(source, LegacyEquipmentRebuildReason.LevelUp, clock);
            if (!admission.Admitted)
                throw Invalid("futureQueryAdmissionUnavailable");
            return ProjectAdmitted(source, catalog, metadata, profile, admission, restoreHitpoints: true);
        }

        // PlayerDeath uses the same full arithmetic but does not perform the
        // UpdateLevel HP=max restoration. A denied FineRain query retains the
        // explicitly supplied prior query DATA in the death projection caller.
        internal static FutureEquipmentReceiveData? ProjectPlayerDeathQuery(WorldSession source,
            WorldDataCatalog catalog, ILegacySignedMillisecondClock clock,
            LegacyOwnedPhysicalMetadataWitness.AdmissionQueryData metadata, ConquerProtocolProfile profile,
            out WorldSession querySession)
        {
            ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(clock); ArgumentNullException.ThrowIfNull(metadata);
            if (profile != ConquerProtocolProfile.VisibleDx8D13 || !metadata.IsCurrentFor(source, catalog) ||
                HasLegacyPlayerMagicInterserverAttribute(source)) throw Invalid("futureDeathQueryInputUnavailable");
            var admission = LegacyEquipmentRebuildAdmission.ApplyCurrent(source, LegacyEquipmentRebuildReason.PlayerDeath, clock);
            if (!admission.Admitted)
            {
                querySession = admission.Session;
                _ = LegacyMsgMessagePacket.SystemTalk("You Shoud wait 6 seconds.").ToFrameBytes();
                return null;
            }
            var result = ProjectAdmitted(source, catalog, metadata, profile, admission, restoreHitpoints: false);
            querySession = result.Session;
            var selected = source.PlayerItems with { EquipmentItems = LegacyEquipmentQuerySelector.Select(source) };
            _ = LegacyShowEquipmentPacket.CreateEquipmentRefresh(selected, source.AlternateEquipmentActive).ToFrameBytes();
            uint action = LegacyActionState.RequireCurrent(source.Character);
            var appearance = WithCharacterAttribute(querySession.Character, "Action", action);
            // Packet representability only. The actual death writer retains
            // each historical prefix maximum and native status-vector phase.
            _ = LegacyPlayerSpawnPacket.FromSnapshot(appearance, selected, source.AlternateEquipmentActive,
                maxLife: result.MaximumHitpoints).ToFrameBytes();
            _ = new LegacyServerInfoPacket(checked((ushort)LegacyEquipmentQueryBounds.ReadVigor(querySession.Character))).ToFrameBytes();
            var fields = CalculateLegacyRuntimeStatusFields(querySession, catalog, profile);
            fields[LegacyPlayerStatusPacket.MagicAttackIndex] = result.Magic.MagicAttack;
            fields[LegacyPlayerStatusPacket.MagicPercentIndex] = result.Magic.MagicPercent;
            fields[LegacyPlayerStatusPacket.SkillCStrikeIndex] = result.Magic.SkillCStrike;
            fields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex] = result.Magic.MagicDamageIncrease;
            _ = new LegacyPlayerStatusPacket(fields).ToFrameBytes();
            return result;
        }

        private static FutureEquipmentReceiveData ProjectAdmitted(WorldSession source, WorldDataCatalog catalog,
            LegacyOwnedPhysicalMetadataWitness.AdmissionQueryData metadata, ConquerProtocolProfile profile,
            LegacyEquipmentRebuildResult admission, bool restoreHitpoints)
        {
            if (!admission.Admitted || !LegacyEquipmentQueryExecution.CanPrepareDerivedSpells(catalog, source, out _))
                throw Invalid("futureQueryAdmissionUnavailable");

            var stable = CaptureEquipmentQueryInputs(source);
            var equipment = LegacyEquipmentQuerySelector.Select(stable);
            var runes = CaptureRunes(stable, catalog);
            // The existing actual query documents this physical-flat addition
            // as unjoined. A correct HP reduction alone cannot prove the later
            // physical receive's complete status, so keep that selected branch held.
            if (HasLegacyStatusFlag(stable.Character, LegacySacrificeFlag) && runes.ContainsKey("Sacrifice") &&
                LegacyMagicRuntime.TryResolveLearnedSpell(stable.PlayerSpells, catalog,
                    LegacySacrificeAttachStatusSpellId, out _, out _))
                throw Invalid("futureQuerySacrificePhysicalContributionUnjoined");

            var overlay = ProjectPhysicalPrefix(stable, equipment, catalog, profile);
            var magic = ProjectEquipmentQueryMagicData(null, stable, equipment, catalog);
            byte Rune(string name) => runes.TryGetValue(name, out var level) ? level : (byte)0;
            var perfection = LegacyNativePerfectionSnapshot.FromPrestige(magic.Prestige, Rune);
            if (ReadAttributeUInt32(stable.Character.Attributes, "VipLevel") == 7)
                perfection = perfection with { LuckyStrike = perfection.LuckyStrike + 3 };
            // Relic LuckyStrike additions occur before FromPrestige's reset and
            // therefore do not survive in the actual completed query either.
            overlay = overlay with
            {
                MaxMana = magic.Mana,
                JiangHuMaxMana = LegacyEquipmentNativeMana.JiangHu(stable.Character, catalog),
                PerfectionToxinEraser = perfection.ToxinEraser,
            };

            var derived = ProjectAttributeDerivedSpells(stable, catalog);
            uint uid = checked((uint)stable.Character.Uid);
            PreflightDerivedSpellFrames(uid, derived, null, metadata);
            var hp = PreflightAttributeHitpoints(stable, equipment, catalog, null, metadata);
            var vigor = LegacyEquipmentQueryBounds.ClampVigor(LegacyEquipmentQueryBounds.ReadVigor(stable.Character),
                LegacyMaxHitpointsCalculator.CalculateQueryMaxVigor(equipment, catalog));
            var character = WithEquipmentQueryVigor(derived.Character, vigor);
            character = LegacyEquipmentQueryBounds.WithHitpoints(character, hp.Current);
            // The full query postlude derives 194 from its captured effective
            // fans; an existing native row's timer remains runtime-only evidence.
            character = ProjectWindWalker(character, equipment);
            // UpdateLevel restores current HP only AFTER the full query. Typed
            // MaxMana/current MP are preserved; the query maximum is an overlay.
            if (restoreHitpoints)
                character = LegacyEquipmentQueryBounds.WithHitpoints(character, checked((int)hp.Maximum));
            character = new BoundHitpointsInputs(source.Character, character).After;
            var after = admission.Session with
            {
                Character = character, PlayerSpells = derived.Spells,
                PhysicalStatusOverlay = overlay,
                EquipmentHitpointsStatus = new(source.Character.Uid, hp.Maximum),
            };
            foreach (var packet in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(uid, hp.Maximum, checked((uint)hp.Current)))
                _ = packet.ToFrameBytes();
            if (restoreHitpoints)
                foreach (var packet in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(uid, hp.Maximum, hp.Maximum))
                    _ = packet.ToFrameBytes();
            _ = LegacyUserTotalRefineLevPacket.FromEquipmentPerfection(uid, stable.PlayerItems).ToFrameBytes();
            if (!metadata.IsCurrentFor(source, catalog)) throw Invalid("futureQueryInputChanged");
            return new(after, hp, magic, perfection, derived,
                stable.Character.Reborn >= 2 && stable.Character.Level >= 15, runes);
        }

        private static Dictionary<string, byte> CaptureRunes(WorldSession source, WorldDataCatalog catalog)
        {
            var runes = new Dictionary<string, byte>(StringComparer.Ordinal);
            foreach (var name in QueryRuneNames)
                if (TryGetLegacyEligibleEquippedRuneLevel(source, catalog, name, out var level)) runes.Add(name, level);
            return runes;
        }

        private static LegacyPhysicalStatusOverlay ProjectPhysicalPrefix(WorldSession source,
            IReadOnlyList<PlayerItemSnapshotItem> equipment, WorldDataCatalog catalog, ConquerProtocolProfile profile)
        {
            var sourceItems = source.PlayerItems with { EquipmentItems = equipment };
            var rawSource = source with { PlayerItems = sourceItems };
            uint uid = checked((uint)source.Character.Uid);
            var fields = LegacyPlayerStatusCalculator.CalculateFields(source.Character, sourceItems, catalog,
                GetLegacyEquipmentRuntimeMaxHitpoints(rawSource, catalog),
                LegacyUserTotalRefineLevPacket.FromEquipmentPerfection(uid, sourceItems).Level,
                source.CombatGear ?? CombatGearSnapshot.Empty, alternateEquipmentActive: source.AlternateEquipmentActive,
                playerSpells: source.PlayerSpells, protocolProfile: profile);
            ApplyLegacyRuntimeStatusFieldOverrides(source.Character, fields);
            var overlay = LegacyPhysicalStatusOverlay.ForOwner(source) with
            {
                Breakthrough = fields[LegacyPlayerStatusPacket.BreakthroughIndex],
                Counteraction = fields[LegacyPlayerStatusPacket.CounteractionIndex],
                RidingCropItemId = 0, RightWeaponItemEffect = 0, RingItemEffect = 0,
                MaxMana = 0,
            };
            // NecklaceEffect and the earned FrontBreak deadline are not reset.
            // The actual ordered selected-item traversal overwrites durable slots.
            foreach (var item in equipment)
            {
                if (item.Durability == 0) continue;
                overlay = (item.Position % 50) switch
                {
                    18 => overlay with { RidingCropItemId = item.ItemId },
                    4 => overlay with { RightWeaponItemEffect = item.Effect },
                    6 => overlay with { RingItemEffect = item.Effect },
                    2 => overlay with { NecklaceItemEffect = item.Effect },
                    _ => overlay,
                };
            }
            return overlay;
        }

        private static CharacterSnapshot ProjectWindWalker(CharacterSnapshot character,
            IReadOnlyList<PlayerItemSnapshotItem> equipment)
        {
            bool desired = LegacyEquipmentQueryExecution.HasEffectiveFans(equipment);
            bool before = (LegacyWindWalkerStatusProjection.Read(character)[LegacyWindWalkerStatusProjection.Word] &
                LegacyWindWalkerStatusProjection.Mask) != 0;
            bool removeOnDeath = false;
            if (desired && before)
            {
                // Duplicate native AddFlag retains its actual registration. The
                // managed input must contain its known removal policy; absence
                // cannot be replaced by guessing the new-row default.
                var attributes = LegacyNativeAttributeMapProof.CaptureRaw(character.Attributes);
                string key = LegacyStatusFlagDeathRegistration.Key(LegacyWindWalkerStatusProjection.Flag);
                var rows = attributes.Fields.Where(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (rows.Length != 1 || !bool.TryParse(rows[0].Value, out removeOnDeath) || !attributes.IsCurrentPlain())
                    throw Invalid("futureQueryWindWalkerHistoryUnavailable");
            }
            return LegacyWindWalkerStatusProjection.Apply(character, desired, removeOnDeath);
        }

        private static InvalidOperationException Invalid(string reason) => new(reason);
    }
}
