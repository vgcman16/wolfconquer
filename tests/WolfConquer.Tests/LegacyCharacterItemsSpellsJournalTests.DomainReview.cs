using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterItemsSpellsJournalTests
{
    [Theory]
    [InlineData("duplicate-items")]
    [InlineData("missing-spells")]
    [InlineData("spells-number")]
    [InlineData("relative-spells")]
    [InlineData("noncanonical-spells")]
    [InlineData("wrong-domain-kind")]
    public void RetainedV9MalformedDomainNeverReplays(string mutation) => Run(f =>
    {
        Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells, f.Changes(),
            LegacyCharacterItemsSpellsJournalInterruption.AfterPending, out _));
        var document = JsonNode.Parse(File.ReadAllBytes(f.Pending))!.AsObject();
        var domain = document["CharacterItemsSpellsDomain"]!.AsObject();
        switch (mutation)
        {
            case "missing-spells": domain.Remove("Spells"); break;
            case "spells-number": domain["Spells"] = 123; break;
            case "relative-spells": domain["Spells"] = "relative-spells"; break;
            case "noncanonical-spells": domain["Spells"] = f.Spells + Path.DirectorySeparatorChar; break;
            case "wrong-domain-kind":
                var copy = domain.DeepClone();
                document.Remove("CharacterItemsSpellsDomain");
                document["CharacterSpellsDomain"] = copy;
                break;
        }
        string json = document.ToJsonString();
        if (mutation == "duplicate-items")
        {
            string field = "\"Items\":" + JsonSerializer.Serialize(f.Items);
            Assert.Contains(field, json);
            Assert.Equal(json.IndexOf(field, StringComparison.Ordinal), json.LastIndexOf(field, StringComparison.Ordinal));
            json = json.Replace(field, field + "," + field, StringComparison.Ordinal);
        }
        File.WriteAllText(f.Pending, json);
        byte[] retained = File.ReadAllBytes(f.Pending);
        var recovery = f.Recovery();
        Assert.False(recovery.TryEnsureInitialized(out _));
        Assert.True(recovery.IsBlocked);
        Assert.Equal(retained, File.ReadAllBytes(f.Pending));
        f.Images(f.Character, f.ItemBefore, f.SpellBefore);
    });
}
