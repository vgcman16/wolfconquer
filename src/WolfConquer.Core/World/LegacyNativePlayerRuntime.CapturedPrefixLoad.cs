using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    internal sealed partial class ExistingPrefixLoad
    {
        internal static ExistingPrefixLoad? TryPrepareCaptured(LegacyNativeGameClientRuntime client,
            LegacyCapturedCharacterPrefix captured) => captured is null ? null :
            TryPrepareValues(client, captured.Snapshot, captured.SavedPlayerValues);

        // Immutable prepared DATA only; the enclosing captured wrapper never
        // exposes this participant's existing publication machinery.
        internal LegacyNativePrefixValues PreparedPlayerValues => after;

        private static ExistingPrefixLoad? TryPrepareValues(LegacyNativeGameClientRuntime client,
            CharacterSnapshot snapshot, LegacyNativePrefixValues saved)
        {
            if (client?.Player is not { } player || snapshot is null || saved is null ||
                !ReferenceEquals(player.Owner, client) || client.FullLoading || client.Fake ||
                player.Name is null || player.Spouse is null || saved.Name is null || saved.Spouse is null ||
                snapshot.Uid is <= 0 or > uint.MaxValue || snapshot.Class is < 0 or > uint.MaxValue ||
                snapshot.Level is < 0 or > ushort.MaxValue || saved.Uid != snapshot.Uid ||
                saved.Class != snapshot.Class || saved.Level != snapshot.Level ||
                player.UID != 0 && player.UID != snapshot.Uid || player._class != 0 && player._class != snapshot.Class) return null;
            var before = Read(player);
            var job = player._class; var transformation = player._transformationid;
            var prepared = saved with { Online = checked(before.Online + 1) };
            var candidate = new ExistingPrefixLoad(player, client, before, prepared, job, transformation);
            return candidate.IsCurrent() ? candidate : null;
        }
    }
}
