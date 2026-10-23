namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyNpcClickFallbackText = "This NPC is present, but its legacy script is not modeled yet.";



    private const uint LegacyTeleportallNpcId = 15_641;
    private const uint LegacyArchiveJoinNpcId = 641_372;
    private const uint LegacyArchiveExitNpcId = 641_374;
    private const uint LegacyJobCenterNpcId = 43_433;
    private const uint LegacyGreatMerchantNpcId = 432;
    private const uint LegacyAccountManagerNpcId = 45_632;
    private const uint LegacyOnlinePointsNpcId = 50_032;
    private const uint LegacyIronTongNpcId = 20_499;
    private const uint LegacyExchangeShopGoodsNpcId = 1_251;
    private const uint LegacyExchangeShopGoodsSourceNpcId = 21_875;
    private const uint LegacyExchangeShopGoodsGemShopId = 196;
    private const uint LegacyExchangeShopGoodsOtherShopId = 174;
    private const uint LegacyHauKanPrimaryNpcId = 21_875;
    private const uint LegacyHauKanSecondaryNpcId = 22_575;
    private const uint LegacyHauKanSupplyShopId = 23;
    private const uint LegacyNezhaPrimaryNpcId = 22_568;
    private const uint LegacyNezhaSecondaryNpcId = 22_578;
    private const uint LegacySaintTaiyiPrimaryNpcId = 22_569;
    private const uint LegacySaintTaiyiSecondaryNpcId = 22_579;
    private const uint LegacyNezhaRelicDecomposeWindowId = 904;
    private const uint LegacySaintTaiyiRelicSpiritItemId = 3_307_142;
    private const uint LegacySaintTaiyiRelicCrystalItemId = 3_306_885;
    private const ushort LegacySaintTaiyiRelicCrystalCost = 100;
    private const long LegacySaintTaiyiRelicSpiritMoneyCost = 50_000;
    private const uint LegacyPointsNpcId = 12_221;
    private const uint LegacyPointsShopWindowId = 6_003;
    private const uint LegacyMountRetailerNpcId = 5_517;
    private const uint LegacyOpenLotteryNpcId = 161_618;
    private const uint LegacyOpenLotteryWindowId = 924;
    private const uint LegacyEquipmentBlacksmithNpcId = 9_071;
    private const uint LegacyEquipmentBlacksmithWindowId = 506;
    private const uint LegacyGarmentShopKeeperNpcId = 6_002;
    private const uint LegacyGarmentShopWindowId = 502;
    private const uint LegacySteedRaceNpcId = 6_001;
    private const uint LegacySteedRaceShopWindowId = 464;
    private const uint LegacyBeauticianNpcId = 10_020;
    private const uint LegacyBeauticianWindowId = 741;
    private const uint LegacyTwinCityConductressNpcId = 10_050;
    private const uint LegacyPheonixCityConductressNpcId = 10_052;
    private const uint LegacyDesertCityConductressNpcId = 10_051;
    private const uint LegacyApeCityConductressNpcId = 10_053;
    private const uint LegacyBirdCityConductressNpcId = 10_056;
    private const uint LegacyMarketConductressNpcId = 45;
    private const uint LegacyBoxerConductorNpcId = 180;
    private const uint LegacyGuildGateKeeperNpcId = 7_000;
    private const uint LegacyLevelHuntMap1NpcId = 20_201;
    private const uint LegacyLevelHuntMap2NpcId = 20_202;
    private const uint LegacyLevelHuntMap3NpcId = 20_203;
    private const uint LegacyLevelHuntMap4NpcId = 20_204;
    private const uint LegacyLevelHuntMap5NpcId = 20_205;
    private const uint LegacyLevelHuntMap6NpcId = 20_206;
    private const uint LegacyDragonIslandConductressNpcId = 20_508;
    private const uint LegacyCobbWindNpcId = 20_500;
    private const uint LegacyDragonYuKoonNpcId = 20_823;
    private const uint LegacyYuKoonPrimaryNpcId = 20_824;
    private const uint LegacyYuKoonSecondaryNpcId = 22_573;
    private const uint LegacyDeityCobbWindPrimaryNpcId = 20_825;
    private const uint LegacyDeityCobbWindSecondaryNpcId = 22_574;
    private const uint LegacyDeitylandPortalFirstNpcId = 20_861;
    private const uint LegacyDeitylandPortalLastNpcId = 20_865;
    private const uint LegacyRareDeitylandChestKeyItemId = 3_303_973;
    private const uint LegacyEpicDeitylandChestDialogKeyItemId = 3_303_974;
    private const uint LegacyEpicDeitylandChestUnlockKeyItemId = LegacyEpicDeitylandChestDialogKeyItemId;
    private const uint LegacyEpicDeitylandChestConquerPointReward = 200_000;
    private const uint LegacyDeityAltarPrimaryNpcId = 22_560;
    private const uint LegacyDeityAltarSecondaryNpcId = 22_561;
    private const uint LegacyDeityAltarJadeOfFaithItemId = 3_307_083;
    private const uint LegacyDeityAltarNoMercyRuneItemId = 4_031_001;
    private const uint LegacyDeityAltarWitcheryRuneItemId = 4_031_301;
    private const string RuntimeLegacyDeityAltarSacrificedJadesAttribute = "DeitylandSacrificeJades";
    private const uint LegacyVictoryChestPrimaryNpcId = 21_972;
    private const uint LegacyVictoryChestSecondaryNpcId = 22_577;
    private const uint LegacyLotteryBoxFirstNpcId = 925;
    private const uint LegacyLotteryBoxLastNpcId = 930;
    private const long LegacyLotteryBoxMapId = 700;
    private const uint LegacyLotterySmallTicketItemId = 711_504;
    private const uint LegacyLotterySmallTicketCost = 3;
    private const int LegacyLotteryEntryLimit = int.MaxValue;
    private const uint LegacyGlowtownPortal1NpcId = 20_571;
    private const uint LegacyGlowtownPortal2NpcId = 20_572;
    private const uint LegacyGlowtownPortal3NpcId = 20_573;
    private const uint LegacyNpcShopGuiId = 32;
    private const ushort LegacyTeleportallAvatarId = 286;
    private const ushort LegacyGreatMerchantAvatarId = 174;
    private const ushort LegacyAccountManagerRootAvatarId = 261;
    private const ushort LegacyAccountManagerShopAvatarId = 263;
    private const ushort LegacyIronTongAvatarId = 55;
    private const ushort LegacyExchangeShopGoodsAvatarId = 164;
    private const ushort LegacyTwinCityConductressAvatarId = 10;
    private const ushort LegacyLevelHuntExitAvatarId = 76;
    private const ushort LegacyCobbWindAvatarId = 85;
    private const ushort LegacyGlowtownPortalAvatarId = 156;
    private const long LegacyTwinCityConductressTeleportCost = 100;
    private const string LegacyTeleportallDialogText =
        "We made it easy the matter for you, from here you can move around the city.";
    private const string LegacyTeleportallCityDialogText = "You can choose the city you want to go to.";
    private const string LegacyGreatMerchantDialogText1 =
        "We have various kinds of stores in the market. But if you are interested in some rare items with special effect, I will be the";
    private const string LegacyGreatMerchantDialogText2 = "~perfect one to help you with your needs.";
    private const ulong LegacyAccountManagerPaidKingCost = 150;
    private const ulong LegacyAccountManagerPaidPrinceCost = 100;
    private const ulong LegacyAccountManagerChargeChiCost = 100;
    private const ulong LegacyAccountManagerGuildSkillCost = 100;
    private const ulong LegacyAccountManagerGuildSkillDonationGrant = 1_500;
    private const ulong LegacyAccountManagerFullPromotionCost = 30;
    private const ulong LegacyAccountManagerGarmentMountCost = 30;
    private const ulong LegacyAccountManagerOneBillionConquerPointsCost = 100;
    private const long LegacyAccountManagerOneBillionConquerPointsGrant = 1_000_000_000;
    private const ulong LegacyAccountManagerVip6Cost = 30;
    private const ulong LegacyAccountManagerVip6Level = 6;
    private const ulong LegacyAccountManagerVip7Cost = 100;
    private const ulong LegacyAccountManagerVip7Level = 7;
    private const ulong LegacyAccountManagerAnima14Cost = 20;
    private const ulong LegacyAccountManagerAnima15Cost = 30;
    private const ulong LegacyAccountManagerAnima16Cost = 50;
    private const ulong LegacyAccountManagerAnima17Cost = 70;
    private const ulong LegacyAccountManagerAnima18Cost = 100;
    private const ulong LegacyAccountManagerHolyGrailCost = 50;
    private const ulong LegacyAccountManagerRunePackCost = 30;
    private const uint LegacyAccountManagerAnima14ItemId = 4_200_014;
    private const uint LegacyAccountManagerAnima15ItemId = 4_200_015;
    private const uint LegacyAccountManagerAnima16ItemId = 4_200_016;
    private const uint LegacyAccountManagerAnima17ItemId = 4_200_017;
    private const uint LegacyAccountManagerAnima18ItemId = 4_200_018;
    private const uint LegacyAccountManagerHolyGrailItemId = 2_100_095;
    private const uint LegacyAccountManagerCriticalStrikeRunePackItemId = 4_035_009;
    private const uint LegacyAccountManagerFireBurstRunePackItemId = 4_032_109;
    private const uint LegacyAccountManagerMiracleRunePackItemId = 4_032_309;
    private const uint LegacyAccountManagerNirvanaRunePackItemId = 4_032_609;
    private const uint LegacyAccountManagerBloodFeastRunePackItemId = 4_033_709;
    private const uint LegacyAccountManagerUniversalShieldRunePackItemId = 4_034_709;
    private const uint LegacyAccountManagerMiracleTouchRunePackItemId = 4_034_509;
    private const uint LegacyAccountManagerInvincibleOrientalItemId = 195_485;
    private const uint LegacyAccountManagerSkyDragonItemId = 205_003;
    private const uint LegacyAccountManagerFinalSurvivorItemId = 196_195;
    private const uint LegacyAccountManagerHungryDragonAbyssItemId = 200_771;
    private const uint LegacyAccountManagerFloralBrocadeItemId = 196_755;
    private const uint LegacyAccountManagerRoseMistGloriousItemId = 199_255;
    private const uint LegacyAccountManagerPureFragrantLotusItemId = 196_885;
    private const uint LegacyAccountManagerWingedBobcatPhantomItemId = 200_731;
    private const uint LegacyAccountManagerLightningLeopardStarItemId = 200_696;
    private const uint LegacyAccountManagerGhostRocketBiterItemId = 200_732;
    private const int LegacyAccountManagerVipDays = 30;
    private const string LegacyAccountManagerRootDialogText =
        "Hello, I am the account manager. How can I help you?";
    private const string LegacyAccountManagerInsufficientDonatePointsText =
        "You do not have enough donation points.";
    private const string LegacyArchiveJoinDialogText =
        "Do you want to enter the awakening map to improve your battle power?";
    private const string LegacyArchiveExitDialogText = "Do you want to return to Twin City?";
    private const string LegacyJobCenterDialogText =
        "Do you want to improve your strength? I can take you to the next training map.";
    private static readonly string[] LegacyIronTongDialogTexts =
    [
        "<tip desc=Long#ago,#five#masters#defeated#the#evil#king#and#destroyed#his#twisted#hell#array#on#Dragon#Island.>Iron Tong</tip>:",
        "The evil queen and her army have suddenly attacked Dragon Island.",
        "I fear she has found a way to reactivate that array. We cannot let that happen.",
        "Please join us and help defend Dragon Island.",
    ];
    private const string LegacyExchangeShopGoodsDialogText =
        "Hello, I can help you exchange your items for gems and other goods.";
    private static readonly string[] LegacyHauKanDialogTexts =
    [
        "<tip desc=Hau#Kan#has#served#in#the#Legion#of#Heroes#for#dozens#of#years.>Hau Kan</tip>:",
        "I am old, but still vigorous. If you need any supplies, find me.",
        "Flowers and herbs in Deityland have miraculous effects. Bring Wonder Herbs and exchange them for supplies.",
    ];
    private const string LegacyHauKanWinnerDialogText = "Winner of Super Elimination Prize";
    private const string LegacySaintTaiyiNoMoneyText = "You do not have enough Money point.";
    private const string LegacySaintTaiyiNoRelicCrystalsText =
        "It seems you don`t have enough Relic Crystals to craft a Relic.\nYou can collect Relic Crystals by decomposing spare Relics. Hunting monsters in the wild also gives you a chance.";
    private const string LegacyPointsDialogText = "Hi, I can exchange points and open special shops for you.";
    private const string LegacyOpenLotteryDialogText = "Would you like to try your luck?";
    private const string LegacySteedRaceDialogText =
        "Hey, races are held every few hours. Are you interested in joining the horse race?";
    private static readonly string[] LegacyEquipmentBlacksmithDialogTexts =
    [
        "Greetings! If you want to enhance your equipment, you came to the right place.",
        "I can help lower equipment level when you need it.",
    ];
    private const string LegacyTwinCityConductressDialogText =
        "Where do you want to go? I can teleport you for 100 silver.";
    private const string LegacyTwinCityConductressNoMoneyText = "You do not have enough silver.";
    private const string LegacyRegionalConductressDialogText =
        "Where~are~you~heading~for?~I~can~teleport~you~for~a~price~of~100~silver.";
    private const string LegacyRegionalConductressNoMoneyText =
        "I~can~teleport~you~for~a~price~of~100~silver.";
    private const string LegacyMarketConductressDialogText =
        "Do you want to leave the market? I can teleport you for free.";
    private const string LegacyBoxerConductorDialogText =
        "Tell me when you are ready to leave. It is free, and I will send you back to the city you came from.";
    private const string LegacyGuildGateKeeperDialogText = "Are you going to leave here?";
    private const string LegacyGlowtownPortalDialogText = "Are you sure you want to return to town?";
    private const string LegacyCobbWindCurrentBossTransferText =
        "Some demons have moved into Deityland. Heroes can go there to hunt them and claim treasure.";
    private const string RuntimeLegacyDragonYuKoonDeitylandMessageBox = "DragonYuKoonDeityland";
    private const string LegacyDragonYuKoonEnterDeitylandPromptText =
        "Your kills in the Deity will increase your PK. Penitence Amulet can only be used in the Deityland once every 30 minutes. Are you sure you want to enter the Deityland?";
    private const string LegacyDragonYuKoonEnterDeitylandBoxText =
        "Your kills in the Deity will increase your PK. Penitence Amulet can be used once every 30 minutes.";
    private const string LegacyDeityCobbWindNoConquerPointsText = "You do not have enough conquer point.";
    private const byte LegacyDeityCobbWindBossTeleportCost = 1;
    private static readonly LegacyNpcDialogOption[] LegacyTeleportallOptions =
    [
        new("GuildWar.", 1),
        new("SuperGuildwar", 2),
        new("CTF", 3),
        new("ClanWar[Choose-Map]", 4),
        new("Arena", 5),
        new("SkillTeamPK", 6),
        new("TeamPK", 7),
        new("ElitePK", 8),
        new("Events-Server", 9),
        new("Poker", 10),
        new("No Thx.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTeleportallCityOptions =
    [
        new("T.C.", 15),
        new("Phonix", 20),
        new("ApeCity", 30),
        new("DesertCity", 40),
        new("BirdIsland", 50),
        new("No Thx.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyGreatMerchantOptions =
    [
        new("Well,~impress~me.", 1),
        new("Purchase~Item.", 20),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAccountManagerRootOptions =
    [
        new("Buy~from~GM.", 1),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAccountManagerShopOptions =
    [
        new("Full~Chi~99~[100~points].", 2),
        new("1kkk~CPs~[100~points].", 3),
        new("Anima14~[20~points].", 4),
        new("Anima15~[30~points].", 5),
        new("Anima16~[50~points].", 6),
        new("Anima17~[70~points].", 7),
        new("Anima18~[100~points].", 8),
        new("HolyGrail~[50~points].", 9),
        new("30-day~King~[150~points].", 10),
        new("Rune~Pack~[30~points].", 31),
        new("30-day~Prince~[100~points].", 34),
        new("VIP6~30~days~[30~points].", 35),
        new("Full~Promotion~[30~points].", 36),
        new("Garment~and~Mount~[30~points].", 37),
        new("VIP7~30~days~[100~points].", 197),
        new("Full~Guild~Skill~100~[100~points].", 166),
        new("Never~mind.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAccountManagerRunePackOptions =
    [
        new("Critical~Strike~Booster.", 12),
        new("Fire~Burst.", 13),
        new("Miracle.", 14),
        new("Nirvana.", 15),
        new("Blood~Feast.", 16),
        new("Universal~Shield.", 17),
        new("Miracle~Touch.", 18),
        new("Never~mind.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAccountManagerGarmentMountOptions =
    [
        new("Invincible~Oriental.", 38),
        new("Sky~Dragon.", 39),
        new("Final~Survivor.", 40),
        new("Hungry~Dragon~Abyss.", 41),
        new("Floral~Brocade.", 42),
        new("Rose~Mist~Glorious.", 43),
        new("Pure~Fragrant~Lotus.", 44),
        new("Winged~Bobcat~Phantom.", 45),
        new("Lightning~Leopard~Star.", 46),
        new("Ghost~Rocket~Biter.", 47),
        new("Never~mind.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyArchiveJoinOptions =
    [
        new("Yes.", 1),
        new("Maybe~later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyArchiveExitOptions =
    [
        new("Yes.", 1),
        new("No.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyJobCenterOptions =
    [
        new("Take~me~there.", 1),
        new("No,~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyIronTongOptions =
    [
        new("Send~me~to~Dragon~Island.", 1),
        new("I~will~come~back~later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyExchangeShopGoodsOptions =
    [
        new("Open~gem~shop.", 1),
        new("Open~other~shop.", 2),
        new("No,~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyHauKanOptions =
    [
        new("Exchange~for~supplies.", 1),
        new("Who~won~the~super~elimination~prize?", 2),
        new("No,~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyHauKanWinnerOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyNezhaOptions =
    [
        new("Decompose~a~Relic.", 1),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySaintTaiyiOptions =
    [
        new("Craft~a~Relic~Spirit.~(50,000~Silver)", 1),
        new("Craft~a~Relic.~(100~Relic~Crystals)", 2),
        new("How~to~obtain~the~Relics?", 3),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySaintTaiyiRelicsInfoOptions =
    [
        new("Attributes~of~Relics.", 4),
        new("Durability~of~Relics.", 5),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySaintTaiyiSingleCloseOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySaintTaiyiAwwOptions =
    [
        new("Aww!", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPointsOptions =
    [
        new("Study~Points.", 1),
        new("Inner~Power~Points.", 2),
        new("Ninja~Points.", 3),
        new("Warrior~Items.", 4),
        new("Items.", 5),
        new("Shops.", 6),
        new("No.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyOpenLotteryOptions =
    [
        new("Open~lottery~window.", 1),
        new("No,~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySteedRaceOptions =
    [
        new("Sign~me~up.", 1),
        new("Open~shop.", 2),
        new("I~will~come~later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyEquipmentBlacksmithOptions =
    [
        new("Lower~Equipment~Level.", 1),
        new("I~will~come~later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTwinCityConductressOptions =
    [
        new("Market.", 14),
        new("Dragon~Island.", 7),
        new("Item~Hunt~Map~1.", 8),
        new("Point~Hunt~Map~2.", 9),
        new("Item~Hunt~Map~3.", 10),
        new("Point~Hunt~Map~4.", 11),
        new("Point~Hunt~Map~5.", 12),
        new("Hunt~Map~6.", 13),
        new("Phoenix~Castle.", 1),
        new("Black~Tiger~City.", 2),
        new("Cloud~Gate.", 3),
        new("Bird~Island.", 4),
        new("Never~mind.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyGuildGateKeeperOptions =
    [
        new("Yes.", 1),
        new("No.~Wait~a~moment.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTwinCityConductressNoMoneyOptions =
    [
        new("All~right.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRegionalConductressNoMoneyOptions =
    [
        new("Ah yes.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPheonixCityConductressOptions =
    [
        new("Twin~City", 1),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDesertCityConductressOptions =
    [
        new("Twin~City", 1),
        new("Mystic~Castle", 2),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyApeCityConductressOptions =
    [
        new("Twin~City", 1),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyBirdCityConductressOptions =
    [
        new("Twin~City.", 1),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyMarketConductressOptions =
    [
        new("Yeah.Thanks.", 1),
        new("No,I shall stay here.", LegacyNpcClosedOption),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyBoxerConductorOptions =
    [
        new("Yes,~thanks.", 1),
        new("No,~I~will~stay.", LegacyNpcClosedOption),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyLevelHuntExitOptions =
    [
        new("Yes Plase", 1),
        new("See~you~later.", LegacyNpcClosedOption),
    ];

    private static readonly string[] LegacyDragonIslandConductressDialogTexts =
    [
        "<tip color=0xffffff00 desc=The#Conductress#inherited#the#station#from#her#father.#She#prospered#the#business#",
        "by#adopting#advance#management #methods,#and#is#praised#for#providing#excellent#teleport#services.>",
        "Conductress</tip>:\n",
        "May I help you? I provide decent teleport services.",
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDragonIslandConductressOptions =
    [
        new("Twin~City.", 1),
        new("Market.", 20),
        new("Deityland.~(new~hunting~area)", 3),
        new("I`m~just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyGlowtownPortalOptions =
    [
        new("Yes.", 1),
        new("I~will~look~around.", LegacyNpcClosedOption),
    ];

    private static readonly string[] LegacyCobbWindDialogBaseTexts =
    [
        "<tip desc=Cobb#Wind#was#once#a#pirate,#but#Sky#Shan#saved#him#and#he#turned#to#justice.>Cobb Wind</tip>:",
        "Are you here to support us? Great. The demons have occupied the Nine Hells portal ruins.",
        "They have summoned Howling Sea Giant, Forbidden Heaven Beast, and Crystal Snow Demon to Dragon Island.",
        "The alliance has called every hero to defeat these demons and retake Dragon Island.",
    ];

    private static readonly string[] LegacyCobbWindDetailsDialogTexts =
    [
        "-----------------------------",
        "       [Demon]                 [Location]              [Time]\n",
        "      Howling Sea Giant        Dragon Island (568,372)  hourly at :15\n",
        "      Forbidden Heaven Beast   Dragon Island (349,635)  hourly at :30\n",
        "      Crystal Snow Demon       Dragon Island (658,718)  hourly at :57\n",
        "-----------------------------\n",
        "Note: these demons do not appear from 00:00 to 09:59.",
    ];

    private static readonly string[] LegacyYuKoonDialogTexts =
    [
        "<tip desc=To#rescue#more#people#from#misery,#Yu#Koon#decided#to#join#the#Legion#of#",
        "Heroes#as#an#adviser.#His#careful#and#tactical#planning#has#helped#the#legion#won#many#",
        "battles#against#the#devil#army.>Yo Koon</tip>:\n",
        "The Deityland hides ancient treasures, but devils may ambush you somewhere. Be careful!",
    ];

    private static readonly string[] LegacyDragonYuKoonDialogTexts =
    [
        "<tip desc=To#rescue#more#people#from#misery,#Yu#Koon#decided#to#join#the#Legion#of#",
        "Heroes#as#an#adviser.#His#careful#and#tactical#planning#has#helped#the#legion#won#many#",
        "battles#against#the#devil#army.>Yo Koon</tip>:\n",
        "It`s said the ancient deities sealed the sources of their power in the Deityland after the battle of justice thousands of years ago.",
        "~However, this land is under attack from the Queen of Evil and her devil army. Hurry and go smash the greedy enemies!\n",
        "Rare Rewards: 3,000 CPs, Yellow Rune, Relics, etc.\n",
        "Common Rewards: Rune Fragment, Meteors, Favored Training Pills, etc.\n",
    ];

    private static readonly string[] LegacyDeitylandChaosInfoTexts =
    [
        "\t\t\t\t\t\t\t \t\t*Deityland in Chaos*\n",
        "--------------------------------------------------------\n",
        "1. Killing devils in the Deityland will bring you rewards.\nIt may be rare rewards if you have <tip color=0xfff05050 desc=100#Luck#Points#are#added#at#00:00#every#day.#10#Luck#Points#is#consumed#per#rare#treasure#you#get.>Luck Points</tip> while hunting.\n",
        "   Without Luck Point, you can only get common rewards.\nAt the end of a day, 80% of your remaining Luck will be added to your Luck next day, up to 2.5x points in a day.\n",
        "2. You`ll be granted 1 stack of <tip color=0xfff05050 desc=Each#Bliss#lasts#for#5#minutes.#When#your#stack#of#Bliss#increases,#your#Bliss#will#be#reset#to#5#minutes.>Bliss</tip> per opponent you defeat, up to 9 stacks.\n",
        "    The more stacks of your Bliss, the higher drop rate of treasures.\n",
        "3. Defeating robber Sprite will definitely bring you treasures and also a random 90-sec <tip color=0xfff05050 desc=1.#P-Attack#10000,#M-Attack#30000\n2.#HP#100000\n3.#Break#200%\n4.#Immunity#200%\n5.#Storm#effect>buff</tip>.\n",
        "4. Defeating the Queen of Evil within 8 minutes or each of the other beasts within 3 minutes\n",
        "    will bring you extra rewards.\n",
        "--------------------------------------------------------\n",
    ];

    private static readonly string[] LegacyDeityCobbWindDialogBaseTexts =
    [
        "<tip desc=Cobb#Wind,#formerly#a#pirate,#was#save#by#Sky#Shan#from#a#murder.#",
        "He#has#turned#to#the#side#of#justice#since#then,#and#made#lots#of#contributions.>Cobb Wind</tip>:\n",
        "The <tip color=0xfff05050 desc=Killing#the#Queen#of#Evil#brings#+2#Stone,#Blazing#CP#Fragment,#etc.>Queen of Evil</tip> has gathered",
        "~<tip color=0xfff05050 desc=Killing#Nether#Tyrant#brings#Savage#Bone,#P7#Dragon#Soul#Fragment,#Meteor#Scroll,#etc.>Nether Tyrant</tip>,",
        "~<tip color=0xfff05050 desc=Killing#Bloody#Banshee#brings#Dragon#Ball,#+2#Stone,#Mount#Armor,#Weapon#Accessory,#etc.>Bloody Banshee</tip>,",
        "~<tip color=0xfff05050 desc=Killing#Dragon#Wraith#brings#Superior#Refinery#Pack,#Blazing#CP#Fragment,#etc.>Dragon Wraith</tip> and",
        "~<tip color=0xfff05050 desc=Killing#Chilling#Spook#brings#P6#Dragon#Soul,#Dragon#Ball,#Blazing#CP#Fragment,#etc.>Chilling Spook</tip>",
        "~in the Deityland. They must have known the treasures of ancient deities in the land. Hurry up! We have to stop them from getting stronger.\n",
    ];

    private static readonly string[] LegacyDeityCobbWindDetailsDialogTexts =
    [
        "-----------------------------\n",
        "     [Beast]                    [Location]               [Appear Time]\n",
        "Dragon Wraith     (163,415), upper-left part of Deityland     At the 5th minute of every hour of every day\n",
        "Chilling Spook    (1020,698), lower-right part of Deityland   At the 30th minute of every hour of every day\n",
        "Bloody Banshee    (484,176), upper-right part of Deityland    At the 57th minute of every hour of every day\n",
        "Nether Tyrant     Random, central part of Deityland           At the 45th minute of every hour of every day\n",
        "Queen of Evil     Random, central part of Deityland           At 19:10 and 21:10, every day\n",
        "-----------------------------\n",
        "Tip: All the beasts above will not appear between 00:00 and 04:59, every day.\n",
    ];

    private static readonly string[] LegacyNezhaDialogTexts =
    [
        "<tip color=0xffffff00 desc=Nezha#is#a#human-shaped#talisman#created#by#Saint#Taiyi.#He#may#look#lovely,#",
        "but#he#possesses#stunning#abilities#and#is#out#of#control#sometimes.#His#tricks#make#Saint#Taiyi#loves#",
        "and#hates#him#the#most.>Nezha</tip>:\n",
        "My master looks so cute when he is angry. I often decompose his crafts into Relics Crystals, so you know...",
        "~I`m not making trouble. Relics Crystal is really useful. I can show you if you have a Relic to decompose.",
    ];

    private static readonly string[] LegacySaintTaiyiDialogTexts =
    [
        "<tip color=0xffffff00 desc=Saint#Taiyi#may#not#the#most#powerful#taoist#in#Mt.#Kunlun,#",
        "but#his#crafting#skill#is#incomparable.#He#has#crafted#countless#talismans#and#beneficial#pills,#",
        "including#his#favorite#Nezha,#a#human-shape#talisman.#Though#Nezha#is#very#naughty#sometimes,#",
        "Saint#Taiyi#could#never#hate#him.>Saint Taiyi</tip>:\n",
        "Do you know the `Relics`? They are rare attribute items crafted by the ancient deities. Fortunately, I`ve acquired this crafting skill.",
        "~If you have some <tip color=0xfff05050 desc=The#magic#crystal#can#be#used#to#craft#or#repair#Relics.#How#to#obtain:#decompose#Relics#(100%#chance#to#get),#hunting#monsters#in#each#zone,#10x#drop#rate#on#Dragon#Island#devils.>Relic Crystals</tip>,",
        "~I can help you make a Relic providing more than 3 kinds of attributes.\nBy the way,",
        "I can give 1 <tip color=0xfff05050 desc=As#the#core#of#Relics,#it#can#be#shaped#into#a#Relic#after#absorbing#energy#from#treasures#like#DBs#and#Meteors.#Once#a#day.>Relic Spirit</tip> for every 50,000 Silver.",
    ];

    private static readonly string[] LegacySaintTaiyiRelicsInfoTexts =
    [
        "                      *How to Obtain Relics?*\n",
        "----------------------------------------------------------\n",
        "1. The common devils in the Deityland may carry such a Relic.\n",
        "2. Killing the Beast/BOSS will definitely bring you such a Relic.\n",
        "3. Each Relic can be decomposed into several Relic Crystals according to their attached attributes. The Relic Crystals can be used to craft or repair Relics.\n4. When defeated, a hero`s equipped Relic may be dropped.\n",
        "----------------------------------------------------------\n",
    ];

    private static readonly string[] LegacySaintTaiyiRelicDurabilityTexts =
    [
        "                      *Durability of Relics*\n",
        "----------------------------------------------------------\n",
        "1. The durability of your equipped Relic will decrease if you were killed.\n",
        "2. When your Relic is broken, you can use Relic Crystal to repair it within 48 hours.\n",
        "3. Death will not lead to drop or durability loss of Relics during Guild War, Clan War, Plunder War, or Arena.\n",
        "----------------------------------------------------------\n",
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCobbWindOptions =
    [
        new("Detailed~guest~information.", 1),
        new("Thanks~for~the~warning.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCobbWindDetailsOptions =
    [
        new("I~understand.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyYuKoonOptions =
    [
        new("Return~to~the~Dragon~Island.", 1),
        new("Any~other~tips~for~me?", 2),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDragonYuKoonOptions =
    [
        new("Send~me~to~the~Deityland.", 1),
        new("Return~to~the~Glowtown.", 2),
        new("Any~other~tips~for~me?", 3),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDeitylandChaosInfoOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    private const string LegacyDeitylandPortalDialogText =
        "This portal will send you to the supply station. Do you want to use the portal now?";

    private static readonly string[] LegacyRareDeitylandChestDialogTexts =
    [
        "\t\t\t\t\t\t\t\t\t*<tip desc=This#delicate#chest#with#vivid#dragon#pattern#contains#valuable#items~crafted~by~the~ancient~deities.#However,#it`s#locked.>Rare Deityland Chest</tip>*\n",
        "----------------------------------------------------------\n",
        "Requirement: A <tip color=0xfff05050 desc=The#devils#in#the#Deityland#may#carry#the#Rare#Chest#Key.>Rare Chest Key</tip>\n",
        "Rewards: A chance to get Chasm Star Stone Pack, points, etc.\n",
        "----------------------------------------------------------\n",
    ];

    private static readonly string[] LegacyEpicDeitylandChestDialogTexts =
    [
        "\t\t\t\t\t\t\t\t\t*<tip desc=This#divine#chest#contains#fabulous#treasures#of#the#ancient#deities.#However,#it`s#locked.>Epic Deityland Chest</tip>*\n",
        "----------------------------------------------------------\n",
        "Requirement: An <tip color=0xfff05050 desc=The#devils#in#the#Deityland#may#carry#the#Epic#Chest#Key.>Epic Chest Key</tip>\n",
        "Rewards: A chance to get 200,000 CPs, Frozen Chi Pill Scrap, Anima p5, P7 Dragon Soul, etc.\n",
        "----------------------------------------------------------\n",
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDeitylandPortalOptions =
    [
        new("Yes.", 1),
        new("No.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDeitylandChestKeyOptions =
    [
        new("I~own~the~key!", 1),
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDeitylandChestNoKeyOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly uint[] LegacyRareDeitylandChestRewards =
    [
        3_306_362,
        3_303_480,
        3_003_124,
    ];

    private static readonly uint[] LegacyEpicDeitylandChestRewards =
    [
        4_200_005,
        3_007_269,
        3_004_247,
    ];

    private static readonly string[] LegacyDeityAltarRulesDialogTexts =
    [
        "\t\t\t\t\t\t\t\t\t*Rules for Deityland Sacrifice*\n",
        "------------------------------------------------------------\n",
        "1. Killing devils in the Deityland brings you a chance of getting Jades of Faith.\n",
        "2. Sacrifice 10 Jades of Faith to get a Yellow Rune (B) Pack, and 30 Jades to qualify for the ranking.\n",
        "3. An extra Yellow Rune (B) Pack will be rewarded for entering the top 10 for the first time.\n",
        "4. An extra prize will be rewarded to the final top 10 heroes announced at 23:59 every day.\n",
        "5. The Deityland Altar allows Rune transformation between L1 No Mercy and L1 Witchery.\n",
        "------------------------------------------------------------\n",
    ];

    private static readonly string[] LegacyDeityAltarTransformDialogTexts =
    [
        "We allow Rune transformation between L1 No Mercy and L1 Witchery here.\n",
        "What`s the Rune you want to transform?",
    ];

    private const string LegacyDeityAltarMissingWitcheryText =
        "Failed to transform. Please make sure you have a L1 Witchery.";
    private const string LegacyDeityAltarMissingNoMercyText =
        "Failed to transform. Please make sure you have a L1 No Mercy.";
    private static readonly string[] LegacyDeityAltarMissingJadesTexts =
    [
        "It seems you don`t have enough Jades of Faith to sacrifice.",
        "Go hunting devils in the Deityland to collect more Jades of Faith.",
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDeityAltarMainOptions =
    [
        new("Submit~a~Jade~of~Faith.", 1),
        new("Rune~transformation.", 2),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDeityAltarRulesOptions =
    [
        new("Submit~1~Jade~of~Faith.", 5),
        new("Submit~10~Jades~of~Faith.", 6),
        new("Submit~all~Jades~of~Faith.", 7),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDeityAltarTransformOptions =
    [
        new("Change~L1~Witchery~to~No~Mercy.", 3),
        new("Change~L1~No~Mercy~to~Witchery.", 4),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDeityAltarSingleCloseOptions =
    [
        new("Alright.", LegacyNpcClosedOption),
    ];

    private static readonly string[] LegacyVictoryChestDialogTexts =
    [
        "\t\t\t\t\t\t\t\t\t*<tip desc=Heroes#who#have#killed#100#devils#in#the#Deityland#can#unlock#this#chest#and#get#wonderful#rewards.>Victory Chest</tip>*\n",
        "----------------------------------------------------------\n",
        "Requirement: Kill 100 devils in the Deityland, once a day.\n",
        "Rewards: 1 Bright Star Stone for sure, and a shot to win +2 Stone, Blazing CP Fragment, 150 Chi, etc.\n",
        "----------------------------------------------------------\n",
    ];

    private const string LegacyVictoryChestAlreadyUnlockedText =
        "You`ve already unlocked this chest today. Please try again tomorrow.";
    private const string LegacyVictoryChestMissingKillsText =
        "You need to kill 100 devils in the Deityland before you can unlock this chest.";

    private static readonly LegacyNpcDialogOption[] LegacyVictoryChestUnlockedOptions =
    [
        new("Unlock~this~chest.", 1),
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyVictoryChestLockedOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyVictoryChestSingleCloseOptions =
    [
        new("Okay.", LegacyNpcClosedOption),
    ];

    private static readonly uint[] LegacyVictoryChestRewards =
    [
        3_008_727,
        3_009_002,
        4_200_004,
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDeityCobbWindOptions =
    [
        new("Go~kill~the~Dragon~Wraith!", 2),
        new("Go~kill~the~Chilling~Spook!", 3),
        new("Go~kill~the~Bloody~Banshee!", 4),
        new("Go~kill~the~QueenOfEvil!", 5),
        new("Go~kill~the~RockMonsterKing!", 6),
        new("Learn~more~about~these~beasts.", 1),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDeityCobbWindDetailsOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDeityCobbWindNoConquerPointsOptions =
    [
        new("Aww!", LegacyNpcClosedOption),
    ];

    private static readonly IReadOnlyDictionary<uint, string> LegacyNpcScriptNames =
        new Dictionary<uint, string>
        {
            [1] = "TheStorekeeper",
            [3] = "Pharmacist",
            [4] = "Armorer",
            [5] = "BlackSmith",
            [8] = "Warehause",
            [44] = "Warehause",
            [LegacyGreatMerchantNpcId] = "GreatMerchant",
            [LegacyNobilityDonorNpcId] = "NobilityDonor",
            [LegacyNobilityGeneralJuddNpcId] = "NobilityDonor",
            [LegacyAccountManagerNpcId] = "AccountManager",
            [LegacyOnlinePointsNpcId] = "OnlinePoints",
            [4_101] = "Warehause",
            [7_762] = "Warehause",
            [10_011] = "Warehause",
            [10_012] = "Warehause",
            [10_027] = "Warehause",
            [10_028] = "Warehause",
            [LegacyTeleportallNpcId] = "Teleportall",
            [LegacyJobCenterNpcId] = "joinJopCentr",
            [LegacyArchiveJoinNpcId] = "joinArchaveMap",
            [LegacyArchiveExitNpcId] = "outArchaveMap",
            [LegacyIronTongNpcId] = "IronTong",
            [LegacyExchangeShopGoodsNpcId] = "Exchange_shop_Goods",
            [LegacyHauKanPrimaryNpcId] = "HauKan",
            [LegacyHauKanSecondaryNpcId] = "HauKan",
            [LegacyNezhaPrimaryNpcId] = "Nezha",
            [LegacyNezhaSecondaryNpcId] = "Nezha",
            [LegacySaintTaiyiPrimaryNpcId] = "SaintTaiyi",
            [LegacySaintTaiyiSecondaryNpcId] = "SaintTaiyi",
            [LegacyPointsNpcId] = "Points",
            [LegacyMountRetailerNpcId] = "MountRetailer",
            [LegacySteedRaceNpcId] = "SteedRace",
            [LegacyOpenLotteryNpcId] = "openlottery",
            [LegacyEquipmentBlacksmithNpcId] = "EquipmentBlacksmith",
            [LegacyGarmentShopKeeperNpcId] = "GarmentShopKeeper",
            [LegacyBeauticianNpcId] = "Beautician",
            [LegacyTwinCityConductressNpcId] = "TwinCityConductress",
            [LegacyPheonixCityConductressNpcId] = "PheonixCityConductress",
            [LegacyDesertCityConductressNpcId] = "DesertCityConductress",
            [LegacyApeCityConductressNpcId] = "ApeCityConductress",
            [LegacyBirdCityConductressNpcId] = "BirdCityConductress",
            [LegacyMarketConductressNpcId] = "MarketConductress",
            [LegacyBoxerConductorNpcId] = "BoxerConductor",
            [LegacyGuildGateKeeperNpcId] = "GuildGateKeeper",
            [19_121] = "KingDoomCrystals",
            [19_122] = "KingDoomCrystals",
            [19_123] = "KingDoomCrystals",
            [19_124] = "KingDoomCrystals",
            [19_125] = "KingDoomCrystals",
            [LegacyLevelHuntMap1NpcId] = "leveHuntMap1",
            [LegacyLevelHuntMap2NpcId] = "leveHuntMap2",
            [LegacyLevelHuntMap3NpcId] = "leveHuntMap3",
            [LegacyLevelHuntMap4NpcId] = "leveHuntMap4",
            [LegacyLevelHuntMap5NpcId] = "leveHuntMap5",
            [LegacyLevelHuntMap6NpcId] = "leveHuntMap6",
            [LegacyDragonIslandConductressNpcId] = "DragonIslandConductress",
            [LegacyCobbWindNpcId] = "CobbWind",
            [LegacyDragonYuKoonNpcId] = "DragonYuKoon",
            [LegacyYuKoonPrimaryNpcId] = "YuKoon",
            [LegacyYuKoonSecondaryNpcId] = "YuKoon",
            [LegacyDeityCobbWindPrimaryNpcId] = "DeityCobbWind",
            [LegacyDeityCobbWindSecondaryNpcId] = "DeityCobbWind",
            [21_973] = "RareDeitylandChests",
            [21_974] = "RareDeitylandChests",
            [21_975] = "RareDeitylandChests",
            [21_976] = "RareDeitylandChests",
            [22_598] = "RareDeitylandChests",
            [22_599] = "RareDeitylandChests",
            [22_600] = "RareDeitylandChests",
            [22_601] = "RareDeitylandChests",
            [22_602] = "RareDeitylandChests",
            [22_603] = "RareDeitylandChests",
            [22_604] = "RareDeitylandChests",
            [22_605] = "RareDeitylandChests",
            [22_607] = "EpicDeitylandChests",
            [22_608] = "EpicDeitylandChests",
            [22_609] = "EpicDeitylandChests",
            [LegacyDeityAltarPrimaryNpcId] = "DeityAltar",
            [LegacyDeityAltarSecondaryNpcId] = "DeityAltar",
            [LegacyVictoryChestPrimaryNpcId] = "VictoryChest",
            [LegacyVictoryChestSecondaryNpcId] = "VictoryChest",
            [925] = "LotteryBoxes",
            [926] = "LotteryBoxes",
            [927] = "LotteryBoxes",
            [928] = "LotteryBoxes",
            [929] = "LotteryBoxes",
            [930] = "LotteryBoxes",
            [20_861] = "DeitylandPortals",
            [20_862] = "DeitylandPortals",
            [20_863] = "DeitylandPortals",
            [20_864] = "DeitylandPortals",
            [20_865] = "DeitylandPortals",
            [LegacyGlowtownPortal1NpcId] = "GlowtownPortal1",
            [LegacyGlowtownPortal2NpcId] = "GlowtownPortal2",
            [LegacyGlowtownPortal3NpcId] = "GlowtownPortal3",
        };

    private static readonly IReadOnlySet<uint> LegacyOpenGuiNpcIds = new HashSet<uint>
    {
        1,
        3,
        4,
        5,
    };

    private static readonly IReadOnlyDictionary<byte, LegacyNpcTeleportDestination> LegacyTeleportallDestinations =
        new Dictionary<byte, LegacyNpcTeleportDestination>
        {
            [1] = new(1_002, 319, 318, "NpcHandler.Teleportall"),
            [2] = new(1_002, 328, 307, "NpcHandler.Teleportall"),
            [3] = new(1_002, 319, 318, "NpcHandler.Teleportall"),
            [5] = new(1_002, 422, 273, "NpcHandler.Teleportall"),
            [6] = new(1_002, 448, 271, "NpcHandler.Teleportall"),
            [7] = new(1_002, 443, 272, "NpcHandler.Teleportall"),
            [8] = new(1_002, 438, 272, "NpcHandler.Teleportall"),
            [9] = new(1_002, 372, 363, "NpcHandler.Teleportall"),
            [10] = new(1_002, 347, 444, "NpcHandler.Teleportall"),
            [15] = new(1_002, 431, 269, "NpcHandler.Teleportall"),
            [20] = new(1_011, 209, 260, "NpcHandler.Teleportall"),
            [30] = new(1_020, 565, 583, "NpcHandler.Teleportall"),
            [40] = new(1_000, 492, 667, "NpcHandler.Teleportall"),
            [50] = new(1_015, 706, 571, "NpcHandler.Teleportall"),
        };

    private static readonly IReadOnlyDictionary<byte, LegacyNpcTeleportDestination> LegacyTwinCityConductressDestinations =
        new Dictionary<byte, LegacyNpcTeleportDestination>
        {
            [1] = new(1_002, 680, 320, "NpcHandler.TwinCityConductress"),
            [2] = new(1_002, 348, 841, "NpcHandler.TwinCityConductress"),
            [3] = new(1_002, 33, 528, "NpcHandler.TwinCityConductress"),
            [4] = new(1_002, 559, 576, "NpcHandler.TwinCityConductress"),
            [5] = new(1_002, 107, 449, "NpcHandler.TwinCityConductress"),
            [6] = new(10_445, 64, 81, "NpcHandler.TwinCityConductress"),
            [7] = new(1_002, 580, 586, "NpcHandler.TwinCityConductress"),
            [8] = new(10_342, 66, 267, "NpcHandler.TwinCityConductress"),
            [9] = new(10_341, 122, 95, "NpcHandler.TwinCityConductress"),
            [10] = new(10_323, 122, 95, "NpcHandler.TwinCityConductress"),
            [11] = new(10_324, 122, 95, "NpcHandler.TwinCityConductress"),
            [12] = new(10_322, 122, 95, "NpcHandler.TwinCityConductress"),
            [13] = new(10_343, 122, 95, "NpcHandler.TwinCityConductress"),
            [14] = new(10_364, 302, 280, "NpcHandler.TwinCityConductress"),
        };

    private static readonly IReadOnlyDictionary<byte, LegacyNpcTeleportDestination> LegacyPheonixCityConductressDestinations =
        new Dictionary<byte, LegacyNpcTeleportDestination>
        {
            [1] = new(1_011, 13, 376, "NpcHandler.PheonixCityConductress"),
            [2] = new(1_036, 217, 198, "NpcHandler.PheonixCityConductress"),
        };

    private static readonly IReadOnlyDictionary<byte, LegacyNpcTeleportDestination> LegacyDesertCityConductressDestinations =
        new Dictionary<byte, LegacyNpcTeleportDestination>
        {
            [1] = new(1_000, 973, 668, "NpcHandler.DesertCityConductress"),
            [2] = new(1_000, 82, 321, "NpcHandler.DesertCityConductress"),
            [3] = new(1_036, 217, 198, "NpcHandler.DesertCityConductress"),
        };

    private static readonly IReadOnlyDictionary<byte, LegacyNpcTeleportDestination> LegacyApeCityConductressDestinations =
        new Dictionary<byte, LegacyNpcTeleportDestination>
        {
            [1] = new(1_020, 378, 13, "NpcHandler.ApeCityConductress"),
            [2] = new(1_036, 217, 198, "NpcHandler.ApeCityConductress"),
        };

    private static readonly IReadOnlyDictionary<byte, LegacyNpcTeleportDestination> LegacyBirdCityConductressDestinations =
        new Dictionary<byte, LegacyNpcTeleportDestination>
        {
            [1] = new(1_015, 1_015, 710, "NpcHandler.BirdCityConductress"),
            [2] = new(1_036, 217, 198, "NpcHandler.BirdCityConductress"),
        };
}
