using System.Text.Json.Nodes;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneGameplayTests
{
    [Theory]
    [InlineData((ushort)114)]
    [InlineData((ushort)134)]
    public Task ARealNewTwelveSlotIntentCannotWidenTheHistoricalNineSlotV9ProducerBoundary(ushort slot) =>
        Run(async f =>
        {
            await f.StageAsync(0, LegacyOwnedRuneGameplayFixture.Equip(LegacyOwnedRuneGameplayFixture.Yellow, slot));
            Assert.Equal(f.Before[f.CharacterPath], f.After[f.CharacterPath]);
            Assert.Equal(f.Before[f.SpellsPath], f.After[f.SpellsPath]);
            await f.CloseAsync();
            // Negative corruption of a genuine v14 capture, never a fabricated
            // positive legacy journal or a claim of old producer provenance.
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
            Assert.Equal(14, record["Version"]!.GetValue<int>());
            record["Version"] = 9; Assert.True(record.Remove("RuneOperation"));
            File.WriteAllText(f.PendingPath, record.ToJsonString());
            f.AssertStartupRefused();
        }, extraSlots: 4, flags: 31);
}
