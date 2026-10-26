using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    internal MailboxPirateRewardProjection CaptureMailboxPirateReward(LegacyNativeGameClientRuntime client,
        CombatGearSnapshot canonicalSource, byte[] canonicalSourceImage, uint points, uint pirateExp) =>
        MailboxPirateRewardProjection.Create(this, client, canonicalSource, canonicalSourceImage, points, pirateExp);

    internal sealed class MailboxPirateRewardProjection
    {
        private readonly LegacyNativeMyCombatGear gear;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint uid, job, points, afterPoints, afterExp;
        private readonly bool fullLoading, fake, wireGate;
        private readonly LegacyNativeCombatGearTable.MailboxLoadProof tables;
        private readonly MailboxGearState before;
        private readonly CombatGearSnapshot after;
        private readonly byte[] beforeImage, afterImage, finalFrame;
        private bool published;

        private MailboxPirateRewardProjection(LegacyNativeMyCombatGear gear, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, uint uid, uint job, bool fullLoading, bool fake, bool wireGate,
            LegacyNativeCombatGearTable.MailboxLoadProof tables, MailboxGearState before, CombatGearSnapshot after, byte[] beforeImage, byte[] afterImage,
            uint points, byte[] finalFrame)
        {
            this.gear = gear; this.client = client; this.player = player; this.uid = uid; this.job = job;
            this.fullLoading = fullLoading; this.fake = fake; this.wireGate = wireGate; this.before = before;
            this.tables = tables;
            this.after = after; this.beforeImage = beforeImage; this.afterImage = afterImage;
            this.points = points; this.finalFrame = finalFrame;
            afterPoints = after.AirPowers.Points; afterExp = after.PirateExp;
        }

        internal static MailboxPirateRewardProjection Create(LegacyNativeMyCombatGear gear,
            LegacyNativeGameClientRuntime client, CombatGearSnapshot source, byte[] sourceImage,
            uint points, uint pirateExp)
        {
            if (client is null || client.Player is not { } player) throw MailboxRewardUnavailable();
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                try
                {
                    uint uid = player.UID, job = player.Class;
                    if (uid == 0 || !ReferenceEquals(player.Owner, client) || !gear.IsCombatOwnedBy(client) ||
                        !gear.MailboxRecoveryTablesCurrent() || !LegacyNativeCombatGearTable.TryCaptureMailboxLoadProof(out var tables) ||
                        !LegacyNativeAtributesStatus.IsPirate(job) || gear.GetClass() != job)
                        throw MailboxRewardUnavailable();
                    bool phase = client.FullLoading, fake = client.Fake, gate = LegacyNativeCombatGearTable.m_Load;
                    if (sourceImage is null || sourceImage.Length is 0 or > MailboxGearMaximumImage)
                        throw MailboxRewardUnavailable();
                    // Canonical rows require actual bytes; refuse impossible counts
                    // before copying caller-owned lists or invoking the writer.
                    if (source is null || source.BlackJadeBag is null || source.Count > sourceImage.Length ||
                        source.BlackJadeBag.Count > sourceImage.Length) throw MailboxRewardUnavailable();
                    var original = CloneMailboxGearSnapshot(source);
                    var image = CopyMailboxGearImage(original, sourceImage);
                    var state = MailboxGearState.Capture(gear, original.Items.Count, original.BlackJadeBag.Count);
                    if (!MailboxGearImage(state.ToSnapshot(original)).AsSpan().SequenceEqual(image))
                        throw MailboxRewardUnavailable();
                    var next = original with
                    {
                        PirateExp = checked(original.PirateExp + pirateExp),
                        AirPowers = original.AirPowers with { Points = checked(original.AirPowers.Points + points) }
                    };
                    var projection = new MailboxPirateRewardProjection(gear, client, player, uid, job, phase, fake,
                        gate, tables!, state, next, image, MailboxGearImage(next), points,
                        PrepareMailboxPointsFrame(original.AirPowers.Points, points, uid));
                    if (!projection.IsCurrent()) throw MailboxRewardUnavailable();
                    return projection;
                }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
                { throw new InvalidDataException("Mailbox Pirate projection could not capture complete current state.", error); }
            }
        }

        internal byte[] CopyBeforeImage() => beforeImage.ToArray();
        internal byte[] CopyAfterImage() => afterImage.ToArray();
        internal CombatGearSnapshot CopyAfterSnapshot() => CloneMailboxGearSnapshot(after);

        internal byte[] CopyFrameForPoints(uint accumulatedPoints)
        {
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                if (accumulatedPoints > points || !IsCurrent()) throw MailboxRewardUnavailable();
                byte[] frame = accumulatedPoints == points ? finalFrame.ToArray() :
                    PrepareMailboxPointsFrame(before.Air.Points, accumulatedPoints, uid);
                if (!IsCurrent()) throw MailboxRewardUnavailable();
                return frame;
            }
        }

        internal bool IsCurrent()
        {
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
                return !published && OwnerCurrent() && before.IsCurrent(gear, before.Scalars.PirateExp, before.Air.Points);
        }

        internal bool TryPublishCommitted(LegacyMailboxPackageCommitProof proof)
        {
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                if (proof is null || !proof.MatchesCombatGear(uid, beforeImage, afterImage) || !IsCurrent()) return false;
                // Silent field publication only. The genuine journal already owns durability;
                // do not call AddPoints, Save, sends or setters here.
                gear.AirPowers.Points = afterPoints;
                gear.PirateExp = afterExp;
                published = true;
                return true; // Delivery must separately require IsPublishedCurrent().
            }
        }

        internal bool IsPublishedCurrent()
        {
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
                return published && OwnerCurrent() && before.IsCurrent(gear, afterExp, afterPoints);
        }

        private bool OwnerCurrent() => gear.IsCombatOwnedBy(client) && ReferenceEquals(client.Player, player) &&
            tables.IsCurrent() && gear.MailboxRecoveryTablesCurrent() &&
            ReferenceEquals(player.Owner, client) && player.UID == uid && player.Class == job && gear.GetClass() == job &&
            client.FullLoading == fullLoading && client.Fake == fake && LegacyNativeCombatGearTable.m_Load == wireGate;
    }

    private static byte[] PrepareMailboxPointsFrame(uint baseline, uint reward, uint uid)
    {
        _ = checked(baseline + reward);
        var detached = new Powers { Points = baseline };
        byte[]? frame = detached.AddPoints(reward, uid); // Actual native mutation/codec, on detached data only.
        if (frame is null || frame.Length == 0) throw MailboxRewardUnavailable();
        return frame;
    }

    private static InvalidDataException MailboxRewardUnavailable() =>
        new("Mailbox Pirate reward requires a current constructor-owned native Gear and complete canonical source.");
}
