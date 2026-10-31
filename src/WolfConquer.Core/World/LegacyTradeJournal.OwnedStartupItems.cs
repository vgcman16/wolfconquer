using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // Closed startup custody is not a producer or a general pending-WAL bypass.
    internal static bool TryRecoverOwnedStartupItems(LegacyItemUidRegistry.OwnedStartupCustody custody,
        out string reason)
    {
        reason = string.Empty;
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            if (!OwnedStartupItemRecovery.TryCapture(custody, out var recovery, out reason)) return false;
            if (recovery is null)
            { reason = RecoveryRequired + ":ownedCapturedRecordMissing"; return false; }
            var configured = custody.Configuration;
            if (!LegacyItemUidRawCatalog.TryScanOwnedStartup(configured.ItemsRoot, configured.MailboxRoot,
                    configured.RedeemFile, configured.ClaimFile, recovery, out var observed, out reason)) return false;
            if (observed.Maximum > custody.HighWater)
            { reason = RecoveryRequired + ":ownedStartupHighWaterExceeded"; return false; }
            return recovery.TryRecover(out reason);
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = RecoveryRequired + ":ownedStartupInvalid:" + error.GetType().Name; return false; }
    }

    // Only an actual captured record and the existing exclusive namespace lease
    // can issue this context. No caller can supply target paths or ignored files.
    internal sealed partial class OwnedStartupItemRecovery
    {
        private readonly LegacyItemUidRegistry.OwnedStartupCustody custody;
        private readonly RecoveryCapture capture;
        private readonly JournalTarget[] characters;
        private readonly JournalTarget[] items;
        private readonly HashSet<uint>? protectedPhysicalUids;
        private readonly string? protectedItemPath;
        private readonly LegacyCharacterCreationRecoveryEvidence? creationNames;

        private OwnedStartupItemRecovery(LegacyItemUidRegistry.OwnedStartupCustody custody, RecoveryCapture capture)
        {
            this.custody = custody;
            this.capture = capture;
            characters = capture.Targets.Where(target => target.Change.Kind == LegacyTradeJournalFileKind.Character).ToArray();
            items = capture.Targets.Where(target => target.Change.Kind == LegacyTradeJournalFileKind.Items).ToArray();
            if (capture.Record.Version == 19)
            {
                var recipe = ValidateAuthenticatedCreationImages(capture.Record, custody.HighWater);
                creationNames = LegacyCharacterCreationRecoveryEvidence.Capture(custody.Configuration, recipe.Account,
                    recipe.Projection.Name, capture.Record.Changes, capture.Record.TransactionId);
            }
            if (capture.Record.Version is 2 or 9 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20)
            {
                var target = items.Single();
                protectedPhysicalUids = capture.Record.Version == 19 ? [] :
                    LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(target.Change.Before!).ToHashSet();
                protectedItemPath = target.Path;
                if (capture.Record.Version == 18)
                {
                    // The exact allocation was already joined to pinned real
                    // custody. At prefix0 these births may not exist here yet;
                    // none may borrow a physical owner elsewhere meanwhile.
                    var allocation = capture.Record.RuneDecompose!.Allocation;
                    for (var index = 0; index < allocation.Count; index++)
                        protectedPhysicalUids.Add(checked(allocation.First + (uint)index));
                }
                if (capture.Record.Version == 20 && capture.Record.RuneBag!.Allocation is { } bagAllocation)
                    protectedPhysicalUids.Add(bagAllocation.First);
                if (capture.Record.Version == 19)
                {
                    var allocation = capture.Record.OrdinaryCreation!.Allocation;
                    for (var index = 0; index < allocation.Count; index++)
                        protectedPhysicalUids.Add(checked(allocation.First + (uint)index));
                }
            }
        }

        internal static bool TryCapture(LegacyItemUidRegistry.OwnedStartupCustody custody,
            out OwnedStartupItemRecovery? recovery, out string reason)
        {
            ArgumentNullException.ThrowIfNull(custody);
            recovery = null;
            custody.RequireCurrent();
            var configured = custody.Configuration;
            if (!TryCaptureRecovery(configured.UsersRoot, configured.ItemsRoot, configured.QuestsRoot,
                    configured.MailboxRoot, configured.CombatGearRoot, configured.Roots, out var captured, out reason))
                return false;
            if (captured is null)
            { reason = RecoveryRequired + ":ownedCapturedRecordMissing"; return false; }
            custody.RequireRoots(captured.Roots.Users, captured.Roots.Items);
            ValidateImages(captured, custody.HighWater, custody);
            var issued = new OwnedStartupItemRecovery(custody, captured);
            issued.RequireCurrent();
            recovery = issued;
            return true;
        }

        private static void ValidateImages(RecoveryCapture captured, uint highWater,
            LegacyItemUidRegistry.OwnedStartupCustody custody)
        {
            if (captured.Record.Version == 20)
            {
                ValidateRecordedRuneBagImages(captured.Record, highWater);
                var operation = captured.Record.RuneBag!;
                if (operation.Allocation is { } allocation) custody.RequireRuneBagAllocation(operation.Request!, allocation);
                return;
            }
            if (captured.Record.Version == 19)
            {
                _ = ValidateAuthenticatedCreationImages(captured.Record, highWater);
                custody.RequireAuthenticatedCreationAllocation(captured.Record.OrdinaryCreation!);
                return;
            }
            if (captured.Record.Version == 18)
            {
                ValidateRecordedRuneDecomposeImages(captured.Record, highWater);
                var operation = captured.Record.RuneDecompose!;
                custody.RequireRuneDecomposeAllocation(operation.Request, operation.Allocation);
                return;
            }
            if (captured.Record.Version == 17)
            {
                ValidateRecordedRuneChanceImages(captured.Record, highWater);
                return;
            }
            if (captured.Record.Version == 16)
            {
                ValidateRecordedRuneComposeImages(captured.Record, highWater);
                return;
            }
            if (captured.Record.Version is 13 or 15)
            {
                ValidateRecordedRuneContainerImages(captured.Record, highWater);
                return;
            }
            if (captured.Record.Version is 12 or 14)
            {
                ValidateRecordedRuneImages(captured.Record, highWater);
                return;
            }
            if (captured.Record.Version == 9)
            {
                ValidateOwnedStartupItemSpellImages(captured, highWater);
                return;
            }
            if (captured.Record.Version == 1)
            {
                ValidateOwnedStartupPairImages(captured, highWater);
                return;
            }
            if (captured.Record.Version != 2 || captured.Targets.Length != 2 ||
                captured.Targets.Count(target => target.Change.Kind == LegacyTradeJournalFileKind.Character) != 1 ||
                captured.Targets.Count(target => target.Change.Kind == LegacyTradeJournalFileKind.Items) != 1)
                throw new InvalidDataException("Owned startup supports only the single Character/Items version-2 record.");
            var character = captured.Targets.Single(target => target.Change.Kind == LegacyTradeJournalFileKind.Character).Change;
            var items = captured.Targets.Single(target => target.Change.Kind == LegacyTradeJournalFileKind.Items).Change;
            if (character.FileId is <= 0 or > uint.MaxValue || items.FileId is <= 0 or > uint.MaxValue ||
                character.Before is not { } beforeCharacter || character.After is not { } afterCharacter ||
                items.Before is not { } beforeItems || items.After is not { } afterItems)
                throw new InvalidDataException("Owned startup requires complete existing Character and Items images.");
            foreach (var image in new[] { beforeCharacter, afterCharacter })
            {
                if (ReadCharacterUid(image) != items.FileId)
                    throw new InvalidDataException("Character UID differs from the owned Items participant.");
                var text = CharacterSnapshotFileEncoding.Decode(image, out _);
                LegacyOrdinaryAdmissionValidation.ValidateCharacter(CharacterSnapshotParser.ParseText(text));
            }
            var before = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(beforeItems);
            var after = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(afterItems);
            var beforeSet = before.ToHashSet();
            if (before.Any(uid => uid > highWater) || after.Any(uid => uid > highWater) ||
                after.Any(uid => !beforeSet.Contains(uid)))
                throw new InvalidDataException("Owned startup cannot introduce or exceed the pinned physical UID set.");
        }

        internal void RequireScanRoots(string itemsRoot, string mailboxRoot, string redeemFile, string claimFile)
        {
            var configured = custody.Configuration;
            if (!SamePath(itemsRoot, configured.ItemsRoot) || !SamePath(mailboxRoot, configured.MailboxRoot) ||
                !SamePath(redeemFile, configured.RedeemFile) || !SamePath(claimFile, configured.ClaimFile))
                throw new InvalidDataException("Owned startup scan differs from the configured UID namespace.");
            RequireCurrent();
        }

        internal void RequireCurrent()
        {
            custody.RequireCurrent();
            custody.RequireRoots(capture.Roots.Users, capture.Roots.Items);
            RequireConfiguredSpellsRoot();
            RequirePendingIdentity();
            creationNames?.RequireCurrent();
            EnsureTargetsMatch(capture.Targets, allowAfter: true);
            foreach (var target in capture.Targets) ValidateStage(target);
            // An unrelated Character stage is evidence too; unlike PlayersItems,
            // Users is not otherwise part of the physical-UID directory scan.
            foreach (var path in Directory.EnumerateFileSystemEntries(capture.Roots.Users))
            {
                // The exact captured journal directory was just validated;
                // its name shares the stage prefix but it is not target scratch.
                if (SamePath(path, capture.Roots.Journal)) continue;
                if (!Path.GetFileName(path).Contains(".managed-trade-", StringComparison.OrdinalIgnoreCase)) continue;
                EnsureSafePath(path);
                if (!characters.Any(target => SamePath(path, StagePath(target, capture.Record.TransactionId))))
                    throw new InvalidDataException("Owned startup found a foreign Character staging entry.");
            }
            RequirePendingIdentity();
            custody.RequireCurrent();
        }

        private void RequirePendingIdentity()
        {
            if (!TryCapturePendingImage(capture.Roots.Users, out var current, out var reason) || current is null ||
                !SamePath(current.Path, capture.Pending.Path) ||
                !current.Bytes.AsSpan().SequenceEqual(capture.Pending.Bytes))
                throw new InvalidDataException("Owned startup pending record changed or disappeared: " + reason);
        }

        private void ValidateStage(JournalTarget target)
        {
            var path = StagePath(target, capture.Record.TransactionId);
            EnsureSafePath(path);
            if (Directory.Exists(path)) throw new InvalidDataException("Owned staging path is a directory.");
            var bytes = ReadTarget(path);
            if (bytes is not null && (bytes.Length > target.Change.After.Length ||
                !bytes.AsSpan().SequenceEqual(target.Change.After.AsSpan(0, bytes.Length))))
                throw new InvalidDataException("Owned stage is not a bounded prefix of its actual afterimage.");
        }

        internal bool IsKnownItemStage(string path)
        {
            if (!items.Any(target => SamePath(path, StagePath(target, capture.Record.TransactionId)))) return false;
            RequireCurrent();
            return File.Exists(path);
        }

        // Only the conserved v1 pair needs a logical view: its mixed physical
        // prefix can contain the same transferred UID at both owners. Single-
        // owner versions keep their physical scan and captured BEFORE custody.
        internal byte[] ReadOwnedStartupScanImage(string path, byte[] physical)
        {
            if (capture.Record.Version != 1) return physical;
            var target = items.SingleOrDefault(target => SamePath(path, target.Path));
            if (target is null) return physical;
            RequireCurrent();
            if (!Matches(physical, target.Change.Before) && !Matches(physical, target.Change.After))
                throw new InvalidDataException("Owned startup scan target differs from its captured images.");
            return target.Change.After.ToArray();
        }

        // Pure closed BCL proof in the physical-row loop. Capture and scan
        // boundaries still validate custody/current targets/pending/stages.
        // A retired BEFORE UID or v18 reserved birth cannot borrow a physical
        // owner elsewhere. References retain their ordinary highwater rules.
        internal void RequirePhysicalUidInput(string path, LegacyItemUidRawCatalog.Kind kind, uint uid)
        {
            if (protectedPhysicalUids?.Contains(uid) == true &&
                (kind != LegacyItemUidRawCatalog.Kind.PlayerItems || !JournalDomainComparer.Equals(path, protectedItemPath)))
                throw new InvalidDataException("Captured physical UID is owned by an unrelated physical input.");
        }

        internal void RequireExactJournal(IReadOnlyList<string> paths, IReadOnlyList<LegacyTradeJournalChange> changes)
        {
            ArgumentNullException.ThrowIfNull(paths);
            ArgumentNullException.ThrowIfNull(changes);
            RequireCurrent();
            if (paths.Count != capture.Targets.Length || changes.Count != capture.Record.Changes.Length)
                throw new InvalidDataException("Owned startup fence target count differs from its captured record.");
            for (var index = 0; index < paths.Count; index++)
            {
                var actual = changes[index];
                var retained = capture.Record.Changes[index];
                if (actual is null || !SamePath(paths[index], capture.Targets[index].Path) ||
                    actual.Kind != retained.Kind || actual.FileId != retained.FileId ||
                    !Matches(actual.Before, retained.Before) || !Matches(actual.After, retained.After))
                    throw new InvalidDataException("Owned startup fence paths or images differ from its captured record.");
            }
        }

        internal bool PermitsConfiguredPath(string users, string path)
        {
            RequireCurrent();
            return SamePath(users, capture.Roots.Users) && capture.Targets.Any(target => SamePath(target.Path, path));
        }

        internal bool PermitsOrdinaryOwner(LegacyOrdinaryAdmissionUidOwner owner, string path)
        {
            RequireCurrent();
            var configured = custody.Configuration;
            return owner.Domain == configured.ExpectedDomain && owner.Roots == configured.Roots &&
                SamePath(owner.MailboxRoot, configured.MailboxRoot) && PermitsConfiguredPath(owner.Roots.Users, path);
        }

        internal void ClearStages()
        {
            foreach (var target in capture.Targets)
            {
                RequireCurrent();
                if (!TryDelete(StagePath(target, capture.Record.TransactionId)))
                    throw new IOException("Owned startup staging cleanup is incomplete.");
            }
            RequireCurrent();
        }

        internal bool TryRecover(out string reason)
        {
            RequireCurrent();
            return RecoverCaptured(capture, out _, out reason, this);
        }

        private static bool SamePath(string left, string right) => JournalDomainComparer.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)));
    }
}
