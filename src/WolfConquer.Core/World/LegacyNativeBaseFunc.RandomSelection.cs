namespace WolfConquer.Core.World;

public static partial class LegacyNativeBaseFunc
{
    // Whole native BaseFunc145..152. Null/empty returns BEFORE a shared RNG read.
    public static uint RandFromGivingNums(uint[]? nums)
    {
        if (nums == null || nums.Length == 0)
        {
            return 0u;
        }
        return nums[LegacyNativePoolRandom.GetRandom.Next(0, nums.Length)];
    }
}
