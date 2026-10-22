namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player719/725 physical fields. The list is internal solely because
    // its genuine native enum owner is internal; no projection or nullable gate.
    // SpecialTitleID and SpecialWingID already exist in RankingAppearance.
    internal List<LegacyNativeMsgTitleStorage.TitleType> SpecialTitles =
        new List<LegacyNativeMsgTitleStorage.TitleType>();

    public uint SpecialTitleScore = 0u;
}
