using System.Globalization;
using System.Reflection;
using NativeFlags = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeBitVector32;

namespace WolfConquer.Tests;

public sealed class LegacyNativeStatusFlagEnumTests
{
    // Complete independent source declaration golden, including order, original names and aliases.
    private const string NativeDeclarations = """
        Normal=3 FlashingName=0 Poisoned=1 Invisible=2
        XPList=4 Dead=5 TeamLeader=6 StarOfAccuracy=7
        MagicShield=8 Shield=8 Stigma=9 Ghost=0xA
        FadeAway=0xB RedName=0xE BlackName=0xF ReflectMelee=0x11
        Superman=0x12 Ball=0x13 Ball2=0x14 Focused=0x15
        Invisibility=0x16 Cyclone=0x17 Dodge=0x1A Fly=0x1B
        Intensify=0x1C CastPray=0x1E Praying=0x1F Cursed=0x20
        HeavenBlessing=0x21 TopGuildLeader=0x22 TopDeputyLeader=0x23 MonthlyPKChampion=0x24
        WeeklyPKChampion=0x25 TopWarrior=0x26 TopTrojan=0x27 TopArcher=0x28
        TopWaterTaoist=0x29 TopFireTaoist=0x2A TopNinja=0x2B ShurikenVortex=0x2E
        FatalStrike=0x2F Flashy=0x30 Ride=0x32 TopSpouse=0x33
        Accelerated=0x34 Deceleration=0x35 Frightened=0x36 HeavenSparkle=0x37
        IncMoveSpeed=0x38 GodlyShield=0x39 Dizzy=0x3A Freeze=0x3B
        Confused=0x3C Top8Weekly=0x3F Top4Weekly=0x40 Top2Weekly=0x41
        ChaintBolt=0x5C AzureShield=0x5D ScurvyBomb=0x60 TyrantAura=0x62
        FeandAura=0x64 MetalAura=0x66 WoodAura=0x68 WaterAura=0x6A
        FireAura=0x6C EartAura=0x6E SoulShackle=0x6F Oblivion=0x70
        ShieldBlock=0x71 TopMonk=0x72 TopPirate=0x7A CTF_Flag=0x76
        PoisonStar=0x77 CannonBarrage=0x78 BlackbeardsRage=0x79 DefensiveStance=0x7E
        MagicDefender=0x80 RemoveName=0x81 PurpleBall=0x83 BlueBall=0x84
        PathOfShadow=0x91 BladeFlurry=0x92 KineticSpark=0x93 AutoHunt=0x94
        SuperCyclone=0x96 TopSuperGuildWarFiveStars=0x97 TopSuperGuildWarThreeStars=0x98 TopSuperGuildWarOneStar=0x99
        DragonFlow=0x94 TopDragonLee=0x9A DragonFury=0x9E DragonCyclone=0x9F
        DragonSwing=0xA0 Goldbrick=0xA5 TopMrConquer=0xA6 TopMrsConquer=0xA7
        lianhuaran01=0xA8 lianhuaran02=0xA9 lianhuaran03=0xAA lianhuaran04=0xAB
        FullPowerWater=0xAC FullPowerFire=0xAD ShieldBreak=0xB0 DivineGuard=0xB1
        Backfire=0xB3 ScarofEarth=0xB4 ManiacDance=0xB5 Pounce=0xB6
        Omnipotence=0xC0 Bleed=0xC1 WindWalkerFan=0xC2 HealingSnow=0xC4
        ChillingSnow=0xC5 xChillingSnow=0xC6 Thunderbolt=0xC7 FreezingPelter=0xC8
        xFreezingPelter=0xC9 RevengeTail=0xCA TopWindWalker=0xCB ShadowofChaser=0xCC
        Sacrifice=0xD3 FineRain1=0xD4 FineRain2=0xD5 Rampage=0xD6
        Infinity=0xD7 Absolution=0xD8 IronShield=0xD9 Slayer=0xDA
        FrostArrows=0xDB ImmortalForce=0xDC NeptuneCurse=0xDE Duel=0xE1
        RiseofTaoism=0xE3 IronGuard=0xE4 BloodTide=0xE5 CrackStar=0xE6
        CrackStarNegative=0xE7 TopThunderstriker=0xE8 AttackUp=0xE9 UndyingImprinting=0xEA
        ThunderRampage=0xEB HeavensWrath=0xEC LightningShieldActivated=0xED LightningShield=0xF0
        SparkShield=0xF1 DevouringStrike=0xF2 Overwhelm=0xF3 TidalWave=0xF5
        Quench=0xF6 RisingPhoenix=0xF7 SoulChain=0xF8 CelestialDance=0xFA
        AxeShadow=0xFB SageSpeedMode=0x111 DeathSigh=0xFD CleanSweep=0xFE
        SacredBlessing=0xFF HawksAmbition=0x100 SongofPhoenix=0x101 DeadlyStrike=0x102
        HookMoon=0x103 HookMoonCaster=0x104 Overall=0x10B Fire=0x10C
        Water=0x10D Earth=0x10E Wind=0x10F Lightning=0x110
        SageMode=0x111 MudWall=0x112 HellVortex=0x119 InfiniteMist=0x11A
        SlashSealProwess=0x11B WildSigilBurning=0x11C PrisonSigilFutility=0x11D WhirlSigilAura=0x11E
        BonePulse=0x11F PaperDance=0x120 HeavensWonder=0x121 LotusDemon=0x123
        SierraBeast=0x124 Immersion=0x12F Insouciance=0x130 WildDash=0x131
        DragonPierceCrit=0x133 DragonPierceTortoise=0x134 DragonPierceBreak=0x135 DragonPierceStamina=0x136
        ArmorofImmunity=0x137 ShieldofTruth=0x138 FrontBreak=0x139 Venom=0x163
        Solid=0x164
        """;

