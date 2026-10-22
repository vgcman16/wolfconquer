using System;

namespace WolfConquer.Core.World;

// Complete native CombatGear enum declarations, including their original Flags markers.
internal partial class LegacyNativeCombatGear
{
    [Flags]
    public enum TypeID : uint
    {
        None = 0u,
        Dragonhowl = 0xC9u,
        Bloodlust = 0xCAu,
        Redcurse = 0xCBu,
        StoneCracker = 0x191u,
        ColdMoon = 0x192u,
        TharnCutter = 0x193u,
        Taoist1 = 0x515u,
        Taoist2 = 0x516u,
        Taoist3 = 0x517u,
        Taoist4 = 0x518u,
        Taoist5 = 0x519u,
        Pirate1 = 0x2BDu,
        Pirate2 = 0x2BEu,
        Pirate3 = 0x2BFu
    }

    [Flags]
    public enum SpellID : ushort
    {
        Rectangle = 0x3304,
        NormalAttack3 = 0x3D4A,
        ShurikenVortex = 0x177A,
        WarSuit201NormalATK1 = 0x4434,
        WarSuit201NormalATK2 = 0x443E,
        WarSuit201NormalATK3 = 0x4448,
        TripleAttackDragonhowl = 0x4452,
        Ironbone = 0x44E8,
        IronbonePassive = 0x44F2,
        DragonPierce = 0x44D4,
        WarSuit202NormalATK1 = 0x445C,
        WarSuit202NormalATK2 = 0x4466,
        WarSuit202NormalATK3 = 0x4470,
        TripleAttackBloodlust = 0x447A,
        Immersion = 0x44C0,
        Insouciance = 0x44CA,
        WarSuit203NormalATK1 = 0x4484,
        WarSuit203NormalATK2 = 0x448E,
        WarSuit203NormalATK3 = 0x4498,
        TripleAttackRedcurse = 0x44A2,
        WildDash = 0x44DE,
        WildDashAttack = 0x44FC,
        ArmorofImmunity = 0x44AC,
        WarSuit401NormalATK1 = 0x43F8,
        WarSuit401NormalATK2 = 0x43F9,
        ArrowBlades = 0x472C,
        ArchShadow = 0x4A24,
        StarFlow = 0x2AC6,
        WarSuit402NormalATK1 = 0x2EAE,
        WarSuit402NormalATK2 = 0x2EAF,
        WarSuit402NormalATK3 = 0x2EB0,
        Revenge = 0x2AB2,
        RevengeEx = 0x4402,
        Hunter = 0x2A8A,
        WarSuit403NormalATK1 = 0x43EE,
        WarSuit403NormalATK2 = 0x43EF,
        WarSuit403NormalATK3 = 0x43F0,
        FireArrow = 0x4916,
        IceArrow = 0x492A,
        PoisonArrow = 0x48C6,
        WindArrow = 0x4718,
        ThunderArrow = 0x4704,
        CrackShot = 0x4902,
        ElementalArrowData = 0x46F0,
        SolidBulwark = 0x4EE8,
        FlowKnack = 0x4EF2,
        MagneticLight = 0x4EFC,
        WaterAegis = 0x4F06,
        FantasyKnack = 0x4F10,
        MysticalMelody = 0x4F1A,
        DivineEmptiness = 0x4F24,
        FloraWard = 0x4F2E,
        WeepStorm = 0x4F38,
        FlameShield = 0x4F42,
        PhoenixBlessing = 0x4F4C,
        WackeSpirit = 0x4F56,
        DivineAttraction = 0x4F60,
        DeadwoodCurse = 0x4F6A,
        Substitution = 0x4F74,
        HolyProtection = 0x4F7E,
        AblazeBlade = 0x4F88,
        WildPhoenix = 0x4F92,
        CrackMantra = 0x4F9C,
        NobleSpirit = 0x4FA6,
        SubstitutionAttack = 0x5154,
        Diabolize = 0x547E,
        Overlord = 0x53F2,
        Armed = 0x54CE,
        Sense = 0x54D8,
        HolySanction = 0x5406,
        HolySanctionPassive = 0x54A6,
        TwospinedSpearPassive = 0x54B0,
        GiantGunPassive = 0x54C4,
        Drukyle = 0x541A,
        Spitfire = 0x5460,
        SpitfirePassive = 0x54BA,
        SandMist = 0x5492,
        CaptiveArrow = 0x5488,
        GiantGun = 0x549C,
        StarVolcano = 0x5474,
        StarVolcanoPassive = 0x5636,
        LavaSea = 0x546A,
        LavaSeaPassive = 0x5640,
        IceAgePassive = 0x564A,
        PheasantBeakPassive = 0x5654,
        DrukylePassive = 0x565E,
        Barrier = 0x54E2,
        Shell = 0x54EC,
        Storm = 0x54F6,
        Thrash = 0x5500,
        Thunder = 0x550A,
        Torrent = 0x5514,
        Tide = 0x551E,
        Splash = 0x5528,
        Sailing = 0x5532,
        Vast = 0x553C,
        Fusing = 0x5456,
        Revelator = 0x5410,
        ColdBloodline = 0x542E,
        Dark = 0x55AA,
        IceAge = 0x5438,
        TwospinedSpear = 0x5442,
        PheasantBeak = 0x544C,
        ThunderlordAttack = 0x55A0,
        Thunderlord = 0x5424,
        LavaNut = 0x5618,
        ThunderNut = 0x5622,
        FrozenNut = 0x562C
    }

