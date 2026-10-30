using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneGameplayTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)]
    public Task ActualV14GrantAndRetirementRecoverEveryReplacementPrefixAsOneRecordedOperation(int prefix, bool retirement) => Run(async f =>
    {
        if (retirement) await f.DispatchAsync(F.Equip());
        await f.StageAsync(prefix, retirement ? F.Unequip() : F.Equip());
        var targets = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        for (var index = 0; index < targets.Length; index++)
            Assert.Equal(index < prefix ? f.After[targets[index]] : f.Before[targets[index]], File.ReadAllBytes(targets[index]));
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.AssertRecovered(); f.AssertMarker(retirement ? "" : "14160:16");
        var first = f.Data.Files(); await f.CloseAsync(); f.Open(); f.AssertRecovered(); f.Data.AssertFiles(first);
    });

    [Theory]
    [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)]
    public Task RealV14FlushedStagesAreValidatedAndRemovedByOwnedRestart(int ordinal, bool retirement) => Run(async f =>
    {
        if (retirement) await f.DispatchAsync(F.Equip());
        await f.StopAfterStageFlushedAsync(ordinal, retirement ? F.Unequip() : F.Equip());
        var targets = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        var stages = targets.Select(path => StagePath(f, path)).ToArray();
        for (var index = 0; index < targets.Length; index++)
        {
            Assert.Equal(f.Before[targets[index]], File.ReadAllBytes(targets[index]));
            if (index < ordinal) Assert.Equal(f.After[targets[index]], File.ReadAllBytes(stages[index]));
            else Assert.False(File.Exists(stages[index]));
        }
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.AssertRecovered();
        Assert.All(stages, path => Assert.False(File.Exists(path)));
        await f.CloseAsync(); var recovered = f.Data.Files(); f.Open(); f.AssertRecovered(); f.Data.AssertFiles(recovered);
    });

    [Theory]
    [InlineData("missing-operation")] [InlineData("selected-uid")] [InlineData("wrong-action")]
    [InlineData("missing-metadata")] [InlineData("malformed-mapping")]
    [InlineData("changed-grant")] [InlineData("changed-marker")]
    [InlineData("malformed-marker")] [InlineData("changed-owned-before")]
    public Task ActualV14CaptureDoesNotAuthorizeMalformedDependenciesOrAnAlteredGrantOutcome(string fault) => Run(async f =>
    {
        if (fault == "changed-owned-before") await f.DispatchAsync(F.Equip());
        await f.StageAsync(3, fault == "changed-owned-before" ? F.Unequip() : F.Equip()); await f.CloseAsync();
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
        var operation = record["RuneOperation"]!.AsObject();
        var changes = record["Changes"]!.AsArray();
        if (fault == "missing-operation") record.Remove("RuneOperation");
        else if (fault == "selected-uid") operation["SelectedUid"] = F.Shared;
        else if (fault == "wrong-action") operation["Equip"] = false;
        else if (fault == "missing-metadata") operation["Metadata"]!.AsArray().RemoveAt(0);
        else if (fault == "malformed-mapping") operation["Metadata"]!.AsArray().Single(row =>
            row!["ItemId"]!.GetValue<uint>() == F.FirstType)!["RuneSpellId"] = 1U;
        else if (fault == "changed-owned-before")
        {
            var change = changes.Single(row => row!["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Spells)!;
            var before = Convert.FromBase64String(change["Before"]!.GetValue<string>());
            before[^8] = 1; // Existing marked grant's Experience low byte; AFTER files still exact.
            change["Before"] = Convert.ToBase64String(before);
        }
        else
        {
            var kind = fault is "changed-marker" or "malformed-marker" ? LegacyTradeJournalFileKind.Character : LegacyTradeJournalFileKind.Spells;
            var change = changes.Single(row => row!["Kind"]!.GetValue<int>() == (int)kind)!;
            var bytes = Convert.FromBase64String(change["After"]!.GetValue<string>());
            if (fault is "changed-marker" or "malformed-marker")
            {
                var text = CharacterSnapshotFileEncoding.Decode(bytes, out var encoding); Assert.Contains("14160:16", text, StringComparison.Ordinal);
                bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace("14160:16",
                    fault == "malformed-marker" ? "14160:016" : "14160:17", StringComparison.Ordinal))).ToArray();
            }
            else bytes[^10] = 17; // New default record's ushort Level low byte.
            change["After"] = Convert.ToBase64String(bytes);
            File.WriteAllBytes(kind == LegacyTradeJournalFileKind.Character ? f.CharacterPath : f.SpellsPath, bytes);
        }
        File.WriteAllText(f.PendingPath, record.ToJsonString()); f.AssertStartupRefused();
    });

    [Theory]
    [InlineData("foreign-stage")] [InlineData("tampered-stage")] [InlineData("foreign-physical-uid")]
    public Task TheNewRecordedOperationStillRequiresExactStagesAndGlobalPhysicalUidOwnership(string fault) => Run(async f =>
    {
        if (fault == "tampered-stage") await f.StopAfterStageFlushedAsync(3, F.Equip()); else await f.StageAsync(3, F.Equip());
        await f.CloseAsync();
        if (fault == "foreign-physical-uid")
            File.WriteAllBytes(Path.Combine(f.Data.Items, (F.Uid + 1) + ".bin"),
                PlayerItemSnapshotLoader.ToLegacyBytes(new([], [], [], [], [F.Item(F.First, F.FirstType)])));
        else
        {
            var path = StagePath(f, f.SpellsPath); var bytes = f.After[f.SpellsPath].ToArray();
            if (fault == "foreign-stage") path = f.SpellsPath + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp";
            else bytes[0] ^= 0xff;
            File.WriteAllBytes(path, bytes);
        }
        f.AssertStartupRefused();
    });

    private static string StagePath(F fixture, string target) => target + ".managed-trade-" +
        JsonNode.Parse(File.ReadAllBytes(fixture.PendingPath))!["TransactionId"]!.GetValue<string>() + ".tmp";
}
