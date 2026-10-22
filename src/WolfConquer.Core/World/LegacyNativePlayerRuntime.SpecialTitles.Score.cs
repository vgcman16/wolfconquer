using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Complete native Player title methods, not automatic qualification or a host route.
    public uint GetTitlesScore()
    {
        uint num = 0u;
        foreach (LegacyNativeMsgTitleStorage.TitleType specialTitle in SpecialTitles)
        {
            if (LegacyNativeTitleStorage.Titles.ContainsKey((uint)specialTitle))
            {
                unchecked
                {
                    num += LegacyNativeTitleStorage.Titles[(uint)specialTitle].Score;
                }
            }
        }
        return num;
    }
}
