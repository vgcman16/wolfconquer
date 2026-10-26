using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    internal static bool TryPrepareMailboxRecoveryOwner(LegacyMailboxPackageProducer producer,
        LegacyNativeGameClientRuntime client, long account, CharacterSnapshot snapshot,
        out MailboxRecoveryOwner? prepared, out string reason)
    {
        prepared = null; reason = "mailboxRecoveryPirate:OwnerUnavailable";
        try
        {
            if (producer is null || snapshot is null || client?.Player is not { } player ||
                !ReferenceEquals(player.Owner, client) || client.FullLoading || client.Fake || client.CombatGear is not null ||
                account is <= 0 or > uint.MaxValue || snapshot.Uid is <= 0 or > uint.MaxValue ||
                snapshot.Class is < 0 or > uint.MaxValue || !LegacyNativeAtributesStatus.IsPirate((uint)snapshot.Class) ||
                player.UID != 0 && player.UID != snapshot.Uid || player.Class != 0 && player.Class != snapshot.Class ||
                !producer.IsCurrent(producer.UsersRoot, producer.ItemsRoot, producer.MailboxRoot, producer.Recovery) ||
                !producer.HasPendingAccountWork(account, out var pending, out reason) || !pending ||
                snapshot.Attributes is null || !snapshot.Attributes.TryGetValue("ClassExperience", out var raw) ||
                !uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var experience))
            {
                if (string.IsNullOrEmpty(reason)) reason = "mailboxRecoveryPirate:OwnerUnavailable";
                return false;
            }
            var path = Path.Combine(producer.CombatGearRoot, snapshot.Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
            MailboxRecoveryOwner? candidate = null;
            var failure = "mailboxRecoveryPirate:CanonicalGearUnavailable";
            var result = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                lock (LegacyNativeCombatGearTable.MailboxLoadGate)
                {
                    if (!LegacyNativeCombatGearTable.TryCaptureMailboxLoadProof(out var tables))
                    { failure = "mailboxRecoveryPirate:CompleteTablesUnavailable"; return false; }
                    if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "mailboxRecoveryPirate", out var image, out _) ||
                        !LegacyTradeJournal.TryReadCompleteMailboxGear(image, out var parsed) || parsed is null) return false;
                    var saved = CloneMailboxGearSnapshot(parsed);
                    var bytes = CopyMailboxGearImage(saved, image);
                    var gear = RestoreMailboxGear(client, saved, checked((uint)snapshot.Class), tables!);
                    if (!tables!.IsCurrent()) return false;
                    candidate = new(producer, client, player, gear, tables, account, snapshot, experience, saved, bytes);
                    return candidate.IsDetachedCurrent();
                }
            });
            if (!result || candidate is null) { reason = failure; return false; }
            prepared = candidate; reason = string.Empty; return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or
            InvalidOperationException or OverflowException or UnauthorizedAccessException or FormatException)
        { reason = "mailboxRecoveryPirate:" + error.GetType().Name; return false; }
    }
}
