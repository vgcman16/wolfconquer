using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Future calculation DATA only. This never starts a query, creates an XP
    // purpose/marker, publishes native state or advances the real holder.
    internal sealed partial class DragonhowlLevelExperienceData
    {
        private readonly WorldPacketHandlerContext context;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession live, calculated;
        private readonly WorldDataCatalog catalog;
        private readonly ILegacySignedMillisecondClock clock;
        private readonly LegacyNativeAttributeMapProof liveAttributes, livePaid, calculatedAttributes, calculatedPaid;
        private readonly LegacyItemSaveGraph items;
        private readonly LegacyPerformerSpellSource liveSpells, calculatedSpells;
        private readonly List<LevelQueryData> queries = [];
        private readonly SelectedProgressionData progression;
        private readonly string characterPath = string.Empty, itemsPath = string.Empty;
        private readonly byte[]? characterImage, itemsImage;
        private readonly LegacyPreparedSpellFileWrite? spellFile;
        private readonly LegacyNativeStatusFlagsBigVector32? nativeVector;
        private readonly LegacyNativeStatusFlagsBigVector32.WindWalkerSnapshot? nativeWind;
        private readonly DragonhowlLevelExperienceData? previous;
        private byte[]? projectedCharacterImage, projectedSpellImage;
        internal bool RequiresDerivedSpellWrite { get; private set; }
        internal WorldSession ProjectedAfter { get; private set; }

        private DragonhowlLevelExperienceData(WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding,
            WorldSession live, WorldSession calculated, ulong gain, DragonhowlLevelExperienceData? previous)
        {
            this.context = context; this.binding = binding; this.live = live; this.calculated = calculated;
            this.previous = previous;
            if (previous is not null && (!ReferenceEquals(previous.binding, binding) ||
                !ReferenceEquals(previous.ProjectedAfter, calculated) || !previous.IsCurrentPlain())) throw Invalid();
            projectedCharacterImage = previous?.projectedCharacterImage;
            projectedSpellImage = previous?.projectedSpellImage;
            catalog = context.WorldData ?? throw Invalid(); clock = context.LegacyClock ?? LegacySignedMillisecondClock.System;
            liveAttributes = LegacyNativeAttributeMapProof.CaptureRaw(live.Character.Attributes);
            livePaid = LegacyNativeAttributeMapProof.CaptureRaw(live.Character.PaidNobility);
            calculatedAttributes = LegacyNativeAttributeMapProof.CaptureRaw(calculated.Character.Attributes);
            calculatedPaid = LegacyNativeAttributeMapProof.CaptureRaw(calculated.Character.PaidNobility);
            liveSpells = new(live.PlayerSpells.Spells); calculatedSpells = new(calculated.PlayerSpells.Spells);
            if (!LegacyItemSaveGraph.TryCapture(live.PlayerItems, out var captured) || captured is null ||
                !ReferenceEquals(calculated.PlayerItems, live.PlayerItems)) throw Invalid();
            items = captured; binding.Items.RequireCurrentBeforePreparation();
            progression = new(catalog);
            if (gain == 0 || gain > long.MaxValue || calculated.Character.Experience < 0 ||
                calculated.Character.Level is < 1 or > 140 ||
                LegacyExperienceContinuationCodec.ReadCharacter(calculated.Character, calculated.AccountId) is not null ||
                LegacyExperienceContinuationCodec.ReadCharacter(live.Character, live.AccountId) is not null) throw Invalid();
            _ = LegacyNativeAttributeState.From(calculated.Character);
            _ = LegacyNativeActorScalarState.RequireCurrent(calculated.Character);
            ulong experience = checked((ulong)calculated.Character.Experience + gain);
            if (experience > long.MaxValue) throw Invalid();
            var current = calculated with { Character = new BoundHitpointsInputs(calculated.Character,
                calculated.Character with { Experience = calculated.Character.Level == 140 ? 0 : checked((long)experience) }).After };
            ProjectedAfter = current;
            characterPath = Path.Combine(binding.UsersRoot, binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            characterImage = SnapshotFileLockCoordinator.ExecuteLocked(new[] { characterPath }, () =>
            {
                if (!CharacterSnapshotFileBaseline.TryReadBytes(characterPath, "dragonhowlXpScalarData", out var image, out _) ||
                    !LegacyCombatCharacterImageInputs.Matches(CharacterSnapshotParser.ParseText(
                        CharacterSnapshotFileEncoding.Decode(image, out _)), live.Character)) throw Invalid();
                return image;
            });
            if (current.Character.Level == 140 || experience < checked((ulong)progression.RequireThreshold(
                checked((int)current.Character.Level)).Experience))
            {
                projectedCharacterImage = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(
                    projectedCharacterImage ?? characterImage, current.Character);
                _ = LegacyMsgUpdatePacket.CreateLegacyRuntimeExperienceUpdate(checked((uint)binding.Uid),
                    checked((ulong)current.Character.Experience)).ToFrameBytes();
                if (!IsCurrentPlain()) throw Invalid();
                return; // No level query needs no optional derived-file route.
            }
            nativeVector = binding.Player.BitVector;
            if (!Monitor.IsEntered(nativeVector.SyncRoot) || !nativeVector.TryCaptureWindWalker(out var wind) || wind is null ||
                wind.Present && wind.RemoveOnDeath) throw Invalid();
            nativeWind = wind;
            if (!PreparedAttributeRebuild.SameAttributeSpellRoot(context.PlayerSpellsRoot, binding.SpellsRoot) ||
                context.PlayerItemsRoot is not { } root || !Path.IsPathFullyQualified(root) ||
                !binding.Recovery.IsReadyForCharacterItemsSpells(binding.UsersRoot, root, binding.SpellsRoot) ||
                !binding.Recovery.TryRequireLiveJournalClear(out _)) throw Invalid();
            characterPath = Path.Combine(binding.UsersRoot, binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            itemsPath = Path.Combine(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), binding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            string spellsPath = Path.Combine(binding.SpellsRoot, binding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            (characterImage, itemsImage, spellFile) = SnapshotFileLockCoordinator.ExecuteLocked(
                new[] { characterPath, itemsPath, spellsPath }, () =>
                {
                    if (!CharacterSnapshotFileBaseline.TryReadBytes(characterPath, "dragonhowlXpData", out var character, out _) ||
                        !CharacterSnapshotFileBaseline.TryReadBytes(itemsPath, "dragonhowlXpItemsData", out var savedItems, out _) ||
                        !LegacyCombatCharacterImageInputs.Matches(CharacterSnapshotParser.ParseText(
                            CharacterSnapshotFileEncoding.Decode(character, out _)), live.Character) ||
                        !PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(savedItems)).AsSpan()
                            .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(live.PlayerItems))) throw Invalid();
                    return (character, savedItems, binding.Store.PrepareSpellFileWrite(binding.Connection, live,
                        binding.Recovery, binding.UsersRoot, binding.SpellsRoot));
                });

            while (current.Character.Level < 140)
            {
                var threshold = progression.RequireThreshold(checked((int)current.Character.Level));
                if (experience < checked((ulong)threshold.Experience)) break;
                experience -= checked((ulong)threshold.Experience);
                long priorLevel = current.Character.Level, level = priorLevel + 1;
                if (level == 140) experience = 0;
                progression.RequireStats(current.Character, checked((int)level));
                var character = LegacyClassLevelStatProgression.Apply(current.Character with
                    { Level = level, Experience = checked((long)experience) }, catalog, priorLevel, out var change);
                if (character.Reborn == 0 && level <= (character.Class is >= 13002 and <= 13057 ? 110 : 120) && !change.AppliedSourceStats)
                    throw Invalid();
                character = new BoundHitpointsInputs(current.Character, character).After;
                _ = LegacyNativeAttributeState.From(character); _ = LegacyNativeActorScalarState.RequireCurrent(character);
                var next = current with { Character = character };
                _ = PrepareCombatExperienceFrames(new(current.Character, character, gain, true, change));
                var query = new LevelQueryData(context, next, catalog, clock);
                queries.Add(query); RequiresDerivedSpellWrite |= query.Derived.Changes.Count != 0;
                ValidateFutureImages(next, query);
                current = query.After;
            }
            _ = LegacyMsgUpdatePacket.CreateLegacyRuntimeExperienceUpdate(checked((uint)binding.Uid),
                checked((ulong)current.Character.Experience)).ToFrameBytes();
            ProjectedAfter = current;
            if (!IsCurrentPlain()) throw Invalid();
        }

        internal static bool TryPrepare(WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding,
            WorldSession calculatedBefore, ulong gain, out DragonhowlLevelExperienceData? data,
            DragonhowlLevelExperienceData? previous = null)
        {
            data = null;
            try
            {
                if (!binding.Store.TryGet(binding.Connection, out var live) ||
                    !binding.Store.IsLevelUpRebuildOwnerHeld(binding, live, context) ||
                    !ReferenceEquals(context.WorldData, binding.ItemCatalog) || !ReferenceEquals(calculatedBefore.NativeGameClient, binding.Client) ||
                    calculatedBefore.AccountId != binding.AccountId || calculatedBefore.Character.Uid != binding.Uid ||
                    !context.Connection.HasSendRoute || binding.Client.Status is null || binding.Player.BitVector is null) return false;
                data = new(context, binding, live, calculatedBefore, gain, previous); return true;
            }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
                OverflowException or FormatException or IOException or UnauthorizedAccessException or NotSupportedException)
            { return false; }
        }

        internal bool IsCurrentPlain()
        {
            if (!binding.Store.IsLevelUpRebuildOwnerHeld(binding, live, context) || !RecordedInputsAreCurrent() ||
                nativeVector is not null && (!ReferenceEquals(binding.Player.BitVector, nativeVector) ||
                    nativeWind is null || !nativeVector.IsWindWalkerCurrentPlain(nativeWind)) ||
                previous is not null && !previous.IsCurrentPlain() ||
                !binding.Recovery.TryRequireLiveJournalClear(out _)) return false;
            binding.Items.RequireCurrentBeforePreparation();
            if (spellFile is null)
                return characterImage is not null && itemsImage is null && queries.Count == 0 &&
                    ScalarDeathInputsAreCurrent();
            return SnapshotFileLockCoordinator.ExecuteLocked(new[] { characterPath, itemsPath, spellFile.SpellsPath }, () =>
                CharacterSnapshotFileBaseline.TryReadBytes(characterPath, "dragonhowlXpDataCurrent", out var character, out _) &&
                character.AsSpan().SequenceEqual(characterImage) &&
                CharacterSnapshotFileBaseline.TryReadBytes(itemsPath, "dragonhowlXpItemsDataCurrent", out var savedItems, out _) &&
                savedItems.AsSpan().SequenceEqual(itemsImage) && spellFile.DiskImageIsCurrent());
        }

        // Historical calculation inputs only. Actual later publication still
        // needs the registered award/query/clear receipts; this predicate does
        // not accept a new holder, file image or native cache.
        internal bool RecordedInputsAreCurrent() => liveAttributes.IsCurrentPlain() && livePaid.IsCurrentPlain() &&
            calculatedAttributes.IsCurrentPlain() && calculatedPaid.IsCurrentPlain() && liveSpells.IsCurrent() &&
            calculatedSpells.IsCurrent() && items.IsCurrent(live.PlayerItems) && progression.IsCurrent() &&
            queries.All(query => query.IsCurrent()) && (previous is null || previous.RecordedInputsAreCurrent());

        private void ValidateFutureImages(WorldSession beforeQuery, LevelQueryData query)
        {
            // Future bytes are DATA, not a file permit. Reserve the full bounded
            // actual marker's possible encoded line without inventing an award ID.
            byte[] scalar = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(
                projectedCharacterImage ?? characterImage ?? throw Invalid(), beforeQuery.Character);
            byte[] character = LegacyDerivedSpellOrigins.ProjectCharacterImage(scalar, beforeQuery.Character);
            byte[]? spells = projectedSpellImage ?? spellFile?.CopyBeforeImage();
            if (!PlayerSpellSnapshotLoader.ToLegacyBytes(spells is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(spells))
                .AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(beforeQuery.PlayerSpells))) throw Invalid();
            foreach (var desired in query.Derived.Changes)
            {
                var source = desired.Id == 21_930 ? LegacyDerivedSpellQuerySource.Rank : LegacyDerivedSpellQuerySource.MythSoul;
                var ranks = source == LegacyDerivedSpellQuerySource.Rank && beforeQuery.Character.Class / 1000 is 2 or 4 or 7 or 13 or 14
                    ? catalog.CombatGearRanks.Select(row => new LegacyDerivedSpellQueryRankInput(row.CharacterId, row.Fields)).ToArray()
                    : Array.Empty<LegacyDerivedSpellQueryRankInput>();
                MagicType? granted = null;
                if (desired.After is { } grant)
                    granted = catalog.MagicByTypeId[desired.Id].Single(row => row.Fields.Count > 8 &&
                        ushort.TryParse(row.Fields[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) && level == grant.Level);
                var operation = new LegacyDerivedSpellQueryOperation(checked((uint)binding.Uid), source,
                    beforeQuery.AlternateEquipmentActive, itemsImage ?? throw Invalid(), ranks, granted);
                if (operation.Derive(CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(character, out _)),
                    spells is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(spells)) != desired) throw Invalid();
                var projected = operation.Project(character, spells);
                character = projected.CopyCharacterAfter(); spells = projected.CopySpellsAfter();
            }
            // A missing saved file remains missing when this query makes no
            // spell write. Empty model rows are not a four-byte disk image.
            if (!PlayerSpellSnapshotLoader.ToLegacyBytes(query.After.PlayerSpells).AsSpan().SequenceEqual(
                PlayerSpellSnapshotLoader.ToLegacyBytes(spells is null ? PlayerSpellSnapshot.Empty :
                    PlayerSpellSnapshotLoader.Parse(spells)))) throw Invalid();
            character = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(character, query.After.Character);
            _ = CharacterSnapshotFileEncoding.Decode(character, out var encoding);
            int reserve = encoding.GetMaxByteCount(LegacyExperienceContinuationCodec.Key.Length +
                LegacyExperienceContinuationCodec.MaxEncodedLength + 5);
            if (character.Length > LegacyTradeJournal.DerivedSpellQueryMaximumReferenceBytes - reserve ||
                (spells?.Length ?? 0) > LegacyTradeJournal.DerivedSpellQueryMaximumReferenceBytes) throw Invalid();
            LegacyDerivedSpellOrigins.RequireSavedRows(query.After.Character, spells);
            projectedCharacterImage = character;
            projectedSpellImage = spells;
        }

        private static InvalidOperationException Invalid() => new("dragonhowlLevelExperienceDataUnavailable");

        private sealed class LevelQueryData
        {
            private readonly WorldSession before;
            private readonly WorldDataCatalog catalog;
            private readonly ILegacySignedMillisecondClock clock;
            private readonly LegacyOwnedPhysicalMetadataWitness.AdmissionQueryData metadata;
            private readonly LegacyAttackSpeedMetadata speed;
            internal FutureEquipmentReceiveData Future { get; }
            internal LegacyDerivedSpellReconciliation.Model Derived => Future.Derived;
            internal WorldSession After => Future.Session;
            internal LevelQueryData(WorldPacketHandlerContext context, WorldSession source, WorldDataCatalog catalog,
                ILegacySignedMillisecondClock clock)
            {
                before = source; this.catalog = catalog; this.clock = clock;
                if (HasLegacyPlayerMagicInterserverAttribute(source)) throw Invalid();
                metadata = LegacyOwnedPhysicalMetadataWitness.AdmissionQueryData.Capture(source, catalog);
                var equipment = LegacyEquipmentQuerySelector.Select(source);
                var durable = equipment.Where(row => row.Durability != 0).ToArray();
                speed = new(catalog, durable.SingleOrDefault(row => row.Position % 50 == 4)?.ItemId ?? 0,
                    durable.SingleOrDefault(row => row.Position % 50 == 5)?.ItemId ?? 0,
                    durable.SingleOrDefault(row => row.Position % 50 == 6)?.ItemId ?? 0);
                PreflightEquipmentQueryDataCore(source, catalog, clock, null, metadata, null, LegacyEquipmentRebuildReason.LevelUp, true);
                // This computes the complete modeled receive inputs, including
                // query resets and native-cache values. It issues no future
                // native completion; the real driver must later match them.
                Future = FutureEquipmentReceiveData.Project(source, catalog, clock, metadata, context.ProtocolProfile);
            }
            internal bool IsCurrent() => metadata.IsCurrentFor(before, catalog) && speed.IsCurrentPlain(catalog) &&
                LegacyEquipmentRebuildAdmission.ApplyCurrent(before, LegacyEquipmentRebuildReason.LevelUp, clock).Admitted;
        }

        // Only reached thresholds and actually selected automatic-stat rows are
        // pinned. Unused class/level rows are not a whole-catalog obligation.
        private sealed class SelectedProgressionData
        {
            private readonly WorldDataCatalog catalog;
            private readonly Dictionary<(int, int), LevelExperience> levels;
            private Dictionary<(string, int), ClassLevelStat>? stats;
            private readonly Dictionary<int, LevelExperience> usedLevels = [];
            private readonly Dictionary<(string, int), ClassLevelStat> usedStats = [];
            internal SelectedProgressionData(WorldDataCatalog catalog)
            {
                this.catalog = catalog;
                levels = catalog.LevelExperienceByTypeAndLevel as Dictionary<(int, int), LevelExperience> ?? throw Invalid();
                if (!ReferenceEquals(levels.Comparer, EqualityComparer<(int, int)>.Default) || !Plain(catalog.LevelExperience)) throw Invalid();
            }
            internal LevelExperience RequireThreshold(int level)
            {
                if (!levels.TryGetValue((0, level), out var row) || row.Type != 0 || row.Level != level || row.Experience <= 0 ||
                    !ReferenceEquals(catalog.LevelExperience.FirstOrDefault(value => value is not null && value.Type == 0 && value.Level == level), row)) throw Invalid();
                usedLevels[level] = row; return row;
            }
            internal void RequireStats(CharacterSnapshot character, int level)
            {
                if (character.Reborn != 0 || level > (character.Class is >= 13002 and <= 13057 ? 110 : 120)) return;
                stats ??= catalog.ClassLevelStatsByClassAndLevel as Dictionary<(string, int), ClassLevelStat> ?? throw Invalid();
                if (!ReferenceEquals(stats.Comparer, EqualityComparer<(string, int)>.Default) || !Plain(catalog.ClassLevelStats)) throw Invalid();
                if (!LegacyClassLevelStatProgression.TryGetSourceClassName(character.Class, out var name) ||
                    !stats.TryGetValue((name, level), out var row) || row.ClassName != name || row.Level != level ||
                    !ReferenceEquals(catalog.ClassLevelStats.FirstOrDefault(value => value is not null && value.ClassName == name && value.Level == level), row)) throw Invalid();
                usedStats[(name, level)] = row;
            }
            internal bool IsCurrent() => ReferenceEquals(catalog.LevelExperienceByTypeAndLevel, levels) &&
                Plain(catalog.LevelExperience) &&
                usedLevels.All(pair => levels.TryGetValue((0, pair.Key), out var row) && ReferenceEquals(row, pair.Value) &&
                    ReferenceEquals(catalog.LevelExperience.FirstOrDefault(value => value is not null && value.Type == 0 && value.Level == pair.Key), row)) &&
                (stats is null ? usedStats.Count == 0 : ReferenceEquals(catalog.ClassLevelStatsByClassAndLevel, stats) &&
                    Plain(catalog.ClassLevelStats) && usedStats.All(pair => stats.TryGetValue(pair.Key, out var row) && ReferenceEquals(row, pair.Value) &&
                    ReferenceEquals(catalog.ClassLevelStats.FirstOrDefault(value => value is not null && (value.ClassName, value.Level) == pair.Key), row)));
            private static bool Plain<T>(IReadOnlyList<T> rows) => rows is not null && rows.Count <= 262_144 &&
                (rows.GetType() == typeof(T[]) || rows.GetType() == typeof(List<T>));
        }
    }
}
