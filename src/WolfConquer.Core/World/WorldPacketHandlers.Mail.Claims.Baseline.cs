using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyMailboxClaimPaths(string Users, string ItemsRoot, string MailboxRoot,
        string Character, string Items, string Mailbox)
    {
        internal string[] All => [Character, Items, Mailbox];
    }

    private sealed record LegacyMailboxClaimBaseline(LegacyMailboxClaimOwner Owner, LegacyMailboxClaimPaths Paths,
        CharacterSnapshotFileBaseline.Snapshot Character, byte[] ItemsBefore,
        LegacyItemSaveGraph Items, LegacyMailboxStore.ClaimCapture Mail);

    private static bool TryReadLegacyMailboxClaimBaseline(LegacyMailboxClaimOwner owner,
        out LegacyMailboxClaimBaseline baseline, out string reason)
    {
        baseline = null!;
        reason = MailboxClaimPrefix + "StorageBindingInvalid";
        var expected = owner.Expected;
        if (!MailboxClaimAdmissionCurrent(owner) ||
            !LegacyItemSaveGraph.TryCapture(expected.PlayerItems, out var graph) ||
            !TryMailboxClaimPaths(owner, out var paths)) return false;
        LegacyMailboxClaimBaseline? captured = null;
        var failure = MailboxClaimPrefix + "OwnerChanged";
        var valid = false;
        // Same order as persisted CAS: operation -> Trade -> mailbox -> Store -> files.
        owner.Context.Sessions!.WithCurrentNativeClientIdentity(owner.Context.Connection,
            expected.Character.Uid, identity =>
        {
            if (!ReferenceEquals(identity, owner.Identity) || !MailboxClaimAdmissionCurrent(owner)) return;
            valid = SnapshotFileLockCoordinator.ExecuteLocked(paths.All, () =>
            {
                if (LegacyTradeJournal.HasPending(paths.Users))
                { failure = MailboxClaimRecoveryRequired + ":pending"; return false; }
                if (!CharacterSnapshotFileBaseline.TryRead(paths.Character, MailboxClaimPrefix, out var character, out failure) ||
                    !ValidateMailboxClaimCharacter(character!, expected) ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(paths.Items, MailboxClaimPrefix, out var items, out failure) ||
                    !graph.HasBytes(items) || !graph.IsCurrent(expected.PlayerItems) ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(paths.Mailbox, MailboxClaimPrefix, out var mail, out failure) ||
                    !owner.Context.Mailboxes!.TryCaptureClaims(checked((uint)expected.Character.Uid), mail,
                        out var capture, out failure))
                { if (failure.Length == 0) failure = MailboxClaimPrefix + "BaselineStale"; return false; }
                if (!MailboxClaimAdmissionCurrent(owner)) return false;
                captured = new(owner, paths, character!, items, graph, capture);
                return true;
            });
        });
        baseline = captured!;
        reason = failure;
        return valid;
    }

    private static bool TryMailboxClaimPaths(LegacyMailboxClaimOwner owner, out LegacyMailboxClaimPaths paths)
    {
        paths = null!;
        var expected = owner.Expected;
        var usersRoot = expected.UsersRoot;
        var itemsRoot = owner.Context.PlayerItemsRoot;
        var mailboxRoot = owner.Context.Mailboxes!.ClaimRoot;
        if (string.IsNullOrWhiteSpace(usersRoot) || string.IsNullOrWhiteSpace(itemsRoot) ||
            string.IsNullOrWhiteSpace(mailboxRoot) || !Path.IsPathFullyQualified(usersRoot) ||
            !Path.IsPathFullyQualified(itemsRoot) || !Path.IsPathFullyQualified(mailboxRoot) ||
            !LegacyTradeJournal.IsMailboxDomainConfigured(usersRoot, itemsRoot, mailboxRoot)) return false;
        var users = Path.GetFullPath(usersRoot);
        var items = Path.GetFullPath(itemsRoot);
        var mailbox = Path.GetFullPath(mailboxRoot);
        var name = expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini";
        var character = Path.Combine(users, name);
        var source = expected.Character.SourceFile;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(source, name, comparison) && (string.IsNullOrWhiteSpace(source) ||
            !Path.IsPathFullyQualified(source) || !string.Equals(Path.GetFullPath(source), character, comparison))) return false;
        var leaf = expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin";
        paths = new(users, items, mailbox, character, Path.Combine(items, leaf), Path.Combine(mailbox, leaf));
        return true;
    }

    private static bool ValidateMailboxClaimCharacter(CharacterSnapshotFileBaseline.Snapshot file, WorldSession expected)
    {
        var text = CharacterSnapshotFileEncoding.Decode(file.Bytes, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(file.Bytes)) return false;
        var parsed = CharacterSnapshotParser.ParseText(text, expected.Character.SourceFile);
        if (parsed.Uid != expected.Character.Uid || parsed.Money != expected.Character.Money ||
            parsed.ConquerPoints != expected.Character.ConquerPoints ||
            !LegacyTradeEscrowClaims.TryRead(parsed.Attributes, out var diskClaim, out _) ||
            !LegacyTradeEscrowClaims.TryRead(expected.Character.Attributes, out var liveClaim, out _) || diskClaim != liveClaim ||
            !LegacySecondaryPasswordState.TryRead(parsed.Attributes, out _, out _)) return false;
        var current = new Dictionary<string, string>(expected.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        var hasPin = current.TryGetValue("SecurityPass", out var livePin);
        return hasPin == parsed.Attributes.TryGetValue("SecurityPass", out var diskPin) &&
            (!hasPin || string.Equals(livePin, diskPin, StringComparison.Ordinal));
    }
}
