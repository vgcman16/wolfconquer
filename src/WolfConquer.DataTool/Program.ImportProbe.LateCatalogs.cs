using WolfConquer.Core.Client;
using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbeShops(string dbServerPath, ImportProbeResult result)
    {
        try
        {
            var shopData = ReadShops(dbServerPath);
            Console.WriteLine($"shops: {shopData.Shops.Count:N0}");
            Console.WriteLine($"shopItems: {shopData.Items.Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"shops: {ex.Message}");
        }

        ProbeExchangeRows(Path.Combine(dbServerPath, "exchange_shop_goods.txt"), "exchangeShopGoods", result);
        ProbeExchangeRows(Path.Combine(dbServerPath, "exchange_shop_goods_ex.txt"), "exchangeShopGoodsEx", result);
        ProbeExchangeRows(Path.Combine(dbServerPath, "exchange_shop_lev.txt"), "exchangeShopLevels", result);
        ProbeHonorShop(dbServerPath, result);
        ProbePointShop(dbServerPath, "RacePointShop.ini", "racePointShop", result);
        ProbePointShop(dbServerPath, "GoldenLeagueShop.ini", "goldenLeagueShop", result);
        ProbeBooths(dbServerPath, result);
    }

    static ShopData ReadShops(string dbServerPath)
    {
        var shopsPath = Path.Combine(dbServerPath, "shops");
        if (!Directory.Exists(shopsPath))
        {
            throw new DirectoryNotFoundException($"Missing shops directory: {shopsPath}");
        }

        var catalogs = new[]
        {
        ShopParser.ParseShopDat(File.ReadAllText(Path.Combine(shopsPath, "Shop.dat")), "Shop.dat"),
        ShopParser.ParseLegacyEmoneyShop(File.ReadAllText(Path.Combine(shopsPath, "emoneyshop.ini")), "emoneyshop.ini"),
        ShopParser.ParseEmoneyShopV2(File.ReadAllText(Path.Combine(shopsPath, "emoneyShopV2.ini")), "emoneyShopV2.ini"),
    };

        return new ShopData(
            catalogs.SelectMany(catalog => catalog.Shops).ToArray(),
            catalogs.SelectMany(catalog => catalog.Items).ToArray());
    }

    static void ProbeHonorShop(string dbServerPath, ImportProbeResult result)
    {
        try
        {
            var honorShop = ReadHonorShop(dbServerPath);
            Console.WriteLine($"honorShopItems: {honorShop.Items.Count:N0}");
            Console.WriteLine($"honorShopRecommendations: {honorShop.Recommendations.Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"honorShop: {ex.Message}");
        }
    }

    static HonorShopCatalog ReadHonorShop(string dbServerPath)
    {
        var path = Path.Combine(dbServerPath, "shops", "HonorShop.ini");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ShopParser.ParseHonorShop(File.ReadAllText(path), Path.GetFileName(path));
    }

    static void ProbePointShop(string dbServerPath, string fileName, string label, ImportProbeResult result)
    {
        try
        {
            var pointShop = ReadPointShop(dbServerPath, fileName);
            Console.WriteLine($"{label}Items: {pointShop.Items.Count:N0}");
            Console.WriteLine($"{label}Recommendations: {pointShop.Recommendations.Count:N0}");
            if (pointShop.Config.Count > 0)
            {
                Console.WriteLine($"{label}Config: {pointShop.Config.Count:N0}");
            }
        }
        catch (Exception ex)
        {
            result.RecordFailure($"{label}: {ex.Message}");
        }
    }

    static PointShopCatalog ReadPointShop(string dbServerPath, string fileName)
    {
        var path = Path.Combine(dbServerPath, "shops", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ShopParser.ParsePointShop(File.ReadAllText(path), Path.GetFileName(path));
    }

    static void ProbeBooths(string dbServerPath, ImportProbeResult result)
    {
        try
        {
            var booths = ReadBooths(dbServerPath);
            Console.WriteLine($"booths: {booths.Booths.Count:N0}");
            Console.WriteLine($"boothItems: {booths.Items.Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"booths: {ex.Message}");
        }
    }

    static BoothCatalog ReadBooths(string dbServerPath)
    {
        var path = Path.Combine(dbServerPath, "shops", "Booths.txt");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ShopParser.ParseBooths(File.ReadAllText(path), Path.GetFileName(path));
    }

    static void ProbeExchangeRows(string path, string label, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"{label}: {ReadExchangeRows(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"{label}: {ex.Message}");
        }
    }

    static void ProbeArenaRecords(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"arenaRecords: {ReadArenaRecords(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"arenaRecords: {ex.Message}");
        }
    }

    static IReadOnlyList<ArenaRecord> ReadArenaRecords(string path)
    {
        return PvpEventParser.ParseArenaFile(path);
    }

    static void ProbeTeamArenaRecords(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"teamArenaRecords: {ReadTeamArenaRecords(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"teamArenaRecords: {ex.Message}");
        }
    }

    static IReadOnlyList<TeamArenaRecord> ReadTeamArenaRecords(string path)
    {
        return PvpEventParser.ParseTeamArenaFile(path);
    }

    static void ProbeElitePkRecords(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"elitePkRecords: {ReadElitePkRecords(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"elitePkRecords: {ex.Message}");
        }
    }

    static IReadOnlyList<PkTournamentRecord> ReadElitePkRecords(string path)
    {
        return PvpEventParser.ParsePkTournamentFile(path);
    }

    static void ProbeTeamElitePkRecords(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"teamElitePkRecords: {ReadTeamElitePkRecords(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"teamElitePkRecords: {ex.Message}");
        }
    }

    static IReadOnlyList<PkTournamentRecord> ReadTeamElitePkRecords(string path)
    {
        return PvpEventParser.ParsePkTournamentFile(path);
    }

    static void ProbeSkillTeamPkRecords(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"skillTeamPkRecords: {ReadSkillTeamPkRecords(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"skillTeamPkRecords: {ex.Message}");
        }
    }

    static IReadOnlyList<PkTournamentRecord> ReadSkillTeamPkRecords(string path)
    {
        return PvpEventParser.ParsePkTournamentFile(path);
    }

    static void ProbeClassPkWarRecords(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"classPkWarRecords: {ReadClassPkWarRecords(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"classPkWarRecords: {ex.Message}");
        }
    }

    static IReadOnlyList<ClassPkWarRecord> ReadClassPkWarRecords(string path)
    {
        return PvpEventParser.ParseClassPkWarFile(path);
    }

    static void ProbeCombatGearRanks(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"combatGearRanks: {ReadCombatGearRanks(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"combatGearRanks: {ex.Message}");
        }
    }

    static IReadOnlyList<CombatGearRankEntry> ReadCombatGearRanks(string path)
    {
        return RankingSnapshotParser.ParseCombatGearRanksFile(path);
    }

    static void ProbePrestigeRanks(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"prestigeRanks: {ReadPrestigeRanks(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"prestigeRanks: {ex.Message}");
        }
    }

    static IReadOnlyList<PrestigeRankEntry> ReadPrestigeRanks(string path)
    {
        return RankingSnapshotParser.ParsePrestigeRanksFile(path);
    }

    static void ProbeInnerPowerRanks(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"innerPowerRanks: {ReadInnerPowerRanks(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"innerPowerRanks: {ex.Message}");
        }
    }

    static IReadOnlyList<InnerPowerRankEntry> ReadInnerPowerRanks(string path)
    {
        return RankingSnapshotParser.ParseInnerPowerRanksFile(path);
    }

    static void ProbeRuneRankings(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"runeRankings: {ReadRuneRankings(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"runeRankings: {ex.Message}");
        }
    }

    static IReadOnlyList<RuneRankingRecord> ReadRuneRankings(string path)
    {
        return RuneRankingParser.ParseFile(path);
    }

    static void ProbeStaticStatues(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"staticStatues: {ReadStaticStatues(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"staticStatues: {ex.Message}");
        }
    }

    static IReadOnlyList<StaticStatueRecord> ReadStaticStatues(string path)
    {
        return StaticStatueParser.ParseFile(path);
    }

    static void ProbeNewPlayerRewards(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"newPlayerRewards: {ReadNewPlayerRewards(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"newPlayerRewards: {ex.Message}");
        }
    }

    static IReadOnlyList<NewPlayerRewardRecord> ReadNewPlayerRewards(string path)
    {
        return RankingSnapshotParser.ParseNewPlayerRewardsFile(path);
    }

    static void ProbeVotes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"votes: {ReadVotes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"votes: {ex.Message}");
        }
    }

    static IReadOnlyList<VoteRecord> ReadVotes(string path)
    {
        return RankingSnapshotParser.ParseVotesFile(path);
    }

    static void ProbeCouplesPkRecords(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"couplesPkRecords: {ReadCouplesPkRecords(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"couplesPkRecords: {ex.Message}");
        }
    }

    static IReadOnlyList<CouplesPkRecord> ReadCouplesPkRecords(string path)
    {
        return RankingSnapshotParser.ParseCouplesPkFile(path);
    }

    static void ProbeSocialAssociations(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"socialAssociations: {ReadSocialAssociations(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"socialAssociations: {ex.Message}");
        }
    }

    static IReadOnlyList<SocialAssociationRecord> ReadSocialAssociations(string path)
    {
        return SocialAssociationParser.ParseFile(path);
    }

    static void ProbeGuildWarInfo(string path, ImportProbeResult result)
    {
        try
        {
            var guildWarInfo = ReadGuildWarInfo(path);
            Console.WriteLine($"guildWarInfoEntries: {guildWarInfo.Entries.Count:N0}");
            Console.WriteLine($"guildWarInfoConductors: {guildWarInfo.Conductors.Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"guildWarInfo: {ex.Message}");
        }
    }

    static GuildWarData ReadGuildWarInfo(string path)
    {
        return GuildWarParser.ParseFile(path);
    }

    static void ProbeSuperGuildWarInfo(string path, ImportProbeResult result)
    {
        try
        {
            var guildWarInfo = ReadSuperGuildWarInfo(path);
            Console.WriteLine($"superGuildWarInfoEntries: {guildWarInfo.Entries.Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"superGuildWarInfo: {ex.Message}");
        }
    }

    static GuildWarData ReadSuperGuildWarInfo(string path)
    {
        return GuildWarParser.ParseFile(path);
    }

    static void ProbeStageGoal(string path, ImportProbeResult result)
    {
        try
        {
            var catalog = ReadStageGoal(path);
            Console.WriteLine($"stageGoalStages: {catalog.Stages.Count:N0}");
            Console.WriteLine($"stageGoalTasks: {catalog.Tasks.Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"stageGoal: {ex.Message}");
        }
    }

    static StageGoalCatalog ReadStageGoal(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        return StageGoalParser.Parse(encoding.GetString(File.ReadAllBytes(path)));
    }

    static IReadOnlyList<ExchangeShopRow> ReadExchangeRows(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<ExchangeShopRow>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            rows.Add(ShopParser.ParseExchangeRow(Path.GetFileName(path), lineNumber, line));
        }

        return rows;
    }

    static void ProbeClientConfigs(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"clientConfigs: {ReadClientConfigs(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"clientConfigs: {ex.Message}");
        }
    }

    static IReadOnlyList<ClientConfigRecord> ReadClientConfigs(string path)
    {
        return ClientConfigParser.ParseFile(path);
    }
}
