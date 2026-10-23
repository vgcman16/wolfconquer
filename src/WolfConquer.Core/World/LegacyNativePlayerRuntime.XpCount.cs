namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    private ushort _xpc;

    public ushort XPCount
    {
        get => _xpc;
        set
        {
            if (!TrySetXpCount(value))
                throw new InvalidDataException("XP counter transition requires a current owner and its required goal admission.");
        }
    }

    internal bool TrySetXpCount(ushort value)
    {
        var client = Owner;
        lock (client.SpellAdmissionSyncRoot)
        lock (SpellAdmissionSyncRoot)
        {
            uint uid = UID;
            if (!ReferenceEquals(client.Player, this)) return false;
            if (value != 20)
            {
                _xpc = value;
                return true; // Do not inspect an unrelated missing/malformed goal owner.
            }
            var rewards = client.HeroRewards;
            return rewards is not null && rewards.TryCommitXpTwenty(this, uid);
        }
    }

    // Only the genuine goal owner calls this after all fallible book preparation,
    // complete revalidation and final client/Player/UID/current-goal-owner checks,
    // with client -> player -> goal -> old-book gates still held.
    internal void CommitXpCountTwenty() => _xpc = 20;
}
