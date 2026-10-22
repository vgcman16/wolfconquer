using System.Globalization;

namespace WolfConquer.Core.Data;

public static class CharacterSnapshotParser
{
    private const string CharacterSection = "Character";
    private const string LegacyMainFlagStorageKey = "ClaimStateGift";
    private const string PaidNobilitySection = "PaidNobility";
    private const string RuntimeMainFlagAttribute = "MainFlag";

    public static CharacterSnapshot Parse(IniDocument document, string sourceFile = "")
    {
        ArgumentNullException.ThrowIfNull(document);

        var values = document.GetSection(CharacterSection);
        var paidNobility = document.Sections.TryGetValue(PaidNobilitySection, out var section)
            ? Copy(section)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var attributes = Copy(values);
        if (values.TryGetValue(LegacyMainFlagStorageKey, out var mainFlag))
        {
            attributes[RuntimeMainFlagAttribute] = mainFlag;
        }
        CharacterSnapshotEpicTrojanSchema.ProjectRuntimeAttributes(values, attributes);

        var minMana = GetInt64(values, "MinMana");

        return new CharacterSnapshot(
            GetInt64(values, "UID"),
            GetString(values, "Name"),
            GetInt64(values, "Body"),
            GetInt64(values, "Face"),
            GetInt64(values, "Haire"),
            GetInt64(values, "Class"),
            GetInt64(values, "FirstClass"),
            GetInt64(values, "SecoundeClass"),
            GetInt64(values, "Reborn"),
            GetInt64(values, "Level"),
            GetInt64(values, "Experience"),
            GetInt64(values, "Map"),
            GetInt64(values, "X"),
            GetInt64(values, "Y"),
            GetInt64(values, "PMap"),
            GetInt64(values, "PMapX"),
            GetInt64(values, "PMapY"),
            GetInt64(values, "Strength"),
            GetInt64(values, "Agility"),
            GetInt64(values, "Vitaliti"),
            GetInt64(values, "Spirit"),
            GetInt64(values, "Atributes"),
            GetInt64(values, "MinHitPoints"),
            minMana,
            GetInt64(values, "Money"),
            GetInt64(values, "ConquerPoints"),
            GetInt64(values, "BoundConquerPoints"),
            GetInt64(values, "BankCps"),
            GetInt64(values, "PkPoints"),
            GetInt64(values, "Online"),
            sourceFile,
            attributes,
            paidNobility)
        {
            ExpBallUsed = GetInt64OrDefault(values, "ExpBallUsed"),
            MaxMana = GetInt64OrDefault(values, "MaxMana", minMana),
        };
    }

    public static CharacterSnapshot ParseText(string content, string sourceFile = "")
    {
        return Parse(IniDocument.Parse(content), sourceFile);
    }

