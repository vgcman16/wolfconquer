using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinaryAdmissionPlan
{
    // This pre-native check must be invoked by the actual admission entry
    // before consuming its permit or constructing a native prefix. The later
    // plan must join the same exact captured image; this object alone grants
    // neither login nor resume authority.
    internal sealed class ExperienceBeforeNativeEvidence
    {
        private readonly CharacterSnapshotLoader loader;
        private readonly CharacterSnapshot raw;
        private readonly long account;
        private readonly string path;
        private readonly byte[] image;
        private LegacyOrdinaryAdmissionPlan? preparedPlan;
        private ExperiencePreNativeReadiness? readiness;
        internal LegacyExperienceContinuation? Pending { get; }

        private ExperienceBeforeNativeEvidence(CharacterSnapshotLoader loader, CharacterSnapshot raw, long account,
            string path, byte[] image, LegacyExperienceContinuation? pending)
        { this.loader = loader; this.raw = raw; this.account = account; this.path = path; this.image = image; Pending = pending; }

        internal bool IsCurrentPlain()
        {
            try
            {
                return CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceBeforeNative", out var current, out _) &&
                    current.AsSpan().SequenceEqual(image) &&
                    LegacyNativeExistingCharacterPrefix.AdmissionMatchesSnapshot(raw, current, path) &&
                    Equals(Pending, ReadPendingIfPresent(current, account, checked((uint)raw.Uid)));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or
                InvalidDataException or ArgumentException or OverflowException or NotSupportedException or System.Security.SecurityException)
            { return false; }
        }

        internal bool BelongsTo(LegacyOrdinaryAdmissionPlan plan) => IsCurrentPlain() &&
            plan.account == account && plan.source.Uid == raw.Uid &&
            string.Equals(plan.roots.Users, LegacyOrdinaryAdmissionRoots.FromLoader(loader).Users,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
            plan.images.GetValueOrDefault(LegacyTradeJournalFileKind.Character) is { } captured && captured.AsSpan().SequenceEqual(image);

        internal bool BindPreparedPlan(LegacyOrdinaryAdmissionPlan plan)
        {
            if (preparedPlan is not null || !BelongsTo(plan) ||
                Pending is not null && (readiness is null || !readiness.MatchesPreparedPlan(plan)) || !plan.IsCurrent()) return false;
            preparedPlan = plan;
            return true;
        }

        internal bool CaptureReadiness(WorldServerFrameHandler frame) => Pending is not null && readiness is null &&
            ExperiencePreNativeReadiness.TryCapture(frame, loader, account, raw, this, out readiness);

        internal bool MatchesPublishedPlan(LegacyOrdinaryAdmissionPlan plan)
        {
            if (!ReferenceEquals(preparedPlan, plan) || Pending is null || !plan.IsPublishedNativeOwnerCurrent()) return false;
            try { return Equals(Pending, LegacyExperienceContinuationCodec.ReadCharacterImage(plan.characterAfter, account, checked((uint)raw.Uid))); }
            catch (Exception error) when (error is InvalidDataException or ArgumentException or OverflowException) { return false; }
        }

        internal static bool TryCapture(CharacterSnapshotLoader loader, CharacterSnapshot raw, long account,
            out ExperienceBeforeNativeEvidence? evidence, out string reason)
        {
            evidence = null; reason = "experienceAdmissionBeforeNativeInvalid";
            try
            {
                if (account is <= 0 or > uint.MaxValue || raw.Uid is <= 0 or > uint.MaxValue) return false;
                string path = LegacyOrdinaryAdmissionRoots.FromLoader(loader).PathFor(LegacyTradeJournalFileKind.Character, account);
                if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceBeforeNative", out var image, out _) ||
                    !LegacyNativeExistingCharacterPrefix.AdmissionMatchesSnapshot(raw, image, path)) return false;
                var pending = ReadPendingIfPresent(image, account, checked((uint)raw.Uid));
                var candidate = new ExperienceBeforeNativeEvidence(loader, raw, account, path, image.ToArray(), pending);
                if (!candidate.IsCurrentPlain()) return false;
                evidence = candidate; reason = string.Empty; return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or
                InvalidDataException or ArgumentException or OverflowException or NotSupportedException or System.Security.SecurityException)
            { reason = "experienceAdmissionMarkerInvalid:" + error.GetType().Name; return false; }
        }

        private static LegacyExperienceContinuation? ReadPendingIfPresent(byte[] image, long account, uint uid)
        {
            var text = CharacterSnapshotFileEncoding.Decode(image, out _);
            using var reader = new StringReader(text);
            var lines = new List<string>();
            while (reader.ReadLine() is { } line) lines.Add(line);
            return LegacyExperienceContinuationFence.ContainsCharacterMarker(lines)
                ? LegacyExperienceContinuationCodec.ReadCharacterImage(image, account, uid) : null;
        }
    }

    // Evidence of one real completed ordinary admission. This does not parse a
    // marker, authorize a credit, or by itself authorize any continuation.
    internal sealed class ExperienceAdmissionOrigin
    {
        private readonly LegacyOrdinaryAdmissionPlan plan;
        private readonly WorldSessionStore store;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession admitted;
        private readonly byte[] before, after;

        private ExperienceAdmissionOrigin(LegacyOrdinaryAdmissionPlan plan, WorldSessionStore store,
            LegacyOrdinarySpellBinding binding, WorldSession admitted, byte[] before, byte[] after)
        {
            this.plan = plan; this.store = store; this.binding = binding; this.admitted = admitted;
            this.before = before; this.after = after;
        }

        internal long AccountId => plan.account;
        internal long CharacterUid => plan.source.Uid;
        internal WorldSession Admitted => admitted;
        internal LegacyOrdinarySpellBinding Binding => binding;
        internal byte[] CopyBeforeImage() => before.ToArray();
        internal byte[] CopyAdmittedImage() => after.ToArray();

        internal bool IsCurrentPlain()
        {
            try
            {
                return plan.IsPublishedNativeOwnerCurrent() && plan.Recovery.IsReadyForUsers(plan.roots.Users) &&
                    plan.Recovery.TryRequireLiveJournalClear(out _) && ReferenceEquals(plan.Candidate, admitted) &&
                    ReferenceEquals(plan.storeLease?.StoreForGearOrigin, store) &&
                    ReferenceEquals(binding.Store, store) && ReferenceEquals(binding.Connection, plan.connection) &&
                    ReferenceEquals(plan.connection.NativeGameClient, binding.Client) &&
                    admitted.AccountId == plan.account && admitted.Character.Uid == plan.source.Uid &&
                    store.TryGet(plan.connection, out var current) && ReferenceEquals(current, admitted) &&
                    store.IsBoundSpellOwnerCurrent(binding) &&
                    CharacterSnapshotFileBaseline.TryReadBytes(plan.roots.PathFor(LegacyTradeJournalFileKind.Character, plan.account),
                        "experienceAdmissionOrigin", out var currentImage, out _) && currentImage.AsSpan().SequenceEqual(after);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or
                InvalidDataException or ArgumentException or NotSupportedException or System.Security.SecurityException)
            { return false; }
        }

        internal static ExperienceAdmissionOrigin? Capture(LegacyOrdinaryAdmissionPlan plan, ExperienceBeforeNativeEvidence before)
        {
            if (!before.MatchesPublishedPlan(plan) || plan.Candidate is not { } admitted || plan.native?.OrdinarySpellBinding is not { } binding ||
                plan.storeLease?.StoreForGearOrigin is not { } store ||
                plan.images.GetValueOrDefault(LegacyTradeJournalFileKind.Character) is not { } beforeImage ||
                beforeImage.Length == 0 || plan.characterAfter.Length == 0) return null;
            var origin = new ExperienceAdmissionOrigin(plan, store, binding, admitted, beforeImage.ToArray(), plan.characterAfter.ToArray());
            return origin.IsCurrentPlain() ? origin : null;
        }
    }

    internal ExperienceAdmissionOrigin? CaptureExperienceAdmissionOrigin(ExperienceBeforeNativeEvidence before) =>
        ExperienceAdmissionOrigin.Capture(this, before);

    // Denial-only proof for a committed initial publication whose stronger
    // image-based origin could not be captured. It grants no resume authority.
    internal bool CoversPendingAdmissionCleanup(WorldSessionStore store, WorldSession expected,
        LegacyOrdinarySpellBinding binding) => committed && published && ReferenceEquals(Candidate, expected) &&
        ReferenceEquals(storeLease?.StoreForGearOrigin, store) && ReferenceEquals(binding.Store, store) &&
        ReferenceEquals(binding.Connection, connection) && ReferenceEquals(expected.NativeGameClient, native) &&
        ReferenceEquals(binding.Client, native) && expected.AccountId == account && expected.Character.Uid == source.Uid;
}
