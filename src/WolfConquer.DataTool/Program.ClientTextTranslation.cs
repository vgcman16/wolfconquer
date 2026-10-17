using System.Text;
using WolfConquer.Core.Client;

internal static partial class Program
{
    private const string DefaultLegacyMagicEffectPath = "data/DBServer/MagicEffect.ini";
    private const string DefaultLegacyQuestInfoPath = "data/DBServer/Questinfo.ini";
    private const string DefaultLegacyGameMapExPath = "data/DBServer/GameMapEx.ini";

    static int PatchClientMagicEffect(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var clientMagicEffectPath = Path.Combine(clientPath, "ini", "MagicEffect.ini");
        var referencePath = ResolveLegacyReferencePath(args, DefaultLegacyMagicEffectPath, "MagicEffect.ini");

        if (!ValidateClientReferenceFile(clientPath, clientMagicEffectPath, referencePath, "MagicEffect"))
        {
            return 1;
        }

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var originalText = encoding.GetString(File.ReadAllBytes(clientMagicEffectPath));
        var referenceText = encoding.GetString(File.ReadAllBytes(referencePath));
        var result = LegacyMagicEffectTranslator.Translate(originalText, referenceText, out var updatedText);
        if (result.Changed)
        {
            File.WriteAllBytes(clientMagicEffectPath, encoding.GetBytes(updatedText));
        }

        Console.WriteLine($"Client MagicEffect: {clientMagicEffectPath}");
        Console.WriteLine($"Reference MagicEffect: {referencePath}");
        Console.WriteLine($"Sections scanned: {result.SectionsScanned}");
        Console.WriteLine($"Matched sections: {result.MatchedSections}");
        Console.WriteLine($"Unmatched sections: {result.UnmatchedSections}");
        Console.WriteLine($"Translated rows: {result.TranslatedRows}");
        Console.WriteLine($"Changed rows: {result.ChangedRows}");
        Console.WriteLine($"Unmatched rows: {result.UnmatchedRows}");
        Console.WriteLine($"Changed file: {result.Changed}");
        return result.TranslatedRows > 0 ? 0 : 2;
    }

    static int PatchClientMagicEffectSourceBacked(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var clientMagicEffectPath = Path.Combine(clientPath, "ini", "MagicEffect.ini");
        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        if (!File.Exists(clientMagicEffectPath))
        {
            Console.Error.WriteLine($"Client MagicEffect file not found: {clientMagicEffectPath}");
            return 1;
        }

        var sourcePaths = ResolveGouYuSkillTextSourcePaths(args, clientPath);
        if (sourcePaths.MagicEffectPaths.Count == 0 && sourcePaths.MagicTypePaths.Count == 0)
        {
            Console.Error.WriteLine("No source-backed MagicEffect text source files were found.");
            return 1;
        }

        var textSources = sourcePaths.MagicEffectPaths
            .SelectMany(path => SourceBackedMagicEffectTextTranslator.ReadMagicEffectTextSources(path))
            .ToArray();
        var skillNameSources = SourceBackedMagicEffectTextTranslator.ReadMagicTypeSkillNameSources(sourcePaths.MagicTypePaths);
        var result = SourceBackedMagicEffectTextTranslator.PatchFile(
            clientMagicEffectPath,
            textSources,
            skillNameSources);

        Console.WriteLine($"Client source-backed MagicEffect: {clientMagicEffectPath}");
        Console.WriteLine($"MagicEffect text sources: {string.Join("; ", sourcePaths.MagicEffectPaths)}");
        Console.WriteLine($"magictype sources: {string.Join("; ", sourcePaths.MagicTypePaths)}");
        Console.WriteLine($"Source rows: {result.SourceRows}");
        Console.WriteLine($"Source keys: {result.SourceKeys}");
        Console.WriteLine($"Conflicting source keys: {result.ConflictingSourceKeys.Count}");
        Console.WriteLine($"Skill name source rows: {result.SkillNameSourceRows}");
        Console.WriteLine($"Skill name source IDs: {result.SkillNameSourceIds}");
        Console.WriteLine($"Conflicting skill name IDs: {result.ConflictingSkillNameIds.Count}");
        Console.WriteLine($"Target rows: {result.TargetRows}");
        Console.WriteLine($"Rows needing translation: {result.RowsNeedingTranslation}");
        Console.WriteLine($"Same-key matched rows: {result.SameKeyMatchedRows}");
        Console.WriteLine($"Skill-name fallback rows: {result.SkillNameFallbackRows}");
        Console.WriteLine($"Placeholder mismatch rows: {result.PlaceholderMismatchRows}");
        Console.WriteLine($"Conflict rows: {result.ConflictRows}");
        Console.WriteLine($"Unmatched rows: {result.UnmatchedRows}");
        Console.WriteLine($"Already translated rows: {result.AlreadyTranslatedRows}");
        Console.WriteLine($"Changed rows: {result.ChangedRows}");
        Console.WriteLine($"Remaining CJK rows: {result.RemainingCjkRows}");
        Console.WriteLine($"Changed file: {result.Changed}");
        return result.TargetRows > 0 ? 0 : 2;
    }

