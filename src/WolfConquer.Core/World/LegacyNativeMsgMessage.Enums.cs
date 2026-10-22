namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgMessage
{
    public enum MsgColor : uint
    {
        black = 0u,
        blue = 255u,
        orange = 16753920u,
        white = 16777215u,
        whitesmoke = 16119285u,
        yellow = 16776960u,
        yellowgreen = 10145074u,
        violet = 15631086u,
        purple = 8388736u,
        red = 16711680u,
        pink = 16761035u,
        lightyellow = 16777184u,
        cyan = 65535u,
        blueviolet = 9055202u,
        antiquewhite = 16444375u
    }

    public enum ChatMode : uint
    {
        Talk = 2000u,
        Whisper = 2001u,
        Team = 2003u,
        Guild = 2004u,
        TopLeftSystem = 2005u,
        Clan = 2006u,
        System = 2000u,
        Friend = 2009u,
        Center = 2011u,
        TopLeft = 2012u,
        Service = 2014u,
        NewChat = 2032u,
        Tip = 2015u,
        CrossServerIcon = 2016u,
        Ally = 2025u,
        WebSite = 2105u,
        World = 2021u,
        Qualifier = 2022u,
        Study = 2024u,
        JianHu = 2026u,
        InnerPower = 2027u,
        PopUP = 2100u,
        Dialog = 2101u,
        CrosTheServer2 = 2400u,
        SlideCrosTheServer = 2401u,
        CrosTheServer = 2402u,
        FirstRightCorner = 2108u,
        ContinueRightCorner = 2109u,
        SystemWhisper = 2110u,
        GuildAnnouncement = 2111u,
        asd = 2032u,
        Agate = 2115u,
        BroadcastMessage = 2500u,
        Monster = 2600u,
        SlideFromRight = 100000u,
        HawkMessage = 2104u,
        SlideFromRightRedVib = 1000000u,
        WhiteVibrate = 10000000u
    }
}
