using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string MailboxClaimPrefix = "mailboxClaim";
    private const string MailboxClaimRecoveryRequired = "mailboxClaimRecoveryRequired";

    private static async ValueTask HandleLegacyMailboxClaimsAsync(WorldPacketHandlerContext context,
        LegacyMailboxModePacket packet, CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
            context.TradeOperations is not { } operations || context.TradeRecovery is not { } recovery ||
            context.Mailboxes is not { } mailboxes)
        {
            await context.Log.WriteLineAsync("WorldServer skipped mailbox claim; required owners unavailable.").ConfigureAwait(false);
            return;
        }
        LegacyMailboxClaimPlan? delivery = null;
        var failure = MailboxClaimPrefix + "OwnerUnavailable";
        using (await operations.EnterExclusiveAsync(cancellationToken).ConfigureAwait(false))
        lock (trades.SyncRoot)
        lock (mailboxes.SyncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var durable = false;
            try
            {
                if (TryCaptureLegacyMailboxClaimOwner(context, out var owner) &&
                    TryReadLegacyMailboxClaimBaseline(owner, out var baseline, out failure) &&
                    TryPrepareLegacyMailboxClaims(baseline, packet, out var plan, out failure))
                {
                    if (!plan.Changed)
                    {
                        if (MailboxClaimAdmissionCurrent(owner) && baseline.Items.IsCurrent(owner.Expected.PlayerItems) &&
                            baseline.Mail.IsCurrent()) delivery = plan;
                    }
                    else if (sessions.TryCommitSession(context.Connection, owner.Expected, plan.Updated, () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!MailboxClaimAdmissionCurrent(owner)) return false;
                        if (!TryPersistLegacyMailboxClaim(plan, out failure)) return false;
                        durable = true;
                        mailboxes.PublishClaims(baseline.Mail, plan.MailAfter);
                        return true;
                    })) delivery = plan;
                }
            }
            catch (Exception error) when (IsMailboxClaimPreparationFailure(error))
            { failure = (durable ? MailboxClaimRecoveryRequired + ":durableCommittedMemoryPublication:" :
                    MailboxClaimPrefix + "Preparation:") + error.GetType().Name; }
            if (failure.StartsWith(MailboxClaimRecoveryRequired, StringComparison.Ordinal))
            { recovery.Block(failure); mailboxes.BlockClaims(failure); }
        }
        if (delivery is null)
        {
            await context.Log.WriteLineAsync($"WorldServer skipped mailbox claim type={packet.Type} reason={failure}; " +
                (recovery.IsBlocked ? "durable outcome requires recovery; success publication stopped."
                    : "no claim committed.")).ConfigureAwait(false);
            return;
        }
        // No world-operation lease or file/mailbox monitor is held across transport awaits.
        foreach (var frame in delivery.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!delivery.ItemsAfter.IsCurrent(delivery.Updated.PlayerItems) ||
                !delivery.Updated.Character.Attributes.SequenceEqual(delivery.UpdatedFields) ||
                !IsCurrentLegacyMailboxClaimOwner(delivery.Baseline.Owner, delivery.Updated))
            {
                await context.Log.WriteLineAsync("WorldServer mailbox claim delivery stopped; owner retired after outcome.")
                    .ConfigureAwait(false);
                return;
            }
            // A send exception is postcommit when Changed; never refund/restore attachments.
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        if (IsLegacyMailboxDeletion(packet.Type))
        {
            await LogLegacyMailboxDeletionAsync(context, packet, delivery).ConfigureAwait(false);
            return;
        }
        await context.Log.WriteLineAsync($"WorldServer handled LegacyMailboxMode type={packet.Type} id={packet.Id} " +
            $"money={delivery.Updated.Character.Money - delivery.Baseline.Owner.Expected.Character.Money} " +
            $"eMoney={delivery.Updated.Character.ConquerPoints - delivery.Baseline.Owner.Expected.Character.ConquerPoints} " +
            $"committed={delivery.Changed} ack={delivery.Frames.Length != 0}.").ConfigureAwait(false);
    }

    private static bool IsMailboxClaimPreparationFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        ArgumentException or InvalidOperationException or OverflowException or FormatException or
        IndexOutOfRangeException or NotSupportedException or System.Security.SecurityException;
}