    static int PatchClientQuestInfo(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var clientQuestInfoPath = Path.Combine(clientPath, "ini", "Questinfo.ini");
        var referencePath = ResolveLegacyReferencePath(args, DefaultLegacyQuestInfoPath, "Questinfo.ini");

        if (!ValidateClientReferenceFile(clientPath, clientQuestInfoPath, referencePath, "Questinfo"))
        {
            return 1;
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var originalText = encoding.GetString(File.ReadAllBytes(clientQuestInfoPath));
        var referenceText = encoding.GetString(File.ReadAllBytes(referencePath));
        var fallbackSourcePaths = ResolveQuestInfoNameFallbackSourcePaths(args, clientPath);
        var fallbackCatalog = LegacyQuestInfoNameFallbackCatalog.FromSourcePaths(fallbackSourcePaths);
        var result = LegacyQuestInfoTranslator.Translate(originalText, referenceText, fallbackCatalog, out var updatedText);
        if (result.Changed)
        {
            File.WriteAllBytes(clientQuestInfoPath, encoding.GetBytes(updatedText));
        }

        Console.WriteLine($"Client Questinfo: {clientQuestInfoPath}");
        Console.WriteLine($"Reference Questinfo: {referencePath}");
        Console.WriteLine($"Fallback name sources: {string.Join("; ", fallbackSourcePaths)}");
        Console.WriteLine($"Fallback item names: {fallbackCatalog.ItemNames.Count}");
        Console.WriteLine($"Fallback NPC names: {fallbackCatalog.NpcNames.Count}");
        Console.WriteLine($"Fallback monster names: {fallbackCatalog.MonsterNames.Count}");
        Console.WriteLine($"Fallback map names: {fallbackCatalog.MapNames.Count}");
        Console.WriteLine($"Sections scanned: {result.SectionsScanned}");
        Console.WriteLine($"Matched sections: {result.MatchedSections}");
        Console.WriteLine($"Unmatched sections: {result.UnmatchedSections}");
        Console.WriteLine($"Translated keys: {result.TranslatedKeys}");
        Console.WriteLine($"Changed keys: {result.ChangedKeys}");
        Console.WriteLine($"Unmatched keys: {result.UnmatchedKeys}");
        Console.WriteLine($"Changed file: {result.Changed}");
        return result.SectionsScanned > 0 ? 0 : 2;
    }

    static int PatchClientQuestInfoPassion(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var clientQuestInfoPath = Path.Combine(clientPath, "ini", "QuestinfoPassionServer.ini");
        var referencePaths = ResolveQuestInfoPassionReferencePaths(args, clientPath);

        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        if (!File.Exists(clientQuestInfoPath))
        {
            Console.Error.WriteLine($"Client QuestinfoPassionServer file not found: {clientQuestInfoPath}");
            return 1;
        }

        foreach (var referencePath in referencePaths)
        {
            if (!File.Exists(referencePath))
            {
                Console.Error.WriteLine($"Reference Questinfo source not found: {referencePath}");
                return 1;
            }
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var originalText = encoding.GetString(File.ReadAllBytes(clientQuestInfoPath));
        var referenceTexts = referencePaths
            .Select(path => encoding.GetString(File.ReadAllBytes(path)))
            .ToArray();
        var fallbackSourcePaths = ResolveQuestInfoNameFallbackSourcePaths(args, clientPath);
        var fallbackCatalog = LegacyQuestInfoNameFallbackCatalog.FromSourcePaths(fallbackSourcePaths);
        var result = LegacyQuestInfoPassionTranslator.Translate(originalText, referenceTexts, fallbackCatalog, out var updatedText);
        if (result.Changed)
        {
            File.WriteAllBytes(clientQuestInfoPath, encoding.GetBytes(updatedText));
        }

        Console.WriteLine($"Client QuestinfoPassionServer: {clientQuestInfoPath}");
        Console.WriteLine($"Reference Questinfo sources: {string.Join("; ", referencePaths)}");
        Console.WriteLine($"Fallback name sources: {string.Join("; ", fallbackSourcePaths)}");
        Console.WriteLine($"Fallback item names: {fallbackCatalog.ItemNames.Count}");
        Console.WriteLine($"Fallback NPC names: {fallbackCatalog.NpcNames.Count}");
        Console.WriteLine($"Fallback monster names: {fallbackCatalog.MonsterNames.Count}");
        Console.WriteLine($"Fallback map names: {fallbackCatalog.MapNames.Count}");
        Console.WriteLine($"Reference sections scanned: {result.ReferenceSectionsScanned}");
        Console.WriteLine($"Reference mission ids: {result.ReferenceMissionIds}");
        Console.WriteLine($"Reference fields: {result.ReferenceFields}");
        Console.WriteLine($"Ambiguous reference fields: {result.AmbiguousReferenceFields}");
        Console.WriteLine($"Target sections scanned: {result.TargetSectionsScanned}");
        Console.WriteLine($"Target sections with MissionId: {result.TargetSectionsWithMissionId}");
        Console.WriteLine($"Translatable fields: {result.TranslatableFields}");
        Console.WriteLine($"Matched fields: {result.MatchedFields}");
        Console.WriteLine($"Changed fields: {result.ChangedFields}");
        Console.WriteLine($"Ambiguous fields: {result.AmbiguousFields}");
        Console.WriteLine($"Unmatched fields: {result.UnmatchedFields}");
        Console.WriteLine($"Changed file: {result.Changed}");
        return result.ReferenceMissionIds > 0 && result.TargetSectionsScanned > 0 ? 0 : 2;
    }

    static int PatchClientQuestInfoRandomSourceBacked(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var clientQuestInfoRandomPath = Path.Combine(clientPath, "ini", "QuestinfoRandom.lua");

        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        if (!File.Exists(clientQuestInfoRandomPath))
        {
            Console.Error.WriteLine($"Client QuestinfoRandom file not found: {clientQuestInfoRandomPath}");
            return 1;
        }

        var sourcePaths = ResolveQuestInfoRandomSourceBackedPaths(args, clientPath, clientQuestInfoRandomPath);
        if (sourcePaths.Count == 0)
        {
            Console.Error.WriteLine("No source-backed QuestinfoRandom source files were found.");
            return 1;
        }

        var result = LegacyQuestInfoRandomTranslator.PatchFile(clientQuestInfoRandomPath, sourcePaths);
        Console.WriteLine($"Client QuestinfoRandom: {clientQuestInfoRandomPath}");
        Console.WriteLine($"QuestinfoRandom sources: {string.Join("; ", sourcePaths)}");
        Console.WriteLine($"Source rows: {result.SourceRows}");
        Console.WriteLine($"Source keys: {result.SourceKeys}");
        Console.WriteLine($"Conflicting source keys: {result.ConflictingSourceKeys.Count}");
        Console.WriteLine($"Target sections: {result.TargetSections}");
        Console.WriteLine($"Target rows: {result.TargetRows}");
        Console.WriteLine($"Rows needing translation: {result.RowsNeedingTranslation}");
        Console.WriteLine($"Source-matched rows: {result.SourceMatchedRows}");
        Console.WriteLine($"Same-format matched rows: {result.SameFormatMatchedRows}");
        Console.WriteLine($"Compound NPC matched rows: {result.CompoundNpcMatchedRows}");
        Console.WriteLine($"Anchor matched rows: {result.AnchorMatchedRows}");
        Console.WriteLine($"Conflict rows: {result.ConflictRows}");
        Console.WriteLine($"Unmatched rows: {result.UnmatchedRows}");
        Console.WriteLine($"Missing source keys: {result.MissingSourceKeys.Count}");
        Console.WriteLine($"Changed rows: {result.ChangedRows}");
        Console.WriteLine($"Remaining CJK rows: {result.RemainingCjkRows}");
        Console.WriteLine($"Changed file: {result.Changed}");
        if (result.MissingSourceKeys.Count > 0)
        {
            Console.WriteLine("First missing source keys: " + string.Join(", ", result.MissingSourceKeys.Take(25)));
        }

        return result.TargetRows > 0 ? 0 : 2;
    }

    static int PatchClientQuestInfoRandomPassionSourceBacked(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var clientQuestInfoRandomPath = Path.Combine(clientPath, "ini", "QuestinfoRandomPassion.lua");

        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        if (!File.Exists(clientQuestInfoRandomPath))
        {
            Console.Error.WriteLine($"Client QuestinfoRandomPassion file not found: {clientQuestInfoRandomPath}");
            return 1;
        }

        var sourcePaths = ResolveQuestInfoRandomSourceBackedPaths(args, clientPath, clientQuestInfoRandomPath);
        if (sourcePaths.Count == 0)
        {
            Console.Error.WriteLine("No source-backed QuestinfoRandomPassion source files were found.");
            return 1;
        }

        var fallbackSourcePaths = ResolveQuestInfoNameFallbackSourcePaths(args, clientPath);
        var fallbackCatalog = LegacyQuestInfoNameFallbackCatalog.FromSourcePaths(fallbackSourcePaths);
        var result = LegacyQuestInfoRandomTranslator.PatchFile(
            clientQuestInfoRandomPath,
            sourcePaths,
            fallbackCatalog);
        Console.WriteLine($"Client QuestinfoRandomPassion: {clientQuestInfoRandomPath}");
        Console.WriteLine($"QuestinfoRandomPassion sources: {string.Join("; ", sourcePaths)}");
        Console.WriteLine($"Fallback name sources: {string.Join("; ", fallbackSourcePaths)}");
        Console.WriteLine($"Fallback item names: {fallbackCatalog.ItemNames.Count}");
        Console.WriteLine($"Fallback NPC names: {fallbackCatalog.NpcNames.Count}");
        Console.WriteLine($"Fallback monster names: {fallbackCatalog.MonsterNames.Count}");
        Console.WriteLine($"Fallback map names: {fallbackCatalog.MapNames.Count}");
        Console.WriteLine($"Source rows: {result.SourceRows}");
        Console.WriteLine($"Source keys: {result.SourceKeys}");
        Console.WriteLine($"Conflicting source keys: {result.ConflictingSourceKeys.Count}");
        Console.WriteLine($"Target sections: {result.TargetSections}");
        Console.WriteLine($"Target rows: {result.TargetRows}");
        Console.WriteLine($"Rows needing translation: {result.RowsNeedingTranslation}");
        Console.WriteLine($"Source-matched rows: {result.SourceMatchedRows}");
        Console.WriteLine($"Same-format matched rows: {result.SameFormatMatchedRows}");
        Console.WriteLine($"Compound NPC matched rows: {result.CompoundNpcMatchedRows}");
        Console.WriteLine($"Anchor matched rows: {result.AnchorMatchedRows}");
        Console.WriteLine($"Fallback matched rows: {result.FallbackMatchedRows}");
        Console.WriteLine($"Conflict rows: {result.ConflictRows}");
        Console.WriteLine($"Unmatched rows: {result.UnmatchedRows}");
        Console.WriteLine($"Missing source keys: {result.MissingSourceKeys.Count}");
        Console.WriteLine($"Changed rows: {result.ChangedRows}");
        Console.WriteLine($"Remaining CJK rows: {result.RemainingCjkRows}");
        Console.WriteLine($"Changed file: {result.Changed}");
        if (result.MissingSourceKeys.Count > 0)
        {
            Console.WriteLine("First missing source keys: " + string.Join(", ", result.MissingSourceKeys.Take(25)));
        }

        return result.TargetRows > 0 ? 0 : 2;
    }

    static int PatchClientGameMapEx(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var clientGameMapExPath = Path.Combine(clientPath, "ini", "GameMapEx.ini");
        var referencePath = ResolveLegacyReferencePath(args, DefaultLegacyGameMapExPath, "GameMapEx.ini");

        if (!ValidateClientReferenceFile(clientPath, clientGameMapExPath, referencePath, "GameMapEx"))
        {
            return 1;
        }

        var result = LegacyGameMapExNameTranslator.PatchFile(clientGameMapExPath, referencePath);
        Console.WriteLine($"Client GameMapEx: {clientGameMapExPath}");
        Console.WriteLine($"Reference GameMapEx: {referencePath}");
        Console.WriteLine($"Reference maps scanned: {result.ReferenceMapsScanned}");
        Console.WriteLine($"Target maps scanned: {result.TargetMapsScanned}");
        Console.WriteLine($"Translatable names: {result.TranslatableNames}");
        Console.WriteLine($"Matched names: {result.MatchedNames}");
        Console.WriteLine($"Changed names: {result.ChangedNames}");
        Console.WriteLine($"Unmatched names: {result.UnmatchedNames}");
        Console.WriteLine($"Changed file: {result.Changed}");
        return result.ReferenceMapsScanned > 0 && result.TargetMapsScanned > 0 ? 0 : 2;
    }

    static int PatchClientRuneDesc(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var clientRuneDescPath = Path.Combine(clientPath, "ini", "RuneDesc.ini");
        var referencePaths = ResolveRuneDescReferencePaths(args, clientPath);

        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        if (!File.Exists(clientRuneDescPath))
        {
            Console.Error.WriteLine($"Client RuneDesc file not found: {clientRuneDescPath}");
            return 1;
        }

        if (referencePaths.Count == 0)
        {
            Console.Error.WriteLine($"RuneDesc source not found under: {clientPath}");
            return 1;
        }

        foreach (var referencePath in referencePaths)
        {
            if (!File.Exists(referencePath))
            {
                Console.Error.WriteLine($"RuneDesc source not found: {referencePath}");
                return 1;
            }
        }

        var result = LegacyRuneDescTranslator.PatchFile(clientRuneDescPath, referencePaths);
        Console.WriteLine($"Client RuneDesc: {clientRuneDescPath}");
        Console.WriteLine($"Reference RuneDesc sources: {string.Join("; ", referencePaths)}");
        Console.WriteLine($"Source descriptions scanned: {result.SourceDescriptionsScanned}");
        Console.WriteLine($"Usable source descriptions: {result.SourceDescriptions}");
        Console.WriteLine($"Conflicting source IDs: {result.ConflictingSourceIds.Count}");
        Console.WriteLine($"Desc rows scanned: {result.RowCount}");
        Console.WriteLine($"Translated rows: {result.TranslatedRows}");
        Console.WriteLine($"Changed rows: {result.ChangedRows}");
        Console.WriteLine($"Unmatched rows: {result.UnmatchedRows}");
        Console.WriteLine($"Unique rune IDs: {result.UniqueRuneIds}");
        Console.WriteLine($"Matched unique rune IDs: {result.MatchedUniqueRuneIds}");
        Console.WriteLine($"Unmatched unique rune IDs: {result.UnmatchedRuneIds.Count}");
        Console.WriteLine($"Changed file: {result.Changed}");
        if (result.UnmatchedRuneIds.Count > 0)
        {
            Console.WriteLine("First unmatched rune IDs: " + string.Join(", ", result.UnmatchedRuneIds.Take(25)));
        }

        return result.TranslatedRows > 0 ? 0 : 2;
    }

    static int PatchClientGouYuSkillText(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var gouYuPath = Path.Combine(clientPath, "ini", "GouYu.lua");
        var sourcePaths = ResolveGouYuSkillTextSourcePaths(args, clientPath);

        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        if (!File.Exists(gouYuPath))
        {
            Console.Error.WriteLine($"Client GouYu file not found: {gouYuPath}");
            return 1;
        }

        if (sourcePaths.MagicEffectPaths.Count == 0 &&
            sourcePaths.MagicTypePaths.Count == 0 &&
            sourcePaths.GouYuTypePaths.Count == 0)
        {
            Console.Error.WriteLine("No GouYu skill text source files were found.");
            return 1;
        }

        var result = GouYuSkillTextSourceBackedTranslator.PatchFile(
            gouYuPath,
            sourcePaths.MagicEffectPaths,
            sourcePaths.MagicTypePaths,
            sourcePaths.GouYuTypePaths);
        Console.WriteLine($"Client GouYu: {gouYuPath}");
        Console.WriteLine($"MagicEffect sources: {string.Join("; ", sourcePaths.MagicEffectPaths)}");
        Console.WriteLine($"magictype sources: {string.Join("; ", sourcePaths.MagicTypePaths)}");
        Console.WriteLine($"gouyu_type sources: {string.Join("; ", sourcePaths.GouYuTypePaths)}");
        Console.WriteLine($"Text rows scanned: {result.TextRowsScanned}");
        Console.WriteLine($"Unique text IDs: {result.UniqueTextIds}");
        Console.WriteLine($"Source skill names: {result.SourceSkillNames}");
        Console.WriteLine($"Conflicting source IDs: {result.ConflictingSourceIds.Count}");
        Console.WriteLine($"Matched text IDs: {result.MatchedTextIds}");
        Console.WriteLine($"Translated rows: {result.TranslatedRows}");
        Console.WriteLine($"Changed rows: {result.ChangedRows}");
        Console.WriteLine($"Remaining CJK rows: {result.RemainingCjkRows}");
        Console.WriteLine($"Missing source IDs: {result.MissingSourceIds.Count}");
        Console.WriteLine($"Changed file: {result.Changed}");
        if (result.MissingSourceIds.Count > 0)
        {
            Console.WriteLine("First missing source IDs: " + string.Join(", ", result.MissingSourceIds.Take(25)));
        }

        return result.TextRowsScanned > 0 ? 0 : 2;
    }

    static int PatchClientLimitShopSourceBacked(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var limitShopPath = Path.Combine(clientPath, "ini", "LimitShop.lua");

        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        if (!File.Exists(limitShopPath))
        {
            Console.Error.WriteLine($"Client LimitShop file not found: {limitShopPath}");
            return 1;
        }

        var sourcePaths = ResolveLimitShopSourceBackedPaths(args, clientPath, limitShopPath);
        if (sourcePaths.Count == 0)
        {
            Console.Error.WriteLine("No source-backed LimitShop source files were found.");
            return 1;
        }

        var result = SourceBackedLimitShopItemTextTranslator.PatchFile(limitShopPath, sourcePaths);
        Console.WriteLine($"Client LimitShop: {limitShopPath}");
        Console.WriteLine($"Source-backed LimitShop sources: {string.Join("; ", sourcePaths)}");
        Console.WriteLine($"Text rows scanned: {result.RowCount}");
        Console.WriteLine($"Rows needing translation: {result.RowsNeedingTranslation}");
        Console.WriteLine($"Source-matched rows: {result.SourceMatchedRows}");
        Console.WriteLine($"Changed rows: {result.ChangedRows}");
        Console.WriteLine($"Skipped rows without source: {result.SkippedRowsWithoutSource}");
        Console.WriteLine($"Unique item IDs needing translation: {result.UniqueItemIdsNeedingTranslation}");
        Console.WriteLine($"Matched unique item IDs: {result.MatchedUniqueItemIds}");
        Console.WriteLine($"Missing unique item IDs: {result.MissingItemIds.Count}");
        Console.WriteLine($"Conflicted source item IDs: {result.ConflictedItemIds.Count}");
        Console.WriteLine($"Changed file: {result.Changed}");
        if (result.MissingItemIds.Count > 0)
        {
            Console.WriteLine("First missing item IDs: " + string.Join(", ", result.MissingItemIds.Take(25)));
        }

        return result.RowCount > 0 ? 0 : 2;
    }

    static int PatchClientItemUiSourceBacked(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        var targetPaths = new[]
        {
            Path.Combine(clientPath, "ini", "Item.lua"),
            Path.Combine(clientPath, "ini", "ItemQuenchTipInfo.ini"),
            Path.Combine(clientPath, "ini", "Storage.ini"),
        };
        var existingTargets = targetPaths.Where(File.Exists).ToArray();
        if (existingTargets.Length == 0)
        {
            Console.Error.WriteLine($"No client item UI target files found under: {clientPath}");
            return 1;
        }

        var sourcePaths = ResolveItemUiSourceBackedPaths(args, clientPath, existingTargets);
        if (sourcePaths.Count == 0)
        {
            Console.Error.WriteLine("No source-backed item UI source files were found.");
            return 1;
        }

        var totalRows = 0;
        var totalChangedRows = 0;
        var changedFiles = 0;
        Console.WriteLine($"Source-backed item UI sources: {string.Join("; ", sourcePaths)}");
        foreach (var targetPath in existingTargets)
        {
            var result = SourceBackedItemUiTextTranslator.PatchFile(targetPath, sourcePaths);
            Console.WriteLine($"Client item UI target: {targetPath}");
            Console.WriteLine($"Text rows scanned: {result.RowCount}");
            Console.WriteLine($"Rows needing translation: {result.RowsNeedingTranslation}");
            Console.WriteLine($"Source-matched rows: {result.SourceMatchedRows}");
            Console.WriteLine($"Changed rows: {result.ChangedRows}");
            Console.WriteLine($"Skipped rows without source: {result.SkippedRowsWithoutSource}");
            Console.WriteLine($"Skipped rows with conflicting source: {result.SkippedRowsWithConflictingSource}");
            Console.WriteLine($"Skipped rows with unsafe replacement: {result.SkippedRowsWithUnsafeReplacement}");
            Console.WriteLine($"Skipped rows with unsupported field: {result.SkippedRowsWithUnsupportedField}");
            Console.WriteLine($"Unique item IDs needing translation: {result.UniqueItemIdsNeedingTranslation}");
            Console.WriteLine($"Matched unique item IDs: {result.MatchedUniqueItemIds}");
            Console.WriteLine($"Blocked rows: {result.BlockedRows.Count}");
            Console.WriteLine($"Conflicting source rows: {result.Conflicts.Count}");
            Console.WriteLine($"Changed file: {result.Changed}");
            if (result.BlockedRows.Count > 0)
            {
                Console.WriteLine("First blocked rows: " + string.Join(
                    "; ",
                    result.BlockedRows.Take(10).Select(row => $"{row.Target}:{row.ItemId}:{row.Field}:{row.Reason}")));
            }

            totalRows += result.RowCount;
            totalChangedRows += result.ChangedRows;
            if (result.Changed)
            {
                changedFiles++;
            }
        }

        Console.WriteLine($"Item UI targets scanned: {existingTargets.Length}");
        Console.WriteLine($"Total item UI rows scanned: {totalRows}");
        Console.WriteLine($"Total item UI changed rows: {totalChangedRows}");
        Console.WriteLine($"Changed files: {changedFiles}");
        return totalRows > 0 ? 0 : 2;
    }

    static int PatchClientStatusTextSourceBacked(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        var statusTipsPath = Path.Combine(clientPath, "ini", "StatusTips.ini");
        var infoLuaPath = Path.Combine(clientPath, "ini", "Info.lua");
        if (!File.Exists(statusTipsPath) && !File.Exists(infoLuaPath))
        {
            Console.Error.WriteLine($"No client status text target files found under: {clientPath}");
            return 1;
        }

        var sourcePaths = ResolveStatusTextSourceBackedPaths(args, clientPath);
        if (sourcePaths.StatusSourcePaths.Count == 0 && sourcePaths.MagicTypePaths.Count == 0)
        {
            Console.Error.WriteLine("No source-backed status text source files were found.");
            return 1;
        }

        var scannedTargets = 0;
        var totalVisibleRows = 0;
        var totalRemainingCjkRows = 0;
        var changedFiles = 0;
        if (File.Exists(statusTipsPath))
        {
            var statusSources = sourcePaths.StatusSourcePaths
                .SelectMany(path => SourceBackedClientStatusTextTranslator.ReadIniStatusTextSources(path))
                .ToArray();
            var result = SourceBackedClientStatusTextTranslator.PatchStatusTipsFile(statusTipsPath, statusSources);
            Console.WriteLine($"Client StatusTips: {statusTipsPath}");
            Console.WriteLine($"Status text sources: {string.Join("; ", sourcePaths.StatusSourcePaths)}");
            Console.WriteLine($"Source rows: {result.SourceRows}");
            Console.WriteLine($"Source keys: {result.SourceKeys}");
            Console.WriteLine($"Conflicting source keys: {result.ConflictingSourceKeys.Count}");
            Console.WriteLine($"Target rows: {result.TargetRows}");
            Console.WriteLine($"Rows needing translation: {result.RowsNeedingTranslation}");
            Console.WriteLine($"Matched rows: {result.MatchedRows}");
            Console.WriteLine($"Placeholder mismatch rows: {result.PlaceholderMismatchRows}");
            Console.WriteLine($"Unmatched rows: {result.UnmatchedRows}");
            Console.WriteLine($"Already translated rows: {result.AlreadyTranslatedRows}");
            Console.WriteLine($"Changed rows: {result.ChangedRows}");
            Console.WriteLine($"Remaining CJK rows: {result.RemainingCjkRows}");
            Console.WriteLine($"Changed file: {result.Changed}");
            totalVisibleRows += result.TargetRows;
            totalRemainingCjkRows += result.RemainingCjkRows;
            scannedTargets++;
            if (result.Changed)
            {
                changedFiles++;
            }
        }

        if (File.Exists(infoLuaPath))
        {
            var result = SourceBackedClientStatusTextTranslator.PatchInfoLuaFile(
                infoLuaPath,
                sourcePaths.MagicTypePaths);
            Console.WriteLine($"Client Info.lua: {infoLuaPath}");
            Console.WriteLine($"magictype sources: {string.Join("; ", sourcePaths.MagicTypePaths)}");
            Console.WriteLine($"Skill rows: {result.SkillRows}");
            Console.WriteLine($"Unique skill IDs: {result.UniqueSkillIds}");
            Console.WriteLine($"Source skill names: {result.SourceSkillNames}");
            Console.WriteLine($"Conflicting source IDs: {result.ConflictingSourceIds.Count}");
            Console.WriteLine($"Matched skill rows: {result.MatchedSkillRows}");
            Console.WriteLine($"Missing skill rows: {result.MissingSkillRows}");
            Console.WriteLine($"Translated rows: {result.TranslatedRows}");
            Console.WriteLine($"Changed rows: {result.ChangedRows}");
            Console.WriteLine($"Remaining CJK rows: {result.RemainingCjkRows}");
            Console.WriteLine($"Changed file: {result.Changed}");
            totalVisibleRows += result.SkillRows;
            totalRemainingCjkRows += result.RemainingCjkRows;
            scannedTargets++;
            if (result.Changed)
            {
                changedFiles++;
            }
        }

        Console.WriteLine($"Status text targets scanned: {scannedTargets}");
        Console.WriteLine($"Changed files: {changedFiles}");
        return totalVisibleRows > 0 && totalRemainingCjkRows == 0 ? 0 : 2;
    }

    static int PatchClientMonsterSayTemplates(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        var monsterSayPath = Path.Combine(clientPath, "ini", "monstersay.ini");
        if (!File.Exists(monsterSayPath))
        {
            Console.Error.WriteLine($"Client monstersay file not found: {monsterSayPath}");
            return 1;
        }

        var result = LegacyMonsterSayTemplateTranslator.PatchFile(monsterSayPath);
        Console.WriteLine($"Client monstersay: {monsterSayPath}");
        Console.WriteLine($"Key/value rows scanned: {result.KeyValueRowsScanned}");
        Console.WriteLine($"Supported monster-say rows: {result.SupportedKeyRows}");
        Console.WriteLine($"Already English rows: {result.AlreadyEnglishRows}");
        Console.WriteLine($"Changed rows: {result.ChangedRows}");
        Console.WriteLine($"Unsupported CJK rows: {result.UnsupportedCjkRows}");
        Console.WriteLine($"Changed file: {result.Changed}");
        return result.KeyValueRowsScanned > 0 ? 0 : 2;
    }

    static int PatchClientStrResSourceBacked(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        var strResPath = Path.Combine(clientPath, "ini", "StrRes.ini");
        if (!File.Exists(strResPath))
        {
            Console.Error.WriteLine($"Client StrRes file not found: {strResPath}");
            return 1;
        }

        var sourcePaths = ResolveStrResSourceBackedPaths(args, clientPath, strResPath);
        var result = SourceBackedStrResTranslator.PatchFile(strResPath, sourcePaths);
        Console.WriteLine($"Client source-backed StrRes: {strResPath}");
        Console.WriteLine($"StrRes sources: {string.Join("; ", sourcePaths)}");
        Console.WriteLine($"Source rows: {result.SourceRows}");
        Console.WriteLine($"Source keys: {result.SourceKeys}");
        Console.WriteLine($"Conflicted source keys: {result.ConflictedSourceKeys}");
        Console.WriteLine($"Target rows: {result.TargetRows}");
        Console.WriteLine($"Rows needing translation: {result.RowsNeedingTranslation}");
        Console.WriteLine($"Translated rows: {result.TranslatedRows}");
        Console.WriteLine($"Placeholder mismatch rows: {result.PlaceholderMismatchRows}");
        Console.WriteLine($"Unmatched rows: {result.UnmatchedRows}");
        Console.WriteLine($"Already translated rows: {result.AlreadyTranslatedRows}");
        Console.WriteLine($"Changed rows: {result.ChangedRows}");
        Console.WriteLine($"Remaining CJK rows: {result.RemainingCjkRows}");
        Console.WriteLine($"Changed file: {result.Changed}");
        return result.TargetRows > 0 && result.RemainingCjkRows == 0 ? 0 : 2;
    }

    static int PatchClientCnResSourceBacked(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        var targetPaths = new[]
        {
            Path.Combine(clientPath, "ini", "Cn_Res.ini"),
            Path.Combine(clientPath, "ini", "Server_Cn_Res.ini"),
        };

        foreach (var targetPath in targetPaths)
        {
            if (!File.Exists(targetPath))
            {
                Console.Error.WriteLine($"Client Cn_Res source-backed target not found: {targetPath}");
                return 1;
            }
        }

        var sourcePaths = ResolveCnResSourceBackedPaths(args, clientPath, targetPaths);
        if (sourcePaths.Count == 0)
        {
            Console.Error.WriteLine("No Cn_Res source-backed input files were found.");
            return 1;
        }

        var totalTargetRows = 0;
        var totalTranslatedRows = 0;
        var totalChangedRows = 0;
        var totalUnmatchedRows = 0;
        var changedFiles = 0;
        Console.WriteLine($"Cn_Res source files: {string.Join("; ", sourcePaths)}");
        foreach (var targetPath in targetPaths)
        {
            var result = CnResSourceBackedTranslator.PatchUtf8File(
                targetPath,
                sourcePaths,
                includeTargetAsSource: false);
            Console.WriteLine($"Client Cn_Res source-backed target: {targetPath}");
            Console.WriteLine($"Source rows: {result.SourceRows}");
            Console.WriteLine($"Source keys: {result.SourceKeys}");
            Console.WriteLine($"Conflicted source keys: {result.ConflictedSourceKeys}");
            Console.WriteLine($"Target rows: {result.TargetRows}");
            Console.WriteLine($"Translated rows: {result.TranslatedRows}");
            Console.WriteLine($"Changed rows: {result.ChangedRows}");
            Console.WriteLine($"Unmatched rows: {result.UnmatchedRows}");
            Console.WriteLine($"Changed file: {result.Changed}");
            totalTargetRows += result.TargetRows;
            totalTranslatedRows += result.TranslatedRows;
            totalChangedRows += result.ChangedRows;
            totalUnmatchedRows += result.UnmatchedRows;
            if (result.Changed)
            {
                changedFiles++;
            }
        }

        Console.WriteLine($"Cn_Res targets scanned: {targetPaths.Length}");
        Console.WriteLine($"Total target rows: {totalTargetRows}");
        Console.WriteLine($"Total translated rows: {totalTranslatedRows}");
        Console.WriteLine($"Total changed rows: {totalChangedRows}");
        Console.WriteLine($"Total unmatched rows: {totalUnmatchedRows}");
        Console.WriteLine($"Changed files: {changedFiles}");
        return totalTargetRows > 0 ? 0 : 2;
    }

    static int ScanClientTextFiles(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        var files = Directory.EnumerateFiles(clientPath, "*", SearchOption.AllDirectories)
            .Where(IsClientTextCandidate)
            .Order(StringComparer.OrdinalIgnoreCase);

        var scanned = 0;
        var packed = 0;
        foreach (var path in files)
        {
            var bytes = File.ReadAllBytes(path);
            var result = ClientTextFileClassifier.Classify(bytes);
            scanned++;
            if (result.IsLikelyPackedOrBinary)
            {
                packed++;
            }

            var relativePath = Path.GetRelativePath(clientPath, path);
            Console.WriteLine(
                $"{relativePath}\ttext={result.IsLikelyText}\tpacked={result.IsLikelyPackedOrBinary}\tnuls={result.NullByteCount}\tcontrols={result.ControlByteRatio:0.###}\tnewlines={result.NewlineCount}\tprintable={result.PrintableRatio:0.###}\t{result.Reason}");
        }

        Console.WriteLine($"Scanned files: {scanned}");
        Console.WriteLine($"Likely packed/binary files: {packed}");
        return 0;
    }

    private static bool ValidateClientReferenceFile(
        string clientPath,
        string clientFilePath,
        string referencePath,
        string label)
    {
        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return false;
        }

        if (!File.Exists(clientFilePath))
        {
            Console.Error.WriteLine($"Client {label} file not found: {clientFilePath}");
            return false;
        }

        if (!File.Exists(referencePath))
        {
            Console.Error.WriteLine($"Reference {label} source not found: {referencePath}");
            return false;
        }

        if (!ClientTextSourcePathFence.IsAllowedSourcePath(referencePath))
        {
            Console.Error.WriteLine($"Reference {label} source is blocked because it is from a non-Conquer client: {referencePath}");
            return false;
        }

        return true;
    }

    private static string ResolveLegacyReferencePath(string[] args, string defaultPath, string fileName)
    {
        if (args.Length <= 2)
        {
            return Path.GetFullPath(defaultPath);
        }

        var path = Path.GetFullPath(args[2]);
        return Directory.Exists(path)
            ? Path.Combine(path, fileName)
            : path;
    }

    private static IReadOnlyList<string> ResolveQuestInfoPassionReferencePaths(string[] args, string clientPath)
    {
        var paths = new List<string>();
        if (args.Length <= 2)
        {
            paths.Add(DefaultLegacyQuestInfoPath);
            paths.Add(Path.Combine(clientPath, "ini", "Questinfo.ini"));
        }
        else
        {
            for (var index = 2; index < args.Length; index++)
            {
                var path = Path.GetFullPath(args[index]);
                paths.Add(Directory.Exists(path) ? Path.Combine(path, "Questinfo.ini") : path);
            }
        }

        return paths
            .Select(Path.GetFullPath)
            .Where(ClientTextSourcePathFence.IsAllowedSourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveQuestInfoRandomSourceBackedPaths(
        string[] args,
        string clientPath,
        string targetPath)
    {
        var paths = new List<string>
        {
            targetPath,
            DefaultLegacyQuestInfoPath,
            DefaultLegacyDbServerPath,
            Path.Combine(clientPath, "ini", "Questinfo.ini"),
            Path.Combine(clientPath, "ini", "QuestinfoPassionServer.ini"),
        };

        for (var index = 2; index < args.Length; index++)
        {
            AddQuestInfoRandomSourceBackedPath(paths, args[index]);
        }

        return ExistingDistinctFilesOrDirectories(paths);
    }

    private static void AddQuestInfoRandomSourceBackedPath(List<string> paths, string rawPath)
    {
        var path = Path.GetFullPath(rawPath);
        if (!Directory.Exists(path))
        {
            paths.Add(path);
            return;
        }

        paths.Add(Path.Combine(path, "QuestinfoRandom.lua"));
        paths.Add(Path.Combine(path, "Questinfo.ini"));
        paths.Add(Path.Combine(path, "QuestinfoPassionServer.ini"));
        paths.Add(Path.Combine(path, "ini", "QuestinfoRandom.lua"));
        paths.Add(Path.Combine(path, "ini", "Questinfo.ini"));
        paths.Add(Path.Combine(path, "ini", "QuestinfoPassionServer.ini"));
        paths.Add(Path.Combine(path, "GameServer", "DBServer", "Questinfo.ini"));
        paths.Add(Path.Combine(path, "GameServer", "DBServer"));
        paths.Add(Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "Questinfo.ini"));
        paths.Add(Path.Combine(path, "lqtxfwd", "GameServer", "DBServer"));
    }

    private static IReadOnlyList<string> ResolveQuestInfoNameFallbackSourcePaths(string[] args, string clientPath)
    {
        var paths = new List<string>
        {
            DefaultLegacyItemTypePath,
            DefaultLegacyDbServerPath,
            clientPath,
            Path.Combine(clientPath, "Script", "Server_Key.lua"),
            Path.Combine(clientPath, "ini", "Server_Key.lua"),
            Path.Combine(clientPath, "ini", "GameMapEx.ini"),
        };

        var decompiledNpcPath = Path.GetFullPath(Path.Combine(
            DefaultLegacyDbServerPath,
            "..",
            "..",
            "DecompiledSource",
            "GameServer",
            "MrRayzo.Game.MsgNpc",
            "NpcID.cs"));
        paths.Add(decompiledNpcPath);

        for (var index = 2; index < args.Length; index++)
        {
            paths.Add(Path.GetFullPath(args[index]));
        }

        return paths
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Select(Path.GetFullPath)
            .Where(ClientTextSourcePathFence.IsAllowedSourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveRuneDescReferencePaths(string[] args, string clientPath)
    {
        if (args.Length <= 2)
        {
            var defaultPaths = new List<string>();
            defaultPaths.AddRange(ResolveClientServerKeyPaths(clientPath));
            defaultPaths.Add(DefaultLegacyItemTypePath);
            return defaultPaths
                .Where(File.Exists)
                .Select(Path.GetFullPath)
                .Where(ClientTextSourcePathFence.IsAllowedSourcePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var paths = new List<string>();
        for (var index = 2; index < args.Length; index++)
        {
            var path = Path.GetFullPath(args[index]);
            if (!Directory.Exists(path))
            {
                paths.Add(path);
                continue;
            }

            AddRuneDescReferenceDirectoryPaths(paths, path);
        }

        return paths
            .Where(ClientTextSourcePathFence.IsAllowedSourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddRuneDescReferenceDirectoryPaths(List<string> paths, string directory)
    {
        var beforeCount = paths.Count;
        AddExistingPath(paths, Path.Combine(directory, "Server_Key.lua"));
        foreach (var serverKeyPath in ResolveClientServerKeyPaths(directory))
        {
            paths.Add(serverKeyPath);
        }

        AddExistingPath(paths, Path.Combine(directory, "RuneDesc.ini"));
        AddExistingPath(paths, Path.Combine(directory, "ini", "RuneDesc.ini"));
        AddExistingPath(paths, Path.Combine(directory, "itemtype.txt"));
        AddExistingPath(paths, Path.Combine(directory, "GameServer", "DBServer", "itemtype.txt"));
        AddExistingPath(paths, Path.Combine(directory, "lqtxfwd", "GameServer", "DBServer", "itemtype.txt"));

        if (paths.Count == beforeCount)
        {
            paths.Add(Path.Combine(directory, "Server_Key.lua"));
        }
    }

    private static void AddExistingPath(List<string> paths, string path)
    {
        if (File.Exists(path))
        {
            paths.Add(path);
        }
    }

    private static GouYuSkillTextSourcePaths ResolveGouYuSkillTextSourcePaths(string[] args, string clientPath)
    {
        var magicEffectPaths = new List<string>
        {
            Path.Combine(clientPath, "ini", "MagicEffect.ini"),
            DefaultLegacyMagicEffectPath,
        };
        var magicTypePaths = new List<string>
        {
            Path.Combine(DefaultLegacyDbServerPath, "magictype.txt"),
        };
        var gouYuTypePaths = new List<string>
        {
            Path.Combine(DefaultLegacyDbServerPath, "gouyu_type.txt"),
        };

        for (var index = 2; index < args.Length; index++)
        {
            AddGouYuSkillTextSourcePath(magicEffectPaths, magicTypePaths, gouYuTypePaths, args[index]);
        }

        return new GouYuSkillTextSourcePaths(
            ExistingDistinctPaths(magicEffectPaths),
            ExistingDistinctPaths(magicTypePaths),
            ExistingDistinctPaths(gouYuTypePaths));
    }

    private static void AddGouYuSkillTextSourcePath(
        List<string> magicEffectPaths,
        List<string> magicTypePaths,
        List<string> gouYuTypePaths,
        string rawPath)
    {
        var path = Path.GetFullPath(rawPath);
        if (!Directory.Exists(path))
        {
            AddGouYuSkillTextFilePath(magicEffectPaths, magicTypePaths, gouYuTypePaths, path);
            return;
        }

        AddGouYuSkillTextFilePath(magicEffectPaths, magicTypePaths, gouYuTypePaths, Path.Combine(path, "MagicEffect.ini"));
        AddGouYuSkillTextFilePath(magicEffectPaths, magicTypePaths, gouYuTypePaths, Path.Combine(path, "magictype.txt"));
        AddGouYuSkillTextFilePath(magicEffectPaths, magicTypePaths, gouYuTypePaths, Path.Combine(path, "gouyu_type.txt"));
        AddGouYuSkillTextFilePath(magicEffectPaths, magicTypePaths, gouYuTypePaths, Path.Combine(path, "ini", "MagicEffect.ini"));
        AddGouYuSkillTextFilePath(magicEffectPaths, magicTypePaths, gouYuTypePaths, Path.Combine(path, "GameServer", "DBServer", "MagicEffect.ini"));
        AddGouYuSkillTextFilePath(magicEffectPaths, magicTypePaths, gouYuTypePaths, Path.Combine(path, "GameServer", "DBServer", "magictype.txt"));
        AddGouYuSkillTextFilePath(magicEffectPaths, magicTypePaths, gouYuTypePaths, Path.Combine(path, "GameServer", "DBServer", "gouyu_type.txt"));
        AddGouYuSkillTextFilePath(magicEffectPaths, magicTypePaths, gouYuTypePaths, Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "MagicEffect.ini"));
        AddGouYuSkillTextFilePath(magicEffectPaths, magicTypePaths, gouYuTypePaths, Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "magictype.txt"));
        AddGouYuSkillTextFilePath(magicEffectPaths, magicTypePaths, gouYuTypePaths, Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "gouyu_type.txt"));
    }

    private static void AddGouYuSkillTextFilePath(
        List<string> magicEffectPaths,
        List<string> magicTypePaths,
        List<string> gouYuTypePaths,
        string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.Equals("MagicEffect.ini", StringComparison.OrdinalIgnoreCase))
        {
            magicEffectPaths.Add(path);
            return;
        }

        if (fileName.Equals("magictype.txt", StringComparison.OrdinalIgnoreCase))
        {
            magicTypePaths.Add(path);
            return;
        }

        if (fileName.Equals("gouyu_type.txt", StringComparison.OrdinalIgnoreCase))
        {
            gouYuTypePaths.Add(path);
        }
    }

    private static IReadOnlyList<string> ResolveLimitShopSourceBackedPaths(
        string[] args,
        string clientPath,
        string limitShopPath)
    {
        var paths = new List<string>
        {
            limitShopPath,
            Path.Combine(clientPath, "Script", "Server_Key.lua"),
            Path.Combine(clientPath, "ini", "Server_Key.lua"),
            DefaultLegacyItemTypePath,
        };

        for (var index = 2; index < args.Length; index++)
        {
            AddLimitShopSourceBackedPath(paths, args[index]);
        }

        return ExistingDistinctPaths(paths);
    }

    private static void AddLimitShopSourceBackedPath(List<string> paths, string rawPath)
    {
        var path = Path.GetFullPath(rawPath);
        if (!Directory.Exists(path))
        {
            paths.Add(path);
            return;
        }

        paths.Add(Path.Combine(path, "LimitShop.lua"));
        paths.Add(Path.Combine(path, "ini", "LimitShop.lua"));
        paths.Add(Path.Combine(path, "Server_Key.lua"));
        paths.Add(Path.Combine(path, "Script", "Server_Key.lua"));
        paths.Add(Path.Combine(path, "ini", "Server_Key.lua"));
        paths.Add(Path.Combine(path, "itemtype.txt"));
        paths.Add(Path.Combine(path, "GameServer", "DBServer", "itemtype.txt"));
        paths.Add(Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "itemtype.txt"));
    }

    private static IReadOnlyList<string> ResolveItemUiSourceBackedPaths(
        string[] args,
        string clientPath,
        IEnumerable<string> targetPaths)
    {
        var paths = new List<string>
        {
            DefaultLegacyItemTypePath,
            Path.Combine(clientPath, "Script", "Server_Key.lua"),
            Path.Combine(clientPath, "ini", "Server_Key.lua"),
        };
        paths.AddRange(targetPaths);

        for (var index = 2; index < args.Length; index++)
        {
            AddItemUiSourceBackedPath(paths, args[index]);
        }

        return ExistingDistinctPaths(paths);
    }

    private static void AddItemUiSourceBackedPath(List<string> paths, string rawPath)
    {
        var path = Path.GetFullPath(rawPath);
        if (!Directory.Exists(path))
        {
            paths.Add(path);
            return;
        }

        paths.Add(Path.Combine(path, "itemtype.txt"));
        paths.Add(Path.Combine(path, "Server_Key.lua"));
        paths.Add(Path.Combine(path, "Script", "Server_Key.lua"));
        paths.Add(Path.Combine(path, "ini", "Server_Key.lua"));
        paths.Add(Path.Combine(path, "Item.lua"));
        paths.Add(Path.Combine(path, "ini", "Item.lua"));
        paths.Add(Path.Combine(path, "ItemQuenchTipInfo.ini"));
        paths.Add(Path.Combine(path, "ini", "ItemQuenchTipInfo.ini"));
        paths.Add(Path.Combine(path, "Storage.ini"));
        paths.Add(Path.Combine(path, "ini", "Storage.ini"));
        paths.Add(Path.Combine(path, "GameServer", "DBServer", "itemtype.txt"));
        paths.Add(Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "itemtype.txt"));
    }

    private static StatusTextSourceBackedPaths ResolveStatusTextSourceBackedPaths(string[] args, string clientPath)
    {
        var statusSourcePaths = new List<string>
        {
            Path.Combine(clientPath, "ini", "StatusTips.ini"),
            DefaultLegacyMagicEffectPath,
        };
        var magicTypePaths = new List<string>
        {
            Path.Combine(DefaultLegacyDbServerPath, "magictype.txt"),
        };

        for (var index = 2; index < args.Length; index++)
        {
            AddStatusTextSourceBackedPath(statusSourcePaths, magicTypePaths, args[index]);
        }

        return new StatusTextSourceBackedPaths(
            ExistingDistinctPaths(statusSourcePaths),
            ExistingDistinctPaths(magicTypePaths));
    }

    private static void AddStatusTextSourceBackedPath(
        List<string> statusSourcePaths,
        List<string> magicTypePaths,
        string rawPath)
    {
        var path = Path.GetFullPath(rawPath);
        if (!Directory.Exists(path))
        {
            AddStatusTextSourceFilePath(statusSourcePaths, magicTypePaths, path);
            return;
        }

        AddStatusTextSourceFilePath(statusSourcePaths, magicTypePaths, Path.Combine(path, "StatusTips.ini"));
        AddStatusTextSourceFilePath(statusSourcePaths, magicTypePaths, Path.Combine(path, "MagicEffect.ini"));
        AddStatusTextSourceFilePath(statusSourcePaths, magicTypePaths, Path.Combine(path, "magictype.txt"));
        AddStatusTextSourceFilePath(statusSourcePaths, magicTypePaths, Path.Combine(path, "ini", "StatusTips.ini"));
        AddStatusTextSourceFilePath(statusSourcePaths, magicTypePaths, Path.Combine(path, "ini", "MagicEffect.ini"));
        AddStatusTextSourceFilePath(statusSourcePaths, magicTypePaths, Path.Combine(path, "GameServer", "DBServer", "MagicEffect.ini"));
        AddStatusTextSourceFilePath(statusSourcePaths, magicTypePaths, Path.Combine(path, "GameServer", "DBServer", "magictype.txt"));
        AddStatusTextSourceFilePath(statusSourcePaths, magicTypePaths, Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "MagicEffect.ini"));
        AddStatusTextSourceFilePath(statusSourcePaths, magicTypePaths, Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "magictype.txt"));
    }