    [Flags]
    public enum Promotion : uint
    {
        None = 0u,
        Warrior = 1u,
        Pirate = 2u,
        Archer = 3u,
        Water = 4u,
        Fire = 5u
    }

    [Flags]
    public enum TypeExp : uint
    {
        item = 0u,
        Plus = 1u,
        Super = 2u
    }

    [Flags]
    public enum RankType : uint
    {
        Overall_Warrior = 0x8F0D180u,
        Dragonhowl = 0x8F0D181u,
        Bloodlust = 0x8F0D182u,
        Redcurse = 0x8F0D183u,
        Overall_Archer = 0xB532B80u,
        StoneCracker = 0xB532B81u,
        ColdMoon = 0xB532B82u,
        TharnCutter = 0xB532B83u,
        Overall_Taoist = 0xC845880u,
        Taoist1 = 0xC845881u,
        Taoist2 = 0xC845882u,
        Taoist3 = 0xC845883u,
        Taoist4 = 0xC845884u,
        Taoist5 = 0xC845885u,
        Overall_Pirate = 0xD1CEF00u,
        WEEK_Taoist = 0x989748u,
        WEEK_Archer = 0x9897ACu,
        WEEK_Pirate = 0x989874u
    }

    [Flags]
    public enum Flag : uint
    {
        Immersion = 0x12Fu,
        Insouciance = 0x130u,
        WildDash = 0x131u,
        DragonPierceCrit = 0x133u,
        DragonPierceTortoise = 0x134u,
        DragonPierceBreak = 0x135u,
        DragonPierceStamina = 0x136u,
        ArmorofImmunity = 0x137u,
        CrackShotFire = 0x13Du,
        CrackShotIce = 0x13Eu,
        CrackShotPoison = 0x13Fu,
        CrackShotThunder = 0x140u,
        CrackShotWind = 0x141u,
        Archshadow = 0x145u,
        ArrowBladesAttack = 0x14Cu,
        ArrowBladesActive = 0x14Du,
        Revenge = 0x14Eu,
        StarFlowEx = 0x150u,
        StarFlow = 0x151u,
        Hunter = 0x158u,
        FlowKnack = 0x169u,
        MagneticLight = 0x16Bu,
        SolidBulwark = 0x16Cu,
        Substitution = 0x16Du,
        SubstitutionAttack = 0x16Eu,
        WeepStorm = 0x16Fu,
        WildPhoenix = 0x170u,
        FloraWard = 0x171u,
        NobleSpirit = 0x174u,
        WackeSpirit = 0x176u,
        HolyProtection = 0x178u,
        FantasyKnack = 0x179u,
        CrackMantra1 = 0x17Au,
        CrackMantra2 = 0x17Bu,
        FlameShield = 0x17Cu,
        DivineEmptiness = 0x17Du,
        DeadwoodCurse = 0x17Eu,
        PhoenixBlessing = 0x17Fu,
        Diabolize = 0x18Au,
        CaptiveArrow = 0x18Cu,
        Barrier = 0x18Du,
        Tide = 0x18Fu,
        Vast = 0x18Eu,
        Torrent = 0x190u,
        Thunder = 0x191u,
        Storm = 0x192u,
        Thrash = 0x193u,
        Splash = 0x196u,
        Shell = 0x197u,
        Revelator = 0x198u,
        Fusing = 0x199u,
        ColdBloodline = 0x19Au,
        Sense = 0x19Cu,
        Armed = 0x19Du,
        Overlord = 0x19Eu,
        Dark = 0x1A1u,
        Sailing = 0x1A3u
    }

    [Flags]
    public enum DataType : uint
    {
        ArchiveSkill = 0x78u,
        StarFlow = 0x88u,
        ArrowBlades = 0x89u
    }

    [Flags]
    public enum PacketsID : ushort
    {
        MsgCombatGear = 0x974,
        MsgCombatGearOpt = 0x975,
        MsgCombatGearSkin = 0x976,
        MsgDaoQi = 0x993,
        MsgRank = 0x875,
        MsgRankMemberShow = 0x806,
        MsgDictLottery = 0x9A1,
        MsgAirPowerUp = 0x9A0
    }

    [Flags]
    public enum MapItemEffect : ushort
    {
        FireArrow = 0xE10,
        FloraWard = 0xFBE,
        MysticalMelody = 0xFC8,
        SubstitutionAttack = 0xFD2,
        SandMist = 0x1022,
        LavaSea = 0x1036,
        StarVolcano = 0x1040,
        PheasantBeak = 0x104A,
        IceAge = 0x1054,
        LavaSeaPassive = 0x105E,
        StarVolcanoPassive = 0x1068,
        PheasantBeakPassive = 0x1072,
        IceAgePassive = 0x107C
    }

    [Flags]
    public enum LotteryEffect : ulong
    {
        H1 = 1uL,
        Free10Draws = 2uL,
        Free1Sail = 8uL,
        Free10Sail = 4uL
    }
}
