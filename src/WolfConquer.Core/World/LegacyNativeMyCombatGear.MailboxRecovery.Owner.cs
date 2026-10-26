using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    internal sealed class MailboxRecoveryOwner
    {
        private readonly LegacyMailboxPackageProducer producer;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeMyCombatGear gear;
        private readonly MailboxGearState state;
        private readonly LegacyNativeCombatGearTable.MailboxLoadProof tables;
        private readonly long account;
        private readonly uint uid, job, experience;
        private readonly string path;
        private readonly byte[] image;
        private bool published;

        internal MailboxRecoveryOwner(LegacyMailboxPackageProducer producer, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, LegacyNativeMyCombatGear gear,
            LegacyNativeCombatGearTable.MailboxLoadProof tables, long account, CharacterSnapshot saved,
            uint experience, CombatGearSnapshot savedGear, byte[] image)
        {
            this.producer = producer; this.client = client; this.player = player; this.gear = gear;
            state = MailboxGearState.Capture(gear, savedGear.Items.Count, savedGear.BlackJadeBag.Count);
            if (!MailboxGearImage(state.ToSnapshot(savedGear)).AsSpan().SequenceEqual(image)) throw MailboxRewardUnavailable();
            this.tables = tables; this.account = account; uid = checked((uint)saved.Uid); job = checked((uint)saved.Class);
            this.experience = experience;
            path = Path.Combine(producer.CombatGearRoot, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
            this.image = image.ToArray();
        }

        private bool IdentityCurrent() => ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) &&
            ReferenceEquals(gear.combatConstructorOwner, client) && ReferenceEquals(gear.user, client) &&
            !client.FullLoading && !client.Fake && gear.GetClass() == job && ReferenceEquals(gear.mailboxRecoveryTables, tables) &&
            producer.IsCurrent(producer.UsersRoot, producer.ItemsRoot, producer.MailboxRoot, producer.Recovery) && tables.IsCurrent();

        internal bool IsDetachedCurrent() => !published && client.CombatGear is null && IdentityCurrent() &&
            state.IsCurrent(gear, state.Scalars.PirateExp, state.Air.Points);

        internal bool TryPublish(WorldSessionStore sessions, ConquerTcpConnection connection, WorldSession admitted)
        {
            var applied = false;
            try
            {
                sessions.WithCurrentNativeClientIdentity(connection, uid, _identity =>
                {
                    SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
                    {
                        lock (LegacyNativeCombatGearTable.MailboxLoadGate)
                        {
                            if (!CurrentAdmission(sessions, connection, admitted) || !IsDetachedCurrent()) return false;
                            var canonical = admitted.CombatGear is null ? [] : MailboxGearImage(CloneMailboxGearSnapshot(admitted.CombatGear));
                            if (!canonical.AsSpan().SequenceEqual(image)) return false;
                            if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "mailboxRecoveryPirate", out var currentImage, out _) ||
                                !currentImage.AsSpan().SequenceEqual(image) || !CurrentAdmission(sessions, connection, admitted) ||
                                !IsDetachedCurrent() || !FinalAdmissionCurrent(sessions, connection, admitted)) return false;
                            client.CombatGear = gear;
                            published = true;
                            applied = true; // One-way adoption occurred; later changes cannot turn it into a rejection.
                            return true;
                        }
                    });
                });
            }
            catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or
                InvalidOperationException or OverflowException or UnauthorizedAccessException or FormatException) { return false; }
            return applied;
        }

        private bool CurrentAdmission(WorldSessionStore sessions, ConquerTcpConnection connection, WorldSession admitted) =>
            FinalAdmissionCurrent(sessions, connection, admitted) && admitted.Character.Attributes is not null &&
            admitted.Character.Attributes.TryGetValue("ClassExperience", out var raw) &&
            uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var saved) && saved == experience &&
            producer.HasPendingAccountWork(account, out var pending, out _) && pending &&
            FinalAdmissionCurrent(sessions, connection, admitted);

        // Snapshot collections may execute caller code. This last guard contains
        // only actual Store/transport/reference/scalar reads, after all such reads.
        private bool FinalAdmissionCurrent(WorldSessionStore sessions, ConquerTcpConnection connection, WorldSession admitted) =>
            !published && client.CombatGear is null && !client.FullLoading && !client.Fake &&
            sessions.TryGet(connection, out var current) && ReferenceEquals(current, admitted) && admitted.AccountId == account &&
            admitted.Character.Uid == uid && admitted.Character.Class == job && ReferenceEquals(admitted.NativeGameClient, client) &&
            ReferenceEquals(connection.NativeGameClient, client) && ReferenceEquals(client.Player, player) &&
            ReferenceEquals(player.Owner, client) && player.UID == uid && player.Class == job && player.ClassExperience == experience;

        internal bool IsPublishedCurrent() => published && IdentityCurrent() && player.UID == uid && player.Class == job &&
            player.ClassExperience == experience && ReferenceEquals(client.CombatGear, gear) && gear.IsCombatOwnedBy(client) &&
            state.IsCurrent(gear, state.Scalars.PirateExp, state.Air.Points);
    }
}