    private static void AddStatusTextSourceFilePath(
        List<string> statusSourcePaths,
        List<string> magicTypePaths,
        string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.Equals("StatusTips.ini", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("MagicEffect.ini", StringComparison.OrdinalIgnoreCase))
        {
            statusSourcePaths.Add(path);
            return;
        }

        if (fileName.Equals("magictype.txt", StringComparison.OrdinalIgnoreCase))
        {
            magicTypePaths.Add(path);
        }
    }

    private static IReadOnlyList<string> ResolveStrResSourceBackedPaths(
        string[] args,
        string clientPath,
        string strResPath)
    {
        var paths = new List<string>
        {
            strResPath,
        };

        for (var index = 2; index < args.Length; index++)
        {
            AddStrResSourceBackedPath(paths, args[index]);
        }

        return ExistingDistinctPaths(paths);
    }

    private static void AddStrResSourceBackedPath(List<string> paths, string rawPath)
    {
        var path = Path.GetFullPath(rawPath);
        if (!Directory.Exists(path))
        {
            paths.Add(path);
            return;
        }

        paths.Add(Path.Combine(path, "StrRes.ini"));
        paths.Add(Path.Combine(path, "ini", "StrRes.ini"));
    }

    private static IReadOnlyList<string> ExistingDistinctPaths(IEnumerable<string> paths)
    {
        return paths
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Where(ClientTextSourcePathFence.IsAllowedSourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ExistingDistinctFilesOrDirectories(IEnumerable<string> paths)
    {
        return paths
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Select(Path.GetFullPath)
            .Where(ClientTextSourcePathFence.IsAllowedSourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveCnResSourceBackedPaths(
        string[] args,
        string clientPath,
        IEnumerable<string> defaultTargets)
    {
        var paths = new List<string>();
        if (args.Length <= 2)
        {
            paths.AddRange(defaultTargets);
        }
        else
        {
            for (var index = 2; index < args.Length; index++)
            {
                var path = Path.GetFullPath(args[index]);
                if (!Directory.Exists(path))
                {
                    paths.Add(path);
                    continue;
                }

                paths.Add(Path.Combine(path, "Cn_Res.ini"));
                paths.Add(Path.Combine(path, "Server_Cn_Res.ini"));
                paths.Add(Path.Combine(path, "ini", "Cn_Res.ini"));
                paths.Add(Path.Combine(path, "ini", "Server_Cn_Res.ini"));
            }
        }

        return paths
            .Select(path => Directory.Exists(path) ? Path.Combine(path, "Cn_Res.ini") : path)
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Where(ClientTextSourcePathFence.IsAllowedSourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsClientTextCandidate(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".dat", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".lua", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record GouYuSkillTextSourcePaths(
        IReadOnlyList<string> MagicEffectPaths,
        IReadOnlyList<string> MagicTypePaths,
        IReadOnlyList<string> GouYuTypePaths);

    private sealed record StatusTextSourceBackedPaths(
        IReadOnlyList<string> StatusSourcePaths,
        IReadOnlyList<string> MagicTypePaths);
}
