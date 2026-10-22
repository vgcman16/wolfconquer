namespace WolfConquer.Core.World;

// Complete native static job-family predicates; table state/loading are in the other partials.
internal partial class LegacyNativeAtributesStatus
{
    public static bool IsTrojan(uint Job)
    {
        return Job >= 1000 && Job <= 1051;
    }

    public static bool IsWarrior(uint Job)
    {
        return Job >= 2000 && Job <= 2051;
    }

    public static bool IsArcher(uint Job)
    {
        return Job >= 4000 && Job <= 4051;
    }

    public static bool IsNinja(uint Job)
    {
        return Job >= 5000 && Job <= 5051;
    }

    public static bool IsMonk(uint Job)
    {
        return Job >= 6000 && Job <= 6051;
    }

    public static bool IsPirate(uint Job)
    {
        return Job >= 7000 && Job <= 7051;
    }

    public static bool IsLee(uint Job)
    {
        return Job >= 8000 && Job <= 8051;
    }

    public static bool IsThunderStriker(uint Job)
    {
        return Job >= 9000 && Job <= 9051;
    }

    public static bool IsWater(uint Job)
    {
        return Job >= 13002 && Job <= 13051;
    }

    public static bool IsFire(uint Job)
    {
        return Job >= 14002 && Job <= 14051;
    }

    public static bool IsTaoist(uint Job)
    {
        return Job >= 10000 && Job <= 14051;
    }

    public static bool IsWindWalker(uint Job)
    {
        return Job >= 16000 && Job <= 16051;
    }
}
