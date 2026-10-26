namespace WolfConquer.Core.World;

internal partial class LegacyNativeBeastsRuntime
{
    internal sealed class PrefixBefore
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativeBeastsRuntime? owner;
        private readonly bool activated;
        private readonly uint points, flag;
        private readonly byte level, fixedLevel;
        internal PrefixBefore(LegacyNativeGameClientRuntime client)
        {
            this.client = client; owner = client.Beasts;
            if (owner is null) return;
            if (!ReferenceEquals(owner.constructorOwner, client) || !ReferenceEquals(owner.Owner, client))
                throw new InvalidDataException("Beast constructor ownership is invalid.");
            activated = owner.Activated; points = owner.Points; flag = owner.flag;
            level = owner._level; fixedLevel = owner._fixedlevel;
        }
        internal bool IsCurrent() => ReferenceEquals(client.Beasts, owner) && (owner is null ||
            ReferenceEquals(owner.constructorOwner, client) && ReferenceEquals(owner.Owner, client) &&
            owner.Activated == activated && owner.Points == points && owner.flag == flag && owner._level == level && owner._fixedlevel == fixedLevel);
    }
}