    [Fact]
    public void All193NativeNamesValuesAndDeclarationOrderRemainExactInt32Flags()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(NativeFlags)));
        Assert.NotNull(typeof(NativeFlags).GetCustomAttribute<FlagsAttribute>());
        var expected = NativeDeclarations.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Split('='))
            .Select(parts => (Name: parts[0], Value: parts[1].StartsWith("0x", StringComparison.Ordinal)
                ? Convert.ToInt32(parts[1][2..], 16) : int.Parse(parts[1], CultureInfo.InvariantCulture))).ToArray();
        var fields = typeof(NativeFlags).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(193, expected.Length);
        Assert.Equal(expected.Select(value => value.Name), fields.Select(field => field.Name));
        Assert.Equal(expected.Select(value => value.Value), fields.Select(field => (int)field.GetRawConstantValue()!));
        Assert.All(fields, field => Assert.True(field.IsLiteral));
    }

    [Fact]
    public void OnlyTheThreeSourceAliasGroupsShareValuesAndNoNoneMemberIsInvented()
    {
        var aliases = typeof(NativeFlags).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).GroupBy(field => (int)field.GetRawConstantValue()!)
            .Where(group => group.Count() > 1).OrderBy(group => group.Key).ToArray();
        Assert.Equal(new[] { 8, 148, 273 }, aliases.Select(group => group.Key));
        Assert.Equal(new[] { "MagicShield", "Shield" }, aliases[0].Select(field => field.Name));
        Assert.Equal(new[] { "AutoHunt", "DragonFlow" }, aliases[1].Select(field => field.Name));
        Assert.Equal(new[] { "SageSpeedMode", "SageMode" }, aliases[2].Select(field => field.Name));
        Assert.DoesNotContain("None", Enum.GetNames<NativeFlags>());
        Assert.Equal(0, (int)NativeFlags.FlashingName);
        Assert.Equal(3, (int)NativeFlags.Normal);
    }

    [Fact]
    public void StatusValuesArePassedAsActualIndicesIncludingZeroSignBitAndHighWords()
    {
        var vector = new Vector((int)NativeFlags.Solid + 1);
        foreach (var flag in new[] { NativeFlags.FlashingName, NativeFlags.TeamLeader, NativeFlags.Praying, NativeFlags.Cursed, NativeFlags.Solid })
            vector.Add((int)flag);
        Assert.Equal(0x80000041u, vector.bits[0]);
        Assert.Equal(1u, vector.bits[1]);
        Assert.Equal(16u, vector.bits[11]);
        Assert.Equal(4, vector.Count()); // Praying31 is present but omitted by the source Count expression.
        Assert.True(vector.Contain((int)NativeFlags.Praying));
        Assert.True(vector.Contain((int)NativeFlags.Solid));
        vector.Add((int)NativeFlags.Shield);
        Assert.True(vector.Contain((int)NativeFlags.MagicShield));
        vector.Remove((int)NativeFlags.MagicShield);
        Assert.False(vector.Contain((int)NativeFlags.Shield));
    }

    [Fact]
    public void EnumOrRetainsLiteralIndexArithmeticRatherThanNormalizingIntoIndependentBits()
    {
        var vector = new Vector(96);
        var combined = NativeFlags.MagicShield | NativeFlags.Top4Weekly;
        Assert.Equal(72, (int)combined);
        vector.Add((int)combined);
        Assert.Equal(new uint[] { 0, 0, 256 }, vector.bits);
        Assert.True(vector.Contain(72));
        Assert.False(vector.Contain((int)NativeFlags.MagicShield));
        Assert.False(vector.Contain((int)NativeFlags.Top4Weekly));
        vector.Add((int)(NativeFlags.FlashingName | NativeFlags.Poisoned));
        Assert.True(vector.Contain((int)NativeFlags.Poisoned));
        Assert.False(vector.Contain((int)NativeFlags.FlashingName));
        // Local component arithmetic only; this installs no Player/status consumer.
    }
}
