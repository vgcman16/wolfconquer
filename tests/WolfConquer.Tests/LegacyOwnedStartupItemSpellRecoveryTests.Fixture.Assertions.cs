using System.Buffers.Binary;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class LegacyOwnedStartupItemSpellRecoveryFixture
{
    internal LegacyOwnedAdmissionRuntime Open()
    {
        var success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason);
        if (opened is not null) runtimes.Add(opened);
        Assert.True(success, reason); var runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
        Assert.True(runtime.Ordinary.IsReconciliationComplete);
        Assert.True(runtime.Recovery.IsReadyForOrdinaryAdmission(Data.Options().Roots)); return runtime;
    }

    private void AssertExpectedCapturedImages()
    {
        var characterBefore = Before[CharacterPath]!;
        if (mode is "rune-equip" or "rune-displace")
        {
            var text = CharacterSnapshotFileEncoding.Decode(characterBefore, out var encoding);
            Assert.Contains("[Unrelated]", text, StringComparison.Ordinal);
            var expected = text.Replace("[Unrelated]", "ManagedRuneSpellOrigins=14160:16\r\n[Unrelated]", StringComparison.Ordinal);
            Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(expected)).ToArray(), After[CharacterPath]);
        }
        else Assert.Equal(characterBefore, After[CharacterPath]);
        var items = PlayerItemSnapshotLoader.Parse(Before[ItemsPath]!);
        byte[] expectedSpells;
        if (mode == "book")
        {
            var book = Assert.Single(items.InventoryItems, row => row.Uid == ItemUid);
            items = items with { InventoryItems = items.InventoryItems.Where(row => Stack > 1 || row.Uid != ItemUid)
                .Select(row => row.Uid == ItemUid ? book.WithStackSize(checked((ushort)(Stack - 1))) : row).ToArray() };
            var prior = Before[SpellsPath] ?? new byte[sizeof(int)];
            expectedSpells = new byte[prior.Length + PlayerSpellSnapshotLoader.LegacySpellSize];
            prior.CopyTo(expectedSpells, 0);
            BinaryPrimitives.WriteInt32LittleEndian(expectedSpells, BinaryPrimitives.ReadInt32LittleEndian(prior) + 1);
            BinaryPrimitives.WriteUInt16LittleEndian(expectedSpells.AsSpan(prior.Length), LearnedSpell);
            Assert.Equal(new PlayerSpellSnapshotEntry(LearnedSpell, 0, 0, 0, 0, 0),
                Assert.Single(PlayerSpellSnapshotLoader.Parse(After[SpellsPath]).Spells, row => row.Id == LearnedSpell));
        }
        else
        {
            items = items with { RuneItems = items.RuneItems.Select(row => row.Uid == ItemUid
                ? row.WithPosition(mode is "rune-equip" or "rune-displace" ? (ushort)102 : (ushort)212)
                : mode == "rune-displace" ? row.WithPosition(212) : row)
                .OrderBy(row => row.Position).ThenBy(row => row.Position is 211 or 212 ? 0U : row.Uid).ToArray() };
            if (mode is "rune-equip" or "rune-displace")
            {
                var prior = Before[SpellsPath] ?? new byte[sizeof(int)];
                expectedSpells = new byte[prior.Length + PlayerSpellSnapshotLoader.LegacySpellSize]; prior.CopyTo(expectedSpells, 0);
                BinaryPrimitives.WriteInt32LittleEndian(expectedSpells, BinaryPrimitives.ReadInt32LittleEndian(prior) + 1);
                BinaryPrimitives.WriteUInt16LittleEndian(expectedSpells.AsSpan(prior.Length), RuneSpell);
                BinaryPrimitives.WriteUInt16LittleEndian(expectedSpells.AsSpan(prior.Length + 2), 16);
            }
            else expectedSpells = Before[SpellsPath]?.ToArray() ?? new byte[sizeof(int)];
        }
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(items), After[ItemsPath]);
        Assert.Equal(expectedSpells, After[SpellsPath]);
    }

    internal void AssertRecoveredImages()
    {
        Assert.False(LegacyTradeJournal.HasPending(Data.Users));
        foreach (var (path, bytes) in After) Assert.Equal(bytes, File.ReadAllBytes(path));
        AssertExpectedCapturedImages(); Assert.Equal(RegistryState, File.ReadAllBytes(Data.State));
        Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor, JsonNode.Parse(RegistryState)!["HighWater"]!.GetValue<uint>());
        var beforeUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(Before[ItemsPath]!);
        var afterUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(After[ItemsPath]);
        Assert.All(afterUids, uid => Assert.Contains(uid, beforeUids));
        Assert.Equal(beforeUids.Length - (mode == "book" && Stack == 1 ? 1 : 0), afterUids.Length);
        if (Before[SpellsPath] is not null)
            Assert.Equal(0xA5, After[SpellsPath][15]);
        else Assert.Equal(0, After[SpellsPath][^1]);
        var held = Assert.Single(new PlayerItemSnapshotLoader(Data.Items).LoadByCharacterUid(Uid).WarehouseItems);
        Assert.Equal(WarehouseUid, held.Uid); Assert.Equal(DepositUid, Assert.Single(held.DepositedItems).Uid);
        Assert.Equal((byte)1, held.Bound); Assert.Equal((byte)1, held.Locked);
        Assert.Equal(1U, held.PerfectionLevel); Assert.Equal("RecordedOwner", held.OwnerName); Assert.Equal("item-spell", held.Signature);
    }

    private void AssertNativeMatchesCurrent(bool expectedBlocked = false)
    {
        var current = Current; var client = current.NativeGameClient!;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
        Assert.Equal(expectedBlocked, binding.Recovery.IsBlocked);
        Assert.Equal(!expectedBlocked, Store.IsBoundSpellOwnerCurrent(binding));
        var rows = Assert.IsType<LegacyNativeSpellStorage>(client.MySpells).ClientSpells;
        Assert.Equal(current.PlayerSpells.Spells.Select(row => row.Id).Order(), rows.Keys.Order());
        foreach (var row in current.PlayerSpells.Spells)
        {
            Assert.True(rows.TryGetValue(row.Id, out var actual));
            Assert.Equal(row.Level, actual!.Level); Assert.Equal(row.Experience, actual.Experience);
            Assert.Equal(row.PreviousLevel, actual.PreviousLevel); Assert.Equal(row.SoulLevel, actual.SoulLevel);
            Assert.Equal(row.UseSpellSoul, actual.UseSpellSoul);
        }
        Assert.Equal(current.Character.Money, client.Player.Money); Assert.Equal(current.Character.MinHitPoints, client.Player.HitPoints);
    }

    internal void AssertRefused(LegacyOwnedAdmissionOptions? options = null, bool leaseAvailable = true)
    {
        var files = Data.Files();
        var success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, options ?? Data.Options(), out var opened, out var reason);
        if (opened is not null) runtimes.Add(opened);
        Assert.False(success, reason); Assert.Null(opened); Assert.NotEmpty(reason);
        Data.AssertFiles(files); Assert.True(LegacyTradeJournal.HasPending(Data.Users));
        if (leaseAvailable) AssertLeaseAvailable();
    }
    internal void AssertLeaseAvailable()
    { using var lease = new FileStream(Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.Equal(0L, lease.Length); }
    internal void Own(IDisposable resource) => resources.Add(resource);
}
