using System.Text.Json;
using System.Text.Json.Serialization;
using WolfConquer.Core.Data;

internal static partial class Program
{
    static string ResolveDbServerPath(string command, string[] args)
    {
        var index = command == "probe" && args.Length > 0 && !IsCommand(args[0])
            ? 0
            : 1;

        return args.Length > index
            ? Path.GetFullPath(args[index])
            : @"data/DBServer";
    }

    static string ResolveOutputPath(string[] args)
    {
        return args.Length > 2
            ? Path.GetFullPath(args[2])
            : Path.GetFullPath(Path.Combine(".setup_work", "seed-json"));
    }

    static int Probe(string dbServerPath)
    {
        Console.WriteLine($"DBServer: {dbServerPath}");

        var result = new ImportProbeResult();
        ProbeClientConfigs(Path.Combine(dbServerPath, "client_config.ini"), result);
        ProbeLines(Path.Combine(dbServerPath, "itemtype.txt"), "items", line => ItemTypeParser.ParseLine(line, 66), result);
        ProbeItemTypeSubs(Path.Combine(dbServerPath, "ItemtypeSub.txt"), result);
        ProbeItemAdd(Path.Combine(dbServerPath, "ItemAdd.ini"), result);
        ProbeLevelExperience(Path.Combine(dbServerPath, "levexp.txt"), result);
        ProbeClassLevelStats(Path.Combine(dbServerPath, "Stats.ini"), result);
        ProbeProfessionLevelUps(Path.Combine(dbServerPath, "prof_lev_up.txt"), result);
        ProbeProfessionLevelBenefits(Path.Combine(dbServerPath, "prof_lev_benefit.txt"), result);
        ProbeProfessionTitleBenefits(Path.Combine(dbServerPath, "prof_title_benefit.txt"), result);
        ProbeQuestInfo(Path.Combine(dbServerPath, "Questinfo.ini"), result);
        ProbeQuizQuestions(Path.Combine(dbServerPath, "quizquestins.ini"), result);
        ProbeLines(Path.Combine(dbServerPath, "magictype.txt"), "magic", line => MagicTypeParser.ParseLine(line, 54), result);
        ProbeMagicEffects(Path.Combine(dbServerPath, "MagicEffect.ini"), result);
        ProbeMagicTypeOperations(Path.Combine(dbServerPath, "magictypeop.txt"), result);
        ProbeTitleTypes(Path.Combine(dbServerPath, "title_type.txt"), result);
        ProbeCombatGear(Path.Combine(dbServerPath, "combat_gear.txt"), result);
        ProbeTraps(Path.Combine(dbServerPath, "Traps.txt"), result);
        ProbeLines(Path.Combine(dbServerPath, "Npcs.txt"), "npcs", line => NpcParser.ParseLine(line), result);
        ProbeSobNpcs(Path.Combine(dbServerPath, "SobNpcs.txt"), result);
        ProbePortals(Path.Combine(dbServerPath, "portals.ini"), result);
        ProbeMaps(Path.Combine(dbServerPath, "maps"), result);
        ProbeMapNames(Path.Combine(dbServerPath, "GameMapEx.ini"), result);
        ProbeLegacyGameMaps(Path.Combine(dbServerPath, "ini", "GameMap.dat"), result);
        var monsterTypeIdsByName = ProbeMonsters(Path.Combine(dbServerPath, "Monsters"), result);
        ProbeMonsterSpawns(Path.Combine(dbServerPath, "MonsterSpawns"), monsterTypeIdsByName, result);
        ProbeLotteryDictionary(Path.Combine(dbServerPath, "dict_lottery.txt"), result);
        ProbeCollectionRewardTiers(Path.Combine(dbServerPath, "dict_collection.txt"), result);
        ProbeTaskRewardTypes(Path.Combine(dbServerPath, "task_reward_type.txt"), result);
        ProbeTaskRewardTypeIniEntries(Path.Combine(dbServerPath, "task_reward_type.ini"), result);
        ProbeNeiGong(Path.Combine(dbServerPath, "NeiGongInfo.ini"), result);
        ProbeSubProfessions(Path.Combine(dbServerPath, "SubProfessionInfo.ini"), result);
        ProbeSyndicateLevels(Path.Combine(dbServerPath, "syndicate_level.txt"), result);
        ProbeRuneLevelExperience(Path.Combine(dbServerPath, "rune_levexp.txt"), result);
        ProbeRuneStorageAttributes(Path.Combine(dbServerPath, "rune_storage_attr.txt"), result);
        ProbeItemRefineEffects(Path.Combine(dbServerPath, "item_refine_effect.txt"), result);
        ProbeItemRefineEffectEx(Path.Combine(dbServerPath, "item_refine_effect_ex.txt"), result);
        ProbeItemRefineUpgrades(Path.Combine(dbServerPath, "item_refine_upgrade.txt"), result);
        ProbeTransformInfo(Path.Combine(dbServerPath, "TransformInfo.txt"), result);
        ProbeFurniture(Path.Combine(dbServerPath, "furnitures.txt"), result);
        ProbeHairColorTypes(Path.Combine(dbServerPath, "hair_color_type.txt"), result);
        ProbeHairFaceStorageTypes(Path.Combine(dbServerPath, "hairface_storage_type.txt"), result);
        ProbeCoatStorageTypes(Path.Combine(dbServerPath, "coat_storage_type.txt"), result);
        ProbeCoatStorageAttributes(Path.Combine(dbServerPath, "coat_storage_attr.txt"), result);
        ProbeCoatColorRules(Path.Combine(dbServerPath, "coat_color_rule.txt"), result);
        ProbeXuanbaoAdditionAttributes(Path.Combine(dbServerPath, "xuanbao_addition_attr.txt"), result);
        ProbeXuanbaoComposeAttributeLimits(Path.Combine(dbServerPath, "xuanbao_compose_attr_limit.txt"), result);
        ProbeXuanbaoRandomAttributes(Path.Combine(dbServerPath, "xuanbao_rand_attr.txt"), result);
        ProbeChristmasNpcs(Path.Combine(dbServerPath, "christmasnpcs.txt"), result);
        ProbeTutorTypes(Path.Combine(dbServerPath, "cq_tutor_type.txt"), result);
        ProbeTutorBattleLimitTypes(Path.Combine(dbServerPath, "cq_tutor_battle_limit_type.txt"), result);
        ProbeBeastAttributes(Path.Combine(dbServerPath, "beasts_attr.txt"), result);
        ProbeFateExperience(Path.Combine(dbServerPath, "fate_exp.txt"), result);
        ProbeSpiritRates(Path.Combine(dbServerPath, "spirit_rate.txt"), result);
        ProbeHundredWeapons(Path.Combine(dbServerPath, "hundred_weapon.txt"), result);
        ProbeGouyuTypes(Path.Combine(dbServerPath, "gouyu_type.txt"), result);
        ProbeGouyuImmortals(Path.Combine(dbServerPath, "gouyu_immortal.txt"), result);
        ProbeSpecialEffectAttributes(Path.Combine(dbServerPath, "speffect_attribute.txt"), result);
        ProbeDaoqiDictTypes(Path.Combine(dbServerPath, "daoqi_dict_type.txt"), result);
        ProbeSyndicateFormationTypes(Path.Combine(dbServerPath, "syn_formtype.txt"), result);
        ProbeSyndicateSkillTypes(Path.Combine(dbServerPath, "syn_skill_type.txt"), result);
        ProbeDisdainRules(Path.Combine(dbServerPath, "cq_disdain.txt"), result);
        ProbeJiangHuAttributes(Path.Combine(dbServerPath, "JianghuAttributes.txt"), result);
        ProbeJiangHuCultivateStatuses(Path.Combine(dbServerPath, "JingHuCultivateStatus.txt"), result);
        ProbeRefineryBoxes(Path.Combine(dbServerPath, "databaserefineryboxes.txt"), result);
        ProbeRefinerySouls(dbServerPath, result);
        ProbeRefineryMaterials(Path.Combine(dbServerPath, "Rifinery.txt"), result);
        ProbeStateCountFiles(dbServerPath, result);
        ProbeBinarySeedFiles(dbServerPath, result);
        ProbeEmptySeedFiles(dbServerPath, result);
        ProbeBackupSeedFiles(dbServerPath, result);
        ProbePokerTables(Path.Combine(dbServerPath, "PokerTables.txt"), result);
        ProbeRouletteTables(Path.Combine(dbServerPath, "Roulettes.txt"), result);
        ProbeNpcTimeEntries(Path.Combine(dbServerPath, "NpcTime.txt"), result);
        ProbeActivityTasks(Path.Combine(dbServerPath, "ActivityTask.ini"), result);
        ProbeMeltingTypeRules(dbServerPath, result);
        ProbeLotteryPrizes(Path.Combine(dbServerPath, "lottery.ini"), result);
        ProbeArenaRecords(Path.Combine(dbServerPath, "Arena.ini"), result);
        ProbeTeamArenaRecords(Path.Combine(dbServerPath, "TeamArena.ini"), result);
        ProbeElitePkRecords(Path.Combine(dbServerPath, "ElitePk.ini"), result);
        ProbeTeamElitePkRecords(Path.Combine(dbServerPath, "TeamElitePK.ini"), result);
        ProbeSkillTeamPkRecords(Path.Combine(dbServerPath, "SkillTeamPK.ini"), result);
        ProbeClassPkWarRecords(Path.Combine(dbServerPath, "ClassPkWar.ini"), result);
        ProbeCombatGearRanks(Path.Combine(dbServerPath, "CombatGearRank.txt"), result);
        ProbePrestigeRanks(Path.Combine(dbServerPath, "PrestigeRanking.txt"), result);
        ProbeInnerPowerRanks(Path.Combine(dbServerPath, "InnerPower.txt"), result);
        ProbeRuneRankings(Path.Combine(dbServerPath, "RuneRanking.txt"), result);
        ProbeStaticStatues(Path.Combine(dbServerPath, "StaticStatue.txt"), result);
        ProbeNewPlayerRewards(Path.Combine(dbServerPath, "NewPlayerRewords.txt"), result);
        ProbeVotes(Path.Combine(dbServerPath, "Votes.txt"), result);
        ProbeCouplesPkRecords(Path.Combine(dbServerPath, "CouplesPK.ini"), result);
        ProbeSocialAssociations(Path.Combine(dbServerPath, "Associate.txt"), result);
        ProbeGuildWarInfo(Path.Combine(dbServerPath, "GuildWarInfo.ini"), result);
        ProbeSuperGuildWarInfo(Path.Combine(dbServerPath, "SuperGuildWarInfo.ini"), result);
        ProbeStageGoal(Path.Combine(dbServerPath, "StageGoal.txt"), result);
        ProbeShops(dbServerPath, result);

        Console.WriteLine();
        Console.WriteLine($"Completed with {result.Failures} failure(s).");
        return result.Failures == 0 ? 0 : 2;
    }

