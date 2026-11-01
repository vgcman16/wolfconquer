using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    private OrdinaryAdmissionOwner? ordinaryAdmissionOwner;

    // This receipt only proves birth from the actual ordinary admission plan.
    // It does not grant a current disk lease, gear mutation or combat action.
    internal bool IsAdmissionOriginCurrent(LegacyOrdinarySpellBinding binding, WorldSession current) =>
        ordinaryAdmissionOwner is { } owner && owner.IsPublishedCurrent(binding, current);

    internal sealed class OrdinaryAdmissionOwner
    {
        private readonly LegacyOrdinaryAdmissionPlan plan;
        private readonly WorldServerFrameHandler frame;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeMyCombatGear gear;
        private readonly LegacyNativeCombatGearTable.MailboxLoadProof? tables;
        private readonly CombatGearSnapshot saved;
        private readonly MailboxGearState state;
        private readonly byte[] image;
        private readonly string path;
        private readonly uint uid, job;
        private bool published;

        private OrdinaryAdmissionOwner(LegacyOrdinaryAdmissionPlan plan, WorldServerFrameHandler frame, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, LegacyNativeMyCombatGear gear,
            LegacyNativeCombatGearTable.MailboxLoadProof? tables, CombatGearSnapshot saved, byte[] image,
            uint uid, uint job, string path)
        {
            this.plan = plan; this.frame = frame; this.client = client; this.player = player; this.gear = gear; this.tables = tables;
            this.saved = saved; this.image = CopyMailboxGearImage(saved, image); this.uid = uid; this.job = job;
            this.path = path;
            state = MailboxGearState.Capture(gear, saved.Items.Count, saved.BlackJadeBag.Count);
            if (!MailboxGearImage(state.ToSnapshot(saved)).AsSpan().SequenceEqual(this.image)) throw MailboxRewardUnavailable();
        }

        internal static OrdinaryAdmissionOwner? Prepare(LegacyOrdinaryAdmissionPlan plan, WorldServerFrameHandler frame,
            LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player, CharacterSnapshot source,
            CombatGearSnapshot input, byte[] image, string path)
        {
            if (plan is null || frame is null || client is null || player is null || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) || client.CombatGear is not null || client.Fake || client.FullLoading ||
                source.Uid is <= 0 or > uint.MaxValue || source.Class is < 0 or > uint.MaxValue || string.IsNullOrWhiteSpace(path) ||
                !LegacyTradeJournal.TryReadCompleteMailboxGear(image, out var parsed) || parsed is null)
                throw MailboxRewardUnavailable();
            var saved = CloneMailboxGearSnapshot(parsed);
            if (saved.Items.Any(row => row.ItemID == 0) || saved.BlackJadeBag.BlackJades.Any(id => id == 0))
                throw MailboxRewardUnavailable();
            if (!MailboxGearImage(CloneMailboxGearSnapshot(input)).AsSpan().SequenceEqual(image)) throw MailboxRewardUnavailable();
            LegacyNativeCombatGearTable.MailboxLoadProof? tables = null;
            LegacyNativeMyCombatGear gear;
            if (saved.Items.Count != 0 || saved.BlackJadeBag.Count != 0)
            {
                if (!frame.TryCaptureOrdinaryGearMetadata(out tables) || tables is null)
                    return null; // Valid managed gear stays available; native use remains held.
                gear = RestoreMailboxGear(client, saved, checked((uint)source.Class), tables);
            }
            else
            {
                gear = new LegacyNativeMyCombatGear(client)
                {
                    MasteryPoints = saved.MasteryPoints, TaoistReiki = saved.TaoistReiki,
                    PirateExp = saved.PirateExp, Lottery = saved.Lottery,
                };
                var air = saved.AirPowers;
                gear.AirPowers.SenseLevel = air.SenseLevel; gear.AirPowers.ArmedLevel = air.ArmedLevel;
                gear.AirPowers.OverLoadLevel = air.OverLoadLevel; gear.AirPowers.Points = air.Points;
                gear.AirPowers.SenseAipPower = air.SenseAipPower; gear.AirPowers.ArmedAipPower = air.ArmedAipPower;
                gear.AirPowers.OverLoadAipPower = air.OverLoadAipPower;
                gear.SetClass(checked((uint)source.Class));
            }
            var owner = new OrdinaryAdmissionOwner(plan, frame, client, player, gear, tables, saved, image,
                checked((uint)source.Uid), checked((uint)source.Class), path);
            if (!owner.IsDetachedCurrent()) throw MailboxRewardUnavailable();
            return owner;
        }

        internal bool IsDetachedCurrent() => !published && client.CombatGear is null &&
            ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) &&
            ReferenceEquals(gear.combatConstructorOwner, client) && ReferenceEquals(gear.user, client) &&
            !client.Fake && !client.FullLoading && gear.GetClass() == job && TablesCurrent() &&
            state.IsCurrent(gear, state.Scalars.PirateExp, state.Air.Points);

        private bool TablesCurrent() => tables is null ||
            frame.TryCaptureOrdinaryGearMetadata(out var current) && ReferenceEquals(current, tables);

        internal void Publish()
        {
            var accepted = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                if (!IsDetachedCurrent() || player.UID != uid || player.Class != job ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(path, "ordinaryAdmissionGear", out var actual, out _) ||
                    !actual.AsSpan().SequenceEqual(image) || !IsDetachedCurrent()) return false;
                gear.ordinaryAdmissionOwner = this;
                client.CombatGear = gear;
                published = true;
                return true;
            });
            if (!accepted) throw MailboxRewardUnavailable();
        }

        internal bool IsPublishedCurrent(LegacyOrdinarySpellBinding binding, WorldSession current) =>
            published && ReferenceEquals(client.CombatGear, gear) && gear.IsCombatOwnedBy(client) &&
            ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) &&
            ReferenceEquals(binding.Client, client) && ReferenceEquals(binding.Player, player) &&
            binding.Store.IsBoundSpellOwnerCurrent(binding) && TablesCurrent() &&
            player.UID == uid && player.Class == job && plan.IsPublishedGearOriginCurrent(this, binding, current) &&
            state.IsCurrent(gear, state.Scalars.PirateExp, state.Air.Points);
    }
}
