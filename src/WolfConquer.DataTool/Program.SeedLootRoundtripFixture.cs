using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

internal static partial class Program
{
    static int SeedLootRoundtripFixture(string[] args)
    {
        LootRoundtripSeedOptions options;
        try
        {
            options = ResolveLootRoundtripSeedOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Loot fixture rejected: " + ex.Message);
            return 1;
        }

        try
        {
            WriteLootRoundtripSeed(options);
            Console.WriteLine("Isolated loot roundtrip file fixture created.");
            Console.WriteLine("dbServerRoot=" + options.Destination);
            Console.WriteLine("accountKey=" + options.AccountKey.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("characterUid=" + options.CharacterUid.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("itemUid=" + options.ItemUid.ToString(CultureInfo.InvariantCulture) + " itemId=1001000 stack=1");
            Console.WriteLine("Boundary: isolated file fixture only; no authentication account, password, admin privilege or live-client proof was created.");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            // A partially written, newly owned staging directory is retained on
            // failure. Never recursively clean an unchecked path or reuse a target.
            Console.Error.WriteLine("Loot fixture creation failed without replacing an existing destination: " + ex.Message);
            return 2;
        }
    }

    private static LootRoundtripSeedOptions ResolveLootRoundtripSeedOptions(string[] args)
    {
        if (args.Length != 5)
            throw new ArgumentException("Usage: WolfConquer.DataTool seed-loot-roundtrip-fixture <NEW-absolute-DBServer-dir> <account-key> <character-uid> <item-uid>");

        uint accountKey = ParseLootRoundtripSeedId(args[2], "account-key");
        uint characterUid = ParseLootRoundtripSeedId(args[3], "character-uid");
        uint itemUid = ParseLootRoundtripSeedId(args[4], "item-uid");
        // This is the real CharacterSnapshotLoader's existing account/UID binding,
        // not an invented account mapping or an authentication bypass.
        if (accountKey != characterUid)
            throw new ArgumentException("account-key must equal character-uid for the existing snapshot loader.");
        if (itemUid == characterUid)
            throw new ArgumentException("item-uid must differ from the account/character UID.");

        string destination = ResolveNewLootRoundtripDirectory(args[1]);
        return new(destination, accountKey, characterUid, itemUid);
    }

    private static uint ParseLootRoundtripSeedId(string raw, string name)
    {
        if (!uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) || value == 0)
            throw new FormatException(name + " must be a positive decimal UInt32.");
        return value;
    }

    private static string ResolveNewLootRoundtripDirectory(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !Path.IsPathFullyQualified(raw) ||
            raw.StartsWith(@"\\", StringComparison.Ordinal))
            throw new ArgumentException("The fixture destination must be a new absolute local directory, not a relative, network or device path.");
        var parts = raw.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part is "." or "..") ||
            OperatingSystem.IsWindows() && parts.Any(part => part.EndsWith(' ') || part.EndsWith('.')))
            throw new ArgumentException("The fixture destination cannot contain dot segments or Windows trailing-dot/space aliases.");

        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw));
        string root = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(destination)!);
        if (destination.StartsWith(@"\\", StringComparison.Ordinal))
            throw new ArgumentException("Network and device destinations are not local disposable fixture roots.");
        if (OperatingSystem.IsWindows() && Path.GetRelativePath(root, destination)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
                .Any(part => part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new ArgumentException("The fixture destination contains an invalid filename or alternate-stream component.");
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(destination, root, comparison))
            throw new ArgumentException("A filesystem root cannot be a disposable fixture destination.");
        EnsureLootRoundtripPathAbsent(destination);
        string? parent = Path.GetDirectoryName(destination);
        if (parent is null || !Directory.Exists(parent))
            throw new ArgumentException("The fixture destination's parent must already exist.");
        EnsureLootRoundtripPhysicalDirectoryChain(parent);
        return destination;
    }

    private static void EnsureLootRoundtripPathAbsent(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
        }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }
        throw new IOException("The fixture destination already exists; it will not be read, reused or overwritten: " + path);
    }

    private static void EnsureLootRoundtripPhysicalDirectoryChain(string path)
    {
        for (DirectoryInfo? directory = new(path); directory is not null; directory = directory.Parent)
        {
            var attributes = File.GetAttributes(directory.FullName);
            if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
                throw new IOException("Fixture paths cannot traverse reparse points or non-directories: " + directory.FullName);
        }
    }

    private static void WriteLootRoundtripSeed(LootRoundtripSeedOptions options)
    {
        string parent = Path.GetDirectoryName(options.Destination)!;
        EnsureLootRoundtripPhysicalDirectoryChain(parent);
        EnsureLootRoundtripPathAbsent(options.Destination);
        // Same-volume rename is the final non-overwriting publication. Existing
        // destination contents are never opened, including a creation-time race.
        string staging = Path.Combine(parent, ".loot-roundtrip-seed-" + Guid.NewGuid().ToString("N"));
        EnsureLootRoundtripPathAbsent(staging);
        Directory.CreateDirectory(staging);
        EnsureLootRoundtripPhysicalDirectoryChain(staging);
        foreach (var name in new[] { "Users", "PlayersItems", "PlayersSpells", "PlayersProfs", "PlayersCombatGear", "Quests" })
            Directory.CreateDirectory(Path.Combine(staging, name));

        string characterText = CreateLootRoundtripCharacterText(options, DateTime.Now.DayOfYear);
        var parsed = CharacterSnapshotParser.ParseText(characterText);
        if (parsed.Uid != options.CharacterUid)
            throw new InvalidDataException("The new fixture character did not pass normal snapshot parsing.");
        File.WriteAllText(Path.Combine(staging, "Users", options.AccountKey.ToString(CultureInfo.InvariantCulture) + ".ini"),
            characterText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var items = new PlayerItemSnapshot(
            [new PlayerItemSnapshotItem
            {
                Uid = options.ItemUid,
                ItemId = 1001000,
                StackSize = 1,
                // Current seed catalog item1001000 field21 is1. No catalog or
                // reference/runtime save is opened by this fixture command.
                Durability = 1,
                MaximumDurability = 1,
            }], [], [], [], []);
        new PlayerItemSnapshotLoader(Path.Combine(staging, "PlayersItems")).SaveByCharacterUid(options.CharacterUid, items);
        new PlayerSpellSnapshotLoader(Path.Combine(staging, "PlayersSpells")).SaveByCharacterUid(options.CharacterUid, PlayerSpellSnapshot.Empty);
        new PlayerProficiencySnapshotLoader(Path.Combine(staging, "PlayersProfs")).SaveByCharacterUid(options.CharacterUid, PlayerProficiencySnapshot.Empty);
        new CombatGearSnapshotLoader(Path.Combine(staging, "PlayersCombatGear")).SaveByCharacterUid(options.CharacterUid, CombatGearSnapshot.Empty);
        new PlayerQuestSnapshotLoader(Path.Combine(staging, "Quests")).SaveByCharacterUid(options.CharacterUid, PlayerQuestSnapshot.Empty);

        var loader = new CharacterSnapshotLoader(Path.Combine(staging, "Users"));
        var loaded = loader.LoadByAccountId(options.AccountKey);
        var reloadedItems = loader.LoadPlayerItemsByCharacterUid(options.CharacterUid);
        if (loaded?.Uid != options.CharacterUid ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(items).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(reloadedItems)))
            throw new InvalidDataException("The new fixture failed normal character/item roundtrip validation.");
        EnsureLootRoundtripPhysicalDirectoryChain(parent);
        EnsureLootRoundtripPathAbsent(options.Destination);
        Directory.Move(staging, options.Destination);
    }

    private static string CreateLootRoundtripCharacterText(LootRoundtripSeedOptions options, int dayOfYear) => $$"""
        [Character]
        UID={{options.CharacterUid.ToString(CultureInfo.InvariantCulture)}}
        Name=LootFixture
        Body=1003
        Face=154
        Haire=27
        Class=10
        FirstClass=0
        SecoundeClass=0
        Reborn=0
        Level=120
        Experience=0
        Map=1002
        X=410
        Y=354
        PMap=1002
        PMapX=410
        PMapY=354
        Strength=100
        Agility=50
        Vitaliti=100
        Spirit=0
        Atributes=0
        MinHitPoints=1000
        MinMana=100
        Money=100
        ConquerPoints=50
        BoundConquerPoints=0
        BankCps=0
        PkPoints=0
        Online=0
        SecurityPass=0,0,0
        WHMoney=0
        DynamicID=0
        Day={{dayOfYear.ToString(CultureInfo.InvariantCulture)}}

        """;

    private sealed record LootRoundtripSeedOptions(string Destination, uint AccountKey, uint CharacterUid, uint ItemUid);
}