    private static string GetString(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value))
        {
            throw new FormatException($"Character snapshot is missing '{key}'.");
        }

        return value;
    }

    private static long GetInt64(IReadOnlyDictionary<string, string> values, string key)
    {
        var value = GetString(values, key);
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid character snapshot integer '{key}': '{value}'.");
        }

        return parsed;
    }

    private static long GetInt64OrDefault(IReadOnlyDictionary<string, string> values, string key, long defaultValue = 0)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid character snapshot integer '{key}': '{value}'.");
        }

        return parsed;
    }

    private static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string> values)
    {
        return new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class CharacterSnapshotLoader
{
    private const long LegacyFirstCharacterFace = 154;
    private const long LegacyFirstCharacterHair = 27;
    private const long LegacyFirstCharacterLevel = 1;
    private const long LegacyFirstCharacterMap = 1002;
    private const long LegacyFirstCharacterX = 410;
    private const long LegacyFirstCharacterY = 350;
    private const ulong LegacyNobilityKnightDonation = 30_000_000;
    private const ulong LegacyNobilityBaronDonation = 100_000_000;
    private const ulong LegacyNobilityEarlDonation = 200_000_000;
    private const byte LegacyNobilityRankSerf = 0;
    private const byte LegacyNobilityRankKnight = 1;
    private const byte LegacyNobilityRankBaron = 3;
    private const byte LegacyNobilityRankEarl = 5;
    private const int LegacyNobilityDefaultPosition = -1;

    private readonly string _usersRoot;
    private readonly PlayerItemSnapshotLoader? _playerItemLoader;
    private readonly PlayerSpellSnapshotLoader? _playerSpellLoader;
    private readonly PlayerProficiencySnapshotLoader? _playerProficiencyLoader;
    private readonly CombatGearSnapshotLoader? _combatGearLoader;
    private readonly PlayerQuestSnapshotLoader? _playerQuestLoader;

    public CharacterSnapshotLoader(string usersRoot)
        : this(
            usersRoot,
            TryDeriveSiblingRoot(usersRoot, "PlayersItems"),
            TryDeriveSiblingRoot(usersRoot, "PlayersSpells"),
            TryDeriveSiblingRoot(usersRoot, "PlayersProfs"),
            TryDeriveSiblingRoot(usersRoot, "PlayersCombatGear"),
            TryDeriveSiblingRoot(usersRoot, "Quests"))
    {
    }

    public CharacterSnapshotLoader(string usersRoot, string? playerItemsRoot)
        : this(
            usersRoot,
            playerItemsRoot,
            TryDeriveSiblingRoot(usersRoot, "PlayersSpells"),
            TryDeriveSiblingRoot(usersRoot, "PlayersProfs"),
            TryDeriveSiblingRoot(usersRoot, "PlayersCombatGear"),
            TryDeriveSiblingRoot(usersRoot, "Quests"))
    {
    }

    public CharacterSnapshotLoader(string usersRoot, string? playerItemsRoot, string? playerSpellsRoot)
        : this(
            usersRoot,
            playerItemsRoot,
            playerSpellsRoot,
            TryDeriveSiblingRoot(usersRoot, "PlayersProfs"),
            TryDeriveSiblingRoot(usersRoot, "PlayersCombatGear"),
            TryDeriveSiblingRoot(usersRoot, "Quests"))
    {
    }

    public CharacterSnapshotLoader(
        string usersRoot,
        string? playerItemsRoot,
        string? playerSpellsRoot,
        string? playerProficienciesRoot)
        : this(
            usersRoot,
            playerItemsRoot,
            playerSpellsRoot,
            playerProficienciesRoot,
            TryDeriveSiblingRoot(usersRoot, "PlayersCombatGear"),
            TryDeriveSiblingRoot(usersRoot, "Quests"))
    {
    }

    public CharacterSnapshotLoader(
        string usersRoot,
        string? playerItemsRoot,
        string? playerSpellsRoot,
        string? playerProficienciesRoot,
        string? playersCombatGearRoot)
        : this(
            usersRoot,
            playerItemsRoot,
            playerSpellsRoot,
            playerProficienciesRoot,
            playersCombatGearRoot,
            TryDeriveSiblingRoot(usersRoot, "Quests"))
    {
    }

    public CharacterSnapshotLoader(
        string usersRoot,
        string? playerItemsRoot,
        string? playerSpellsRoot,
        string? playerProficienciesRoot,
        string? playersCombatGearRoot,
        string? playerQuestsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usersRoot);
        _usersRoot = ResolveLegacyUsersRoot(usersRoot);
        _playerItemLoader = string.IsNullOrWhiteSpace(playerItemsRoot)
            ? null
            : new PlayerItemSnapshotLoader(playerItemsRoot);
        _playerSpellLoader = string.IsNullOrWhiteSpace(playerSpellsRoot)
            ? null
            : new PlayerSpellSnapshotLoader(playerSpellsRoot);
        _playerProficiencyLoader = string.IsNullOrWhiteSpace(playerProficienciesRoot)
            ? null
            : new PlayerProficiencySnapshotLoader(playerProficienciesRoot);
        _combatGearLoader = string.IsNullOrWhiteSpace(playersCombatGearRoot)
            ? null
            : new CombatGearSnapshotLoader(playersCombatGearRoot);
        _playerQuestLoader = string.IsNullOrWhiteSpace(playerQuestsRoot)
            ? null
            : new PlayerQuestSnapshotLoader(playerQuestsRoot);
    }

    public string UsersRoot => _usersRoot;

    public string? PlayerItemsRoot => _playerItemLoader?.PlayerItemsRoot;

    public string? PlayerSpellsRoot => _playerSpellLoader?.PlayerSpellsRoot;

    public string? PlayerProficienciesRoot => _playerProficiencyLoader?.PlayerProficienciesRoot;

    public string? PlayersCombatGearRoot => _combatGearLoader?.PlayersCombatGearRoot;

    public string? PlayerQuestsRoot => _playerQuestLoader?.PlayerQuestsRoot;

    public CharacterSnapshot? LoadByAccountId(long accountId)
    {
        var result = TryLoadByAccountId(accountId);
        if (result.Status == CharacterSnapshotLoadStatus.MissingSnapshot)
        {
            return null;
        }

        if (result.Succeeded)
        {
            return result.Snapshot;
        }

        throw new FormatException(
            $"Character snapshot load failed for account {accountId} file '{Path.GetFileName(result.Path)}' " +
            $"reason={result.Reason}: {result.Detail}");
    }

    public CharacterSnapshotLoadResult TryLoadByAccountId(long accountId)
    {
        var path = Path.Combine(_usersRoot, accountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!File.Exists(path))
        {
            return new CharacterSnapshotLoadResult(
                null,
                CharacterSnapshotLoadStatus.MissingSnapshot,
                accountId,
                path,
                "snapshotMissing",
                "Character snapshot file was not found.");
        }

        CharacterSnapshot snapshot;
        try
        {
            snapshot = CharacterSnapshotParser.ParseText(
                CharacterSnapshotFileEncoding.ReadAllText(path, out _),
                Path.GetFileName(path));
        }
        catch (UnauthorizedAccessException ex)
        {
            return new CharacterSnapshotLoadResult(
                null,
                CharacterSnapshotLoadStatus.SnapshotReadDenied,
                accountId,
                path,
                "snapshotReadDenied",
                ex.Message);
        }
        catch (IOException ex)
        {
            return new CharacterSnapshotLoadResult(
                null,
                CharacterSnapshotLoadStatus.SnapshotReadFailed,
                accountId,
                path,
                "snapshotReadFailed",
                ex.Message);
        }
        catch (FormatException ex)
        {
            return new CharacterSnapshotLoadResult(
                null,
                CharacterSnapshotLoadStatus.SnapshotParseFailed,
                accountId,
                path,
                "snapshotParseFailed",
                ex.Message);
        }

        if (snapshot.Uid != accountId)
        {
            return new CharacterSnapshotLoadResult(
                null,
                CharacterSnapshotLoadStatus.UidMismatch,
                accountId,
                path,
                "uidMismatch",
                $"Character snapshot file '{Path.GetFileName(path)}' has UID {snapshot.Uid}, expected {accountId}.");
        }

        snapshot = RestoreExpiredPaidNobility(path, snapshot);
        return new CharacterSnapshotLoadResult(
            snapshot with { PlayerQuests = LoadPlayerQuestsByCharacterUid(snapshot.Uid) },
            CharacterSnapshotLoadStatus.Success,
            accountId,
            path,
            "success",
            string.Empty);
    }

    public CharacterSnapshot CreateLegacyFirstCharacterSnapshot(
        long uid,
        string name,
        long body,
        long classCode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(uid);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Directory.CreateDirectory(_usersRoot);
        var path = Path.Combine(_usersRoot, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        var content = CreateLegacyFirstCharacterSnapshotContent(uid, name, body, classCode);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, CharacterSnapshotFileEncoding.DefaultWriteEncoding))
        {
            writer.Write(content);
        }

        return LoadByAccountId(uid) ??
            throw new FormatException($"Created character snapshot '{Path.GetFileName(path)}' could not be loaded.");
    }

    public int CountSnapshotFiles()
    {
        return Directory.Exists(_usersRoot)
            ? Directory.EnumerateFiles(_usersRoot, "*.ini").Count()
            : 0;
    }

    public PlayerItemSnapshot LoadPlayerItemsByCharacterUid(long characterUid)
    {
        return _playerItemLoader?.LoadByCharacterUid(characterUid) ?? PlayerItemSnapshot.Empty;
    }

    internal PlayerItemSnapshot LoadPlayerItemsByCharacterUid(
        long characterUid, Action<byte[]?> captureRawLoad)
    {
        ArgumentNullException.ThrowIfNull(captureRawLoad);
        return _playerItemLoader?.LoadByCharacterUid(characterUid, captureRawLoad) ?? PlayerItemSnapshot.Empty;
    }

    public PlayerSpellSnapshot LoadPlayerSpellsByCharacterUid(long characterUid)
    {
        return _playerSpellLoader?.LoadByCharacterUid(characterUid) ?? PlayerSpellSnapshot.Empty;
    }

    public PlayerProficiencySnapshot LoadPlayerProficienciesByCharacterUid(long characterUid)
    {
        return _playerProficiencyLoader?.LoadByCharacterUid(characterUid) ?? PlayerProficiencySnapshot.Empty;
    }

    public CombatGearSnapshot LoadCombatGearByCharacterUid(long characterUid)
    {
        return _combatGearLoader?.LoadByCharacterUid(characterUid) ?? CombatGearSnapshot.Empty;
    }

    public PlayerQuestSnapshot LoadPlayerQuestsByCharacterUid(long characterUid)
    {
        return _playerQuestLoader?.LoadByCharacterUid(characterUid) ?? PlayerQuestSnapshot.Empty;
    }

    private static string? TryDeriveSiblingRoot(string usersRoot, string siblingName)
    {
        var fullPath = Path.GetFullPath(usersRoot);
        var trimmed = Path.TrimEndingDirectorySeparator(fullPath);
        if (!string.Equals(Path.GetFileName(trimmed), "Users", StringComparison.OrdinalIgnoreCase))
        {
            return TryFindLegacyDbServerSiblingRoot(trimmed, siblingName);
        }

        var dbRoot = Path.GetDirectoryName(trimmed);
        return string.IsNullOrEmpty(dbRoot)
            ? null
            : Path.Combine(dbRoot, siblingName);
    }

    private static string ResolveLegacyUsersRoot(string usersRoot)
    {
        var fullPath = Path.GetFullPath(usersRoot);
        var trimmed = Path.TrimEndingDirectorySeparator(fullPath);
        if (string.Equals(Path.GetFileName(trimmed), "DBServer", StringComparison.OrdinalIgnoreCase))
        {
            var usersChild = Path.Combine(trimmed, "Users");
            if (Directory.Exists(usersChild))
            {
                return usersChild;
            }
        }

        return usersRoot;
    }

    private static string? TryFindLegacyDbServerSiblingRoot(string usersRoot, string siblingName)
    {
        var current = usersRoot;
        for (var depth = 0; depth < 6 && !string.IsNullOrEmpty(current); depth++)
        {
            var candidate = Path.Combine(current, siblingName);
            if (Directory.Exists(candidate) &&
                IsLegacyDbServerRoot(current))
            {
                return candidate;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    private static bool IsLegacyDbServerRoot(string path)
    {
        return string.Equals(Path.GetFileName(path), "DBServer", StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(Path.Combine(path, "Users"));
    }

    private static string CreateLegacyFirstCharacterSnapshotContent(
        long uid,
        string name,
        long body,
        long classCode)
    {
        var day = DateTime.Now.DayOfYear.ToString(CultureInfo.InvariantCulture);
        var starterStats = LegacyFirstCharacterStarterProfiles.CreateStats(classCode);
        return string.Create(
            CultureInfo.InvariantCulture,
            $$"""
            [Character]
            UID={{uid}}
            Body={{body}}
            Face={{LegacyFirstCharacterFace}}
            Name={{name}}
            Class={{classCode}}
            Map={{LegacyFirstCharacterMap}}
            X={{LegacyFirstCharacterX}}
            Y={{LegacyFirstCharacterY}}
            FirstClass=0
            SecoundeClass=0
            Conquer=0
            Reborn=0
            Level={{LegacyFirstCharacterLevel}}
            Haire={{LegacyFirstCharacterHair}}
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility={{starterStats.Agility}}
            Strength={{starterStats.Strength}}
            Vitaliti={{starterStats.Vitality}}
            Spirit={{starterStats.Spirit}}
            Atributes=0
            MinHitPoints={{starterStats.Hitpoints}}
            MinMana={{starterStats.Mana}}
            MaxMana={{starterStats.Mana}}
            ConquerPoints={{LegacyFirstCharacterStarterProfiles.TunedConquerPoints}}
            BankCps=0
            BoundConquerPoints=0
            Money={{LegacyFirstCharacterStarterProfiles.TunedMoney}}
            GuildDontion=0
            PkPoints=0
            Online=0
            Day={{day}}
            DonationNobility=0

            [PaidNobility]
            Rank=Serf
            Identifier=0
            LastNobilityDonation=0
            PaidRank=0
            PeriodTime=0
            IsActive=False
            Position=-1
            """);
    }

    private static CharacterSnapshot RestoreExpiredPaidNobility(string path, CharacterSnapshot snapshot)
    {
        if (!TryReadActivePaidNobility(snapshot.PaidNobility, out var active) ||
            !active ||
            !IsPaidNobilityExpired(snapshot.PaidNobility))
        {
            return snapshot;
        }

        var restoredDonation = ReadUInt64(snapshot.PaidNobility, "LastNobilityDonation");
        var restoredRank = ComputeLegacyRegularNobilityRank(restoredDonation);
        var attributes = new Dictionary<string, string>(
            snapshot.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["DonationNobility"] = restoredDonation.ToString(CultureInfo.InvariantCulture),
            ["LastNobilityDonation"] = restoredDonation.ToString(CultureInfo.InvariantCulture),
            ["NobilityRank"] = restoredRank.ToString(CultureInfo.InvariantCulture),
            ["NobilityPosition"] = LegacyNobilityDefaultPosition.ToString(CultureInfo.InvariantCulture),
        };
        var paidNobility = new Dictionary<string, string>(
            snapshot.PaidNobility,
            StringComparer.OrdinalIgnoreCase)
        {
            ["Identifier"] = "0",
            ["LastNobilityDonation"] = "0",
            ["PaidRank"] = "0",
            ["PeriodTime"] = "0",
            ["IsActive"] = bool.FalseString,
            ["Rank"] = "Serf",
            ["Position"] = LegacyNobilityDefaultPosition.ToString(CultureInfo.InvariantCulture),
        };

        var updated = snapshot with
        {
            Attributes = attributes,
            PaidNobility = paidNobility,
        };

        TryPersistExpiredPaidNobility(path, snapshot.Uid, restoredDonation, restoredRank);
        return updated;
    }

    private static bool TryReadActivePaidNobility(
        IReadOnlyDictionary<string, string> paidNobility,
        out bool active)
    {
        active = false;
        if (!paidNobility.TryGetValue("IsActive", out var raw))
        {
            return false;
        }

        if (bool.TryParse(raw, out active))
        {
            return true;
        }

        if (byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            active = numeric != 0;
            return true;
        }

        return false;
    }

    private static bool IsPaidNobilityExpired(IReadOnlyDictionary<string, string> paidNobility)
    {
        var periodTimeBinary = ReadInt64(paidNobility, "PeriodTime");
        DateTime periodTime;
        try
        {
            periodTime = DateTime.FromBinary(periodTimeBinary);
        }
        catch (ArgumentException)
        {
            periodTime = DateTime.MinValue;
        }

        return DateTime.Now >= periodTime;
    }

    private static bool TryPersistExpiredPaidNobility(
        string path,
        long expectedCharacterUid,
        ulong restoredDonation,
        byte restoredRank)
    {
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DonationNobility"] = restoredDonation.ToString(CultureInfo.InvariantCulture),
            ["LastNobilityDonation"] = restoredDonation.ToString(CultureInfo.InvariantCulture),
            ["NobilityRank"] = restoredRank.ToString(CultureInfo.InvariantCulture),
            ["NobilityPosition"] = LegacyNobilityDefaultPosition.ToString(CultureInfo.InvariantCulture),
        };
        var paidNobilityValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Identifier"] = "0",
            ["LastNobilityDonation"] = "0",
            ["PaidRank"] = "0",
            ["PeriodTime"] = "0",
            ["IsActive"] = bool.FalseString,
            ["Rank"] = "Serf",
            ["Position"] = LegacyNobilityDefaultPosition.ToString(CultureInfo.InvariantCulture),
        };

        return CharacterSnapshotFileCoordinator.TryUpdateSections(
            path,
            expectedCharacterUid,
            [
                new CharacterSnapshotSectionUpdate(
                    "Character",
                    characterValues,
                    CreateIfMissing: false,
                    MissingReason: "characterSectionMissing"),
                new CharacterSnapshotSectionUpdate(
                    "PaidNobility",
                    paidNobilityValues,
                    CreateIfMissing: true,
                    MissingReason: "paidNobilitySectionMissing"),
            ],
            detailedIoFailures: false,
            out _);
    }

    private static byte ComputeLegacyRegularNobilityRank(ulong donation)
    {
        if (donation >= LegacyNobilityEarlDonation)
        {
            return LegacyNobilityRankEarl;
        }

        if (donation >= LegacyNobilityBaronDonation)
        {
            return LegacyNobilityRankBaron;
        }

        if (donation >= LegacyNobilityKnightDonation)
        {
            return LegacyNobilityRankKnight;
        }

        return LegacyNobilityRankSerf;
    }

    private static long ReadInt64(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) &&
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static ulong ReadUInt64(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) &&
            ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }
}
