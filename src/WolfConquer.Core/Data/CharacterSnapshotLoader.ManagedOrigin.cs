using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

public sealed partial class CharacterSnapshotLoader
{
    // A data receipt for the actual configured loader operation. It is not a
    // session, HP writer, native owner, or permission to publish an outcome.
    internal sealed record ManagedOriginLoad(CharacterSnapshotLoadResult Result, string Path, byte[] Image);

    internal ManagedOriginLoad LoadManagedOrigin(long accountId)
    {
        if (accountId <= 0) throw new InvalidDataException("managedOriginAccountInvalid");
        string path = Path.Combine(_usersRoot, accountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        return CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
        {
            if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "managedOrigin", out var before, out _))
                throw new InvalidDataException("managedOriginCharacterUnavailable");
            // This is the real configured loader, without its older implicit
            // expiry write. Its receipt-aware expiry write follows below.
            var loaded = TryLoadByAccountId(accountId, persistExpiry: false);
            if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "managedOrigin", out var current, out _) ||
                !current.AsSpan().SequenceEqual(before))
                throw new InvalidDataException("managedOriginLoaderImageChanged");
            // Preserve the real loader's error status/diagnostic. An unsuccessful
            // read result cannot be bound as a successful startup origin.
            if (!loaded.Succeeded || loaded.Snapshot is not { } snapshot)
                return new ManagedOriginLoad(loaded, path, before);
            if (snapshot.Uid != accountId) throw new InvalidDataException("managedOriginLoaderUidChanged");
            var updated = PrepareExpiredPaidNobility(snapshot);
            if (ReferenceEquals(updated, snapshot)) return new ManagedOriginLoad(loaded, path, before);

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
            var after = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
            if (!CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, before, after, out var uncertain, out var reason))
                throw new InvalidDataException(uncertain ? "managedOriginExpiryOutcomeUnproven:" + reason : "managedOriginExpiryRefused:" + reason);
            // The receipt is the exact before/after writer result, never an
            // independently reread file promoted to ownership.
            return new ManagedOriginLoad(loaded with { Snapshot = updated }, path, after);
        });
    }
}
