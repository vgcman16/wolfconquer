using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneChanceTests
{
    [Theory]
    [InlineData("Character")] [InlineData("Items")] [InlineData("Spells")]
    public Task ActualSavedBeforeDriftRefusesChanceWithoutRepairOrPublication(string kind) => Run(async f =>
    {
        var path = kind switch { "Character" => f.CharacterPath, "Items" => f.ItemsPath, _ => f.SpellsPath };
        var original = File.ReadAllBytes(path);
        byte[] changed;
        if (kind == "Character")
        {
            var text = CharacterSnapshotFileEncoding.Decode(original, out var encoding);
            var row = CharacterSnapshotParser.ParseText(text);
            var key = "Money=" + row.Money.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Contains(key + "\r\n", text, StringComparison.Ordinal);
            text = text.Replace(key + "\r\n", "Money=" + checked(row.Money + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal);
            changed = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        }
        else if (kind == "Items")
        {
            var items = PlayerItemSnapshotLoader.Parse(original);
            changed = PlayerItemSnapshotLoader.ToLegacyBytes(items with
            { RuneItems = items.RuneItems.Select(row => row.WithRuneExperience(row.RuneExperience + 1)).ToArray() });
        }
        else { changed = original.ToArray(); changed[15] ^= 1; }
        Assert.NotEqual(original, changed); File.WriteAllBytes(path, changed);
        try { await AssertNoMutation(f, () => f.DispatchAsync(ChanceFixture.Request())); }
        finally { File.WriteAllBytes(path, original); }
        f.AssertConverged();
    });
}
