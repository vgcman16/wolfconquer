using System.Buffers;
using System.Buffers.Binary;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneGameplayTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualHigherRawSelfRuneKeepsItsItemIdentityWithoutGrantingOrRemovingASpell(bool drop) => Run(async f =>
    {
        await EquipActualPassiveRune(f);
        var character = File.ReadAllBytes(f.CharacterPath); var ledger = File.ReadAllBytes(f.Data.State);
        var expected = PassiveItemImage(f, drop); int first = f.Sent.Count, observed = 0;
        f.BeforeSend = _ =>
        {
            Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(expected, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(f.OriginalSpells, File.ReadAllBytes(f.SpellsPath)); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
            AssertPassiveBook(f); observed++;
        };
        var request = PassiveRemovalRequest(drop);
        try { await f.DispatchAsync(request); } finally { f.BeforeSend = null; }
        Assert.Equal(drop ? 1 : 5, observed); Assert.Equal(first + observed, f.Sent.Count);
        Assert.Equal(drop ? new MsgItemUsagePacket(F.First, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes()
            : request.ToFrameBytes(), f.Sent[first]);
        Assert.All(f.Sent.Skip(first), bytes => Assert.False(MsgActionPacket.TryParse(PassiveRuneFrame(bytes), out var action) &&
            action.Type == (MsgActionType)109));
        Assert.Equal(drop, !f.Current.PlayerItems.RuneItems.Any(row => row.Uid == F.First));
        if (!drop) AssertPassiveDisplay(f);
    }, passiveFirst: true);

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(3)]
    public Task GenuinePassiveV14RecoversSelectedReplacementPrefixesWithoutPhantomSkills(int prefix) => Run(async f =>
    {
        await EquipActualPassiveRune(f);
        var expectedItems = PassiveItemImage(f, drop: false); var character = File.ReadAllBytes(f.CharacterPath);
        await f.StageAsync(prefix, PassiveRemovalRequest(drop: false));
        Assert.Equal(character, f.After[f.CharacterPath]); Assert.Equal(expectedItems, f.After[f.ItemsPath]);
        Assert.Equal(f.OriginalSpells, f.After[f.SpellsPath]);
        var targets = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        for (var index = 0; index < targets.Length; index++)
            Assert.Equal(index < prefix ? f.After[targets[index]] : f.Before[targets[index]], File.ReadAllBytes(targets[index]));
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.AssertRecovered(); AssertPassiveBook(f); AssertPassiveDisplay(f);
        var recovered = f.Data.Files(); await f.CloseAsync(); f.Open(); f.AssertRecovered(); f.Data.AssertFiles(recovered);
    }, passiveFirst: true);

    [Fact]
    public Task GenuinePassiveV15FlushedDropRecoversExactRetirementWithoutTouchingTheSpellBook() => Run(async f =>
    {
        await EquipActualPassiveRune(f);
        var expected = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            [f.CharacterPath] = File.ReadAllBytes(f.CharacterPath), [f.ItemsPath] = PassiveItemImage(f, drop: true),
            [f.SpellsPath] = f.OriginalSpells.ToArray(),
        };
        var before = expected.Keys.ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        var original = f.Current; int first = f.Sent.Count;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var nativeMap = f.Native.MySpells!.ClientSpells; var nativeItems = binding.Items.Image;
        using (var fault = LegacyJournalInterruption.StopAfterStageFlushed(3))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.DispatchAsync(PassiveRemovalRequest(drop: true)); } finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString());
        }
        Assert.Same(original, f.Current); Assert.Equal(first, f.Sent.Count); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(nativeMap, f.Native.MySpells.ClientSpells);
        Assert.Same(nativeItems, binding.Items.Image); Assert.Equal(f.Registry, File.ReadAllBytes(f.Data.State));
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!; Assert.Equal(15, record["Version"]!.GetValue<int>());
        Assert.NotNull(record["RuneContainer"]!["Transform"]); Assert.Null(record["RuneOperation"]);
        Assert.Equal(3, record["Changes"]!.AsArray().Count);
        var transaction = record["TransactionId"]!.GetValue<string>();
        foreach (var change in record["Changes"]!.AsArray())
        {
            Assert.Equal((long)F.Uid, change!["FileId"]!.GetValue<long>());
            var path = (LegacyTradeJournalFileKind)change["Kind"]!.GetValue<int>() switch
            {
                LegacyTradeJournalFileKind.Character => f.CharacterPath,
                LegacyTradeJournalFileKind.Items => f.ItemsPath,
                LegacyTradeJournalFileKind.Spells => f.SpellsPath,
                _ => throw new InvalidDataException("Unexpected actual passive Drop target."),
            };
            Assert.Equal(before[path], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.Equal(expected[path], Convert.FromBase64String(change["After"]!.GetValue<string>()));
            Assert.Equal(before[path], File.ReadAllBytes(path));
            Assert.Equal(expected[path], File.ReadAllBytes(path + ".managed-trade-" + transaction + ".tmp"));
        }
        Assert.Equal(LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(before[f.ItemsPath]).Where(uid => uid != F.First).Order(),
            LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(expected[f.ItemsPath]).Order());
        await f.CloseAsync(); await f.ReopenFrameAsync(); AssertPassiveBook(f);
        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            Assert.False(File.Exists(pair.Key + ".managed-trade-" + transaction + ".tmp"));
        }
        Assert.DoesNotContain(f.Current.PlayerItems.RuneItems, row => row.Uid == F.First);
        var recovered = f.Data.Files(); await f.CloseAsync(); f.Open(); f.Data.AssertFiles(recovered);
        Assert.Equal(f.Registry, File.ReadAllBytes(f.Data.State)); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }, passiveFirst: true);

    [Theory]
    [InlineData("phantom-classification")] [InlineData("phantom-after-spell")]
    public Task GenuinePassivePendingRecordCannotAuthorizeAClassifiedOrPersistedPhantomAbility(string fault) => Run(async f =>
    {
        await EquipActualPassiveRune(f); await f.StageAsync(3, PassiveRemovalRequest(drop: false)); await f.CloseAsync();
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!;
        if (fault == "phantom-classification")
        {
            var metadata = record["RuneOperation"]!["Metadata"]!.AsArray().Single(row =>
                row!["ItemId"]!.GetValue<uint>() == F.PassiveType)!;
            Assert.Null(metadata["IsRuneSkill"]); metadata["IsRuneSkill"] = true;
        }
        else
        {
            var change = record["Changes"]!.AsArray().Single(row => row!["Kind"]!.GetValue<int>() ==
                (int)LegacyTradeJournalFileKind.Spells)!;
            var old = Convert.FromBase64String(change["After"]!.GetValue<string>());
            var phantom = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot([new(40301, 2, 0, 0, 0, 0)]));
            var changed = old.Concat(phantom.Skip(sizeof(int))).ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(changed, BinaryPrimitives.ReadInt32LittleEndian(old) + 1);
            Assert.Equal(old.AsSpan(sizeof(int)).ToArray(), changed.AsSpan(sizeof(int), old.Length - sizeof(int)).ToArray());
            change["After"] = Convert.ToBase64String(changed); File.WriteAllBytes(f.SpellsPath, changed);
        }
        File.WriteAllText(f.PendingPath, record.ToJsonString()); f.AssertStartupRefused();
    }, passiveFirst: true);

    private static async Task EquipActualPassiveRune(F f)
    {
        Assert.Equal(F.PassiveType, Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == F.First).ItemId);
        Assert.DoesNotContain(f.Catalog.MagicEffects, row => row.Id == 4_030_100);
        var initial = Assert.Single(f.Sent.Select(bytes => LegacyGameItemPacket.TryParse(PassiveRuneFrame(bytes), out var packet)
            ? packet : null).OfType<LegacyGameItemPacket>(), row => row.Uid == F.First);
        Assert.Equal(F.PassiveType, initial.ItemId); Assert.Equal(4_030_101U, initial.SpellId); Assert.Equal((byte)1, initial.Effect);
        var character = File.ReadAllBytes(f.CharacterPath); var ledger = File.ReadAllBytes(f.Data.State);
        var expected = PassiveItemImage(f, drop: false, position: 103); int first = f.Sent.Count, observed = 0;
        var request = F.Equip(F.First, 103);
        f.BeforeSend = _ =>
        {
            Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(expected, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(f.OriginalSpells, File.ReadAllBytes(f.SpellsPath)); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
            AssertPassiveBook(f); observed++;
        };
        try { await f.DispatchAsync(request); } finally { f.BeforeSend = null; }
        Assert.Equal(6, observed); Assert.Equal(first + 6, f.Sent.Count);
        Assert.Equal(new MsgItemUsagePacket(F.First, 212, MsgItemUsageAction.UnequipRune, 0, 0, 0, 0).ToFrameBytes(), f.Sent[first]);
        Assert.Equal(request.ToFrameBytes(), f.Sent[first + 1]);
        Assert.Equal((ushort)103, Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == F.First).Position);
        AssertPassiveDisplay(f);
    }

    private static byte[] PassiveItemImage(F f, bool drop, ushort position = 212) => PlayerItemSnapshotLoader.ToLegacyBytes(
        f.Current.PlayerItems with { RuneItems = f.Current.PlayerItems.RuneItems.Where(row => !drop || row.Uid != F.First)
            .Select(row => row.Uid == F.First ? row.WithPosition(position) : row)
            .OrderBy(row => row.Position).ThenBy(row => row.Position is 211 or 212 ? 0u : row.Uid).ToArray() });

    private static MsgItemUsagePacket PassiveRemovalRequest(bool drop) => drop
        ? new(F.First, 0, MsgItemUsageAction.DropRune, 1234, 22, 33, 44)
        : new(F.First, 103, MsgItemUsageAction.UnequipRune, 112244, 77, 88, 99);

    private static void AssertPassiveBook(F f)
    {
        f.AssertConverged(); f.AssertMarker(null);
        Assert.Equal(f.OriginalSpells, File.ReadAllBytes(f.SpellsPath));
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == 40301);
        Assert.DoesNotContain(f.Native.MySpells!.ClientSpells.Keys, id => id == 40301);
        Assert.Equal((byte)0xA5, File.ReadAllBytes(f.SpellsPath)[15]);
    }

    private static void AssertPassiveDisplay(F f)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var item = Assert.Single(binding.Items.Rune.Objects, row => row.Uid == F.First);
        Assert.Equal(F.PassiveType, item.ITEM_ID); Assert.Equal(4_030_101U, item.SpellID);
        Assert.Equal(LegacyNativeGameItemFlags.ItemEffect.RuneEffect, item.Effect);
        var saved = Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == F.First);
        Assert.Equal(saved.Position, item.Position); Assert.Equal((ushort)10, saved.Durability);
        Assert.Equal((ushort)10, saved.MaximumDurability); Assert.Equal((ushort)1, saved.StackSize);
    }

    private static PacketFrame PassiveRuneFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
