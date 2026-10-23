namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Whole native GameClient939..974. Does not mutate experience/level or send.
    public ulong GainExperience(double Experience, ushort targetlevel)
    {
        int num = Player.Level - targetlevel;
        if (num >= 3)
        {
            if (num >= 3 && num <= 5)
            {
                Experience *= 0.7;
            }
            else if (num > 5 && num <= 10)
            {
                Experience *= 0.2;
            }
            else if (num > 10 && num <= 20)
            {
                Experience *= 0.1;
            }
            else if (num > 20)
            {
                Experience *= 0.05;
            }
        }
        else if (num < -15)
        {
            Experience *= 1.8;
        }
        else if (num < -8)
        {
            Experience *= 1.5;
        }
        else if (num < -5)
        {
            Experience *= 1.3;
        }
        return LegacyNativeExperienceConversions.UInt64(Experience);
    }
}
