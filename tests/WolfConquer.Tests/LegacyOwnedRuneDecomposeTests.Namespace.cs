using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneDecomposeTests
{
    [Theory]
    [InlineData("local-gear-mail-v1")]
    [InlineData("rune-decompose-v1")]
    public Task ActualOrphanNamespaceCleanupPreservesPlainReceiptsIncludingLookalikeWriter(string writer) => Run(async f =>
    {
        var gameBefore = WithoutNamespaceState(f.Data);
        uint priorHighwater = f.HighWater;
        f.Runtime.RuneDecomposeNamespaceInterruption = LegacyRuneDecomposeNamespaceInterruption.AfterReservation;
        await f.SendAsync(DecomposeFixture.Request());
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        AssertSameFiles(gameBefore, WithoutNamespaceState(f.Data));
        var reserved = ReadNamespaceState(f);
        var rows = reserved["Operations"]!.AsArray(); Assert.Equal(2, rows.Count);
        Assert.Equal(writer, rows[0]!["Writer"]!.GetValue<string>());
        Assert.Null(rows[0]!["NamespaceReservation"]);
        var typed = rows[1]!.AsObject(); RequireActualKind(typed);
        Assert.Equal(priorHighwater + 1, typed["First"]!.GetValue<uint>());
        Assert.Equal(2, typed["Count"]!.GetValue<int>()); Assert.Equal(priorHighwater + 2, f.HighWater);
        await f.CloseAsync();

        using (f.Open())
        {
            var cleaned = ReadNamespaceState(f);
            var expected = reserved.DeepClone().AsObject();
            expected["Generation"] = checked(reserved["Generation"]!.GetValue<ulong>() + 1);
            expected["Operations"] = new JsonArray(rows[0]!.DeepClone());
            Assert.True(JsonNode.DeepEquals(expected, cleaned));
            Assert.Equal(rows[0]!.ToJsonString(), cleaned["Operations"]![0]!.ToJsonString());
            AssertSameFiles(gameBefore, WithoutNamespaceState(f.Data));
        }
        var afterCleanup = f.Data.Files();
        using (f.Open()) f.Data.AssertFiles(afterCleanup);
    }, beforeOpen: f =>
    {
        Assert.True(LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain,
            out var opened, out var reason), reason);
        using var issuer = Assert.IsType<LegacyItemUidRegistry>(opened);
        var request = new LegacyItemUidReservationRequest(Guid.NewGuid(), writer, DecomposeFixture.Uid,
            DecomposeFixture.Uid, new string('a', 64), 2);
        Assert.Equal(LegacyItemUidReservationOutcome.Reserved, issuer.ReserveForOperation(f.Data.ExpectedDomain,
            request, out var actualReservation, out var receipt, out reason));
        Assert.NotNull(actualReservation); Assert.NotNull(receipt);
        // A real namespace reservation may intentionally remain unpublished.
        // Its plain receipt cannot be promoted to the new typed kind.
        Assert.Null(JsonNode.Parse(File.ReadAllBytes(f.Data.State))!["Operations"]![0]!["NamespaceReservation"]);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualCleanupInterruptionPreservesCommittedGameAndRecoversOnlyNamespaceBookkeeping(bool afterStateReplace) => Run(async f =>
    {
        var fault = afterStateReplace ? LegacyItemUidInterruption.AfterStateReplace : LegacyItemUidInterruption.AfterPendingFlush;
        var seed = Seed(f); var native = f.Native; var binding = native.OrdinarySpellBinding!;
        var nativeItems = binding.Items.Image; var nativeSpells = native.MySpells!.ClientSpells;
        f.Runtime.RuneDecomposeNamespaceRetirementInterruption = fault;
        uint beforeHighwater = f.HighWater;
        await f.SendAsync(DecomposeFixture.Request());
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.Equal(beforeHighwater + 2, f.HighWater);
        var actualAfter = Expect(seed, ActualFragmentColor(f, seed)); actualAfter.AssertImages(f);
        Assert.False(f.Store.TryGet(f.Connection, out _)); // Durable publication failure intentionally retires the holder.
        Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(nativeItems, binding.Items.Image);
        Assert.True(nativeItems.IsCurrentPlain()); Assert.Same(nativeSpells, native.MySpells.ClientSpells);
        Assert.Equal(seed.Session.PlayerSpells.Spells.Select(row => row.Id).Order(), nativeSpells.Keys.Order());
        foreach (var row in seed.Session.PlayerSpells.Spells)
        {
            var untouched = nativeSpells[row.Id]; Assert.Equal(row.Level, untouched.Level); Assert.Equal(row.Experience, untouched.Experience);
            Assert.Equal(row.PreviousLevel, untouched.PreviousLevel); Assert.Equal(row.SoulLevel, untouched.SoulLevel); Assert.Equal(row.UseSpellSoul, untouched.UseSpellSoul);
        }
        Assert.DoesNotContain(binding.Items.Rune.Objects, row => actualAfter.NewUids.Contains(row.Uid));
        Assert.DoesNotContain(binding.Items.Inventory.ClientItems.Keys, actualAfter.NewUids.Contains);
        Assert.Null(native.Inventory); Assert.Null(native.Rune); // Real holder retirement detaches the old item owner.
        var committedGame = WithoutNamespaceState(f.Data);
        var stateBytes = File.ReadAllBytes(f.Data.State);
        string pendingPath = Path.Combine(f.Data.Registry, "pending.json");
        bool pending = fault == LegacyItemUidInterruption.AfterPendingFlush;
        Assert.Equal(pending, File.Exists(pendingPath));
        Assert.Equal(pending ? 1 : 0, ReadNamespaceState(f)["Operations"]!.AsArray().Count);
        var pendingBytes = pending ? File.ReadAllBytes(pendingPath) : null;
        await f.CloseAsync();

        bool generic = LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain,
            out var genericOwner, out var reason);
        if (pending)
        {
            Assert.False(generic); Assert.Null(genericOwner);
            Assert.Contains("decomposeNamespaceOwnedReplayRequired", reason, StringComparison.Ordinal);
            Assert.Equal(stateBytes, File.ReadAllBytes(f.Data.State)); Assert.Equal(pendingBytes, File.ReadAllBytes(pendingPath));
        }
        else
        {
            // File.Replace already consumed pending.json. No pending retirement
            // can be inferred from the identical finished namespace alone.
            Assert.True(generic, reason); using (Assert.IsType<LegacyItemUidRegistry>(genericOwner)) { }
            Assert.Equal(stateBytes, File.ReadAllBytes(f.Data.State));
        }
        using (f.Open())
        {
            Assert.False(File.Exists(pendingPath)); Assert.Empty(ReadNamespaceState(f)["Operations"]!.AsArray());
            Assert.Equal(beforeHighwater + 2, f.HighWater);
            AssertSameFiles(committedGame, WithoutNamespaceState(f.Data));
        }
        var settled = f.Data.Files();
        using (f.Open()) f.Data.AssertFiles(settled);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ForeignSpellStageRefusesBeforeOrphanOrPendingNamespaceRetirement(bool interruptedCleanup) => Run(async f =>
    {
        if (interruptedCleanup) f.Runtime.RuneDecomposeNamespaceRetirementInterruption = LegacyItemUidInterruption.AfterPendingFlush;
        else f.Runtime.RuneDecomposeNamespaceInterruption = LegacyRuneDecomposeNamespaceInterruption.AfterReservation;
        await f.SendAsync(DecomposeFixture.Request());
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); await f.CloseAsync();
        // Spells is outside the raw item scanner: actual configured full-stage
        // clearance, rather than an unrelated raw-item parse, must reject this.
        string foreign = Path.Combine(f.Data.Spells, "foreign.bin.managed-trade-unknown.tmp");
        File.WriteAllBytes(foreign, [0x45, 0x19, 0xA5]); var expected = f.Data.Files();
        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var refused, out _));
        Assert.Null(refused); f.Data.AssertFiles(expected);
    });

    [Theory]
    [InlineData("unknown-kind")] [InlineData("unknown-version")] [InlineData("null-kind")]
    [InlineData("missing-kind")] [InlineData("old-state-version")] [InlineData("wrong-writer")]
    [InlineData("unknown-member")] [InlineData("duplicate-kind")]
    public Task MalformedKindOnActuallyReservedStateCannotAuthorizeCleanup(string fault) => Run(async f =>
    {
        f.Runtime.RuneDecomposeNamespaceInterruption = LegacyRuneDecomposeNamespaceInterruption.AfterReservation;
        await f.SendAsync(DecomposeFixture.Request());
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); await f.CloseAsync();
        var state = ReadNamespaceState(f); var row = state["Operations"]![0]!.AsObject(); RequireActualKind(row);
        var kind = row["NamespaceReservation"]!.AsObject();
        switch (fault)
        {
            case "unknown-kind": kind["Kind"] = "OtherProducer"; break;
            case "unknown-version": kind["Version"] = 2; break;
            case "null-kind": row["NamespaceReservation"] = null; break;
            case "missing-kind": kind.Remove("Kind"); break;
            case "old-state-version": state["Version"] = 2; state.Remove("ProducerIntents"); break;
            case "wrong-writer": row["Writer"] = "local-gear-mail-v1"; break;
            case "unknown-member": kind["Extra"] = 0; break;
        }
        string text = state.ToJsonString();
        if (fault == "duplicate-kind")
        {
            const string identity = "\"Kind\":\"RuneDecompose\"";
            Assert.Contains(identity, text, StringComparison.Ordinal);
            text = text.Replace(identity, identity + "," + identity, StringComparison.Ordinal);
        }
        File.WriteAllBytes(f.Data.State, Encoding.UTF8.GetBytes(text)); var expected = f.Data.Files();
        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var refused, out _));
        Assert.Null(refused); f.Data.AssertFiles(expected);
    });

    [Fact]
    public Task IdenticalVersionFivePendingIsDeclaredCorruptionAndRemainsUntouched() => Run(async f =>
    {
        f.Runtime.RuneDecomposeNamespaceInterruption = LegacyRuneDecomposeNamespaceInterruption.AfterReservation;
        await f.SendAsync(DecomposeFixture.Request());
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); await f.CloseAsync();
        var state = ReadNamespaceState(f); Assert.Equal(5, state["Version"]!.GetValue<int>());
        RequireActualKind(state["Operations"]![0]!.AsObject());
        // NEGATIVE corruption only. The real File.Replace/Move cannot leave a
        // second identical pending file after a successful state replacement.
        File.Copy(f.Data.State, Path.Combine(f.Data.Registry, "pending.json"));
        var expected = f.Data.Files();
        Assert.False(LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain,
            out var generic, out var reason));
        Assert.Null(generic); Assert.Contains("identicalNamespacePendingUnsupported", reason, StringComparison.Ordinal);
        f.Data.AssertFiles(expected);
        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var owned, out reason));
        Assert.Null(owned); Assert.Contains("identicalNamespacePendingUnsupported", reason, StringComparison.Ordinal);
        f.Data.AssertFiles(expected);
        // Exclusivity probe only, never a Registry/producer permission owner.
        using (var releasedLease = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Equal(0L, releasedLease.Length);
    });

    private static JsonObject ReadNamespaceState(DecomposeFixture f) => JsonNode.Parse(File.ReadAllBytes(f.Data.State))!.AsObject();
    private static void RequireActualKind(JsonObject row)
    {
        Assert.Equal("rune-decompose-v1", row["Writer"]!.GetValue<string>());
        var kind = row["NamespaceReservation"]!.AsObject(); Assert.Equal(2, kind.Count);
        Assert.Equal(1, kind["Version"]!.GetValue<int>()); Assert.Equal("RuneDecompose", kind["Kind"]!.GetValue<string>());
    }
    private static Dictionary<string, byte[]> WithoutNamespaceState(LegacyOwnedAdmissionRuntimeFixture data)
    {
        var files = data.Files();
        files.Remove(Path.GetRelativePath(data.Root, data.State));
        files.Remove(Path.GetRelativePath(data.Root, Path.Combine(data.Registry, "pending.json")));
        return files;
    }
    private static void AssertSameFiles(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }
}
