namespace WolfConquer.Core.World;

internal partial struct LegacyNativeTime32
{
    public LegacyNativeTime32 AddMilliseconds(int Amount)
    {
        return new LegacyNativeTime32(unchecked(int_0 + Amount));
    }

    public int AllMilliseconds()
    {
        return GetHashCode();
    }

    public LegacyNativeTime32 AddSeconds(int Amount)
    {
        return AddMilliseconds(unchecked(Amount * 1000));
    }

    public int AllSeconds()
    {
        return AllMilliseconds() / 1000;
    }

    public LegacyNativeTime32 AddMinutes(int Amount)
    {
        return AddSeconds(unchecked(Amount * 60));
    }

    public int AllMinutes()
    {
        return AllSeconds() / 60;
    }

    public LegacyNativeTime32 AddHours(int Amount)
    {
        return AddMinutes(unchecked(Amount * 60));
    }

    public int AllHours()
    {
        return AllMinutes() / 60;
    }

    public LegacyNativeTime32 AddDays(int Amount)
    {
        return AddHours(unchecked(Amount * 24));
    }

    public int AllDays()
    {
        return AllHours() / 24;
    }
}
