namespace WolfConquer.Core.World;

internal partial struct LegacyNativeMsgSubClass
{
    public enum Action : ushort
    {
        SwitchSubClass,
        ActivateSubClass,
        Upgrade,
        SendUpdate,
        LearnSubClass,
        MartialPromoted,
        Open,
        ShowGUI,
        Animation,
        Join,
        Pro
    }
}
