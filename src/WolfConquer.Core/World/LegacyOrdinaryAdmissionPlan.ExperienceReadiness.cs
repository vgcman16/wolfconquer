using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinaryAdmissionPlan
{
    // Pre-native DATA feasibility only. This does not authorize admission,
    // substitute for query metadata, or predict the later native publication.
    internal sealed class ExperiencePreNativeReadiness
    {
        private readonly LegacyOrdinaryAdmissionRoots roots;
        private readonly long account, uid;
        private readonly Dictionary<LegacyTradeJournalFileKind, byte[]?> images;

        private ExperiencePreNativeReadiness(LegacyOrdinaryAdmissionRoots roots, long account, long uid,
            Dictionary<LegacyTradeJournalFileKind, byte[]?> images)
        { this.roots = roots; this.account = account; this.uid = uid; this.images = images; }

        internal static bool TryCapture(WorldServerFrameHandler frame, CharacterSnapshotLoader loader,
            long account, CharacterSnapshot raw, ExperienceBeforeNativeEvidence evidence,
            out ExperiencePreNativeReadiness? readiness)
        {
            readiness = null;
            try
            {
                if (evidence.Pending is null || !evidence.IsCurrentPlain()) return false;
                var roots = LegacyOrdinaryAdmissionRoots.FromLoader(loader);
                var images = LegacyTradeJournal.CaptureOrdinaryAdmissionInputs(roots, account, raw.Uid);
                if (images.GetValueOrDefault(LegacyTradeJournalFileKind.Character) is not { } character ||
                    !LegacyNativeExistingCharacterPrefix.AdmissionMatchesSnapshot(raw, character,
                        roots.PathFor(LegacyTradeJournalFileKind.Character, account))) return false;
                LegacyTradeJournal.ValidateOrdinaryAdmissionCharacterImage(character, raw.Uid);
                LegacyOrdinaryAdmissionValidation.ValidateStartupCharacter(raw);
                var itemImage = images.GetValueOrDefault(LegacyTradeJournalFileKind.Items);
                if (itemImage is not null) LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(itemImage);
                var items = itemImage is null ? PlayerItemSnapshot.Empty : PlayerItemSnapshotLoader.Parse(itemImage);
                var spellImage = images.GetValueOrDefault(LegacyTradeJournalFileKind.Spells);
                var spells = spellImage is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(spellImage);
                if (spells.Spells.Any(row => row.Id == 0 || row.Experience < 0) ||
                    spells.Spells.Select(row => row.Id).Distinct().Count() != spells.Spells.Count) return false;
                var gear = CombatGearSnapshot.Empty;
                if (images.GetValueOrDefault(LegacyTradeJournalFileKind.CombatGear) is { } gearImage)
                {
                    if (!LegacyTradeJournal.TryReadCompleteMailboxGear(gearImage, out var parsed) || parsed is null) return false;
                    gear = parsed;
                }
                var normalized = frame.PrepareOrdinaryNormalization(raw, items, spells, gear);
                if (normalized.NeedsFreshItemIds || roots.Spells is null &&
                    !PlayerSpellSnapshotLoader.ToLegacyBytes(spells).AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(normalized.Spells))) return false;
                var candidate = new ExperiencePreNativeReadiness(roots, account, raw.Uid, images);
                if (!evidence.IsCurrentPlain() || !candidate.IsCurrentPlain()) return false;
                readiness = candidate; return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or
                InvalidDataException or ArgumentException or OverflowException or FormatException or NotSupportedException or System.Security.SecurityException)
            { return false; }
        }

        internal bool IsCurrentPlain() => LegacyTradeJournal.AdmissionInputsAreCurrent(roots, account, uid, images);

        internal bool MatchesPreparedPlan(LegacyOrdinaryAdmissionPlan plan) => IsCurrentPlain() &&
            Equals(roots, plan.roots) && plan.account == account && plan.source.Uid == uid &&
            images.Count == plan.images.Count && images.All(pair => plan.images.TryGetValue(pair.Key, out var actual) &&
                (pair.Value is null ? actual is null : actual is not null && pair.Value.AsSpan().SequenceEqual(actual)));
    }
}
