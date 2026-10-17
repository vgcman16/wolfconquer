using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbeStateCountFiles(string dbServerPath, ImportProbeResult result)
    {
        try
        {
            var files = ReadStateCountFiles(dbServerPath);
            Console.WriteLine($"stateCountFiles: {files.Count:N0}");
            Console.WriteLine($"stateCountEntries: {files.Sum(file => file.Entries.Count):N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"stateCountFiles: {ex.Message}");
        }
    }

    static IReadOnlyList<StateCountFileData> ReadStateCountFiles(string dbServerPath)
    {
        return GetStateCountFileNames()
            .Select(fileName => StateCountFileParser.ParseFile(Path.Combine(dbServerPath, fileName)))
            .ToArray();
    }

    static IReadOnlyList<string> GetStateCountFileNames()
    {
        return
        [
            "WarriorRank.txt",
        "HWRanking.txt",
        "NinjaRank.txt",
        "Share.txt",
        "JiangHu.txt",
        "Crime.ini",
        "TeamArena.ini",
        "KOBoardRanks.txt",
        "CrossElitePk.ini",
        "BanIp.txt",
        "BanUID.txt",
        "BattleField.txt",
        "RechargeShop.txt",
    ];
    }

    static void ProbeBinarySeedFiles(string dbServerPath, ImportProbeResult result)
    {
        try
        {
            var files = ReadBinarySeedFiles(dbServerPath);
            Console.WriteLine($"binarySeedFiles: {files.Count:N0}");
            Console.WriteLine($"binarySeedBytes: {files.Sum(file => file.Length):N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"binarySeedFiles: {ex.Message}");
        }
    }

    static IReadOnlyList<BinarySeedFile> ReadBinarySeedFiles(string dbServerPath)
    {
        return GetBinarySeedFileNames()
            .Select(fileName => BinarySeedFileParser.ParseFile(Path.Combine(dbServerPath, fileName)))
            .ToArray();
    }

    static IReadOnlyList<string> GetBinarySeedFileNames()
    {
        return
        [
            "ClaimContainer.bin",
        "Insults.bin",
        "lottery.rar",
        "PerfectionRanking.bin",
        "RedeemContainer.bin",
        "SobNpcs.rar",
        "spirit_rate.rar",
    ];
    }

    static void ProbeEmptySeedFiles(string dbServerPath, ImportProbeResult result)
    {
        try
        {
            var files = ReadEmptySeedFiles(dbServerPath);
            Console.WriteLine($"emptySeedFiles: {files.Count:N0}");
            Console.WriteLine($"emptySeedBytes: {files.Sum(file => file.Length):N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"emptySeedFiles: {ex.Message}");
        }
    }

    static IReadOnlyList<EmptySeedFile> ReadEmptySeedFiles(string dbServerPath)
    {
        return GetEmptySeedFileNames()
            .Select(fileName => EmptySeedFileParser.ParseFile(Path.Combine(dbServerPath, fileName)))
            .ToArray();
    }

    static IReadOnlyList<string> GetEmptySeedFileNames()
    {
        return
        [
            "HeroRewards.txt",
        "ItemtypeSub.txt",
    ];
    }

    static void ProbeBackupSeedFiles(string dbServerPath, ImportProbeResult result)
    {
        try
        {
            var files = ReadBackupSeedFiles(dbServerPath);
            Console.WriteLine($"backupSeedFiles: {files.Count:N0}");
            Console.WriteLine($"backupSeedBytes: {files.Sum(file => file.Length):N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"backupSeedFiles: {ex.Message}");
        }
    }

    static IReadOnlyList<BackupSeedFile> ReadBackupSeedFiles(string dbServerPath)
    {
        return GetBackupSeedFileNames()
            .Select(fileName => BackupSeedFileParser.ParseFile(Path.Combine(dbServerPath, fileName)))
            .ToArray();
    }

    static IReadOnlyList<string> GetBackupSeedFileNames()
    {
        return
        [
            "SobNpcs.txt.bak",
    ];
    }
}
