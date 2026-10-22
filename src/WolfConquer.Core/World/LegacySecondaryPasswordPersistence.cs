using System.Collections.ObjectModel;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Native ServerDatabase stores Password,OnReset,ResetAt.Ticks followed by a comma.
// This owner changes only that field, never wallet, escrow, or unrelated character state.
internal static partial class LegacySecondaryPasswordPersistence
{
    private const string StateKey = "SecurityPass";
    private const string RecoveryRequired = "secondaryPasswordRecoveryRequired";

    internal static WorldSession WithState(WorldSession session, LegacySecondaryPasswordState state)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(state);
        var attrs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in session.Character.Attributes)
            if (!key.Equals(StateKey, StringComparison.OrdinalIgnoreCase)) attrs.Add(key, value);
        attrs.Add(StateKey, state.ToLegacyText());
        return session with
        {
            Character = session.Character with { Attributes = new ReadOnlyDictionary<string, string>(attrs) },
        };
    }

    internal static bool TryValidateBaseline(WorldSession expected, out string reason)
    {
        reason = string.Empty;
        try
        {
            if (!TryGetPath(expected, out var path, out reason)) return false;
            var failure = string.Empty;
            var valid = SnapshotFileLockCoordinator.ExecuteLocked(path,
                () => TryValidateLocked(path, expected, out _, out failure));
            reason = failure;
            return valid;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        { reason = "secondaryPasswordBaselineRead:" + ex.GetType().Name; return false; }
    }

    internal static bool TryPersist(WorldSession original, LegacySecondaryPasswordState updated, out string reason)
    {
        reason = string.Empty;
        if (updated is null) { reason = "secondaryPasswordUpdatedStateInvalid"; return false; }
        var updatedText = updated.ToLegacyText();
        if (!LegacySecondaryPasswordState.TryRead(new Dictionary<string, string> { [StateKey] = updatedText }, out _, out _))
        { reason = "secondaryPasswordUpdatedStateInvalid"; return false; }
        try
        {
            if (!TryGetPath(original, out var path, out reason)) return false;
            var failure = string.Empty;
            var saved = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
                PersistLocked(path, original, updated, updatedText, out failure));
            reason = failure;
            return saved;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        { reason = "secondaryPasswordPersistence:" + ex.GetType().Name; return false; }
    }

    private static bool PersistLocked(string path, WorldSession original, LegacySecondaryPasswordState updated,
        string updatedText, out string reason)
    {
        if (!TryValidateLocked(path, original, out var beforeBytes, out reason)) return false;
        var writeReason = string.Empty;
        try
        {
            // The coordinator reenters the same ordered file lock and retains the file's encoding.
            if (CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(path, original.Character.Uid,
                new Dictionary<string, string> { [StateKey] = updatedText }, detailedIoFailures: false, out writeReason))
            {
                if (TryValidateLocked(path, WithState(original, updated), out _, out _)) return true;
                reason = RecoveryRequired + ":postWriteStateUnproven";
                return false;
            }
        }
        catch (Exception ex) when (IsFileFailure(ex))
        { writeReason = "secondaryPasswordWrite:" + ex.GetType().Name; }

        // A failed OS replacement/rollback is not proof of an unchanged file. Check the
        // complete preimage, including unrelated fields, before allowing live mutations.
        try
        {
            if (TryValidateSafePath(path, out _) && File.ReadAllBytes(path).AsSpan().SequenceEqual(beforeBytes))
            { reason = "secondaryPasswordWriteFailed:" + writeReason; return false; }
        }
        catch (Exception ex) when (IsFileFailure(ex)) { }
        reason = RecoveryRequired + ":writeBaselineUnproven";
        return false;
    }

    private static bool TryGetPath(WorldSession session, out string path, out string reason)
    {
        path = string.Empty;
        reason = string.Empty;
        if (session is null || session.AccountId <= 0 || session.Character.Uid <= 0 || string.IsNullOrWhiteSpace(session.UsersRoot))
        { reason = "secondaryPasswordBindingInvalid"; return false; }
        var root = Path.GetFullPath(session.UsersRoot);
        path = Path.Combine(root, session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        return TryValidateSafePath(path, out reason);
    }

    private static bool TryValidateLocked(string path, WorldSession expected, out byte[] bytes, out string reason)
    {
        bytes = [];
        reason = string.Empty;
        if (!LegacySecondaryPasswordState.TryRead(expected.Character.Attributes, out _, out _))
        { reason = "secondaryPasswordExpectedStateInvalid"; return false; }
        if (!TryValidateSafePath(path, out reason) || !TryReadDisk(path, out var fields, out bytes, out reason)) return false;
        if (!fields.TryGetValue("UID", out var uidText) ||
            !long.TryParse(uidText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid) || uid != expected.Character.Uid)
        { reason = "secondaryPasswordUidMismatch"; return false; }
        if (!LegacySecondaryPasswordState.TryRead(fields, out _, out _))
        { reason = "secondaryPasswordDiskStateInvalid"; return false; }
        var expectedFields = expected.Character.Attributes.Where(pair => pair.Key.Equals(StateKey, StringComparison.OrdinalIgnoreCase)).ToArray();
        var present = fields.TryGetValue(StateKey, out var actual);
        if ((expectedFields.Length == 0) == !present &&
            (!present || string.Equals(expectedFields[0].Value, actual, StringComparison.Ordinal))) return true;
        reason = "secondaryPasswordStateStale";
        return false;
    }

    private static bool IsFileFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or System.Security.SecurityException;
}
