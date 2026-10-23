namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    public bool UpdateFlag(int flag, int Secounds, bool SetNewTimer, int MaxSecounds)
    {
        lock (SyncRoot)
        {
            try
            {
                if (!TryCapture(out var state) || !state!.Registrations.TryGetValue(flag, out var value) ||
                    value is null || !ReferenceEquals(FindCapturedFlag(state, flag), value)) return false;
                DateTime timer = value.Timer;
                int seconds;
                if (SetNewTimer) { timer = DateTime.Now; seconds = Secounds; }
                else if (value.Timer.AddSeconds(value.Secounds) > DateTime.Now.AddSeconds(MaxSecounds))
                { timer = DateTime.Now; seconds = MaxSecounds; }
                else seconds = checked(value.Secounds + Secounds);
                if (!TryDeadline(timer, seconds, out _) || !IsCurrent(state)) return false;
                value.Timer = timer;
                value.Secounds = seconds;
                return true;
            }
            catch (Exception) { return false; }
        }
    }

    public bool InLife(int flag, DateTime Now64)
    {
        lock (SyncRoot)
        {
            try { return TryFindCurrentFlag(flag, out var value) && value!.Expire(Now64); }
            catch (Exception) { return false; }
        }
    }

    public bool CheckInvoke(int flag, DateTime Now64)
    {
        lock (SyncRoot)
        {
            try { return TryFindCurrentFlag(flag, out var value) && value!.CheckInvoke(Now64); }
            catch (Exception) { return false; }
        }
    }
}
