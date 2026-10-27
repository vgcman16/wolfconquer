using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinaryAdmissionPlan
{
    internal static bool TryPrepare(WorldServerFrameHandler frame, ConquerTcpConnection connection,
        long accountId, string username, CharacterSnapshot raw, CharacterSnapshotLoader loader,
        LegacyOrdinaryAdmissionUidOwner? uids, LegacyTradeRecoveryState recovery, TimeProvider clock,
        out LegacyOrdinaryAdmissionPlan? plan, out string reason)
    {
        plan = null; reason = "ordinaryAdmissionPreparationRefused";
        try
        {
            if (accountId is <= 0 or > uint.MaxValue || raw.Uid != accountId || recovery.IsBlocked) return false;
            if (!LegacyOrdinaryAdmissionReconciliation.TryRequireOwner(loader, uids, out reason)) return false;
            if (uids?.IsPending(checked((uint)accountId)) == true)
            { reason = "ordinaryAdmissionRecoveryRequired:unsafeRetainedWorkRequiresReconciliation"; return false; }
            LegacyOrdinaryAdmissionValidation.ValidateStartupCharacter(raw);
            LegacyNativeExistingCharacterPrefix? prefix = null;
            if (connection.NativeGameClient is not null &&
                !LegacyNativeExistingCharacterPrefix.TryPrepare(connection, raw, loader.UsersRoot, out prefix, out reason)) return false;
            var roots = LegacyOrdinaryAdmissionRoots.FromLoader(loader);
            var images = LegacyTradeJournal.CaptureOrdinaryAdmissionInputs(roots, accountId);
            if (!images.TryGetValue(LegacyTradeJournalFileKind.Character, out var characterImage) || characterImage is null ||
                !LegacyNativeExistingCharacterPrefix.AdmissionMatchesSnapshot(raw, characterImage,
                    roots.PathFor(LegacyTradeJournalFileKind.Character, accountId)) ||
                prefix is not null && !characterImage.AsSpan().SequenceEqual(prefix.CopyAdmissionBeforeImage())) return false;
            LegacyTradeJournal.ValidateOrdinaryAdmissionCharacterImage(characterImage, accountId);
            var loadedSource = prefix?.AdmissionSnapshot ?? raw;
            var source = loadedSource with
            {
                Online = 1,
                Attributes = new Dictionary<string, string>(loadedSource.Attributes, StringComparer.OrdinalIgnoreCase) { ["Online"] = "1" },
                PaidNobility = new Dictionary<string, string>(loadedSource.PaidNobility, StringComparer.OrdinalIgnoreCase),
            };
            var localNow = clock.GetLocalNow().DateTime;
            var character = LegacyOrdinaryAdmissionImages.PrepareSnapshot(source, localNow, out var pin);
            if (connection.NativeGameClient is null && pin.Password != 0)
            { reason = "ordinaryAdmissionProtectedNativeOwnerMissing"; return false; }
            byte[]? Image(LegacyTradeJournalFileKind kind) => images.GetValueOrDefault(kind);
            var itemsImage = Image(LegacyTradeJournalFileKind.Items);
            LegacyNativeEquipmentRuntime? equipment = null;
            if (roots.Items is not null && connection.NativeGameClient is { } native)
            { equipment = new(native); equipment.CaptureRawLoad(itemsImage); }
            if (itemsImage is not null) LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(itemsImage);
            var items = itemsImage is null ? PlayerItemSnapshot.Empty : PlayerItemSnapshotLoader.Parse(itemsImage);
            var spellsImage = Image(LegacyTradeJournalFileKind.Spells);
            var spells = spellsImage is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(spellsImage);
            if (spells.Spells.Any(row => row.Id == 0 || row.Experience < 0) ||
                spells.Spells.Select(row => row.Id).Distinct().Count() != spells.Spells.Count)
                throw new InvalidDataException("Invalid saved spell membership.");
            var profsImage = Image(LegacyTradeJournalFileKind.Proficiencies);
            var profs = profsImage is null ? PlayerProficiencySnapshot.Empty : PlayerProficiencySnapshotLoader.Parse(profsImage);
            if (profs.Proficiencies.Select(row => row.Id).Distinct().Count() != profs.Proficiencies.Count)
                throw new InvalidDataException("Invalid saved proficiency membership.");
            var questImage = Image(LegacyTradeJournalFileKind.Quests);
            var quests = questImage is null ? PlayerQuestSnapshot.Empty : PlayerQuestSnapshotLoader.Parse(questImage);
            character = character with { PlayerQuests = quests };
            var gearImage = Image(LegacyTradeJournalFileKind.CombatGear);
            var gear = CombatGearSnapshot.Empty;
            if (gearImage is not null)
            {
                if (!LegacyTradeJournal.TryReadCompleteMailboxGear(gearImage, out var parsedGear) || parsedGear is null)
                    throw new InvalidDataException("Incomplete saved CombatGear state.");
                gear = parsedGear;
            }
            var normalized = frame.PrepareOrdinaryNormalization(character, items, spells, gear);
            LegacyOrdinaryAdmissionValidation.ValidateStartupCharacter(normalized.Character);
            if (normalized.NeedsFreshItemIds) throw new InvalidDataException("Ordinary admission cannot allocate starter items.");
            if (roots.Spells is null && !PlayerSpellSnapshotLoader.ToLegacyBytes(spells).AsSpan()
                    .SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(normalized.Spells)))
            { reason = "ordinaryAdmissionSpellsRootMissing"; return false; }
            // A physical native game owner requires its configured proficiency
            // participant. Existing wholly unbound managed/read-only callers
            // may have no proficiency route at all; they retain Empty without
            // inventing a file root or claiming native proficiency ownership.
            if (roots.Proficiencies is null && connection.NativeGameClient is not null)
            { reason = "ordinaryAdmissionProficienciesRootMissing"; return false; }
            var candidate = new LegacyOrdinaryAdmissionPlan(frame, connection, accountId, username, roots, images,
                source, character, items, spells, profs, gear, normalized, pin, prefix, equipment, uids, recovery, localNow);
            if (!candidate.IsCurrent()) { reason = "ordinaryAdmissionOwnerChanged"; return false; }
            plan = candidate; reason = string.Empty; return true;
        }
        catch (Exception error) when (IsPreparationFailure(error))
        { reason = "ordinaryAdmissionPreparation:" + error.GetType().Name; return false; }
    }

    private static bool IsPreparationFailure(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or
        ArgumentException or InvalidOperationException or FormatException or OverflowException or NotSupportedException or
        System.Security.SecurityException;
}
