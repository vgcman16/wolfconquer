using ProtoBuf;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    // A preparation, not a durable reward or permission to claim a mailbox entry.
    // The real transaction must prove its saved before/after MsgNinja images.
    internal bool TryPrepareMailboxPoints(LegacyNativeGameClientRuntime client, string expectedMsgNinjaLine,
        uint points, out MailboxPointsProjection? projection)
    {
        projection = null;
        try
        {
            if (points == 0 || !MailboxPointsCapture.TryCreate(this, client, out var capture) ||
                !capture!.MatchesLine(expectedMsgNinjaLine, out var parts)) return false;
            var after = checked(Exp + points);
            parts![1] = after.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var afterText = string.Join('/', parts);
            var frame = PrepareMailboxPointsFrame(after);
            if (!capture.IsCurrent()) return false;
            projection = new(capture, expectedMsgNinjaLine, afterText, after, frame);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { return false; }
    }

    private static byte[] PrepareMailboxPointsFrame(uint points)
    {
        // Same statically-object protobuf call and complete required-zero contract
        // as native Packet. No Gear-table gate, unmanaged loan or transport send.
        object message = new LegacyNativeMsgNinja.MsgNinjaUser { Type = 2, Points = points };
        using var output = new MemoryStream();
        Serializer.Serialize((Stream)output, message);
        var writer = new PacketWriter(LegacyNativeMsgNinja.MsgNinjaUser_ID, checked((int)output.Length));
        output.GetBuffer().AsSpan(0, checked((int)output.Length)).CopyTo(writer.Payload);
        return writer.ToArray();
    }

    internal sealed class MailboxPointsProjection
    {
        private readonly MailboxPointsCapture capture;
        private readonly byte[] frame;
        private bool published;
        internal string BeforeText { get; }
        internal string AfterText { get; }
        internal uint AfterExp { get; }

        internal MailboxPointsProjection(MailboxPointsCapture capture, string beforeText, string afterText,
            uint afterExp, byte[] frame)
        {
            this.capture = capture; this.frame = frame;
            BeforeText = beforeText; AfterText = afterText; AfterExp = afterExp;
        }

        internal byte[] CopyFrame() => frame.ToArray();
        internal bool IsCurrent() => !published && capture.IsCurrent();
        internal bool IsPublishedCurrent() => published && capture.IsCurrent(AfterExp);

        // Caller retains the real transaction/session gates and invokes only after
        // proving durable joint afterimages. This method does not claim durability.
        internal bool TryPublishCommitted()
        {
            if (!IsCurrent()) return false;
            capture.PublishExp(AfterExp);
            published = true;
            return IsPublishedCurrent();
        }
    }
}
