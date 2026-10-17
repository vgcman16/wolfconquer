namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly LegacyNpcDialogOption[] LegacyPromotionCloseOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyPromotionMaster[] LegacyPromotionMasters =
    [
        new(
            10_000,
            "NpcHandler.PromotionTaoist",
            "NpcHandler.cs:41206-42994",
            "Taoist",
            6,
            10_000,
            IsLegacyPromotionTaoistClass,
            ["Every Taoist wants holy spirit, and few gain. You are gifted in controlling inner power. Pay attention to the details, and you may be the next achiever!"],
            [new("I~want~to~get~promoted.", 1), new("I'll~think~about~it.", LegacyNpcClosedOption)],
            [new("I~want~to~get~promoted.", 53), new("Learn~class~skills.", 10), new("Learn~Pure~Skills.", 50), new("Nothing.", LegacyNpcClosedOption)],
            [
                Dialog(1, ["Taoists cast devastating skills and deadly spells. Do you want to become a Taoist now?"], 6, new("Yeah,~please!", 2), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(3, ["There are two paths for Taoists. Which one would you choose?"], 6, new("Fire~Taoist.", 4), new("Water~Taoist.", 5), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(10, ["I know many skills and spells for a true Taoist. Which kind do you want to learn first?"], 6, new("Learn~magic.", 11), new("Learn~XP~Skill.", 12), new("Learn~Epic~Skills.", 95), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(11, ["The Taoist skills are varied and powerful. Which skill do you want to learn?"], 6, new("Cure(Lv.1).", 21), new("Meditation(Lv.44).", 22), new("Thunder(Lv.1).", 20), new("Fire(Lv.40).", 23), new("Tornado(Lv.92).", 24), new("HealingRain(Lv.40).", 29), new("StarofAccuracy(Lv.45).", 30), new("MagicShield(Lv.50).", 31), new("Stigma(Lv.55).", 32), new("Invisibility(Lv.60).", 33), new("Pray(Lv.70).", 34), new("AdvancedCure(Lv.81).", 35), new("Nectar(Lv.94).", 36), new("Advanced~fire~magic.", 13), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(12, ["The Taoist XP skills are varied and powerful. Which skill do you want to learn?"], 6, new("ChainBolt(Lv.1).", 40), new("Lightning(Lv.3).", 41), new("Vulcano(Lv.40).", 42), new("Revive(Lv.40).", 43), new("SpeedLightning(Lv.40).", 44), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(13, ["Advanced Fire Taoist magic includes source branches hidden behind the legacy fire path."], 6, new("ChainBolt(Lv.1).", 70), new("Lightning(Lv.1).", 71), new("FireRing(Lv.55).", 25), new("FireMeteor(Lv.52).", 26), new("FireCircle(Lv.65).", 27), new("Bomb(Lv.82).", 28), new("FireofHell(Lv.93).", 58), new("Back.", 11), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(50, ["Pure Taoist skills require matching Taoist lives. Reincarnation can change your class combination."], 6, new("Azure~Shield(Lv.40).", 51), new("Heaven~Blade(Lv.40).", 52), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(95, ["Which epic Taoist skill do you want to learn?"], 6, new("Aurora~Lotus[2150~CPs].", 91), new("Blessing~Touch[2150~CPs].", 92), new("Flame~Lotus[2150~CPs].", 93), new("Searing~Touch[2150~CPs].", 94), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(110, ["You look promising. You can learn class skills and get promoted here to start your life."], 259, new LegacyNpcDialogOption("Thanks.", 111)),
                Dialog(190, ["The Great Sun King knows the way of forging a Taoist Epic Weapon."], 6, new("Take~me~to~see~Sun~King.", 191), new("I'll~think~about~it.", LegacyNpcClosedOption)),
            ],
            [2, 4, 5, 250]),

        new(
            8_314,
            "NpcHandler.PromotionMonk",
            "NpcHandler.cs:42996-44000",
            "Monk",
            226,
            6_000,
            IsLegacyPromotionMonkClass,
            ["Monks and Saints understand both the physical and spiritual world. Through intense training they have gained devastating power. What is it, my child?"],
            [new("I~want~to~get~promoted.", 1), new("I'll~think~about~it.", LegacyNpcClosedOption)],
            [new("I~want~to~get~promoted.", 22), new("Learn~class~skills.", 3), new("Learn~Pure~Skills.", 20), new("Learn~Epic~Skills.", 90), new("Nothing.", LegacyNpcClosedOption)],
            [
                Dialog(1, ["You can get promoted from Intern to Monk and learn Whirlwind Kick when you are Level 15 or above."], 226, new("Get~promoted.", 2), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(3, ["The Monk skills are varied and powerful. Which skill do you want to learn?"], 226, new("Deflection~Aura(Lv.20).", 4), new("Tyrant~Aura(Lv.20).", 5), new("Serenity(Lv.40).", 6), new("Radiant~Palm(Lv.40).", 7), new("Tranquility(Lv.70).", 8), new("Next~page.", 9)),
                Dialog(9, ["The Monk skills are varied and powerful. Which skill do you want to learn?"], 226, new("Compassion(Lv.100).", 10), new("ElementalAuraMetal(Lv.100).", 11), new("ElementalAuraWood(Lv.100).", 12), new("ElementalAuraWater(Lv.100).", 13), new("ElementalAuraFire(Lv.100).", 14), new("ElementalAuraEarth(Lv.100).", 15), new("Next~page.", 70)),
                Dialog(20, ["Only a Pure Monk or Saint can learn Soul Shackle. Reincarnation can change your class combination."], 226, new("I~want~to~learn~it~now.", 21), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(70, ["The Monk skills are varied and powerful. Which skill do you want to learn?"], 226, new("WhirlwindKick(Lv.1).", 71), new("TripleAttack(Lv.1).", 72), new("Oblivion(Lv.1).", 73)),
                Dialog(90, ["Hello there, which epic skill do you want me to teach you?"], 203, new("InfernalEcho[2150~CPs].", 83), new("WrathOfTheEmperor[2150~CPs].", 84), new("GraceOfHeaven[2150~CPs].", 85), new("Sounds~crazy.", LegacyNpcClosedOption)),
                Dialog(110, ["You look promising. You can learn class skills and get promoted here to start your life."], 259, new LegacyNpcDialogOption("Thanks.", 111)),
                Dialog(253, ["Golden Cicada is recruiting Monks and Saints for trials toward the Monk Epic Weapon."], 226, new("Head~to~see~Golden~Cicada.", 91), new("Secret~is~secret.", LegacyNpcClosedOption)),
            ],
            []),

        new(
            4_972,
            "NpcHandler.PromotionNinja",
            "NpcHandler.cs:44002-45002",
            "Ninja",
            203,
            5_000,
            IsLegacyPromotionNinjaClass,
            ["Cloaked in shadow, mysterious and aloof, the Ninja is the fastest class, with devastating attacks. What can I do for you?"],
            [new("I~want~to~get~promoted.", 1), new("I'll~think~about~it.", LegacyNpcClosedOption)],
            [new("I~want~to~get~promoted.", 20), new("Learn~Katana~skills.", 6), new("Learn~Scythe~skills.", 8), new("Learn~XP~skills.", 11), new("Epic~Quest.~(L70,~2nd~reborn)", 80), new("Learn~class~skills.", 3), new("Learn~Pure~Skills.", 15), new("You~bet!", LegacyNpcClosedOption)],
            [
                Dialog(1, ["You can get promoted to Ninja and learn Fatal Strike when you are Level 15 or above."], 203, new("Yeah,~please!", 2), new("Let~me~think~it~over.", LegacyNpcClosedOption)),
                Dialog(3, ["Which class skill do you want to learn?"], 203, new("Archer~Bane(Lv.100).", 5), new("ToxicFog(Lv.15).", 14), new("Okay.~I~see.", LegacyNpcClosedOption)),
                Dialog(6, ["Which Katana skill do you want to learn?"], 203, new("TwofoldBlades(Lv.40).", 7), new("SuperTwofoldBlades(Lv.70).", 249), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(8, ["Which Scythe skill do you want to learn?"], 203, new("MortalDrag(Lv.20).", 9), new("BloodyScythe(Lv.40).", 10), new("GapingWounds(Lv.100).", 87), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(11, ["Which XP skill do you want to learn?"], 203, new("ShurikenVortex(Lv.70).", 12), new("FatalStrike(Lv.3).", 13), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(15, ["Only Pure Ninjas can learn Counter Kill. Reincarnation can change your class combination."], 203, new("I~want~to~learn~it~now.", 16), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(80, ["The Divine Shadow opened the Infinite Abyss and set three trials for second-reborn Ninjas at Level 70 or above."], 203, new("Learn~Epic~Spells.", 82), new("Sounds~crazy.", LegacyNpcClosedOption)),
                Dialog(82, ["I can exchange Shadow, Twilight, or Fatal scroll progress for the corresponding epic Ninja spell."], 203, new("ShadowClone[2150~CPs].", 83), new("TwilightDance[2150~CPs].", 84), new("FatalSpin[2150~CPs].", 85), new("Sounds~crazy.", LegacyNpcClosedOption)),
                Dialog(110, ["You look promising. You can learn class skills and get promoted here to start your life."], 259, new LegacyNpcDialogOption("Thanks.", 111)),
            ],
            []),

        new(
            10_001,
            "NpcHandler.PromotionWarrior",
            "NpcHandler.cs:45004-46085",
            "Warrior",
            8,
            2_000,
            IsLegacyPromotionWarriorClass,
            ["To be a good Warrior, you must know your weapon, wear your armor like a second skin, and use your shield to ward off enemies."],
            [new("I~want~to~get~promoted.", 1), new("I'll~think~about~it.", LegacyNpcClosedOption)],
            [new("I~want~to~get~promoted.", 3), new("Learn~skills~for~Fist.", 130), new("Learn~shield~skills.", 4), new("Learn~XP~skills.", 10), new("Learn~weapon~skills.", 20), new("Learn~Pure~Skills.", 21), new("You~bet!", LegacyNpcClosedOption)],
            [
                Dialog(1, ["You can get promoted from Intern to Warrior and learn XP skills when you are Level 15 or above."], 8, new("Get~promoted.", 2), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(10, ["Which Warrior XP skill do you want to learn?"], 8, new("Superman(Lv.3).", 11), new("Accuracy(Lv.3).", 12), new("Roar(Lv.3).", 13), new("Shield(Lv.3).", 14), new("FlyingMoon(Lv.40).", 15), new("Let~me~think~it~over.", LegacyNpcClosedOption)),
                Dialog(21, ["Only Pure Warriors can learn Perseverance. Reincarnation can change your class combination."], 8, new("I~want~to~learn~it~now.", 22), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(130, ["Fist skills include Wave of Blood, Backfire, Scar of Earth, Twist of War, and Maniac Dance."], 8, new("Scar~of~Earth(Lv.40).", 131), new("Wave~of~Blood(Lv.40).", 132), new("Maniac~Dance(Lv.40).", 133), new("Twist~of~War(Lv.70).", 134), new("Backfire(Lv.100).", 135)),
                Dialog(110, ["You look promising. You can learn class skills and get promoted here to start your life."], 259, new LegacyNpcDialogOption("Thanks.", 111)),
            ],
            []),

        new(
            19_634,
            "NpcHandler.PromotionWindWalker",
            "NpcHandler.cs:46087-47624",
            "WindWalker",
            333,
            16_000,
            IsLegacyPromotionWindWalkerClass,
            ["The WindWalker school follows kindness and justice. WindWalkers defend the land from invaders and demons."],
            [new("I~want~to~get~promoted.", 50), new("Learn~skills.", 8), new("Switch~to~another~branch.", 1), new("I'll~talk~to~you~later.", LegacyNpcClosedOption)],
            [new("I~want~to~get~promoted.", 50), new("Learn~skills.", 8), new("Switch~to~another~branch.", 1), new("I'll~talk~to~you~later.", LegacyNpcClosedOption)],
            [
                Dialog(1, ["WindWalkers can switch between the melee Stomper branch and the ranged Chaser branch."], 333, new("Switch~branch.", 2), new("Switch~with~Wind~Core.", 4), new("Switch~with~27~CPs.", 5), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(8, ["The WindWalker school has two branches: Chasers focus on ranged combat, while Stompers excel at melee combat."], 333, new("Learn~Stomper~skills.", 9), new("Learn~Chaser~skills.", 12), new("Learn~rebirth~skills.", 13), new("Learn~universal~skills.", 14)),
                Dialog(9, ["Stomper skills use swift melee actions to disable enemies."], 333, new("Stomper~Wrath(Lv.1).", 20), new("Burnt~Frost(Lv.15).", 21), new("Healing~Snow(Lv.15).", 22), new("War~Cry(Lv.40).", 23), new("Stomper~Fear(Lv.40).", 24), new("Chilling~Snow(Lv.70).", 25), new("Peace~of~Stomper(Lv.70).", 26), new("Next.", 10)),
                Dialog(10, ["Stomper high skills continue the melee branch."], 333, new("Revenge~Tail(Lv.100).", 27), new("Freezing~Pelter(Lv.100).", 28), new("I~want~to~learn~other~skills.", 8), new("Previous.", 9), new("I'll~talk~to~you~later.", LegacyNpcClosedOption)),
                Dialog(12, ["Chaser skills control wind for strong ranged attacks."], 333, new("Whirlwind(Lv.15).", 30), new("Chaser~Shadow(Lv.15).", 31), new("Triple~Blasts(Lv.40).", 32), new("Thundercloud(Lv.70).", 33), new("Thunderbolt(Lv.100).", 34), new("I~want~to~learn~other~skills.", 8), new("I'll~talk~to~you~later.", LegacyNpcClosedOption)),
                Dialog(13, ["Frost Gaze is WindWalker's rebirth skill. Prior WindWalker lives determine which rank can be learned."], 333, new("Frost~Gaze~I.", 40), new("Frost~Gaze~II.", 41), new("Frost~Gaze~III.", 42), new("I~want~to~learn~other~skills.", 8), new("I'll~talk~to~you~later.", LegacyNpcClosedOption)),
                Dialog(14, ["Omnipotence and Justice Chant are universal WindWalker skills."], 333, new("Omnipotence(Lv.3).", 35), new("Justice~Chant(Lv.3).", 36), new("I~want~to~learn~other~skills.", 8), new("I'll~talk~to~you~later.", LegacyNpcClosedOption)),
                Dialog(60, ["The WindWalker school and demonic forces have fought for centuries. Return to society and investigate their invasion."], 333, new LegacyNpcDialogOption("I~understand.", 61)),
            ],
            [54, 61]),

        new(
            10_022,
            "NpcHandler.PromotionTrojan",
            "NpcHandler.cs:47625-48674",
            "Trojan",
            5,
            1_000,
            IsLegacyPromotionTrojanClass,
            ["Wielding dual weapons, Trojans charge fearlessly into combat and believe courage is the secret of victory."],
            [new("I~want~to~get~promoted.", 1), new("Okay.~I~see.", LegacyNpcClosedOption)],
            [new("I~want~to~get~promoted.", 3), new("Learn~class~and~weapon~skills.", 4), new("Pure~skills~for~Trojans.", 30), new("Okay.~I~see.", LegacyNpcClosedOption)],
            [
                Dialog(4, ["Trojan skills cover XP powers, weapon guidance, and Epic skills."], 5, new("Trojan~skills.", 6), new("Trojan~Epic~skills.", 7), new("Let~me~see.", LegacyNpcClosedOption)),
                Dialog(5, ["General Pak's ghost waits for a chosen Trojan to change history and earn a fabulous Epic Weapon."], 5, new("Head~to~see~General~Pak.", 101), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(6, ["Which Trojan skill would you like to learn?"], 5, new("Monster~Hunter(Lv.40).", 251), new("Undying~Will(Lv.40).", 252), new("Super~Cyclone(Lv.40).", 8), new("Accuracy~and~Cyclone(Lv.40).", 9), new("Golem(Lv.40).", 10), new("Hercules(Lv.40).", 11), new("Spirit~Healing(Lv.40).", 12), new("Breath~Focus(Lv.40).", 13), new("Superior~skills.", 14), new("Learn~weapon~skills.", 15), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(7, ["Fatal Cross and Mortal Strike are Trojan Epic skills."], 5, new("Fatal~Cross[2150~CPs].", 16), new("Mortal~Strike[2150~CPs].", 17), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(30, ["Dragon Whirl is a Pure Trojan skill for heroes who have been Trojan in all three lives."], 5, new("Learn~Dragon~Whirl(Lv.40).", 31), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(110, ["You look promising. You can learn class skills and get promoted here to start your life."], 259, new LegacyNpcDialogOption("Thanks.", 111)),
            ],
            []),

        new(
            400,
            "NpcHandler.PromotionArcher",
            "NpcHandler.cs:48676-49644",
            "Archer",
            10,
            4_000,
            IsLegacyPromotionArcherClass,
            ["Born with sharp insight and swift skill, Archers are experts in shooting arrows and wielding throwing knives."],
            [new("I~want~to~get~promoted.", 1), new("I'll~think~about~it.", LegacyNpcClosedOption)],
            [new("I~want~to~get~promoted.", 33), new("Learn~the~Class~Skill.", 5), new("Learn~the~Pure~Skill.", 30), new("I'll~think~about~it.", LegacyNpcClosedOption)],
            [
                Dialog(1, ["Flight, ranged attack, and maneuverability are powers possessed by Archers."], 10, new("I~want~to~get~promoted.", 2), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(5, ["Archers can learn bow skills or Assassin skills after promotion."], 10, new("Learn~Archer~skills.", 10), new("Learn~Assassin~skills.", 11), new("Leave.", LegacyNpcClosedOption)),
                Dialog(10, ["After equipping bow and arrows, Archers can fly and attack enemies in the air."], 10, new("XP~Fly(Lv.1).", 70), new("Rapid~Fire(Lv.46).", 13), new("Improved~Fly(Lv.70).", 14), new("Arrow~Rain(Lv.70).", 15), new("Intensify(Lv.71).", 16), new("Scatter~Fire(Lv.23).", 17)),
                Dialog(11, ["Assassin skills use throwing knives to deadly purpose."], 10, new("Path~Of~Shadow(Lv.40).", 20), new("Blade~Flurry(Lv.40).", 21), new("Mortal~Wound(Lv.40).", 22), new("Kinetic~Spark(Lv.50).", 23), new("Blistering~Wave(Lv.70).", 24), new("Spirit~Focus(Lv.90).", 25), new("Dagger~Storm(Lv.100).", 26), new("Leave.", LegacyNpcClosedOption)),
                Dialog(30, ["Only Pure Archers can learn Star Arrow. Reincarnation can change your class combination."], 10, new("I~want~to~learn~it~now.", 31), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(110, ["You look promising. You can learn class skills and get promoted here to start your life."], 259, new LegacyNpcDialogOption("Thanks.", 111)),
            ],
            []),

        new(
            17_126,
            "NpcHandler.PromotionLeeLong",
            "NpcHandler.cs:49646-50751",
            "Dragon-Warrior",
            259,
            8_000,
            IsLegacyDragonWarriorClass,
            ["Dragon-Warriors fight fast, accurately, and ruthlessly. They master practical martial arts and many weapons, including nunchaku."],
            [new("I~want~to~get~promoted.", 3), new("I'll~think~about~it.", LegacyNpcClosedOption)],
            [new("I~want~to~get~promoted.", 4), new("Learn~the~Class~Skill.", 5), new("Learn~the~Pure~Skill.", 30), new("I'll~think~about~it.", LegacyNpcClosedOption)],
            [
                Dialog(5, ["Dragon-Warriors use brilliant combo skills to get close to enemies and finish them quickly."], 259, new("Dragon~Punch(Lv.15).", 6), new("Dragon~Cyclone(Lv.15).", 7), new("AirKick(Lv.15).", 8), new("AirRaid(Lv.15).", 9), new("AirSweep(Lv.15).", 10), new("Dragon~Flow(Lv.15).", 11), new("Cracking~Swipe(Lv.70).", 12), new("Next.", 15)),
                Dialog(15, ["Dragon-Warrior combo skills continue with higher level techniques."], 259, new("Splitting~Swipe(Lv.100).", 13), new("Dragon~Slash(Lv.100).", 14), new("Dragon~Roar(Lv.70).", 16), new("Dragon~Swing(Lv.70).", 17), new("SpeedKick(Lv.40).", 18), new("ViolentKick(Lv.40).", 19), new("Previous~page.", 5), new("Next.", 120)),
                Dialog(30, ["Dragon Fury is exclusive to Dragon-Warriors who were Dragon-Warriors in the previous two lives."], 259, new("Learn~Dragon~Fury.", 31), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(120, ["Dragon-Warrior starter techniques are quick and practical."], 259, new("AirStrike(Lv.1).", 121), new("EarthSweep(Lv.1).", 122), new("Kick(Lv.1).", 123), new("StormKick(Lv.40).", 20), new("Previous~page.", 5)),
                Dialog(110, ["You look promising. You can learn class skills and get promoted here to start your life."], 259, new LegacyNpcDialogOption("Thanks.", 111)),
            ],
            []),

        new(
            23_599,
            "NpcHandler.PromotionThunderstriker",
            "NpcHandler.cs:50753-51258",
            "Thunderstriker",
            234,
            9_000,
            IsLegacyThunderstrikerClass,
            ["Thunderstrikers inherit great thunder and lightning powers, wielding legendary weapons to launch deadly attacks."],
            [new("I~want~to~get~promoted.", 1), new("Learn~Class~skills.", 2), new("I'll~talk~to~you~later.", LegacyNpcClosedOption)],
            [new("I~want~to~get~promoted.", 1), new("Learn~Class~skills.", 2), new("I'll~talk~to~you~later.", LegacyNpcClosedOption)],
            [
                Dialog(2, ["As the agent of Lord Thor, I can teach Thunderstrikers to wield the power of thunder."], 234, new("Learn~Class~skill.", 10), new("Learn~Thunderstriker~pure~skill.", 11), new("Learn~XP~skill.", 12)),
                Dialog(10, ["You can learn class skills here."], 234, new("Windstorm~Battleaxe(Lv.1).", 20), new("Devouring~Strike(Lv.1).", 21), new("Sky~Fall(Lv.1).", 22), new("Cracking~Shock(Lv.15).", 23), new("Undying~Imprinting(Lv.40).", 24), new("Lightning~Shield(Lv.70).", 25), new("Thunder~Blast(Lv.100).", 26), new("Megabolt(Lv.100).", 27)),
                Dialog(11, ["Only Thunderstrikers in three straight lives can learn the pure skill Heaven's Wrath."], 234, new("Heaven's~Wrath(Lv.100).", 3), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(12, ["Thunder Rampage is Thunderstriker's XP skill."], 234, new LegacyNpcDialogOption("ThunderRampage(Lv.3).", 13)),
            ],
            []),

        new(
            9_391,
            "NpcHandler.PromotionPirate",
            "NpcHandler.cs:51260-52247",
            "Pirate",
            234,
            7_000,
            IsLegacyPiratePromotionClass,
            ["Pirates pursue freedom and strength. The rapier and pistol stand for cruelty and mercilessness."],
            [new("I~want~to~get~promoted.", 1), new("I'll~think~about~it.", LegacyNpcClosedOption)],
            [new("I~want~to~get~promoted.", 4), new("Learn~the~Class~Skill.", 5), new("Learn~the~Pure~Skill.", 20), new("I'll~think~about~it.", LegacyNpcClosedOption)],
            [
                Dialog(1, ["As an Intern Pirate, you can get promoted to Pirate and learn Blade Tempest after you reach Level 15."], 234, new("Get~promoted.", 3), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(5, ["What type of Pirate skill would you like to learn?"], 234, new("Universal~skill.", 250), new("Skills~for~Epic~Weapon.", 251)),
                Dialog(15, ["Which Pirate skill do you want to learn?"], 234, new("Black~Spot(Lv.15).", 12), new("Windstorm(Lv.15).", 13), new("Learn~the~Golem(Lv.15).", 14), new("CannonBarrage(Lv.15).", 60), new("Previous~page.", 5)),
                Dialog(20, ["To learn Scurvy Bomb, you need to be a pure Pirate for three straight lives."], 234, new("Learn~the~skill.", 21), new("I'll~think~about~it.", LegacyNpcClosedOption)),
                Dialog(250, ["Which Pirate skill do you want to learn?"], 234, new("Blade~Tempest(Lv.15).", 6), new("Gale~Bomb(Lv.20).", 7), new("Adrenaline~Rush(Lv.40).", 8), new("Eagle~Eye(Lv.40).", 9), new("Blackbeard's~Rage(Lv.40).", 10), new("Kraken's~Revenge(Lv.70).", 11), new("Next.", 15)),
                Dialog(251, ["Pirate Epic skills contain the mighty power of the sea."], 234, new("Sea~Burial(Lv.15)[2150~CPs].", 249), new("Bloom~of~Death(Lv.40)[2150~CPs].", 248), new("Immortal~Force(Lv.70)[2150~CPs].", 247), new("Learn~about~others.", 5)),
                Dialog(110, ["You look promising. You can learn class skills and get promoted here to start your life."], 234, new LegacyNpcDialogOption("Thanks.", 111)),
            ],
            []),
    ];

    internal static async ValueTask<bool> TryHandleLegacyPromotionMastersNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyPromotionMaster(npcId, out var master))
        {
            return false;
        }

        if (optionId == LegacyNpcClosedOption)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer ignored LegacyNpc PromotionMaster closed option {FormatSession(context)} source={master.SourceMethod} npcId={npcId}.")
                .ConfigureAwait(false);
            return true;
        }

        if (!master.IsClassAllowed(session.Character.Class))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    master.SourceMethod,
                    [$"Sorry, but you are not a member of the {master.ClassName} class."],
                    [new LegacyNpcDialogOption("Alright.", LegacyNpcClosedOption)],
                    master.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 0)
        {
            var options = session.Character.Class == master.BaseClass ? master.BaseRootOptions : master.AdvancedRootOptions;
            await SendLegacyNpcDialogAsync(
                    context,
                    master.SourceMethod,
                    master.RootTexts,
                    options,
                    master.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (npcId == LegacyPromotionWindWalkerNpcId &&
            await TryHandleLegacyWindWalkerBranchDialogAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        if (TryGetLegacyPromotionDialog(master, optionId, out var dialog))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    master.SourceMethod,
                    dialog.Texts,
                    dialog.Options,
                    dialog.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (await TryHandleLegacyPromotionMasterSkillAsync(context, session, npcId, optionId, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyTrojanPromotionSkillAsync(context, session, npcId, optionId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (Array.IndexOf(master.MutationOptions, optionId) >= 0)
        {
            await SendLegacyPromotionMutationUnavailableAsync(
                    context,
                    master,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static LegacyPromotionDialog Dialog(
        byte optionId,
        string[] texts,
        ushort avatarId,
        params LegacyNpcDialogOption[] options)
    {
        return new LegacyPromotionDialog(optionId, texts, options, avatarId);
    }

    private static bool TryGetLegacyPromotionMaster(uint npcId, out LegacyPromotionMaster master)
    {
        foreach (var candidate in LegacyPromotionMasters)
        {
            if (candidate.NpcId == npcId)
            {
                master = candidate;
                return true;
            }
        }

        master = null!;
        return false;
    }

    private static bool TryGetLegacyPromotionDialog(
        LegacyPromotionMaster master,
        byte optionId,
        out LegacyPromotionDialog dialog)
    {
        foreach (var candidate in master.Dialogs)
        {
            if (candidate.OptionId == optionId)
            {
                dialog = candidate;
                return true;
            }
        }

        dialog = null!;
        return false;
    }

    private static async ValueTask SendLegacyPromotionMutationUnavailableAsync(
        WorldPacketHandlerContext context,
        LegacyPromotionMaster master,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc PromotionMaster mutation unavailable " +
                $"{FormatSession(context)} source={master.SourceMethod} sourceRange={master.SourceRange} " +
                $"npcId={npcId} option={optionId}; source branch can mutate class, inventory, attributes, skills, rewards, or quest state, " +
                "so WolfConquer leaves character state unchanged until persistence is modeled.")
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                master.SourceMethod,
                ["This promotion master branch is not available yet. Your class, items, attributes, skills, and rewards were left unchanged."],
                LegacyPromotionCloseOptions,
                master.AvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsLegacyPromotionTaoistClass(long classCode)
    {
        return classCode is >= 10_000 and <= 14_057;
    }

    private static bool IsLegacyPromotionTrojanClass(long classCode)
    {
        return classCode is >= 1_000 and <= 1_057;
    }

    private static bool IsLegacyPromotionWarriorClass(long classCode)
    {
        return classCode is >= 2_000 and <= 2_057;
    }

    private static bool IsLegacyPromotionArcherClass(long classCode)
    {
        return classCode is >= 4_000 and <= 4_057;
    }

    private static bool IsLegacyPromotionNinjaClass(long classCode)
    {
        return classCode is >= 5_000 and <= 5_057;
    }

    private static bool IsLegacyPromotionMonkClass(long classCode)
    {
        return classCode is >= 6_000 and <= 6_057;
    }

    private static bool IsLegacyDragonWarriorClass(long classCode)
    {
        return classCode is >= 8_000 and <= 8_057;
    }

    private static bool IsLegacyThunderstrikerClass(long classCode)
    {
        return classCode is >= 9_000 and <= 9_057;
    }

    private static bool IsLegacyPiratePromotionClass(long classCode)
    {
        return classCode is >= 7_000 and <= 7_057;
    }

    private static bool IsLegacyPromotionWindWalkerClass(long classCode)
    {
        return classCode is >= 16_000 and <= 16_057;
    }

    private sealed record LegacyPromotionMaster(
        uint NpcId,
        string SourceMethod,
        string SourceRange,
        string ClassName,
        ushort AvatarId,
        long BaseClass,
        Func<long, bool> IsClassAllowed,
        string[] RootTexts,
        LegacyNpcDialogOption[] BaseRootOptions,
        LegacyNpcDialogOption[] AdvancedRootOptions,
        LegacyPromotionDialog[] Dialogs,
        byte[] MutationOptions);

    private sealed record LegacyPromotionDialog(
        byte OptionId,
        string[] Texts,
        LegacyNpcDialogOption[] Options,
        ushort AvatarId);
}
