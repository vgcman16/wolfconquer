using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    private sealed record CreationCapture(CreationBefore Before, Dictionary<string, byte[]> After,
        LegacyAuthenticatedOrdinaryCreationRecipe Recipe, LegacyItemUidReservationReceipt Allocation, string Transaction);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public Task GenuineSixParticipantCreationRestartsOnlyItsRecordedBirthAtSelectedReplacementPrefixes(int prefix) => Run(async f =>
    {
        var captured = await CaptureCreation(f, prefix);
        int index = 0;
        foreach (var target in captured.After)
        {
            if (index++ < prefix) Assert.Equal(target.Value, File.ReadAllBytes(target.Key));
            else Assert.False(File.Exists(target.Key));
        }
        await RestartCreation(f, captured);
    });

    [Fact]
    public Task ActualSixFlushedCreationStagesRecoverBeforeAnyNewNativeAdmission() => Run(async f =>
    {
        var captured = await CaptureCreation(f, 6, flushed: true);
        foreach (var target in captured.After)
        {
            Assert.False(File.Exists(target.Key));
            Assert.Equal(target.Value, File.ReadAllBytes(target.Key + ".managed-trade-" + captured.Transaction + ".tmp"));
        }
        await RestartCreation(f, captured);
    });

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task ActualCreatorIntentAllocationAndCommittedCutsFinishTheSameCompleteAuthenticatedRecipeExactlyOnce(int cut) => Run(async f =>
    {
        var before = BeforeCreation(f);
        f.Handler.AuthenticatedCreationProducerInterruption = (LegacyAuthenticatedCreationInterruption)cut;
        try { await f.CreateAsync(); }
        finally { f.Handler.AuthenticatedCreationProducerInterruption = LegacyAuthenticatedCreationInterruption.None; }
        Assert.Empty(f.Sent); Assert.False(f.TryCurrent(out _)); Assert.Equal(0U, f.Native.Player.UID);
        Assert.Null(f.Native.Inventory); Assert.Null(f.Native.Equipment); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.False(File.Exists(f.PendingPath));
        var issued = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
        var intent = Assert.Single(issued["ProducerIntents"]!.AsArray())!;
        var payload = Convert.FromBase64String(intent["PayloadBase64"]!.GetValue<string>());
        var recipe = LegacyAuthenticatedOrdinaryCreationRecipe.Decode(payload, f.Data.Options().Roots, f.Data.Mailbox);
        int count = recipe.Projection.Items.Length;
        bool allocated = cut != (int)LegacyAuthenticatedCreationInterruption.AfterIntent;
        Assert.Equal(before.HighWater + (allocated ? (uint)count : 0), issued["HighWater"]!.GetValue<uint>());
        Assert.Equal(allocated ? 1 : 0, issued["Operations"]!.AsArray().Count);
        if (cut != (int)LegacyAuthenticatedCreationInterruption.AfterGameCommit)
            foreach (var kind in LegacyAuthenticatedOrdinaryCreationRecipe.TargetKinds) Assert.False(File.Exists(f.DataPath(kind)));
        else
        {
            Assert.Equal(recipe.Projection.CopyCharacterImage(), File.ReadAllBytes(f.CharacterPath));
            Assert.Equal(recipe.Projection.CopySpellsImage(), File.ReadAllBytes(f.SpellsPath));
        }
        var issuedFiles = f.Data.Files(); await f.CreateAsync(); f.Data.AssertFiles(issuedFiles); Assert.Empty(f.Sent);
        await f.CloseAsync();
        using (f.Open())
        {
            var completed = CompletedIntent(f); Assert.Equal(payload, completed.CopyPayload());
            Assert.Equal(before.HighWater + 1, completed.Allocation!.First); Assert.Equal(count, completed.Allocation.Count);
            Assert.Equal(ExpectedStarterItems(recipe.Projection, completed.Allocation), File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(recipe.Projection.CopyCharacterImage(), File.ReadAllBytes(f.CharacterPath));
            Assert.Equal(recipe.Projection.CopySpellsImage(), File.ReadAllBytes(f.SpellsPath));
            Assert.Equal(recipe.Projection.CopyProficienciesImage(), File.ReadAllBytes(f.DataPath(LegacyTradeJournalFileKind.Proficiencies)));
            Assert.Equal(recipe.Projection.CopyQuestsImage(), File.ReadAllBytes(f.DataPath(LegacyTradeJournalFileKind.Quests)));
            Assert.Equal(recipe.Projection.CopyCombatGearImage(), File.ReadAllBytes(f.DataPath(LegacyTradeJournalFileKind.CombatGear)));
            var settled = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
            Assert.Equal(before.HighWater + (uint)count, settled["HighWater"]!.GetValue<uint>());
            Assert.Equal(before.Generation + 3, settled["Generation"]!.GetValue<ulong>());
        }
        var files = f.Data.Files(); using (f.Open()) f.Data.AssertFiles(files);
        await f.ReopenFrameAsync(() => f.Data.AssertFiles(files));
        Assert.Equal(1L, f.Current.Character.Level); Assert.Equal(0L, f.Current.Character.Reborn);
    });

    [Fact]
    public Task ActualEngineIoAfterFirstReplacementRollsBackGameFilesButFencesTheIssuedRangeUntilOwnedStartupFinishesIt() => Run(async f =>
    {
        var before = BeforeCreation(f);
        using (var fault = LegacyJournalInterruption.ThrowAfter(1, LegacyJournalFault.ThrowIo))
        {
            f.Handler.AuthenticatedCreationInterruption = fault;
            try { await f.CreateAsync(); } finally { f.Handler.AuthenticatedCreationInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString()); Assert.Equal(new[] { 0, 1 }, fault.Visited);
            Assert.NotNull(fault.BoundaryImages[0]); Assert.All(fault.BoundaryImages.Skip(1), image => Assert.Null(image));
        }
        Assert.Empty(f.Sent); Assert.False(f.TryCurrent(out _)); Assert.Equal(0U, f.Native.Player.UID);
        Assert.Null(f.Native.Inventory); Assert.Null(f.Native.Equipment); Assert.Null(f.Native.Rune); Assert.True(f.Runtime.Recovery.IsBlocked);
        foreach (var kind in LegacyAuthenticatedOrdinaryCreationRecipe.TargetKinds) Assert.False(File.Exists(f.DataPath(kind)));
        Assert.False(File.Exists(Path.Combine(f.Data.Mailbox, CreationFixture.Account + ".bin"))); Assert.False(File.Exists(f.PendingPath));
        string registryPrefix = Path.GetRelativePath(f.Data.Root, f.Data.Registry) + Path.DirectorySeparatorChar;
        AssertHistoricalFiles(before.Files.Where(pair => !pair.Key.StartsWith(registryPrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            f.Data.Files().Where(pair => !pair.Key.StartsWith(registryPrefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
        var issued = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
        var intent = Assert.Single(issued["ProducerIntents"]!.AsArray())!;
        var allocation = Assert.Single(issued["Operations"]!.AsArray())!;
        var payload = Convert.FromBase64String(intent["PayloadBase64"]!.GetValue<string>());
        var recipe = LegacyAuthenticatedOrdinaryCreationRecipe.Decode(payload, f.Data.Options().Roots, f.Data.Mailbox);
        Assert.Equal(before.HighWater + 1, allocation["First"]!.GetValue<uint>());
        Assert.Equal(recipe.Projection.Items.Length, allocation["Count"]!.GetValue<int>());
        var files = f.Data.Files(); await f.CreateAsync(); f.Data.AssertFiles(files); Assert.Empty(f.Sent);
        await f.CloseAsync();
        using (f.Open())
        {
            var completed = CompletedIntent(f); Assert.Equal(payload, completed.CopyPayload());
            Assert.Equal(allocation["First"]!.GetValue<uint>(), completed.Allocation!.First);
            Assert.Equal(allocation["Generation"]!.GetValue<ulong>(), completed.Allocation.Generation);
            foreach (var image in recipe.AfterImages(completed.Allocation))
                if (image.Key == LegacyTradeJournalFileKind.Mailbox) Assert.Null(image.Value);
                else Assert.Equal(image.Value, File.ReadAllBytes(f.DataPath(image.Key)));
            Assert.Equal(before.HighWater + (uint)recipe.Projection.Items.Length, JsonNode.Parse(File.ReadAllBytes(f.Data.State))!["HighWater"]!.GetValue<uint>());
            Assert.Equal(before.Generation + 3, JsonNode.Parse(File.ReadAllBytes(f.Data.State))!["Generation"]!.GetValue<ulong>());
        }
        var settled = f.Data.Files(); using (f.Open()) f.Data.AssertFiles(settled);
    });

    private static async Task<CreationCapture> CaptureCreation(CreationFixture f, int boundary, bool flushed = false)
    {
        var before = BeforeCreation(f);
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterAuthenticatedCreationStageFlushed(boundary)
            : LegacyJournalInterruption.StopAfterAuthenticatedCreation(boundary))
        {
            f.Handler.AuthenticatedCreationInterruption = fault;
            try { await f.CreateAsync(); } finally { f.Handler.AuthenticatedCreationInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString());
            if (!flushed) Assert.Equal(Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        Assert.Empty(f.Sent); Assert.False(f.TryCurrent(out _)); Assert.Equal(0U, f.Native.Player.UID);
        Assert.Null(f.Native.Inventory); Assert.Null(f.Native.Equipment); Assert.Null(f.Native.Rune); Assert.True(f.Runtime.Recovery.IsBlocked);
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!; Assert.Equal(19, record["Version"]!.GetValue<int>());
        var operation = record["OrdinaryCreation"]!.AsObject();
        Assert.Equal(new[] { "Allocation", "Payload", "PreparedGeneration" }, operation.Select(pair => pair.Key).Order(StringComparer.Ordinal));
        var payload = Convert.FromBase64String(operation["Payload"]!.GetValue<string>());
        var recipe = LegacyAuthenticatedOrdinaryCreationRecipe.Decode(payload, f.Data.Options().Roots, f.Data.Mailbox);
        var allocation = JsonSerializer.Deserialize<LegacyItemUidReservationReceipt>(operation["Allocation"]!.ToJsonString())!;
        Assert.Equal(before.HighWater + 1, allocation.First); Assert.Equal(recipe.Projection.Items.Length, allocation.Count);
        Assert.Equal(f.Data.ExpectedDomain, allocation.DomainId); Assert.Equal(recipe.CreateRequest().OperationId, allocation.OperationId);
        Assert.True(operation["PreparedGeneration"]!.GetValue<ulong>() < allocation.Generation);
        var expected = recipe.AfterImages(allocation);
        expected[LegacyTradeJournalFileKind.Items] = ExpectedStarterItems(recipe.Projection, allocation);
        var changes = record["Changes"]!.AsArray(); Assert.Equal(6, changes.Count);
        var after = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < changes.Count; index++)
        {
            var row = changes[index]!; var kind = LegacyAuthenticatedOrdinaryCreationRecipe.TargetKinds[index];
            Assert.Equal((int)kind, row["Kind"]!.GetValue<int>()); Assert.Equal((long)CreationFixture.Account, row["FileId"]!.GetValue<long>());
            Assert.Null(row["Before"]); var bytes = Convert.FromBase64String(row["After"]!.GetValue<string>());
            Assert.Equal(expected[kind], bytes); after.Add(f.DataPath(kind), bytes);
        }
        Assert.Equal(before.HighWater + (uint)allocation.Count, JsonNode.Parse(File.ReadAllBytes(f.Data.State))!["HighWater"]!.GetValue<uint>());
        return new(before, after, recipe, allocation, record["TransactionId"]!.GetValue<string>());
    }

    private static async Task RestartCreation(CreationFixture f, CreationCapture captured)
    {
        await f.CloseAsync();
        var files = f.Data.Files();
        Assert.False(LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), out var generic, out _)); Assert.Null(generic); f.Data.AssertFiles(files);
        using (var opened = f.Open())
        {
            Assert.True(opened.Recovery.IsReadyForOrdinaryAdmission(f.Data.Options().Roots));
            foreach (var target in captured.After) Assert.Equal(target.Value, File.ReadAllBytes(target.Key));
            Assert.False(File.Exists(f.PendingPath)); Assert.Empty(Directory.GetFiles(f.Data.Items, "*.managed-trade-*.tmp"));
            var intent = CompletedIntent(f); Assert.Equal(captured.Allocation, intent.Allocation); Assert.Equal(captured.Recipe.Encode(), intent.CopyPayload());
        }
        var settled = f.Data.Files(); using (f.Open()) f.Data.AssertFiles(settled);
        await f.ReopenFrameAsync(() => f.Data.AssertFiles(settled));
        AssertCurrentCreated(f, captured.After[f.ItemsPath]);
        var ledger = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
        Assert.Equal(captured.Before.HighWater + (uint)captured.Allocation.Count, ledger["HighWater"]!.GetValue<uint>());
        Assert.Equal(captured.Before.Generation + 3, ledger["Generation"]!.GetValue<ulong>());
        Assert.Equal(captured.After[f.ItemsPath], File.ReadAllBytes(f.ItemsPath));
    }
}