    static IReadOnlyList<T> ReadLines<T>(string path, Func<string, T> parse)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var values = new List<T>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                values.Add(parse(line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return values;
    }

    static IReadOnlyList<Npc> ReadNpcs(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var values = new List<Npc>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                values.Add(NpcParser.ParseLine(line, lineNumber));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return values;
    }

    static void ProbeLines<T>(string path, string label, Func<string, T> parse, ImportProbeResult result)
    {
        if (!File.Exists(path))
        {
            result.RecordFailure($"{label}: missing file {path}");
            return;
        }

        try
        {
            var count = ReadLines(path, parse).Count;
            Console.WriteLine($"{label}: {count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"{label}: {ex.Message}");
        }
    }

    static void ProbeItemTypeSubs(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"itemTypeSubs: {ReadItemTypeSubs(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"itemTypeSubs: {ex.Message}");
        }
    }

    static IReadOnlyList<ItemTypeSub> ReadItemTypeSubs(string path)
    {
        return ItemTypeSubParser.ParseFile(path);
    }

    static void ProbePortals(string path, ImportProbeResult result)
    {
        if (!File.Exists(path))
        {
            result.RecordFailure($"portals: missing file {path}");
            return;
        }

        try
        {
            var portals = PortalParser.Parse(IniDocument.Parse(File.ReadAllText(path)));
            Console.WriteLine($"portals: {portals.Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"portals: {ex.Message}");
        }
    }

    static void ProbeItemAdd(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"itemAddBonuses: {ReadItemAddBonuses(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"itemAddBonuses: {ex.Message}");
        }
    }

    static void ProbeMagicEffects(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"magicEffects: {MagicEffectParser.ParseFile(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"magicEffects: {ex.Message}");
        }
    }

