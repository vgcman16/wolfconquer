namespace WolfConquer.Core.Data;

public static class LegacySobNpcNameCatalog
{
    public static bool TryTranslateKnownName(string name, out string translated)
    {
        translated = name switch
        {
            "GM服务" => "GM Services",
            "GM活动" => "GM Event",
            "VIP地图" => "VIP Map",
            "一键完美装备" => "Perfect Gear Service",
            "不明白活动" => "Hourly War 04",
            "不清楚活动1" => "Hourly War 01",
            "不清楚的比赛" => "Triple Pole War",
            "不清楚的活动" => "Top Pole",
            "不清楚的活动2" => "Duplicate Pole",
            "九门提督" => "Guild Officer",
            "亲王PK赛" => "Emperor War",
            "似乎是职业PK" => "Class War",
            "内功专员" => "Inner Power Specialist",
            "冠军PK赛" => "Champion PK",
            "冰冻洞穴活动" => "Treasure Chest",
            "冰封战" => "Frozen War",
            "出售觉醒物品" => "Awakening Item Seller",
            "功夫大师" => "Kung Fu Master",
            "勇士觉醒" => "Trojan Awakening",
            "双龙城传送" => "Twin City Teleport",
            "双龙城传送员" => "Twin City Teleporter",
            "右城门" => "Right City Gate",
            "团队PK赛" => "Team PK Tournament",
            "在线积分兑换" => "Online Points Exchange",
            "多个任务还不清楚" => "Event Manager",
            "天气管理员" => "Weather Manager",
            "天石银行" => "CP Bank",
            "夫妻PK赛" => "Couples PK Tournament",
            "女神PK赛" => "Beauty PK Tournament",
            "就职地图传送员" => "Job Center Teleporter",
            "左城门" => "Left City Gate",
            "帮战积分赛" => "Clan War Score",
            "帮派战积分" => "Guild War Score",
            "弓箭觉醒" => "Archer Awakening",
            "忍者觉醒" => "Ninja Awakening",
            "战旗争霸赛" => "Flag War",
            "战神觉醒" => "War God Awakening",
            "木桩" => "Wooden Stake",
            "杂货商" => "Grocery Merchant",
            "杂货商人" => "Grocery Store Owner",
            "某种PK赛" => "War Of Players",
            "武林秘宝" => "Martial Treasure",
            "洗点" => "Attribute Reallocator",
            "活动不清楚" => "Hourly War 08",
            "活动传送员" => "Event Teleporter",
            "清洗红名" => "Red Name Cleaner",
            "爵位捐献" => "Nobility Donation",
            "特效专员" => "Effect Specialist",
            "男神PK赛" => "Hero PK Tournament",
            "男神PK赛1" => "Monthly War",
            "疯狂的战争" => "Crazy War",
            "精英PK赛" => "Elite PK Tournament",
            "精英帮战" => "Elite Guild War",
            "练功总管" => "Training Manager",
            "练气大师" => "Chi Master",
            "网站投票" => "Server Vote",
            "职业PK赛" => "Class Pole War",
            "联盟战争" => "Union War",
            "航海家觉醒" => "Pirate Awakening",
            "药剂师" => "Pharmacist",
            "觉醒地图" => "Awakening Map",
            "貌似是家长活动" => "Clan Repeat Event",
            "贵族波兰战争" => "Nobility War",
            "赛马比赛" => "Horse Race",
            "超级帮战" => "Super Guild War",
            "足球比赛" => "Soccer Match",
            "这NPC暂时不开" => "Unavailable NPC",
            "道士觉醒" => "Taoist Awakening",
            "附加状态" => "Status Effects",
            "驿站老板" => "Coachman",
            "骑术技能训练师" => "Riding Skill Trainer",
            "魔法稻人" => "Magic Scarecrow",
            "龙珠活动" => "Dragon Ball Event",
            _ => string.Empty,
        };

        return translated.Length != 0;
    }

    public static bool ContainsCjk(string value)
    {
        foreach (var character in value)
        {
            if (character is >= '\u3400' and <= '\u9fff')
            {
                return true;
            }
        }

        return false;
    }
}
