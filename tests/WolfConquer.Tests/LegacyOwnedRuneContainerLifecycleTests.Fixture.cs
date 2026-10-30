using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedRuneContainerLifecycleTests
{
    private static async Task Run(Func<F, Task> body, bool legacy = false)
    {
        var f = new F(); var errors = new List<Exception>();
        try { await f.InitializeAsync(legacy, 0, 0, false); await body(f); } catch (Exception error) { errors.Add(error); }
        try { await f.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { f.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private static Task Send(F f, string kind, uint uid = F.First) => kind switch
    {
        "store" => f.SendAsync(new LegacyRuneStoragePacket(0, uid, 0).ToFrameBytes()),
        "bag" => f.SendAsync(new LegacyRuneStoragePacket(1, uid, 0).ToFrameBytes()),
        "drop" => f.DispatchAsync(new MsgItemUsagePacket(uid, 0, MsgItemUsageAction.DropRune, 1234, 22, 33, 44)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static async Task PrepareClaim(F f, string kind)
    {
        // Shared's real4020518 shares the storage base with First4020517.
        // Move it through the actual bag route before a successful type0.
        if (kind == "store") await Send(f, "bag", F.Shared);
        await f.DispatchAsync(F.Equip()); f.AssertMarker("14160:16");
    }

    private sealed record Captured(Dictionary<string, byte[]?> Before, Dictionary<string, byte[]> After, byte[] Ledger)
    {
        internal void AssertRecovered(F f, string kind)
        {
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            foreach (var (path, image) in After) Assert.Equal(image, File.ReadAllBytes(path));
            Assert.Equal(Ledger, File.ReadAllBytes(f.Data.State));
            var oldIds = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(Before[f.ItemsPath]!).Order();
            var newIds = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(After[f.ItemsPath]).Order();
            Assert.Equal(kind == "drop" ? oldIds.Where(uid => uid != F.First) : oldIds, newIds);
        }
    }

    private static async Task<Captured> Stage(F f, string kind, int boundary, bool flushed = false)
    {
        await PrepareClaim(f, kind);
        var targets = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        var before = targets.ToDictionary(path => path, path => (byte[]?)File.ReadAllBytes(path), StringComparer.OrdinalIgnoreCase);
        var ledger = File.ReadAllBytes(f.Data.State); var original = f.Current; int first = f.Sent.Count;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var nativeMap = f.Native.MySpells!.ClientSpells; var nativeItems = binding.Items.Image;
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await Send(f, kind); } finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString());
            if (!flushed) Assert.Equal(Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Same(original, f.Current); Assert.Equal(first, f.Sent.Count);
        Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(nativeMap, f.Native.MySpells.ClientSpells);
        Assert.Same(nativeItems, binding.Items.Image); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!;
        Assert.Equal(15, record["Version"]!.GetValue<int>()); Assert.NotNull(record["RuneContainer"]); Assert.Null(record["RuneOperation"]);
        var after = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in record["Changes"]!.AsArray())
        {
            Assert.Equal((long)F.Uid, change!["FileId"]!.GetValue<long>());
            var path = (LegacyTradeJournalFileKind)change["Kind"]!.GetValue<int>() switch
            {
                LegacyTradeJournalFileKind.Character => f.CharacterPath,
                LegacyTradeJournalFileKind.Items => f.ItemsPath,
                LegacyTradeJournalFileKind.Spells => f.SpellsPath,
                _ => throw new InvalidDataException("Unexpected genuine container target."),
            };
            Assert.Equal(before[path], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.True(after.TryAdd(path, Convert.FromBase64String(change["After"]!.GetValue<string>())));
        }
        Assert.Equal(3, after.Count); return new(before, after, ledger);
    }

    private static string StagePath(F f, string target) => target + ".managed-trade-" +
        JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!["TransactionId"]!.GetValue<string>() + ".tmp";
}
