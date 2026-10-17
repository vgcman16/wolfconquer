using System.Threading;

namespace WolfConquer.Core.World;

public sealed class LegacyGuildWarRuntimeStateStore
{
    private int _isActive;

    public bool IsActive => Volatile.Read(ref _isActive) != 0;

    public void SetActive(bool active)
    {
        Volatile.Write(ref _isActive, active ? 1 : 0);
    }

    public void Start()
    {
        SetActive(true);
    }

    public void Stop()
    {
        SetActive(false);
    }

    public bool CanBroadcastMsgNewSynWarOwnershipChanged(uint syndicateId)
    {
        return IsMsgNewSynWarBroadcastAllowed(syndicateId);
    }

    public bool CanBroadcastMsgNewSynWarDestroyed(uint syndicateId)
    {
        return IsMsgNewSynWarBroadcastAllowed(syndicateId);
    }

    private bool IsMsgNewSynWarBroadcastAllowed(uint syndicateId)
    {
        return IsActive && syndicateId != 0;
    }
}
