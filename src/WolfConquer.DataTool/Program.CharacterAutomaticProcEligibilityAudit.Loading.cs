using System.Globalization;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

internal static partial class CharacterAutomaticProcEligibilityAuditor
{
    private static LoadedCharacter LoadCharacter(CharacterAutomaticProcEligibilityAuditOptions options)
    {
        string path;
        if (!string.IsNullOrWhiteSpace(options.CharacterSnapshotPath))
        {
            path = Path.GetFullPath(options.CharacterSnapshotPath);
        }
        else
        {
            var usersRoot = ResolveUsersRoot(options.UsersRoot);
            if (!Directory.Exists(usersRoot))
            {
                throw new DirectoryNotFoundException($"Character Users root not found: {usersRoot}");
            }

            if (options.CharacterUid is { } uid)
            {
                path = Path.Combine(usersRoot, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
            }
            else
            {
                var matches = new List<LoadedCharacter>();
                foreach (var candidatePath in Directory.EnumerateFiles(usersRoot, "*.ini")
                             .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase))
                {
                    CharacterSnapshot candidate;
                    try
                    {
                        candidate = ParseCharacterSnapshot(candidatePath);
                    }
                    catch (FormatException)
                    {
                        continue;
                    }

                    if (string.Equals(candidate.Name, options.CharacterName, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(new LoadedCharacter(candidatePath, candidate));
                    }
                }

                if (matches.Count == 0)
                {
                    throw new InvalidDataException(
                        $"Character name '{options.CharacterName}' was not found in '{usersRoot}'.");
                }

                if (matches.Count > 1)
                {
                    throw new InvalidDataException(
                        $"Character name '{options.CharacterName}' matched multiple snapshots: " +
                        string.Join(", ", matches.Select(match => Path.GetFileName(match.Path))));
                }

                return matches[0];
            }
        }

        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Character snapshot was not found.", path);
        }

        var snapshot = ParseCharacterSnapshot(path);
        if (options.CharacterUid is { } requestedUid && snapshot.Uid != requestedUid)
        {
            throw new InvalidDataException(
                $"Character snapshot '{path}' has UID {snapshot.Uid}, expected {requestedUid}.");
        }

        return new LoadedCharacter(path, snapshot);
    }

    private static CharacterSnapshot ParseCharacterSnapshot(string path)
    {
        return CharacterSnapshotParser.ParseText(ReadCharacterSnapshotText(path), Path.GetFileName(path));
    }

