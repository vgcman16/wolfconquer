using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSubClassRuntime
{
    private enum StudyOperation { Add, Remove, Notify }

    public void AddStudyPoints(LegacyNativeGameClientRuntime client, ushort Study, LegacyNativePacket stream) =>
        ExecuteStudy(client, Study, stream, StudyOperation.Add);

    public void RemoveStudy(LegacyNativeGameClientRuntime client, ushort Study, LegacyNativePacket stream)
    {
        // Existing strict comparison is retained pending client gameplay evidence.
        // A genuine no-op has no notification dependencies to consume.
        lock (studyGate) if (studyPoints <= Study) return;
        ExecuteStudy(client, Study, stream, StudyOperation.Remove);
    }

    public void UpdateStudyPacket(LegacyNativeGameClientRuntime client, LegacyNativePacket stream) =>
        ExecuteStudy(client, 0, stream, StudyOperation.Notify);

    private void ExecuteStudy(LegacyNativeGameClientRuntime client, ushort amount,
        LegacyNativePacket stream, StudyOperation operation)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(stream);
        var player = client.Player ?? throw new InvalidOperationException("Study requires the actual player.");
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (studyGate)
        {
            if (operation == StudyOperation.Remove && studyPoints <= amount) return;
            var binding = new StudyBinding(this, client, player);
            var target = new StudyPacketTarget(stream);
            ushort before = studyPoints;
            ulong revision = studyRevision;
            ushort after = operation switch
            {
                StudyOperation.Add => (ushort)Math.Min(ushort.MaxValue, before + amount),
                StudyOperation.Remove => (ushort)(before - amount),
                _ => before
            };
            bool changesResource = operation != StudyOperation.Notify;
            ulong nextRevision = changesResource ? checked(revision + 1) : revision;
            using var prepared = new StudyPreparation(binding, target, after,
                operation == StudyOperation.Add ? amount : null);
            binding.RequireCurrent();
            target.RequireUnchanged();
            prepared.RequireCurrent();
            if (studyPoints != before || studyRevision != revision)
                throw new InvalidOperationException("Study resource changed during preparation.");
            if (changesResource)
            {
                studyPoints = after;
                studyRevision = nextRevision;
            }
            PublishStudy(binding, target, prepared, changesResource, before, after);
        }
    }
}
