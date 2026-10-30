using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class LegacyOwnedRuneGameplayFixture
{
    internal void AssertMarker(string? expected)
    {
        if (expected is null) Assert.False(Current.Character.Attributes.ContainsKey(Marker));
        else { Assert.True(Current.Character.Attributes.TryGetValue(Marker, out var actual)); Assert.Equal(expected, actual); }
    }
    internal void AssertConverged()
    {
        var current = Current; var native = Native;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
        Assert.True(Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(Catalog, binding.ItemCatalog);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems), File.ReadAllBytes(ItemsPath));
        Assert.Equal(current.PlayerSpells.Spells, PlayerSpellSnapshotLoader.Parse(File.ReadAllBytes(SpellsPath)).Spells);
        Assert.Equal(current.PlayerSpells.Spells.Select(row => row.Id).Order(), native.MySpells!.ClientSpells.Keys.Order());
        foreach (var row in current.PlayerSpells.Spells)
        {
            var physical = native.MySpells.ClientSpells[row.Id];
            Assert.Equal(row.Level, physical.Level); Assert.Equal(row.Experience, physical.Experience);
            Assert.Equal(row.PreviousLevel, physical.PreviousLevel); Assert.Equal(row.SoulLevel, physical.SoulLevel);
            Assert.Equal(row.UseSpellSoul, physical.UseSpellSoul);
        }
        var character = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out _));
        Assert.Equal(current.Character.MinHitPoints, character.MinHitPoints); Assert.Equal(current.Character.Money, character.Money);
        Assert.Equal(current.Character.Attributes.GetValueOrDefault(Marker), character.Attributes.GetValueOrDefault(Marker));
        Assert.False(LegacyTradeJournal.HasPending(Data.Users));
        if (Registry.Length != 0) Assert.Equal(Registry, File.ReadAllBytes(Data.State));
        Assert.Equal(OriginalSpells.AsSpan(sizeof(int)).ToArray(), File.ReadAllBytes(SpellsPath).AsSpan(sizeof(int), OriginalSpells.Length - sizeof(int)).ToArray());
    }
    internal async Task AssertRefusedAsync(Func<Task> action)
    {
        var before = Current; var files = Data.Files(); int first = Sent.Count;
        await action(); Assert.Same(before, Current); Assert.Equal(first, Sent.Count); Data.AssertFiles(files);
        Assert.False(LegacyTradeJournal.HasPending(Data.Users)); Assert.False(Runtime.Recovery.IsBlocked);
    }
    internal Task StageAsync(int boundary, MsgItemUsagePacket request) =>
        ProduceAsync(LegacyJournalInterruption.StopAfter(boundary), request, boundary);
    internal Task StopAfterStageFlushedAsync(int ordinal, MsgItemUsagePacket request) =>
        ProduceAsync(LegacyJournalInterruption.StopAfterStageFlushed(ordinal), request, null);
    private async Task ProduceAsync(LegacyJournalInterruption fault, MsgItemUsagePacket request, int? boundary)
    {
        Before = new[] { CharacterPath, ItemsPath, SpellsPath }.ToDictionary(path => path,
            path => (byte[]?)File.ReadAllBytes(path), StringComparer.OrdinalIgnoreCase);
        var original = Current; int first = Sent.Count;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(Native.OrdinarySpellBinding);
        var nativeMap = Native.MySpells!.ClientSpells; var nativeItems = binding.Items.Image;
        using (fault)
        {
            Handler.ItemSpellJournalInterruption = fault;
            try { await DispatchAsync(request); } finally { Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved, Log.ToString());
            if (boundary is { } count) Assert.Equal(Enumerable.Range(0, count + 1), fault.Visited);
        }
        Assert.True(Runtime.Recovery.IsBlocked); Assert.Same(original, Current); Assert.Equal(first, Sent.Count);
        Assert.False(Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Same(nativeMap, Native.MySpells.ClientSpells); Assert.Same(nativeItems, binding.Items.Image);
        var record = JsonNode.Parse(File.ReadAllBytes(PendingPath))!; Assert.Equal(14, record["Version"]!.GetValue<int>());
        Assert.NotNull(record["RuneOperation"]); After = new(StringComparer.OrdinalIgnoreCase);
        foreach (var change in record["Changes"]!.AsArray())
        {
            Assert.Equal((long)Uid, change!["FileId"]!.GetValue<long>());
            var path = (LegacyTradeJournalFileKind)change["Kind"]!.GetValue<int>() switch
            {
                LegacyTradeJournalFileKind.Character => CharacterPath,
                LegacyTradeJournalFileKind.Items => ItemsPath,
                LegacyTradeJournalFileKind.Spells => SpellsPath,
                _ => throw new InvalidDataException("Unexpected genuine Rune target."),
            };
            Assert.Equal(Before[path], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.True(After.TryAdd(path, Convert.FromBase64String(change["After"]!.GetValue<string>())));
        }
        Assert.Equal(3, After.Count); Assert.Equal(Registry, File.ReadAllBytes(Data.State));
    }
    internal void AssertRecovered()
    {
        Assert.False(LegacyTradeJournal.HasPending(Data.Users));
        foreach (var (path, bytes) in After) Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(Registry, File.ReadAllBytes(Data.State));
        Assert.Equal(LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(Before[ItemsPath]!).Order(),
            LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(After[ItemsPath]).Order());
    }
    internal async Task ReopenFrameAsync()
    {
        Runtime = Open();
        await LoginOpenedFrameAsync();
    }
    internal async Task LoginOpenedFrameAsync()
    {
        Handler = new WorldServerFrameHandler(Data.Loader, null, Catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
            Log, protocolProfile: WolfConquer.Core.Runtime.ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
        await LoginAsync(); AssertConverged();
    }
    internal void AssertStartupRefused()
    {
        var files = Data.Files(); bool success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason);
        if (opened is not null) runtimes.Add(opened);
        Assert.False(success, reason); Assert.Null(opened); Assert.NotEmpty(reason); Data.AssertFiles(files);
        Assert.True(LegacyTradeJournal.HasPending(Data.Users));
        using var lease = new FileStream(Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
}
