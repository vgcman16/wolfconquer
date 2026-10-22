namespace WolfConquer.Core.World;

// Complete native enum declarations. No Packet attribute or Process handler is
// installed; title qualification and world-view mutations remain separate owners.
internal static partial class LegacyNativeMsgTitleStorage
{
    public enum TitleType : uint
    {
        Overlord = 1u,
        RisingStar = 2001u,
        Victor = 2002u,
        Conqueror = 2003u,
        Talent = 2004u,
        Fashionist = 2005u,
        SwiftChaser = 2006u,
        MonkeyRider = 2013u,
        SolarRider = 2014u,
        LunarRider = 2015u,
        SaintRider = 2016u,
        Grandmaster = 2018u,
        Fairy = 2020u,
        Goddess = 2021u,
        Beauty = 2022u,
        Scholarz = 2023u,
        Handsome = 2024u,
        Wise = 2025u,
        Superman = 2026u,
        Scholar = 2027u,
        EarthKnight = 2028u,
        GloryKnight = 2029u,
        SkyKnight = 2030u,
        Paladin = 2031u,
        BigFan = 2032u,
        EuroCollector = 2033u,
        Invincible = 2034u,
        Legendary = 2035u,
        Peerless = 2036u,
        Outstanding = 2037u,
        Expert = 2038u,
        WingsofRomance = 6012u,
        EmeraldGlowWing = 6013u,
        OrangeGlowWings = 6014u,
        FlameGlowWings = 6015u,
        BrightGlowWings = 6016u,
        SirenSong = 6017u,
        WingsofHeart = 6018u,
        ButterflyWing = 6021u,
        LoveWings = 6022u,
        VioletWings = 6023u,
        FairyGirl = 2092u,
        ImmortalFate = 2050u,
        WingsofSolarDra = 4001u,
        WingsofInfernal = 6001u,
        RadiantWings = 6002u,
        StarlightWings = 6003u,
        MoonlightWings = 6004u,
        FairyWings = 6005u,
        VioletCloudWing = 6007u,
        VioletLightning = 6008u,
        WingsofPlanet = 6009u,
        FragrantWings = 6025u,
        Supreme = 6011u,
        ChosenOne = 9u
    }

    [Flags]
    public enum Action : uint
    {
        UpdateScore = 0u,
        UseTitle = 1u,
        RemoveTitle = 3u,
        Equip = 4u,
        UnEquip = 5u,
        FullLoad = 6u
    }
}
