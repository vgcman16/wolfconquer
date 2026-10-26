namespace WolfConquer.Core.World;

internal partial class LegacyNativeBeastsRuntime
{
    internal sealed class PrefixLoad
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeBeastsRuntime? previous;
        internal LegacyNativeBeastsRuntime Target { get; }
        private readonly bool wasActivated;
        private readonly uint wasPoints, wasFlag;
        private readonly byte wasLevel, wasFixed;
        private readonly bool activated;
        private readonly uint points, flag;
        private readonly byte level, fixedLevel;
        private readonly PrefixMetadata metadata;

        private PrefixLoad(LegacyNativeGameClientRuntime client, LegacyNativeBeastsRuntime? previous,
            bool activated, uint totalPoints, uint flag, PrefixMetadata metadata)
        {
            this.client = client; player = client.Player; this.previous = previous; Target = previous ?? new(client);
            wasActivated = Target.Activated; wasPoints = Target.Points; wasFlag = Target.flag;
            wasLevel = Target._level; wasFixed = Target._fixedlevel;
            this.activated = activated; this.flag = flag; this.metadata = metadata;
            uint remaining = totalPoints;
            int loadedLevel = activated ? 1 : 0;
            int loadedFixed = 1;
            foreach (var required in metadata.Required)
            {
                if (remaining < required) break;
                remaining -= required;
                if (++loadedLevel > metadata.Required.Length) { loadedLevel = metadata.Required.Length; loadedFixed++; }
            }
            var last = metadata.Required[^1];
            loadedFixed = checked(loadedFixed + checked((int)(remaining / last)));
            if (loadedFixed > byte.MaxValue) throw new InvalidDataException("Beast saved level exceeds the native byte range.");
            points = remaining % last; level = checked((byte)loadedLevel); fixedLevel = checked((byte)loadedFixed);
        }

        internal static bool TryPrepare(LegacyNativeGameClientRuntime client, bool activated, uint totalPoints, uint flag,
            out PrefixLoad? prepared)
        {
            prepared = null;
            try
            {
                if (client?.Player is not { } player || !ReferenceEquals(player.Owner, client) || client.FullLoading || client.Fake ||
                    !PrefixMetadata.TryCapture(out var metadata)) return false;
                var old = client.Beasts;
                if (old is not null && (!ReferenceEquals(old.constructorOwner, client) || !ReferenceEquals(old.Owner, client))) return false;
                var candidate = new PrefixLoad(client, old, activated, totalPoints, flag, metadata!);
                if (!candidate.IsCurrent()) return false;
                prepared = candidate;
                return true;
            }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or OverflowException or ArgumentException)
            { return false; }
        }

        internal bool IsCurrent() => metadata.IsCurrent() && IsOwnerCurrent();

        internal bool IsOwnerCurrent() => ReferenceEquals(client.Beasts, previous) && ReferenceEquals(client.Player, player) &&
            ReferenceEquals(player.Owner, client) && !client.FullLoading && !client.Fake &&
            ReferenceEquals(Target.constructorOwner, client) && ReferenceEquals(Target.Owner, client) &&
            Target.Activated == wasActivated && Target.Points == wasPoints && Target.flag == wasFlag &&
            Target._level == wasLevel && Target._fixedlevel == wasFixed;

        // Caller has committed the exact Online image after the final current
        // checks. Plain assignments only: no setters/metadata queries/allocations.
        internal void PublishPrepared()
        {
            Target.Activated = activated; Target.Points = points; Target.flag = flag;
            Target._level = level; Target._fixedlevel = fixedLevel;
            client.Beasts = Target;
        }
    }
}
