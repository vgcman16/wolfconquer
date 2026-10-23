namespace WolfConquer.Core.World;

// Complete native ClientAchievement, split by genuine method family.
// No receive attribute, host registration, full DataCore or SaveClient adoption.
internal partial class LegacyNativeClientAchievement
{
    public enum TypeAchievement : uint
    {
        BigHash,
        CheakView,
        SendQueue
    }

    public const int File_Size = 14;
    private LegacyNativeBitVector32 BitVector32;

    public LegacyNativeClientAchievement(uint[] Db_value, uint UID)
    {
        BitVector32 = new LegacyNativeBitVector32(448);
        for (int i = 0; i < Db_value.Length; i++)
        {
            BitVector32.bits[i] = Db_value[i];
        }
    }
}
