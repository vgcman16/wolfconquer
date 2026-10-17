using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Data;

public sealed record CombatGearSnapshot(
    uint MasteryPoints,
    uint TaoistReiki,
    uint PirateExp,
    uint Lottery,
    int Count,
    CombatGearAirPowersSnapshot AirPowers,
    IReadOnlyList<CombatGearSnapshotItem> Items,
    CombatGearBlackJadeBagSnapshot BlackJadeBag)
{
    public static CombatGearSnapshot Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        CombatGearAirPowersSnapshot.Empty,
        [],
        CombatGearBlackJadeBagSnapshot.Empty);
}

public sealed record CombatGearAirPowersSnapshot(
    uint SenseLevel,
    uint ArmedLevel,
    uint OverLoadLevel,
    uint Points,
    uint SenseAipPower,
    uint ArmedAipPower,
    uint OverLoadAipPower)
{
    public static CombatGearAirPowersSnapshot Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);
}

public sealed record CombatGearSnapshotItem(
    uint ItemID,
    uint Level,
    uint Exp,
    uint Equip,
    uint Transform,
    uint MasteryScore,
    IReadOnlyList<IReadOnlyList<uint>> Animas,
    IReadOnlyList<uint> BlackJades);

public sealed record CombatGearBlackJadeBagSnapshot(
    int Count,
    IReadOnlyList<uint> BlackJades)
{
    public static CombatGearBlackJadeBagSnapshot Empty { get; } = new(0, []);
}

public sealed class CombatGearSnapshotLoader
{
    private const string TransactionMarker = ".combat-gear-file-";
    private readonly string _playersCombatGearRoot;

