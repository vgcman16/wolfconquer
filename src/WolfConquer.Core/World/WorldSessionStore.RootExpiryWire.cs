using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Both types are inert unless associated with the private Store tables.
    internal sealed class RootExpiryOutputReceipt { }
    internal sealed class RootExpiryWritePermit { }
    private sealed class RootExpiryOutputData(ConquerTcpConnection connection, SessionHolder holder,
        object identity, WorldSession session, long revision, byte[] frame, LegacyNativeAttributeMapProof fields)
    {
        internal readonly ConquerTcpConnection Connection = connection;
        internal readonly SessionHolder Holder = holder;
        internal readonly object Identity = identity;
        internal readonly WorldSession Session = session;
        internal readonly long Revision = revision;
        internal readonly byte[] Frame = frame;
        internal readonly LegacyNativeAttributeMapProof Fields = fields;
        internal readonly HashSet<ConquerTcpConnection> Issued = new(ReferenceEqualityComparer.Instance);
        internal bool Published;
    }
    private sealed record RootExpiryWriteData(RootExpiryOutputData Output, ConquerTcpConnection Recipient,
        SessionHolder RecipientHolder, WorldSession RecipientSession, object RecipientIdentity);
    private readonly ConditionalWeakTable<RootExpiryOutputReceipt, RootExpiryOutputData> rootExpiryOutputs = new();
    private readonly ConditionalWeakTable<RootExpiryWritePermit, RootExpiryWriteData> rootExpiryWrites = new();

    private RootExpiryOutputReceipt PrepareRootExpiryOutput(ConquerTcpConnection connection, SessionHolder holder,
        WorldSession final, object identity)
    {
        var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
            checked((uint)final.Character.Uid), LegacyPlayerDeathState.CreateStatusFlagVector(final.Character.Attributes,
                LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
        var fields = LegacyNativeAttributeMapProof.CaptureRaw(final.Character.Attributes);
        if (!fields.IsCurrentPlain()) throw InvalidSpellPublication();
        var receipt = new RootExpiryOutputReceipt();
        // The next holder revision is reserved under its real connection gate.
        rootExpiryOutputs.Add(receipt, new(connection, holder, identity, final, unchecked(holder.RootExpiryRevision + 1), frame, fields));
        return receipt;
    }

    private void PublishRootExpiryOutput(RootExpiryOutputReceipt receipt)
    {
        if (!rootExpiryOutputs.TryGetValue(receipt, out var data) || data.Published ||
            !ReferenceEquals(data.Holder.Read(), data.Session) || !ReferenceEquals(data.Holder.NativeClientIdentity, data.Identity) ||
            data.Holder.RootExpiryRevision != data.Revision || !data.Fields.IsCurrentPlain()) throw InvalidSpellPublication();
        Volatile.Write(ref data.Published, true);
    }

    internal bool TryIssueRootExpiryWrite(RootExpiryOutputReceipt receipt, WorldActiveSession recipient,
        CancellationToken cancellationToken, out RootExpiryWritePermit? permit)
    {
        permit = null;
        if (!rootExpiryOutputs.TryGetValue(receipt, out var data) || !Volatile.Read(ref data.Published) ||
            !_connectionGates.TryGetValue(data.Connection, out var subjectGate) ||
            !_connectionGates.TryGetValue(recipient.Connection, out var recipientGate)) return false;
        object first = GetConnectionGateOrder(data.Connection) <= GetConnectionGateOrder(recipient.Connection) ? subjectGate : recipientGate;
        object second = ReferenceEquals(first, subjectGate) ? recipientGate : subjectGate;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGet(data.Connection, out var candidateSubject) || candidateSubject is null) return false;
            var owner = ReferenceEquals(data.Connection, recipient.Connection);
            WorldSession candidateViewer;
            if (owner) candidateViewer = candidateSubject;
            else
            {
                if (!TryGet(recipient.Connection, out var currentViewer) || currentViewer is null ||
                    currentViewer.Character.Uid != recipient.Session.Character.Uid) return false;
                candidateViewer = currentViewer;
            }
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, candidateSubject, candidateViewer);
            lock (first) lock (second)
            {
                using var native = EnterNativeConnectionGate();
                if (!TryReadRootExpirySubject(data, out var subject)) return false;
                if (!ReferenceEquals(subject, candidateSubject)) continue;
                if (!_sessions.TryGetValue(recipient.Connection, out var holder)) return false;
                // A legitimate peer ticker/HP publication can supersede the
                // enumeration snapshot. Retry with its actual current mailbox
                // scope; DATA from the old snapshot never authorizes the write.
                if (!ReferenceEquals(holder.Read(), candidateViewer)) continue;
                if (!WorldServerFrameHandler.IsRootExpiryRecipientVisible(
                    data.Connection, subject, recipient.Connection, candidateViewer)) return false;
                LegacyNativeAttributeMapProof fields;
                try { fields = LegacyNativeAttributeMapProof.CaptureRaw(candidateViewer.Character.Attributes); }
                catch (Exception error) when (error is InvalidOperationException or ArgumentException or FormatException or OverflowException)
                { return false; }
                if (!fields.IsCurrentPlain()) return false;
                lock (data)
                {
                    if (!data.Issued.Add(recipient.Connection)) return false;
                    var token = new RootExpiryWritePermit();
                    rootExpiryWrites.Add(token, new(data, recipient.Connection, holder, candidateViewer,
                        holder.NativeClientIdentity));
                    permit = token; return true;
                }
            }
        }
    }

    // The expiry publication is a trigger, not a frozen image. A real
    // Store successor with the same native connection can carry unrelated HP
    // changes or a new flag generation. The gate-entry producer reads that
    // actual holder and serializes its current vector instead of replaying the
    // old image. Session/UID/identity are never supplied as fresh authority.
    private bool TryReadRootExpirySubject(RootExpiryOutputData data, out WorldSession subject)
    {
        subject = null!;
        if (!Volatile.Read(ref data.Published) || !_sessions.TryGetValue(data.Connection, out var holder) ||
            !ReferenceEquals(holder, data.Holder) || !ReferenceEquals(holder.NativeClientIdentity, data.Identity) ||
            holder.Read() is not { } actual || actual.Character.Uid != data.Session.Character.Uid) return false;
        subject = actual; return true;
    }

    // Called ONLY after the actual connection's transport write gate was taken.
    // The private permit determines acceptance. The trusted transport starts
    // its real write synchronously while both exact holder gates remain held;
    // the gates are released before its asynchronous completion is awaited.
    internal bool TryStartRootExpiryWrite(RootExpiryWritePermit permit, ConquerTcpConnection recipient,
        RootExpiryWireWriter writer, object activeStart, CancellationToken cancellationToken, out ValueTask write)
    {
        write = default;
        if (!rootExpiryWrites.TryGetValue(permit, out var data) || !ReferenceEquals(data.Recipient, recipient) ||
            !ReferenceEquals(writer.Connection, recipient) || !recipient.IsRegisteredRootExpiryWire(writer) ||
            !writer.ConsumeActiveStart(activeStart) || !_connectionGates.TryGetValue(data.Output.Connection, out var subjectGate) ||
            !_connectionGates.TryGetValue(recipient, out var recipientGate)) return false;
        object first = GetConnectionGateOrder(data.Output.Connection) <= GetConnectionGateOrder(recipient) ? subjectGate : recipientGate;
        object second = ReferenceEquals(first, subjectGate) ? recipientGate : subjectGate;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGet(data.Output.Connection, out var candidateSubject) || !TryGet(recipient, out var candidateViewer)) return false;
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, candidateSubject, candidateViewer);
            lock (first) lock (second)
            {
                using var native = EnterNativeConnectionGate();
                if (!TryReadRootExpirySubject(data.Output, out var subject) ||
                    !_sessions.TryGetValue(recipient, out var holder) || !ReferenceEquals(holder, data.RecipientHolder) ||
                    !ReferenceEquals(holder.NativeClientIdentity, data.RecipientIdentity) || holder.Read() is not { } viewer ||
                    viewer.Character.Uid != data.RecipientSession.Character.Uid) return false;
                // Retry with the actual successor's mailbox scope rather than
                // use a scope captured for a stale subject or recipient.
                if (!ReferenceEquals(subject, candidateSubject) || !ReferenceEquals(viewer, candidateViewer)) continue;
                if (!WorldServerFrameHandler.IsRootExpiryRecipientVisible(data.Output.Connection, subject, recipient, viewer)) return false;
                byte[] frame;
                try
                {
                    _ = LegacyWindWalkerStatusProjection.Read(subject.Character); // Both real status-vector aliases must be unambiguous.
                    var subjectFields = LegacyNativeAttributeMapProof.CaptureRaw(subject.Character.Attributes);
                    var viewerFields = LegacyNativeAttributeMapProof.CaptureRaw(viewer.Character.Attributes);
                    frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                        checked((uint)subject.Character.Uid), LegacyPlayerDeathState.CreateStatusFlagVector(subject.Character.Attributes,
                            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
                    if (!subjectFields.IsCurrentPlain() || !viewerFields.IsCurrentPlain() || !rootExpiryWrites.Remove(permit)) return false;
                }
                catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
                { return false; }
                cancellationToken.ThrowIfCancellationRequested();
                write = writer.StartActualWire(frame, cancellationToken);
                return true;
            }
        }
    }
}
