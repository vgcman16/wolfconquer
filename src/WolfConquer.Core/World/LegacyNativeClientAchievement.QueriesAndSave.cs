using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeClientAchievement
{
    public int Score()
    {
        return BitVector32.Count();
    }

    public uint[] MyFlags()
    {
        uint[] array = new uint[14];
        for (byte b = 0; b < BitVector32.bits.Length; b++)
        {
            array[b] = BitVector32.bits[b];
        }
        return array;
    }

    public void Save(LegacyNativeAchievementCollection achiev)
    {
        for (int i = 0; i < 14; i++)
        {
            achiev.Value[i] = BitVector32.bits[i];
        }
    }
}