    public CombatGearSnapshotLoader(string playersCombatGearRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playersCombatGearRoot);
        _playersCombatGearRoot = playersCombatGearRoot;
    }

    public string PlayersCombatGearRoot => _playersCombatGearRoot;

    public CombatGearSnapshot LoadByCharacterUid(long characterUid)
    {
        var path = GetCharacterSnapshotPath(characterUid);
        return SnapshotFileLockCoordinator.ExecuteLocked(path, () => LoadFile(path));
    }

    public void SaveByCharacterUid(long characterUid, CombatGearSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var path = GetCharacterSnapshotPath(characterUid);
        SnapshotFileLockCoordinator.ExecuteLocked(
            path,
            () =>
            {
                Directory.CreateDirectory(_playersCombatGearRoot);
                SnapshotFilePersistence.WriteAllTextAtomically(
                    path,
                    ToLegacyText(snapshot),
                    TransactionMarker);
                return true;
            });
    }

    public static string ToLegacyText(CombatGearSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var builder = new StringBuilder();
        AppendSection(builder, "CombatGear");
        Append(builder, "MasteryPoints", snapshot.MasteryPoints);
        Append(builder, "TaoistReiki", snapshot.TaoistReiki);
        Append(builder, "PirateExp", snapshot.PirateExp);
        Append(builder, "Lottery", snapshot.Lottery);
        Append(builder, "Count", snapshot.Items.Count);
        AppendSection(builder, "AirPowers");
        Append(builder, "SenseLevel", snapshot.AirPowers.SenseLevel);
        Append(builder, "ArmedLevel", snapshot.AirPowers.ArmedLevel);
        Append(builder, "OverLoadLevel", snapshot.AirPowers.OverLoadLevel);
        Append(builder, "Points", snapshot.AirPowers.Points);
        Append(builder, "SenseAipPower", snapshot.AirPowers.SenseAipPower);
        Append(builder, "ArmedAipPower", snapshot.AirPowers.ArmedAipPower);
        Append(builder, "OverLoadAipPower", snapshot.AirPowers.OverLoadAipPower);

        for (var index = 0; index < snapshot.Items.Count; index++)
        {
            var item = snapshot.Items[index];
            AppendSection(builder, "Item" + index.ToString(CultureInfo.InvariantCulture));
            Append(builder, "ItemID", item.ItemID);
            Append(builder, "Level", item.Level);
            Append(builder, "Exp", item.Exp);
            Append(builder, "Equip", item.Equip);
            Append(builder, "Transform", item.Transform);
            Append(builder, "MasteryScore", item.MasteryScore);
            for (var row = 0; row < 2; row++)
            {
                for (var slot = 0; slot < 7; slot++)
                {
                    Append(
                        builder,
                        string.Concat(
                            "Animas[",
                            row.ToString(CultureInfo.InvariantCulture),
                            "]",
                            slot.ToString(CultureInfo.InvariantCulture)),
                        ReadNestedValue(item.Animas, row, slot));
                }
            }

            for (var slot = 0; slot < 6; slot++)
            {
                Append(
                    builder,
                    "BlackJades" + slot.ToString(CultureInfo.InvariantCulture),
                    ReadValue(item.BlackJades, slot));
            }
        }

        AppendSection(builder, "BlackJadeBag");
        Append(builder, "Count", snapshot.BlackJadeBag.BlackJades.Count);
        for (var index = 0; index < snapshot.BlackJadeBag.BlackJades.Count; index++)
        {
            Append(
                builder,
                "BlackJade" + index.ToString(CultureInfo.InvariantCulture),
                snapshot.BlackJadeBag.BlackJades[index]);
        }

        return builder.ToString();
    }

    public static CombatGearSnapshot LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return CombatGearSnapshot.Empty;
        }

        try
        {
            var content = SnapshotFilePersistence.ReadAllTextIfExists(path);
            return content is null ? CombatGearSnapshot.Empty : Parse(content);
        }
        catch (IOException)
        {
            return CombatGearSnapshot.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return CombatGearSnapshot.Empty;
        }
    }

    public static CombatGearSnapshot Parse(string content)
    {
        try
        {
            return TryParse(content, out var snapshot)
                ? snapshot
                : CombatGearSnapshot.Empty;
        }
        catch (FormatException)
        {
            return CombatGearSnapshot.Empty;
        }
        catch (ArgumentException)
        {
            return CombatGearSnapshot.Empty;
        }
    }

    private static bool TryParse(string content, out CombatGearSnapshot snapshot)
    {
        snapshot = CombatGearSnapshot.Empty;
        var document = IniDocument.Parse(content);

        var combatGear = GetSection(document, "CombatGear");
        var airPowers = GetSection(document, "AirPowers");
        var blackJadeBag = GetSection(document, "BlackJadeBag");

        if (!TryReadUInt32(combatGear, "MasteryPoints", out var masteryPoints) ||
            !TryReadUInt32(combatGear, "TaoistReiki", out var taoistReiki) ||
            !TryReadUInt32(combatGear, "PirateExp", out var pirateExp) ||
            !TryReadUInt32(combatGear, "Lottery", out var lottery) ||
            !TryReadInt32(combatGear, "Count", out var count) ||
            count < 0)
        {
            return false;
        }

        if (!TryReadUInt32(airPowers, "SenseLevel", out var senseLevel) ||
            !TryReadUInt32(airPowers, "ArmedLevel", out var armedLevel) ||
            !TryReadUInt32(airPowers, "OverLoadLevel", out var overLoadLevel) ||
            !TryReadUInt32(airPowers, "Points", out var points) ||
            !TryReadUInt32(airPowers, "SenseAipPower", out var senseAipPower) ||
            !TryReadUInt32(airPowers, "ArmedAipPower", out var armedAipPower) ||
            !TryReadUInt32(airPowers, "OverLoadAipPower", out var overLoadAipPower))
        {
            return false;
        }

        var items = new CombatGearSnapshotItem[count];
        for (var i = 0; i < count; i++)
        {
            if (!TryReadItem(document, i, out items[i]))
            {
                return false;
            }
        }

        if (!TryReadInt32(blackJadeBag, "Count", out var blackJadeBagCount) ||
            blackJadeBagCount < 0)
        {
            return false;
        }

        var blackJades = new uint[blackJadeBagCount];
        for (var i = 0; i < blackJades.Length; i++)
        {
            if (!TryReadUInt32(blackJadeBag, "BlackJade" + i.ToString(CultureInfo.InvariantCulture), out blackJades[i]))
            {
                return false;
            }
        }

        snapshot = new CombatGearSnapshot(
            masteryPoints,
            taoistReiki,
            pirateExp,
            lottery,
            count,
            new CombatGearAirPowersSnapshot(
                senseLevel,
                armedLevel,
                overLoadLevel,
                points,
                senseAipPower,
                armedAipPower,
                overLoadAipPower),
            items,
            new CombatGearBlackJadeBagSnapshot(blackJadeBagCount, blackJades));
        return true;
    }

    private static bool TryReadItem(IniDocument document, int index, out CombatGearSnapshotItem item)
    {
        item = new CombatGearSnapshotItem(0, 0, 0, 0, 0, 0, [[], []], []);
        var section = GetSection(document, "Item" + index.ToString(CultureInfo.InvariantCulture));

        if (!TryReadUInt32(section, "ItemID", out var itemId) ||
            !TryReadUInt32(section, "Level", out var level) ||
            !TryReadUInt32(section, "Exp", out var exp) ||
            !TryReadUInt32(section, "Equip", out var equip) ||
            !TryReadUInt32(section, "Transform", out var transform) ||
            !TryReadUInt32(section, "MasteryScore", out var masteryScore))
        {
            return false;
        }

        var animas = new IReadOnlyList<uint>[2];
        for (var row = 0; row < animas.Length; row++)
        {
            var values = new uint[7];
            for (var slot = 0; slot < values.Length; slot++)
            {
                var key = string.Concat(
                    "Animas[",
                    row.ToString(CultureInfo.InvariantCulture),
                    "]",
                    slot.ToString(CultureInfo.InvariantCulture));
                if (!TryReadUInt32(section, key, out values[slot]))
                {
                    return false;
                }
            }

            animas[row] = values;
        }

        var blackJades = new uint[6];
        for (var i = 0; i < blackJades.Length; i++)
        {
            if (!TryReadUInt32(section, "BlackJades" + i.ToString(CultureInfo.InvariantCulture), out blackJades[i]))
            {
                return false;
            }
        }

        item = new CombatGearSnapshotItem(
            itemId,
            level,
            exp,
            equip,
            transform,
            masteryScore,
            animas,
            blackJades);
        return true;
    }

    private static IReadOnlyDictionary<string, string>? GetSection(IniDocument document, string name)
    {
        return document.Sections.TryGetValue(name, out var section)
            ? section
            : null;
    }

    private static bool TryReadUInt32(
        IReadOnlyDictionary<string, string>? section,
        string key,
        out uint value)
    {
        value = 0;
        if (section is null || !section.TryGetValue(key, out var rawValue))
        {
            return true;
        }

        return uint.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private string GetCharacterSnapshotPath(long characterUid)
    {
        return Path.Combine(
            _playersCombatGearRoot,
            characterUid.ToString(CultureInfo.InvariantCulture) + ".ini");
    }

    private static void AppendSection(StringBuilder builder, string section)
    {
        builder.Append('[');
        builder.Append(section);
        builder.AppendLine("]");
    }

    private static void Append(StringBuilder builder, string key, int value)
    {
        builder.Append(key);
        builder.Append('=');
        builder.AppendLine(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void Append(StringBuilder builder, string key, uint value)
    {
        builder.Append(key);
        builder.Append('=');
        builder.AppendLine(value.ToString(CultureInfo.InvariantCulture));
    }

    private static uint ReadNestedValue(IReadOnlyList<IReadOnlyList<uint>> rows, int row, int index)
    {
        return row >= 0 &&
            row < rows.Count &&
            index >= 0 &&
            index < rows[row].Count
            ? rows[row][index]
            : 0;
    }

    private static uint ReadValue(IReadOnlyList<uint> values, int index)
    {
        return index >= 0 && index < values.Count ? values[index] : 0;
    }

    private static bool TryReadInt32(
        IReadOnlyDictionary<string, string>? section,
        string key,
        out int value)
    {
        value = 0;
        if (section is null || !section.TryGetValue(key, out var rawValue))
        {
            return true;
        }

        return int.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
