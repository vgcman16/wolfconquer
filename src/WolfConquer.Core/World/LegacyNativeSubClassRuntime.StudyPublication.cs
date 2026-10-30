namespace WolfConquer.Core.World;

internal partial class LegacyNativeSubClassRuntime
{
    private static void PublishStudy(StudyBinding binding, StudyPacketTarget target,
        StudyPreparation prepared, bool committed, ushort before, ushort after)
    {
        string phase = "Talk";
        try
        {
            binding.RequireCurrent();
            prepared.RequireCurrent();
            if (prepared.Chat is not null) binding.Client.Send(prepared.Chat);
            phase = "Effect";
            binding.RequireCurrent();
            prepared.RequireCurrent();
            target.Write(prepared.Effect);
            binding.Client.Send(target.Packet);
            foreach (var recipient in binding.Recipients)
            {
                binding.RequireCurrent();
                recipient.Send(target.Packet);
            }
            phase = "Animation";
            binding.RequireCurrent();
            prepared.RequireCurrent();
            target.Write(prepared.Animation);
            binding.Client.Send(target.Packet);
        }
        catch (Exception error)
        {
            throw new LegacyNativeStudyNotificationException(committed, before, after, phase, error);
        }
    }
}

// Native Send may swallow/drop failures. This records only observable exceptions;
// it never promises rollback, receiver acceptance, or complete delivery.
internal sealed class LegacyNativeStudyNotificationException : InvalidOperationException
{
    internal bool ResourceCommitted { get; }
    internal ushort PointsBefore { get; }
    internal ushort PointsAfter { get; }
    internal string NotificationPhase { get; }

    internal LegacyNativeStudyNotificationException(bool committed, ushort before, ushort after,
        string phase, Exception inner) : base("Native study notification failed during " + phase + ".", inner)
    {
        ResourceCommitted = committed;
        PointsBefore = before;
        PointsAfter = after;
        NotificationPhase = phase;
    }
}
