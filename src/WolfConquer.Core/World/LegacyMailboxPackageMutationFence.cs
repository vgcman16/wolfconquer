using System.Globalization;

namespace WolfConquer.Core.World;

// Binds ordinary snapshot writers to a real explicitly-created package producer.
// This is a pending-work fence, not a new authority to publish or allocate items.
internal static partial class LegacyMailboxPackageMutationFence
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, LegacyMailboxPackageProducer> Owners =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    internal static void Register(LegacyMailboxPackageProducer producer)
    { lock (Gate) Owners[producer.UsersRoot] = producer; }

    private static LegacyMailboxPackageProducer[] Snapshot()
    { lock (Gate) return Owners.Values.ToArray(); }

    internal static void RequireFileMutation(string path)
    {
        LegacyOrdinaryAdmissionMutationFence.RequireFileMutation(path);
        var absolute = Path.GetFullPath(path);
        foreach (var owner in Snapshot())
        {
            var parent = Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(absolute)!);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var isCharacter = string.Equals(parent, owner.UsersRoot, comparison);
            var isItems = string.Equals(parent, owner.ItemsRoot, comparison) || string.Equals(parent, owner.MailboxRoot, comparison);
            var isGear = string.Equals(parent, owner.CombatGearRoot, comparison);
            if (!isCharacter && !isItems && !isGear) continue;
            var extension = isItems ? ".bin" : ".ini";
            var leaf = Path.GetFileName(absolute);
            if (!leaf.EndsWith(extension, comparison) || !uint.TryParse(leaf[..^extension.Length], NumberStyles.None,
                CultureInfo.InvariantCulture, out var id) || id == 0) continue;
            if (isCharacter ? !owner.TryRequireAccountWritable(id, out _) : !owner.TryRequireCharacterWritable(id, out _))
                throw new IOException("mailboxClaimRecoveryRequired:pendingPackageSnapshotWrite");
        }
    }

    internal static bool CanMutateSession(LegacyMailboxPackageProducer? bound, WorldSession? previous, WorldSession next)
        => CanMutateSession(bound, previous, next, null);

    internal static bool CanMutateSession(LegacyMailboxPackageProducer? bound, WorldSession? previous, WorldSession next,
        LegacyOrdinaryAdmissionPlan? admission)
    {
        if (!LegacySessionStorageRoot.TryNormalize(previous?.UsersRoot, out var oldRoot) ||
            !LegacySessionStorageRoot.TryNormalize(next.UsersRoot, out var nextRoot) ||
            !LegacyOrdinaryAdmissionMutationFence.CanMutateSession(previous, next, admission)) return false;
        if (bound is not null && (!bound.TryRequireAccountWritable(next.AccountId, out _) ||
            previous is not null && !bound.TryRequireAccountWritable(previous.AccountId, out _))) return false;
        foreach (var owner in Snapshot())
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (nextRoot is not null && string.Equals(nextRoot, owner.UsersRoot, comparison) &&
                !owner.TryRequireAccountWritable(next.AccountId, out _)) return false;
            if (previous is not null && oldRoot is not null && string.Equals(oldRoot, owner.UsersRoot, comparison) &&
                !owner.TryRequireAccountWritable(previous.AccountId, out _)) return false;
        }
        return true;
    }
}