    static void ProbeMagicTypeOperations(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"magicTypeOperations: {MagicTypeOperationParser.ParseFile(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"magicTypeOperations: {ex.Message}");
        }
    }

    static void ProbeTitleTypes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"titleTypes: {ReadTitleTypes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"titleTypes: {ex.Message}");
        }
    }

    static void ProbeCombatGear(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"combatGear: {ReadCombatGear(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"combatGear: {ex.Message}");
        }
    }

    static void ProbeTraps(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"traps: {ReadTraps(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"traps: {ex.Message}");
        }
    }

    static void ProbeSobNpcs(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"sobNpcs: {ReadSobNpcs(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"sobNpcs: {ex.Message}");
        }
    }

    static IReadOnlyList<SobNpc> ReadSobNpcs(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var sobNpcs = new List<SobNpc>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                sobNpcs.Add(SobNpcParser.ParseLine(line, lineNumber));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return sobNpcs;
    }

    static void ProbeLevelExperience(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"levelExperience: {ReadLevelExperience(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"levelExperience: {ex.Message}");
        }
    }

    static IReadOnlyList<LevelExperience> ReadLevelExperience(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<LevelExperience>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                rows.Add(LevelExperienceParser.ParseLine(line, lineNumber));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static IReadOnlyList<ItemAddBonus> ReadItemAddBonuses(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var bonuses = new List<ItemAddBonus>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                bonuses.Add(ItemAddParser.ParseLine(line, lineNumber));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return bonuses;
    }

    static void ProbeNeiGong(string path, ImportProbeResult result)
    {
        try
        {
            var neiGong = ReadNeiGong(path);
            Console.WriteLine($"neiGongSecrets: {neiGong.Secrets.Count:N0}");
            Console.WriteLine($"neiGongTypes: {neiGong.Types.Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"neiGong: {ex.Message}");
        }
    }

    static NeiGongCatalog ReadNeiGong(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        try
        {
            return NeiGongParser.ParseFile(path);
        }
        catch (Exception ex)
        {
            throw new FormatException($"{Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    static void ProbeSubProfessions(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"subProfessions: {ReadSubProfessions(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"subProfessions: {ex.Message}");
        }
    }

    static IReadOnlyList<SubProfessionEntry> ReadSubProfessions(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        try
        {
            return SubProfessionParser.ParseFile(path).Entries;
        }
        catch (Exception ex)
        {
            throw new FormatException($"{Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    static void ProbeItemRefineEffects(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"itemRefineEffects: {ReadItemRefineEffects(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"itemRefineEffects: {ex.Message}");
        }
    }

    static IReadOnlyList<ItemRefineEffect> ReadItemRefineEffects(string path)
    {
        return ReadRequiredRows(
            path,
            (sourceFile, lineNumber, line) => ItemRefineParser.ParseEffectRow(sourceFile, lineNumber, line));
    }

    static void ProbeItemRefineEffectEx(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"itemRefineEffectEx: {ReadItemRefineEffectEx(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"itemRefineEffectEx: {ex.Message}");
        }
    }

    static IReadOnlyList<ItemRefineEffectEx> ReadItemRefineEffectEx(string path)
    {
        return ReadRequiredRows(
            path,
            (sourceFile, lineNumber, line) => ItemRefineParser.ParseEffectExRow(sourceFile, lineNumber, line));
    }

    static void ProbeItemRefineUpgrades(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"itemRefineUpgrades: {ReadItemRefineUpgrades(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"itemRefineUpgrades: {ex.Message}");
        }
    }

    static IReadOnlyList<ItemRefineUpgrade> ReadItemRefineUpgrades(string path)
    {
        return ReadRequiredRows(
            path,
            (sourceFile, lineNumber, line) => ItemRefineParser.ParseUpgradeRow(sourceFile, lineNumber, line));
    }

    static void ProbeTransformInfo(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"transformInfo: {ReadTransformInfo(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"transformInfo: {ex.Message}");
        }
    }

    static IReadOnlyList<TransformInfo> ReadTransformInfo(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<TransformInfo>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                rows.Add(TransformInfoParser.ParseLine(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static void ProbeChristmasNpcs(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"christmasNpcs: {ReadChristmasNpcs(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"christmasNpcs: {ex.Message}");
        }
    }

    static IReadOnlyList<ChristmasNpc> ReadChristmasNpcs(string path)
    {
        return ReadRequiredRows(path, ChristmasNpcParser.ParseRow);
    }

    static void ProbeBeastAttributes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"beastAttributes: {ReadBeastAttributes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"beastAttributes: {ex.Message}");
        }
    }

    static IReadOnlyList<BeastAttribute> ReadBeastAttributes(string path)
    {
        return ReadRequiredRows(path, BeastParser.ParseRow);
    }

    static void ProbeFateExperience(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"fateExperience: {ReadFateExperience(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"fateExperience: {ex.Message}");
        }
    }

    static IReadOnlyList<FateExperience> ReadFateExperience(string path)
    {
        return ReadRequiredRows(path, FateParser.ParseRow);
    }

    static void ProbeSpiritRates(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"spiritRates: {ReadSpiritRates(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"spiritRates: {ex.Message}");
        }
    }

    static IReadOnlyList<SpiritRate> ReadSpiritRates(string path)
    {
        return ReadRequiredRows(path, SpiritRateParser.ParseRow);
    }

    static void ProbeHundredWeapons(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"hundredWeapons: {ReadHundredWeapons(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"hundredWeapons: {ex.Message}");
        }
    }

    static IReadOnlyList<HundredWeapon> ReadHundredWeapons(string path)
    {
        return ReadRequiredRows(path, HundredWeaponParser.ParseRow);
    }

    static void ProbeGouyuTypes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"gouyuTypes: {ReadGouyuTypes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"gouyuTypes: {ex.Message}");
        }
    }

    static IReadOnlyList<GouyuType> ReadGouyuTypes(string path)
    {
        return ReadRequiredRows(path, GouyuTypeParser.ParseRow);
    }

    static void ProbeGouyuImmortals(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"gouyuImmortals: {ReadGouyuImmortals(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"gouyuImmortals: {ex.Message}");
        }
    }

    static IReadOnlyList<GouyuImmortal> ReadGouyuImmortals(string path)
    {
        return ReadRequiredRows(path, GouyuImmortalParser.ParseRow);
    }

    static void ProbeSpecialEffectAttributes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"specialEffectAttributes: {ReadSpecialEffectAttributes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"specialEffectAttributes: {ex.Message}");
        }
    }

    static IReadOnlyList<SpecialEffectAttribute> ReadSpecialEffectAttributes(string path)
    {
        return ReadRequiredRows(path, SpecialEffectAttributeParser.ParseRow);
    }

    static void ProbeDaoqiDictTypes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"daoqiDictTypes: {ReadDaoqiDictTypes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"daoqiDictTypes: {ex.Message}");
        }
    }

    static IReadOnlyList<DaoqiDictType> ReadDaoqiDictTypes(string path)
    {
        return ReadRequiredRows(path, DaoqiDictTypeParser.ParseRow);
    }

    static void ProbeDisdainRules(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"disdainRules: {ReadDisdainRules(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"disdainRules: {ex.Message}");
        }
    }

    static IReadOnlyList<DisdainRule> ReadDisdainRules(string path)
    {
        return ReadRequiredRows(path, DisdainRuleParser.ParseRow);
    }

    static void ProbeMeltingTypeRules(string dbServerPath, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"meltingTypeRules: {ReadMeltingTypeRules(dbServerPath).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"meltingTypeRules: {ex.Message}");
        }
    }

    static IReadOnlyList<MeltingTypeRule> ReadMeltingTypeRules(string dbServerPath)
    {
        var fileNames = new[]
        {
        "MeltingType.ini",
        "MeltingType_0.ini",
        "MeltingTYpe_1.ini",
        "MeltingTYpe_2.ini",
        "MeltingTYpe_3.ini",
    };

        var rules = new List<MeltingTypeRule>();
        foreach (var fileName in fileNames)
        {
            rules.AddRange(MeltingTypeParser.ParseFile(Path.Combine(dbServerPath, fileName)));
        }

        return rules;
    }

    static void ProbeLotteryPrizes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"lotteryPrizes: {ReadLotteryPrizes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"lotteryPrizes: {ex.Message}");
        }
    }

    static IReadOnlyList<LotteryPrize> ReadLotteryPrizes(string path)
    {
        return LotteryPrizeParser.ParseFile(path);
    }

    static IReadOnlyList<TitleType> ReadTitleTypes(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<TitleType>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                rows.Add(TitleTypeParser.ParseRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static IReadOnlyList<CombatGear> ReadCombatGear(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<CombatGear>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                rows.Add(CombatGearParser.ParseRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static IReadOnlyList<Trap> ReadTraps(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<Trap>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                rows.Add(TrapParser.ParseRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static IReadOnlyList<T> ReadRequiredRows<T>(
        string path,
        Func<string, int, string, T> parse,
        Func<string, bool>? shouldSkip = null)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<T>();
        var sourceFile = Path.GetFileName(path);
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (shouldSkip is not null && shouldSkip(line))
            {
                continue;
            }

            try
            {
                rows.Add(parse(sourceFile, lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{sourceFile} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static int? TryGetInt(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    static void WriteJson<T>(string path, T value)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        File.WriteAllText(path, JsonSerializer.Serialize(value, options));
    }
}
