using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Whole native Player3376..3378: genuinely empty, NOT an invented BP award.
    public void SetMentorBattlePowers(uint val, uint mentorPotency)
    {
    }

    // Whole native Player5068..5071. Native narrowing/wrap into signed student BP remains.
    public uint GetShareBattlePowers(uint target_battlepower)
    {
        return (uint)LegacyNativeTutorInfo.ShareBattle(Owner, (int)target_battlepower);
    }
}
