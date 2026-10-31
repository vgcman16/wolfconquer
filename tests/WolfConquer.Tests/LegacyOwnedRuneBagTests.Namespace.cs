using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneBagTests
{
    [Theory]
    [InlineData("in")] [InlineData("out-merge")]
    public Task ActualZeroBirthBagBranchesLeaveTheCompleteNamespaceStateUnchanged(string mode) => Run(async f =>
    {
        var state = File.ReadAllBytes(f.Data.State);
        await MoveAndAssert(f, mode == "out-merge" ? BagFixture.Receiver : null);
        Assert.Equal(state, File.ReadAllBytes(f.Data.State));
        Assert.False(File.Exists(Path.Combine(f.Data.Registry, "pending.json")));
    }, mode);

    [Fact]
    public Task ActualOrphanRetiresOnlyItsTypedRowAndPreservesRealPlainAndLookalikeReceipts() => Run(async f =>
    {
        // Quiesce the actual native transport/Runtime before borrowing the real
        // existing exclusive registry through its ordinary public factory.
        await f.CloseAsync();
        Assert.True(LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain,
            out var opened, out var reason), reason);
        using (var registry = Assert.IsType<LegacyItemUidRegistry>(opened))
        {
            foreach (var writer in new[] { "local-gear-mail-v1", LegacyItemUidRegistry.RuneBagWriter, LegacyItemUidRegistry.RuneDecomposeWriter })
            {
                var request = new LegacyItemUidReservationRequest(Guid.NewGuid(), writer, BagFixture.Uid,
                    BagFixture.Uid, new string('a', 64), 1);
                Assert.Equal(LegacyItemUidReservationOutcome.Reserved, registry.ReserveForOperation(f.Data.ExpectedDomain,
                    request, out var materializer, out var receipt, out reason));
                Assert.NotNull(materializer); Assert.NotNull(receipt);
            }
        }
        var plain = BagNamespaceState(f)["Operations"]!.AsArray().Select(row => row!.DeepClone()).ToArray();
        Assert.Equal(3, plain.Length); Assert.All(plain, row => Assert.Null(row["NamespaceReservation"]));
        await f.ReopenFrameAsync(); f.Sent.Clear();
        var seed = Seed(f); var native = f.Native; var binding = native.OrdinarySpellBinding!;
        var image = binding.Items.Image; var spells = native.MySpells!.ClientSpells;
        f.Runtime.RuneBagNamespaceInterruption = LegacyRuneBagNamespaceInterruption.AfterReservation;
        try { await f.DispatchAsync(); }
        finally { f.Runtime.RuneBagNamespaceInterruption = LegacyRuneBagNamespaceInterruption.None; }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent);
        Assert.Same(seed.Session, f.Current); Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Same(image, binding.Items.Image); Assert.Same(spells, native.MySpells.ClientSpells);
        Assert.Equal(seed.Items, PlayerItemSnapshotLoader.ToLegacyBytes(image.FinalSnapshot));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); Assert.Equal(seed.HighWater + 1, f.HighWater);
        AssertBagFilesExceptState(seed.Files, f);
        var reserved = BagNamespaceState(f); var rows = reserved["Operations"]!.AsArray(); Assert.Equal(4, rows.Count);
        AssertActualBagKind(rows[3]!.AsObject());
        Assert.Equal(seed.HighWater + 1, rows[3]!["First"]!.GetValue<uint>());
        var issuedFiles = f.Data.Files();
        await f.DispatchAsync(); f.Data.AssertFiles(issuedFiles); Assert.Empty(f.Sent);
        Assert.Throws<InvalidDataException>(() => f.Runtime.RequireRuneBagPreflight(f.Handler, 1));
        await f.CloseAsync();
        using (var recovered = f.Open())
        {
            Assert.True(recovered.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
            var expected = reserved.DeepClone().AsObject();
            expected["Generation"] = checked(reserved["Generation"]!.GetValue<ulong>() + 1);
            expected["Operations"] = new JsonArray(plain.Select(row => row.DeepClone()).ToArray());
            Assert.True(JsonNode.DeepEquals(expected, BagNamespaceState(f)));
            Assert.Equal(seed.HighWater + 1, f.HighWater); AssertBagFilesExceptState(seed.Files, f);
        }
        var settled = f.Data.Files(); using (f.Open()) f.Data.AssertFiles(settled);
    }, "out-rich");

    [Fact]
    public Task ActualInterruptedNamespaceRetirementKeepsCommittedGameAndReplaysOnlyBookkeeping() => Run(async f =>
    {
        var seed = Seed(f); var expected = Expect(f, seed); var native = f.Native;
        var binding = native.OrdinarySpellBinding!; var image = binding.Items.Image;
        var spells = native.MySpells!.ClientSpells; var stamp = native.Player!.FinRane;
        f.Runtime.RuneBagNamespaceRetirementInterruption = LegacyItemUidInterruption.AfterPendingFlush;
        try { await f.DispatchAsync(); }
        finally { f.Runtime.RuneBagNamespaceRetirementInterruption = LegacyItemUidInterruption.None; }
        expected.AssertImages(f); Assert.Empty(f.Sent); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); Assert.Equal(seed.HighWater + 1, f.HighWater);
        Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Same(image, binding.Items.Image); Assert.Same(spells, native.MySpells.ClientSpells);
        Assert.Equal(seed.Items, PlayerItemSnapshotLoader.ToLegacyBytes(image.FinalSnapshot));
        Assert.DoesNotContain(expected.NewUid!.Value, binding.Items.Inventory.ClientItems.Keys);
        Assert.Null(native.Inventory); Assert.Null(native.Rune); Assert.Equal(stamp, native.Player.FinRane);
        var pendingPath = Path.Combine(f.Data.Registry, "pending.json");
        Assert.True(File.Exists(pendingPath)); var stateBytes = File.ReadAllBytes(f.Data.State);
        var pendingBytes = File.ReadAllBytes(pendingPath); var allFiles = f.Data.Files();
        AssertActualBagKind(Assert.Single(BagNamespaceState(f)["Operations"]!.AsArray())!.AsObject());
        await f.CloseAsync();
        Assert.False(LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain,
            out var generic, out _)); Assert.Null(generic);
        Assert.Equal(stateBytes, File.ReadAllBytes(f.Data.State)); Assert.Equal(pendingBytes, File.ReadAllBytes(pendingPath));
        f.Data.AssertFiles(allFiles);
        using (var recovered = f.Open())
        {
            Assert.True(recovered.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
            Assert.False(File.Exists(pendingPath)); Assert.Empty(BagNamespaceState(f)["Operations"]!.AsArray());
            AssertWholeFiles(f, seed, expected);
        }
        var settled = f.Data.Files(); using (f.Open()) f.Data.AssertFiles(settled);
    }, "out-rich");

    [Fact]
    public Task ActualReservedRowCannotBeReclassifiedIntoAnotherNamespaceKind() => Run(async f =>
    {
        f.Runtime.RuneBagNamespaceInterruption = LegacyRuneBagNamespaceInterruption.AfterReservation;
        try { await f.DispatchAsync(); }
        finally { f.Runtime.RuneBagNamespaceInterruption = LegacyRuneBagNamespaceInterruption.None; }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent); await f.CloseAsync();
        var state = BagNamespaceState(f);
        var row = Assert.Single(state["Operations"]!.AsArray())!.AsObject(); AssertActualBagKind(row);
        // NEGATIVE corruption of an actual typed reservation. This does not
        // manufacture a positive Decompose receipt, work item or grant.
        row["NamespaceReservation"]!["Kind"] = "RuneDecompose";
        File.WriteAllBytes(f.Data.State, JsonSerializer.SerializeToUtf8Bytes(state));
        var files = f.Data.Files();
        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason), reason);
        Assert.Null(opened); f.Data.AssertFiles(files);
        using var lease = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }, "out-rich");

    private static JsonObject BagNamespaceState(BagFixture f) => JsonNode.Parse(File.ReadAllBytes(f.Data.State))!.AsObject();

    private static void AssertActualBagKind(JsonObject row)
    {
        Assert.Equal(LegacyItemUidRegistry.RuneBagWriter, row["Writer"]!.GetValue<string>());
        Assert.Equal(1, row["Count"]!.GetValue<int>());
        var kind = row["NamespaceReservation"]!.AsObject(); Assert.Equal(2, kind.Count);
        Assert.Equal(1, kind["Version"]!.GetValue<int>()); Assert.Equal("RuneBagReturn", kind["Kind"]!.GetValue<string>());
    }

    private static void AssertBagFilesExceptState(Dictionary<string, byte[]> expected, BagFixture f)
    {
        string state = Path.GetRelativePath(f.Data.Root, f.Data.State);
        var actual = f.Data.Files(); expected = expected.Where(pair => !string.Equals(pair.Key, state, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        actual.Remove(state);
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
}