    private static string ReadCharacterSnapshotText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var offset = bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble())
            ? Encoding.UTF8.GetPreamble().Length
            : 0;
        try
        {
            return StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    private static LoadedSidecars LoadSidecars(
        CharacterAutomaticProcEligibilityAuditOptions options,
        LoadedCharacter character)
    {
        var characterDirectory = Path.GetDirectoryName(character.Path) ??
            throw new InvalidDataException($"Character snapshot has no parent directory: {character.Path}");
        var inferredLoader = new CharacterSnapshotLoader(characterDirectory);
        var spellsPath = ResolveSidecarPath(
            options.SpellsSnapshotPath,
            inferredLoader.PlayerSpellsRoot,
            character.Snapshot.Uid);
        var itemsPath = ResolveSidecarPath(
            options.ItemsSnapshotPath,
            inferredLoader.PlayerItemsRoot,
            character.Snapshot.Uid);

        RequireExplicitSnapshot(options.SpellsSnapshotPath, spellsPath, "Player spell");
        RequireExplicitSnapshot(options.ItemsSnapshotPath, itemsPath, "Player item");

        var spellsExists = spellsPath is not null && File.Exists(spellsPath);
        var itemsExists = itemsPath is not null && File.Exists(itemsPath);
        var spells = spellsExists
            ? PlayerSpellSnapshotLoader.Parse(File.ReadAllBytes(spellsPath!), Path.GetFileName(spellsPath!))
            : PlayerSpellSnapshot.Empty;
        var items = itemsExists
            ? PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(itemsPath!), Path.GetFileName(itemsPath!))
            : PlayerItemSnapshot.Empty;

        return new LoadedSidecars(
            spells,
            items,
            new CharacterAutomaticProcSourceReport(
                spellsPath,
                spellsExists,
                Explicit: !string.IsNullOrWhiteSpace(options.SpellsSnapshotPath),
                "legacyBinary",
                spells.Spells.Count),
            new CharacterAutomaticProcSourceReport(
                itemsPath,
                itemsExists,
                Explicit: !string.IsNullOrWhiteSpace(options.ItemsSnapshotPath),
                "legacyBinary",
                CountTopLevelItems(items)));
    }

    private static string? ResolveSidecarPath(string? explicitPath, string? inferredRoot, long uid)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        return string.IsNullOrWhiteSpace(inferredRoot)
            ? null
            : Path.GetFullPath(Path.Combine(inferredRoot, uid.ToString(CultureInfo.InvariantCulture) + ".bin"));
    }

    private static void RequireExplicitSnapshot(string? requestedPath, string? resolvedPath, string label)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath) &&
            (resolvedPath is null || !File.Exists(resolvedPath)))
        {
            throw new FileNotFoundException($"{label} snapshot was not found.", resolvedPath ?? requestedPath);
        }
    }

    private static int CountTopLevelItems(PlayerItemSnapshot items)
    {
        return items.InventoryItems.Count +
            items.RelicFuseItems.Count +
            items.MythsoulBagItems.Count +
            items.EquipmentItems.Count +
            items.RuneItems.Count +
            items.WardrobeItems.Count +
            items.WarehouseItems.Count;
    }

    private static LoadedMagic LoadMagic(
        CharacterAutomaticProcEligibilityAuditOptions options,
        string characterPath)
    {
        var explicitSource = !string.IsNullOrWhiteSpace(options.MagicSnapshotPath) ||
            !string.IsNullOrWhiteSpace(options.WorldDataRoot);
        string path;
        if (!string.IsNullOrWhiteSpace(options.MagicSnapshotPath))
        {
            path = Path.GetFullPath(options.MagicSnapshotPath);
        }
        else
        {
            var worldDataRoot = Path.GetFullPath(
                !string.IsNullOrWhiteSpace(options.WorldDataRoot)
                    ? options.WorldDataRoot
                    : WorldDataOptions.FromEnvironment().RootPath);
            path = Path.Combine(worldDataRoot, "magic.json");
            if (!File.Exists(path) && string.IsNullOrWhiteSpace(options.WorldDataRoot))
            {
                var legacyMagicPath = FindLegacyMagicPath(characterPath);
                if (legacyMagicPath is not null)
                {
                    path = legacyMagicPath;
                }
            }
        }

        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            if (explicitSource)
            {
                throw new FileNotFoundException("Magic metadata snapshot was not found.", path);
            }

            return new LoadedMagic(
                [],
                new CharacterAutomaticProcSourceReport(
                    path,
                    Exists: false,
                    Explicit: false,
                    "missing",
                    EntryCount: 0));
        }

        IReadOnlyList<MagicType> rows;
        string format;
        if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            rows = JsonSerializer.Deserialize<MagicType[]>(File.ReadAllText(path), MagicJsonOptions) ??
                throw new InvalidDataException($"Magic metadata JSON is empty: {path}");
            format = "exportedJson";
        }
        else
        {
            var parsed = new List<MagicType>();
            var lineNumber = 0;
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                {
                    continue;
                }

                try
                {
                    parsed.Add(MagicTypeParser.ParseLine(line));
                }
                catch (FormatException ex)
                {
                    throw new InvalidDataException(
                        $"Could not parse magic metadata '{path}' line {lineNumber}: {ex.Message}",
                        ex);
                }
            }

            rows = parsed;
            format = "legacyAtAtText";
        }

        return new LoadedMagic(
            rows,
            new CharacterAutomaticProcSourceReport(
                path,
                Exists: true,
                explicitSource,
                format,
                rows.Count));
    }

    private static string? FindLegacyMagicPath(string characterPath)
    {
        var current = Path.GetDirectoryName(characterPath);
        for (var depth = 0; depth < 6 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            var candidate = Path.Combine(current, "magictype.txt");
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    private static IReadOnlyList<ResolvedWeapon> ResolveCurrentWeapons(
        PlayerItemSnapshot items,
        bool alternateEquipmentActive)
    {
        var currentEquipment = LegacyEquipmentSelector.SelectCurrentEquipment(items, alternateEquipmentActive);
        return
        [
            ResolveCurrentWeapon(currentEquipment, "right", normalizedPosition: 4, mapBladeToSword: true),
            ResolveCurrentWeapon(currentEquipment, "left", normalizedPosition: 5, mapBladeToSword: false),
        ];
    }

    private static ResolvedWeapon ResolveCurrentWeapon(
        IReadOnlyList<PlayerItemSnapshotItem> currentEquipment,
        string hand,
        ushort normalizedPosition,
        bool mapBladeToSword)
    {
        var item = currentEquipment.FirstOrDefault(candidate =>
            candidate.Durability != 0 &&
            candidate.ItemId != 0 &&
            candidate.Position % 50 == normalizedPosition);
        if (item is null)
        {
            return new ResolvedWeapon(
                new CharacterAutomaticProcWeaponReport(
                    hand,
                    Present: false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                null);
        }

        var weaponType = checked((ushort)(item.ItemId / 1_000));
        var effectiveWeaponType = mapBladeToSword && weaponType == 421
            ? (ushort)420
            : weaponType;
        return new ResolvedWeapon(
            new CharacterAutomaticProcWeaponReport(
                hand,
                Present: true,
                item.Uid,
                item.ItemId,
                item.Position,
                item.Durability,
                item.Effect,
                weaponType,
                effectiveWeaponType),
            item);
    }

}
