using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

public sealed partial class CharacterSnapshotLoader
{
    private sealed class PaidNobilityPersistenceException : Exception
    {
        internal PaidNobilityPersistenceException(bool uncertain, string reason) : base(reason) => Uncertain = uncertain;
        internal bool Uncertain { get; }
    }

    private static CharacterSnapshot RestoreExpiredPaidNobility(string path, CharacterSnapshot snapshot)
    {
        if (ReferenceEquals(PrepareExpiredPaidNobility(snapshot), snapshot)) return snapshot;
        return CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
        {
            if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "paidNobility", out var before, out var reason))
                throw new PaidNobilityPersistenceException(false, reason);
            if (!CharacterSnapshotFileBaseline.TryCaptureBytes(before, "paidNobility", out _, out _))
                throw new PaidNobilityStorageException();
            CharacterSnapshot current;
            try
            {
                var text = CharacterSnapshotFileEncoding.Decode(before, out _);
                // Validate both tracked sections even if the fresh paid state has
                // since been renewed and therefore needs no replacement.
                _ = LegacyOrdinaryAdmissionCharacterText.Project(text,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                current = CharacterSnapshotParser.ParseText(text, Path.GetFileName(path));
            }
            catch (Exception error) when (error is FormatException or InvalidDataException)
            {
                throw new PaidNobilityStorageException();
            }
            if (current.Uid != snapshot.Uid)
                throw new PaidNobilityPersistenceException(false, "paidNobilityCharacterUidChanged");
            current = current with { PlayerQuests = snapshot.PlayerQuests };
            var updated = PrepareExpiredPaidNobility(current);
            if (ReferenceEquals(updated, current)) return current;
            byte[] after;
            try
            {
                after = ProjectPaidExpiryImage(before, updated);
            }
            catch (InvalidDataException)
            {
                throw new PaidNobilityStorageException();
            }
            if (!CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, before, after, out var uncertain, out reason))
                throw new PaidNobilityPersistenceException(uncertain, reason);
            return updated;
        });
    }

    private static byte[] ProjectPaidExpiryImage(byte[] before, CharacterSnapshot updated)
    {
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(before))
            throw new InvalidDataException("managedOriginEncodingInvalid");
        var character = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in new[] { "DonationNobility", "LastNobilityDonation", "NobilityRank", "NobilityPosition" })
            character.Add(key, updated.Attributes[key]);
        var paid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in new[] { "Identifier", "LastNobilityDonation", "PaidRank", "PeriodTime", "IsActive", "Rank", "Position" })
            paid.Add(key, updated.PaidNobility[key]);
        string projected = LegacyOrdinaryAdmissionCharacterText.Project(text, character, paid);
        return encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
    }
}
