using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

// Independent expected image for these canonical synthetic fixtures only.
// Admission is checked byte-exact BEFORE the later ordinary runtime scalar save.
internal static class OrdinaryAdmissionImageExpectations
{
    internal static byte[] RuntimeHpSave(byte[] committed, long hitpoints, long maxMana, long expBallUsed)
    {
        var text = CharacterSnapshotFileEncoding.Decode(committed, out var encoding);
        using var reader = new StringReader(text);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line) lines.Add(line);
        var start = lines.FindIndex(line => line.Trim().Equals("[Character]", StringComparison.OrdinalIgnoreCase));
        Assert.True(start >= 0);
        var end = lines.FindIndex(start + 1, line => line.Trim().StartsWith('['));
        if (end < 0) end = lines.Count;
        // The scalar save writes all 31 scalar keys in this order. These canonical
        // fixtures already contain every required scalar except the two optional
        // defaults below; assert that finite schema rather than accepting runtime
        // attributes or calling the production persistence builder.
        string[] scalarKeys =
        [
            "UID", "Body", "Face", "Class", "FirstClass", "SecoundeClass", "Reborn", "Level",
            "Haire", "Experience", "Map", "X", "Y", "PMap", "PMapX", "PMapY", "Agility",
            "Strength", "Vitaliti", "Spirit", "Atributes", "MinHitPoints", "MinMana", "MaxMana",
            "ConquerPoints", "BankCps", "BoundConquerPoints", "Money", "PkPoints", "Online", "ExpBallUsed",
        ];
        foreach (var key in scalarKeys.Where(key => key is not ("MaxMana" or "ExpBallUsed")))
            Assert.Single(lines.Skip(start + 1).Take(end - start - 1)
                .Where(line => line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)));
        // Ordinary query only changes HP; every other seeded business field and
        // optional attribute must remain byte-exact at its original line.
        foreach (var (key, value) in new[] { ("MinHitPoints", hitpoints), ("MaxMana", maxMana), ("ExpBallUsed", expBallUsed) })
        {
            var index = -1;
            for (var cursor = start + 1; cursor < end; cursor++)
                if (lines[cursor].StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                { Assert.Equal(-1, index); index = cursor; }
            var expected = key + "=" + value.ToString(CultureInfo.InvariantCulture);
            if (index < 0) lines.Insert(end++, expected);
            else lines[index] = expected;
        }
        // After optional stored attributes/Pheonix, the writer always appends the
        // legacy 20-slot EpicTrojan composite when absent. Query does not mutate
        // its quest slots: preserve an existing composite, otherwise pin all 20
        // defaults independently (slots 4,5,14,18 are one; the others are zero).
        var epic = lines.Skip(start + 1).Take(end - start - 1)
            .Where(line => line.StartsWith("EpicTrojan=", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.InRange(epic.Length, 0, 1);
        if (epic.Length == 0)
            lines.Insert(end, "EpicTrojan=0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/");
        // File.WriteAllLines is a later runtime save and normalizes line endings.
        var body = encoding.GetBytes(string.Join(Environment.NewLine, lines) + Environment.NewLine);
        return encoding.GetPreamble().Concat(body).ToArray();
    }
}
