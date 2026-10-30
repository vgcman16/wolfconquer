using System.Buffers;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

// SYNTHETIC LEGACY SAVED INPUT is seeded before bootstrap/admission by the
// declared fixture mode. No former producer or historical positive WAL is run.
// Every operation and interrupted journal below uses the real current producer.
public sealed partial class LegacyOwnedRuneGameplayTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task CurrentProducerRetiresOnlyTheExactDeclaredLegacyPassiveGrant(bool drop) => Run(async f =>
    {
        AssertDeclaredLegacyPassiveInput(f, "owned");
        var expected = LegacyPassiveExpectedImages(f, drop); var before = f.Current;
        var retained = f.Native.MySpells!.ClientSpells[6001]; int first = f.Sent.Count, observed = 0;
        f.BeforeSend = _ =>
        {
            foreach (var pair in expected) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            f.AssertConverged(); f.AssertMarker(""); Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == 40301);
            Assert.Equal(f.Registry, File.ReadAllBytes(f.Data.State)); observed++;
        };
        try { await f.DispatchAsync(LegacyPassiveRemoval(drop)); } finally { f.BeforeSend = null; }
        Assert.Equal(drop ? 2 : 6, observed); Assert.Equal(first + observed, f.Sent.Count);
        AssertLegacyPassiveRemovalFrames(f, first, drop);
        Assert.Same(retained, f.Native.MySpells.ClientSpells[6001]);
        Assert.Equal(before.Character.Money, f.Current.Character.Money); Assert.Equal(before.Character.ConquerPoints, f.Current.Character.ConquerPoints);
    }, passiveFirst: true, legacyPassiveInput: "owned");

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(3)]
    public Task CurrentV14RecoversDeclaredLegacyPassiveCleanupAtSelectedRealPrefixes(int prefix) => Run(async f =>
    {
        AssertDeclaredLegacyPassiveInput(f, "owned"); var expected = LegacyPassiveExpectedImages(f, drop: false);
        await f.StageAsync(prefix, LegacyPassiveRemoval(drop: false));
        foreach (var pair in expected) Assert.Equal(pair.Value, f.After[pair.Key]);
        var paths = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        for (var index = 0; index < paths.Length; index++)
            Assert.Equal(index < prefix ? f.After[paths[index]] : f.Before[paths[index]], File.ReadAllBytes(paths[index]));
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.AssertRecovered(); f.AssertMarker("");
        Assert.Equal(f.OriginalSpells, File.ReadAllBytes(f.SpellsPath));
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == 40301);
        var recovered = f.Data.Files(); await f.CloseAsync(); f.Open(); f.AssertRecovered(); f.Data.AssertFiles(recovered);
    }, passiveFirst: true, legacyPassiveInput: "owned");

    [Fact]
    public Task CurrentV15RecoversFlushedDeclaredLegacyPassiveDropWithoutReplayingIt() => Run(async f =>
    {
        AssertDeclaredLegacyPassiveInput(f, "owned"); var expected = LegacyPassiveExpectedImages(f, drop: true);
        var before = expected.Keys.ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        var original = f.Current; int first = f.Sent.Count;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var nativeMap = f.Native.MySpells!.ClientSpells; var nativeItems = binding.Items.Image;
        using (var fault = LegacyJournalInterruption.StopAfterStageFlushed(3))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.DispatchAsync(LegacyPassiveRemoval(drop: true)); } finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString());
        }
        Assert.Same(original, f.Current); Assert.Equal(first, f.Sent.Count); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(nativeMap, f.Native.MySpells.ClientSpells);
        Assert.Same(nativeItems, binding.Items.Image); Assert.Equal(f.Registry, File.ReadAllBytes(f.Data.State));
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!; Assert.Equal(15, record["Version"]!.GetValue<int>());
        Assert.NotNull(record["RuneContainer"]!["Transform"]); Assert.Null(record["RuneOperation"]);
        Assert.Equal(3, record["Changes"]!.AsArray().Count); var transaction = record["TransactionId"]!.GetValue<string>();
        foreach (var change in record["Changes"]!.AsArray())
        {
            Assert.Equal((long)F.Uid, change!["FileId"]!.GetValue<long>());
            var path = (LegacyTradeJournalFileKind)change["Kind"]!.GetValue<int>() switch
            {
                LegacyTradeJournalFileKind.Character => f.CharacterPath,
                LegacyTradeJournalFileKind.Items => f.ItemsPath,
                LegacyTradeJournalFileKind.Spells => f.SpellsPath,
                _ => throw new InvalidDataException("Unexpected actual declared-legacy Drop target."),
            };
            Assert.Equal(before[path], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.Equal(expected[path], Convert.FromBase64String(change["After"]!.GetValue<string>()));
            Assert.Equal(before[path], File.ReadAllBytes(path));
            Assert.Equal(expected[path], File.ReadAllBytes(path + ".managed-trade-" + transaction + ".tmp"));
        }
        Assert.Equal(LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(before[f.ItemsPath]).Where(uid => uid != F.First).Order(),
            LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(expected[f.ItemsPath]).Order());
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.AssertConverged(); f.AssertMarker("");
        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            Assert.False(File.Exists(pair.Key + ".managed-trade-" + transaction + ".tmp"));
        }
        Assert.DoesNotContain(f.Current.PlayerItems.RuneItems, row => row.Uid == F.First);
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == 40301);
        var recovered = f.Data.Files(); await f.CloseAsync(); f.Open(); f.Data.AssertFiles(recovered);
        Assert.Equal(f.Registry, File.ReadAllBytes(f.Data.State)); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }, passiveFirst: true, legacyPassiveInput: "owned");

    [Theory]
    [InlineData("changed")] [InlineData("opaque")] [InlineData("unmatched")] [InlineData("stored")]
    public Task CurrentRuneActionCannotRetireChangedAmbiguousOrUnboundDeclaredOrigins(string mode) => Run(async f =>
    {
        AssertDeclaredLegacyPassiveInput(f, mode);
        await f.AssertRefusedAsync(() => f.DispatchAsync(LegacyPassiveRemoval(drop: false)));
    }, passiveFirst: true, legacyPassiveInput: mode);

    [Theory]
    [InlineData("unmarked")] [InlineData("permanent")]
    public Task AnUnmarkedOrProgressedLegacyRowRemainsUnknownPermanentProvenance(string mode) => Run(async f =>
    {
        AssertDeclaredLegacyPassiveInput(f, mode);
        var spells = File.ReadAllBytes(f.SpellsPath); var character = File.ReadAllBytes(f.CharacterPath);
        var retained = f.Native.MySpells!.ClientSpells[40301]; var expectedItems = LegacyPassiveItemImage(f, drop: false);
        int first = f.Sent.Count;
        await f.DispatchAsync(LegacyPassiveRemoval(drop: false));
        Assert.Equal(first + 5, f.Sent.Count); Assert.Equal(LegacyPassiveRemoval(drop: false).ToFrameBytes(), f.Sent[first]);
        Assert.All(f.Sent.Skip(first), bytes => Assert.False(MsgActionPacket.TryParse(LegacyPassiveFrame(bytes), out var action) &&
            action.Type == (MsgActionType)109));
        Assert.Equal(spells, File.ReadAllBytes(f.SpellsPath)); Assert.Equal(character, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(expectedItems, File.ReadAllBytes(f.ItemsPath)); Assert.Same(retained, f.Native.MySpells.ClientSpells[40301]);
        f.AssertConverged(); f.AssertMarker(null); Assert.Equal(f.Registry, File.ReadAllBytes(f.Data.State));
    }, passiveFirst: true, legacyPassiveInput: mode);

    [Fact]
    public Task AnActualMappedSharedAbilitySurvivesLaterPassiveItemRemoval() => Run(async f =>
    {
        AssertDeclaredLegacyPassiveInput(f, "owned");
        await f.DispatchAsync(F.Equip(F.Shared, 102));
        f.AssertConverged(); f.AssertMarker("14160:17");
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == 40301);
        Assert.Equal(new PlayerSpellSnapshotEntry(14160, 17, 0, 0, 0, 0), Assert.Single(f.Current.PlayerSpells.Spells, row => row.Id == 14160));
        var spells = File.ReadAllBytes(f.SpellsPath); var ability = f.Native.MySpells!.ClientSpells[14160]; int first = f.Sent.Count;
        await f.DispatchAsync(LegacyPassiveRemoval(drop: false));
        Assert.Equal(first + 5, f.Sent.Count); Assert.Equal(LegacyPassiveRemoval(drop: false).ToFrameBytes(), f.Sent[first]);
        Assert.Equal(spells, File.ReadAllBytes(f.SpellsPath)); Assert.Same(ability, f.Native.MySpells.ClientSpells[14160]);
        f.AssertConverged(); f.AssertMarker("14160:17");
        Assert.Equal((ushort)102, Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == F.Shared).Position);
        Assert.Equal(f.Registry, File.ReadAllBytes(f.Data.State));
    }, passiveFirst: true, legacyPassiveInput: "owned");

    private static void AssertDeclaredLegacyPassiveInput(F f, string mode)
    {
        var selected = Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == F.First);
        Assert.Equal(F.PassiveType, selected.ItemId); Assert.Equal(mode == "stored" ? (ushort)212 : (ushort)103, selected.Position);
        var row = Assert.Single(f.Current.PlayerSpells.Spells, spell => spell.Id == 40301);
        var level = mode == "unmatched" ? (ushort)3 : (ushort)2;
        Assert.Equal(mode == "permanent" ? new PlayerSpellSnapshotEntry(40301, level, 1234, 2, 3, 1)
            : new PlayerSpellSnapshotEntry(40301, level, mode == "changed" ? 1 : 0, 0, 0, 0), row);
        if (mode is "unmarked" or "permanent") f.AssertMarker(null); else f.AssertMarker("40301:" + level);
        var image = File.ReadAllBytes(f.SpellsPath); Assert.Equal(28, image.Length);
        Assert.Equal(f.OriginalSpells.AsSpan(sizeof(int)).ToArray(), image.AsSpan(sizeof(int), f.OriginalSpells.Length - sizeof(int)).ToArray());
        Assert.Equal(mode == "opaque" ? (byte)0xC7 : (byte)0, image[^1]);
        Assert.DoesNotContain(f.Catalog.MagicEffects, entry => entry.Id == 4_030_100);
        f.AssertConverged();
    }

    private static Dictionary<string, byte[]> LegacyPassiveExpectedImages(F f, bool drop)
    {
        var old = File.ReadAllBytes(f.CharacterPath); var text = CharacterSnapshotFileEncoding.Decode(old, out var encoding);
        Assert.Contains("ManagedRuneSpellOrigins=40301:2\r\n", text);
        var changed = text.Replace("ManagedRuneSpellOrigins=40301:2\r\n", "ManagedRuneSpellOrigins=\r\n", StringComparison.Ordinal);
        return new(StringComparer.OrdinalIgnoreCase)
        {
            [f.CharacterPath] = encoding.GetPreamble().Concat(encoding.GetBytes(changed)).ToArray(),
            [f.ItemsPath] = LegacyPassiveItemImage(f, drop), [f.SpellsPath] = f.OriginalSpells.ToArray(),
        };
    }

    private static byte[] LegacyPassiveItemImage(F f, bool drop) => PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems with
    { RuneItems = f.Current.PlayerItems.RuneItems.Where(row => !drop || row.Uid != F.First)
        .Select(row => row.Uid == F.First ? row.WithPosition(212) : row)
        .OrderBy(row => row.Position).ThenBy(row => row.Position is 211 or 212 ? 0u : row.Uid).ToArray() });

    private static MsgItemUsagePacket LegacyPassiveRemoval(bool drop) => drop
        ? new(F.First, 0, MsgItemUsageAction.DropRune, 1234, 22, 33, 44)
        : new(F.First, 103, MsgItemUsageAction.UnequipRune, 112244, 77, 88, 99);

    private static void AssertLegacyPassiveRemovalFrames(F f, int first, bool drop)
    {
        Assert.Equal(new MsgActionPacket { ObjId = F.Uid, DwParam = 40301, Type = (MsgActionType)109 }.ToFrameBytes(), f.Sent[first]);
        Assert.Equal(drop ? new MsgItemUsagePacket(F.First, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes()
            : LegacyPassiveRemoval(drop: false).ToFrameBytes(), f.Sent[first + 1]);
    }

    private static PacketFrame LegacyPassiveFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame;
    }
}
