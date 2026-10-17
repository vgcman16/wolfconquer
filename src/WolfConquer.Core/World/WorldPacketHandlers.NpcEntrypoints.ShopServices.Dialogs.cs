namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly LegacyShopServiceDialog[] LegacyShopServiceDialogs =
    [
        new(
            LegacyFurnitureStoreNpcId,
            [0],
            "NpcHandler.FurnitureStore",
            "38431-38455",
            ["Welcome! This is the Twin City Furniture Store. Take a look around. I have the best furniture in Twin City."],
            [new("Show~me~your~wares.", 1), new("Not~my~concern.", LegacyNpcClosedOption)],
            AvatarId: 188),
        new(
            LegacyTinterNpcId,
            [0],
            "NpcHandler.Tinter",
            "38457-38527",
            ["Welcome! Please wear the armor first. Which armor do you want me to dye?"],
            [new("Cap", 1), new("Coat", 2)],
            AvatarId: 7),
        new(
            LegacyTinterNpcId,
            [1],
            "NpcHandler.Tinter",
            "38457-38527",
            ["There are seven colors for you to choose. You can try all the colors for no extra charge. Which color do you like best?"],
            [
                new("Color~1", 3),
                new("Color~2", 4),
                new("Color~3", 5),
                new("Color~4", 6),
                new("Color~5", 7),
                new("Color~6", 8),
                new("Color~7", 9),
            ],
            AvatarId: 7),
        new(
            LegacyTinterNpcId,
            [2],
            "NpcHandler.Tinter",
            "38457-38527",
            ["There are seven colors for you to choose. You can try all the colors for no extra charge. Which color do you like best?"],
            [
                new("Color~1", 13),
                new("Color~2", 14),
                new("Color~3", 15),
                new("Color~4", 16),
                new("Color~5", 17),
                new("Color~6", 18),
                new("Color~7", 19),
            ],
            AvatarId: 7),
        new(
            LegacyShopboyNpcId,
            [0],
            "NpcHandler.Shopboy",
            "38529-38591",
            [
                "Our shop is famous for dyeing. If you want to have your equipment dyed, please wear it before you enter.",
                "You have a wide choice of colors. One meteor will be charged before you try the colors. Do you want to try?",
            ],
            [new("Yes,~here~is~a~meteor.", 1), new("Can~you~dye~my~armor~black?", 2)],
            AvatarId: 7),
        new(
            LegacyShopboyNpcId,
            [2],
            "NpcHandler.Shopboy",
            "38529-38591",
            [
                "Sure. Once your armor is dyed black, it will never fade when upgraded unless I dye it another color.",
                "I will charge 1 DragonBall to do it.",
            ],
            [new("Yes.~Here's~a~DragonBall.", 3), new("Let~me~think~it~over.", LegacyNpcClosedOption)],
            AvatarId: 7),
        new(
            LegacyPharmacistNpcId,
            [2],
            "NpcHandler.Pharmacist",
            "53233-53326",
            ["Besides MP and HP potions, I also have scrolls that can take you to the city they are made for. You are lucky; I have just got something new for you!"],
            [new("Thanks~a~lot!", 3)],
            AvatarId: 10),
        new(
            LegacyPharmacistNpcId,
            [5],
            "NpcHandler.Pharmacist",
            "53233-53326",
            ["Oops, I have told you all I know. You should pay a visit to the Barber. I am sure you will get something from her."],
            [new("Okay!", 6), new("See~you~later.", LegacyNpcClosedOption)],
            AvatarId: 10),
        new(
            LegacyPharmacistNpcId,
            [7],
            "NpcHandler.Pharmacist",
            "53233-53326",
            ["The Barber is a talented hairdresser. She is worth paying a visit to! Now you should visit the Barber. I am sure you will get something from her."],
            [new("Okay!", LegacyNpcClosedOption)],
            AvatarId: 10),
        new(
            LegacyTheStorekeeperNpcId,
            [2],
            "NpcHandler.TheStorekeeper",
            "53328-53419",
            ["I have boots, necklaces, and rings, which are quite necessary to improve your skills and prowess."],
            [new("I~learnt~something~new!", 3)],
            AvatarId: 10),
        new(
            LegacyTheStorekeeperNpcId,
            [3],
            "NpcHandler.TheStorekeeper",
            "53328-53419",
            ["You are lucky, kid! I have just got something new for you!"],
            [new("Thanks~a~lot!", 4)],
            AvatarId: 10),
        new(
            LegacyTheStorekeeperNpcId,
            [5],
            "NpcHandler.TheStorekeeper",
            "53328-53419",
            ["I have told you all I know. I suggest you find the Pharmacist. She should have something for you there."],
            [new("Thanks!", 6)],
            AvatarId: 10),
        new(
            LegacyTheStorekeeperNpcId,
            [8],
            "NpcHandler.TheStorekeeper",
            "53328-53419",
            ["Have you spoken with the Pharmacist? She has all kinds of potions that will be helpful to you in your journey."],
            [new("Okay!", LegacyNpcClosedOption)],
            AvatarId: 10),
        new(
            LegacyArmorerNpcId,
            [2],
            "NpcHandler.Armorer",
            "53420-53530",
            ["I have armors and headgear for all classes. Different classes use different equipment. Right click the equipment in my shop to buy it!"],
            [new("Thank~you~for~the~knowledge.", 3)],
            AvatarId: 10),
        new(
            LegacyArmorerNpcId,
            [3],
            "NpcHandler.Armorer",
            "53420-53530",
            ["You are lucky, kid! I have just got something new for you!"],
            [new("Thank~you!.", 4)],
            AvatarId: 10),
        new(
            LegacyArmorerNpcId,
            [5, 6],
            "NpcHandler.Armorer",
            "53420-53530",
            ["You should know only one piece of armor is not enough to survive. Now pay a visit to the Storekeeper, will you?"],
            [new("Thanks~for~your~advice.", 7), new("See~you~later!", LegacyNpcClosedOption)],
            AvatarId: 10),
        new(
            LegacyArmorerNpcId,
            [10],
            "NpcHandler.Armorer",
            "53420-53530",
            ["The Storekeeper. She has many useful items. She is worth paying a visit to!"],
            [new("I~won't~forget~this~time.", LegacyNpcClosedOption)],
            AvatarId: 10),
        new(
            LegacyGarmentsDealerNpcId,
            [0],
            "NpcHandler.GarmentsDealer",
            "56297-56416",
            ["Hello there, I can give you a random garment to look cool.", "Want to proceed? Here are my options."],
            [new("1-3~star~[215~CPs].", 1), new("4-5~star~[2150~CPs].", 2), new("I~see.", LegacyNpcClosedOption)],
            AvatarId: null),
        new(
            LegacyMountsDealerNpcId,
            [0],
            "NpcHandler.MountsDealer",
            "56417-56536",
            ["Hello there, I can give you a random mount to look cool.", "Want to proceed? Here are my options."],
            [new("1-3~star~[215~CPs].", 1), new("4-5~star~[2150~CPs].", 2), new("I~see.", LegacyNpcClosedOption)],
            AvatarId: null),
        new(
            LegacyMarketShopNpcId,
            [0, 50],
            "NpcHandler.MarketShop",
            "56537-56817",
            ["Hello, I can provide cool items at discount prices.", "Do you want to continue? Here are the goods I sell."],
            [
                new("10,000~subclass~points~[50,000~CPs].", 111),
                new("10,000~inner~power~[50,000~CPs].", 121),
                new("Level~140~[free].", 11),
                new("Mystery~Fruit~[50,000~CPs].", 122),
                new("Life~Fruit~Basket~[500,000~CPs].", 120),
                new("Super~Thunder~Gem~[2,099~CPs].", 1),
                new("Super~Glory~Gem~[2,099~CPs].", 2),
                new("Super~Infinity~Gem~[2,499~CPs].", 9),
                new("Oblivion~Dew~[149~CPs].", 7),
                new("Class~promotion~points~1000~[500,000~CPs].", 30),
                new("Yellow~Rune~1000~[1,000,000~CPs].", 3),
                new("Blue~Rune~1000~[1,000,000~CPs].", 55),
                new("Tough~Drill~[2,499~CPs].", 4),
                new("Meteor~Scroll~[29~CPs].", 5),
                new("24-hour~VIP~6~[100,000~CPs].", 21),
                new("Chi~Points~100k~[5,000,000~CPs].", 8),
                new("999~HP~and~MP~[10,000~CPs].", 10),
                new("I~see.", LegacyNpcClosedOption),
            ],
            AvatarId: null),
        new(
            LegacyArchveShopNpcId,
            [0],
            "NpcHandler.ArchveSHop",
            "56819-56954",
            ["Hero, I sell excellent items at low prices. Would you like to buy something?", "Do you want items or CP exchanges? You may choose."],
            [
                new("1,000~Divine~Weapon~Spirits~[1,000,000~CPs].", 1),
                new("Ninja~awakening~skill~book.", 2),
                new("Ninja~awakening~points.", 3),
                new("Archer~awakening:~100~Saint~Mechanisms~[10,000,000~CPs].", 4),
                new("Warrior~awakening:~100~Holy~Soul~Stones~[10,000,000~CPs].", 5),
                new("Taoist~awakening:~1,000~Focus~Pebbles~[1,000,000~CPs].", 6),
                new("Pirate~awakening:~1,000~domination~points~[1,000,000~CPs].", 7),
                new("Pirate~awakening:~1,000~universal~fragments~[1,000,000~CPs].", 8),
                new("Next~time.", LegacyNpcClosedOption),
            ],
            AvatarId: null),
        new(
            LegacyAccessoriesDealerNpcId,
            [0],
            "NpcHandler.AccessoriesDealer",
            "57169-57236",
            ["Hello there, I can give you a random accessory to look cool.", "Want to proceed? Here are my options."],
            [new("Try~my~luck.~[1500~CPs].", 1), new("I~see.", LegacyNpcClosedOption)],
            AvatarId: null),
        new(
            LegacyBeauticianDealerNpcId,
            [0],
            "NpcHandler.BeauticianDealer",
            "57237-57357",
            ["Hello there, I can give you a random hairstyle or avatar to look cool.", "Want to proceed? Here are my options."],
            [new("Hairstyle.~[1000~CPs].", 1), new("Avatar.~[1000~CPs].", 2), new("I~see.", LegacyNpcClosedOption)],
            AvatarId: null),
        new(
            LegacySurgeonMiracleNpcId,
            [1],
            "NpcHandler.SurgeonMiracle",
            "38636-38680",
            [
                "If you pay me one DragonBall, I can change your size. You will become more attractive and start a fresh life.",
                "To avoid unexpected trouble, make sure you are not in any disguise form.",
            ],
            [new("Here~is~a~DragonBall.", 2), new("I~have~no~DragonBall.", LegacyNpcClosedOption)],
            AvatarId: 67),
        new(
            LegacyMasterXuanNpcId,
            [0],
            "NpcHandler.MasterXuan",
            "38681-38800",
            ["Hello! May I help you?"],
            [new("Yes!~Upgrade~my~sashes!", 2), new("How~to~upgrade~my~sashes?", 1), new("Nothing.", LegacyNpcClosedOption)],
            AvatarId: 7),
        new(
            LegacyMasterXuanNpcId,
            [1],
            "NpcHandler.MasterXuan",
            "38681-38800",
            ["You can pay 100 CPs to combine 2 Sashes (S) into a Sash (M), and pay 200 CPs to combine 2 Sashes (M) into a Sash (L)."],
            [new("I~see.", LegacyNpcClosedOption)],
            AvatarId: 7),
        new(
            LegacyMasterXuanNpcId,
            [2],
            "NpcHandler.MasterXuan",
            "38681-38800",
            [
                "First, take all items out of the sashes. Otherwise, they will disappear after the upgrade.",
                "You will need to pay 100 CPs to combine 2 Sashes (S) into a Sash (M), or 200 CPs to combine 2 Sashes (M) into a Sash (L). Are you ready?",
            ],
            [new("Yes.~I~want~a~Sash(M)(100CPs).", 3), new("Yes.~I~want~a~Sash(L)(200CPs).", 4), new("Not~yet.", LegacyNpcClosedOption)],
            AvatarId: 7),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTinterCapColorOptions =
    [
        new("Color~1", 3),
        new("Color~2", 4),
        new("Color~3", 5),
        new("Color~4", 6),
        new("Color~5", 7),
        new("Color~6", 8),
        new("Color~7", 9),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTinterCoatColorOptions =
    [
        new("Color~1", 13),
        new("Color~2", 14),
        new("Color~3", 15),
        new("Color~4", 16),
        new("Color~5", 17),
        new("Color~6", 18),
        new("Color~7", 19),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyMarketShopOptions =
    [
        new("10,000~subclass~points~[50,000~CPs].", 111),
        new("10,000~inner~power~[50,000~CPs].", 121),
        new("Level~140~[free].", 11),
        new("Mystery~Fruit~[50,000~CPs].", 122),
        new("Life~Fruit~Basket~[500,000~CPs].", 120),
        new("Super~Thunder~Gem~[2,099~CPs].", 1),
        new("Super~Glory~Gem~[2,099~CPs].", 2),
        new("Super~Infinity~Gem~[2,499~CPs].", 9),
        new("Oblivion~Dew~[149~CPs].", 7),
        new("Class~promotion~points~1000~[500,000~CPs].", 30),
        new("Yellow~Rune~1000~[1,000,000~CPs].", 3),
        new("Blue~Rune~1000~[1,000,000~CPs].", 55),
        new("Tough~Drill~[2,499~CPs].", 4),
        new("Meteor~Scroll~[29~CPs].", 5),
        new("24-hour~VIP~6~[100,000~CPs].", 21),
        new("Chi~Points~100k~[5,000,000~CPs].", 8),
        new("999~HP~and~MP~[10,000~CPs].", 10),
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyArchveShopOptions =
    [
        new("1,000~Divine~Weapon~Spirits~[1,000,000~CPs].", 1),
        new("Ninja~awakening~skill~book.", 2),
        new("Ninja~awakening~points.", 3),
        new("Archer~awakening:~100~Saint~Mechanisms~[10,000,000~CPs].", 4),
        new("Warrior~awakening:~100~Holy~Soul~Stones~[10,000,000~CPs].", 5),
        new("Taoist~awakening:~1,000~Focus~Pebbles~[1,000,000~CPs].", 6),
        new("Pirate~awakening:~1,000~domination~points~[1,000,000~CPs].", 7),
        new("Pirate~awakening:~1,000~universal~fragments~[1,000,000~CPs].", 8),
        new("Next~time.", LegacyNpcClosedOption),
    ];

    private static bool TryGetLegacyShopServiceDialog(
        uint npcId,
        byte optionId,
        out LegacyShopServiceDialog dialog)
    {
        dialog = default!;
        foreach (var candidate in LegacyShopServiceDialogs)
        {
            if (candidate.NpcId == npcId && ContainsOption(candidate.OptionIds, optionId))
            {
                dialog = candidate;
                return true;
            }
        }

        return false;
    }

    private sealed record LegacyShopServiceDialog(
        uint NpcId,
        byte[] OptionIds,
        string SourceMethod,
        string SourceLines,
        string[] Texts,
        LegacyNpcDialogOption[] Options,
        ushort? AvatarId);
}
