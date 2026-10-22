namespace WolfConquer.Core.World;

internal partial struct LegacyNativeTime32
{
    public bool Next(int due = 0, int time = 0)
    {
        if (time == 0)
        {
            time = timeGetTime().int_0;
        }
        return unchecked(int_0 + due) <= time;
    }

    public void Set(int due, int time = 0)
    {
        if (time == 0)
        {
            time = timeGetTime().int_0;
        }
        int_0 = unchecked(time + due);
    }

    public void SetSeconds(int due, int time = 0)
    {
        Set(unchecked(due * 1000), time);
    }
}
