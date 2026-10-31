using System.Buffers.Binary;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneDecomposeTests
{
    private sealed record DecomposeSeed(WorldSession Session, byte[] Character, byte[] Items, byte[] Spells,
        byte[] Ledger, Dictionary<string, byte[]> Files, uint HighWater);
    private sealed record ExpectedDecompose(byte[] Character, byte[] Items, byte[] Spells, uint[] NewUids,
        PlayerItemSnapshotItem[] Essences, PlayerItemSnapshotItem Fragment, bool Merged, ushort[] RetiredSpells)
    {
        internal void AssertImages(DecomposeFixture f)
        {
            Assert.Equal(Character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(Items, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(Spells, File.ReadAllBytes(f.SpellsPath));
        }
    }

    private static DecomposeSeed Seed(DecomposeFixture f) => new(f.Current, File.ReadAllBytes(f.CharacterPath), File.ReadAllBytes(f.ItemsPath),
        File.ReadAllBytes(f.SpellsPath), File.ReadAllBytes(f.Data.State), f.Data.Files(), f.HighWater);

    private static ExpectedDecompose Expect(DecomposeSeed seed, byte? fragmentColor)
    {
        var before = seed.Session.PlayerItems; var target = Assert.Single(before.RuneItems, row => row.Uid == DecomposeFixture.Target);
        uint family = target.ItemId / 10000, level = target.ItemId % 100;
        uint cumulative = family == 402 && level == 17 ? 94000U : family == 402 && level == 27 ? 341500U : 100U;
        uint quantity = checked((uint)(((ulong)cumulative + target.RuneExperience) * 2UL / 25UL));
        uint essenceId = family switch { 401 => 4_060_001, 402 => 4_040_001, _ => 4_050_001 };
        uint fragmentId = family switch { 401 => target.Bound == 0 ? 3_306_366U : 3_306_369U,
            402 => target.Bound == 0 ? 3_306_368U : 3_306_371U, _ => target.Bound == 0 ? 3_306_367U : 3_306_370U };
        var existing = before.InventoryItems.FirstOrDefault(row => row.ItemId == fragmentId && row.Bound == 1 && row.Position == 0 &&
            row.WarehouseId == 0 && Math.Max((uint)row.StackSize, 1U) + 10 <= 10000);
        int count = checked((int)((quantity + 9999U) / 10000U));
        var uids = Enumerable.Range(1, count + (existing is null ? 1 : 0)).Select(index => checked(seed.HighWater + (uint)index)).ToArray();
        var essences = new PlayerItemSnapshotItem[count]; uint remaining = quantity;
        for (int index = 0; index < count; index++)
        {
            essences[index] = DecomposeFixture.Item(uids[index], essenceId, 211).WithStackSize(checked((ushort)Math.Min(10000U, remaining))).WithColor(0);
            remaining -= essences[index].StackSize;
        }
        PlayerItemSnapshotItem fragment;
        if (existing is not null)
        {
            Assert.Null(fragmentColor); fragment = existing.WithStackSize(checked((ushort)(Math.Max((uint)existing.StackSize, 1U) + 10)));
        }
        else
        {
            Assert.NotNull(fragmentColor); Assert.InRange(fragmentColor!.Value, (byte)3, (byte)8);
            fragment = new() { Uid = uids[^1], ItemId = fragmentId, Position = 0, Bound = 1, Color = fragmentColor.Value,
                StackSize = 10, Durability = 1, MaximumDurability = 1 };
        }
        var inventory = existing is null ? before.InventoryItems.Concat([fragment]).ToArray()
            : before.InventoryItems.Select(row => row.Uid == fragment.Uid ? fragment : row).ToArray();
        var runes = before.RuneItems.Where(row => row.Uid != target.Uid).Concat(essences)
            .OrderBy(row => row.Position).ThenBy(row => row.Position == 211 ? 0U : row.Uid).ToArray();
        var after = before with { InventoryItems = inventory, RuneItems = runes };
        bool marked = seed.Session.Character.Attributes.TryGetValue(DecomposeFixture.Marker, out var marker) && !string.IsNullOrEmpty(marker);
        bool shared = before.RuneItems.Any(row => row.Uid != target.Uid && row.Position is 102 or 122 && row.ItemId / 10000 == 402);
        ushort[] retired = marked && !shared ? [(ushort)14160] : [];
        var character = seed.Character; var spells = seed.Spells;
        if (retired.Length != 0)
        {
            var text = CharacterSnapshotFileEncoding.Decode(character, out var encoding);
            string old = DecomposeFixture.Marker + "=" + marker + "\r\n"; Assert.Contains(old, text, StringComparison.Ordinal);
            character = encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace(old, DecomposeFixture.Marker + "=\r\n", StringComparison.Ordinal))).ToArray();
            int oldCount = BinaryPrimitives.ReadInt32LittleEndian(spells);
            var rows = Enumerable.Range(0, oldCount).Select(index => spells.AsSpan(4 + index * 12, 12).ToArray())
                .Where(row => !retired.Contains(BinaryPrimitives.ReadUInt16LittleEndian(row))).ToArray();
            spells = new byte[4 + rows.Length * 12]; BinaryPrimitives.WriteInt32LittleEndian(spells, rows.Length);
            for (int index = 0; index < rows.Length; index++) rows[index].CopyTo(spells, 4 + index * 12);
        }
        return new(character, PlayerItemSnapshotLoader.ToLegacyBytes(after), spells, uids, essences, fragment, existing is not null, retired);
    }

    private static void AssertLedger(DecomposeFixture f, DecomposeSeed seed, ExpectedDecompose expected)
    {
        var before = JsonNode.Parse(seed.Ledger)!.AsObject(); var after = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!.AsObject();
        Assert.Equal(5, after["Version"]!.GetValue<int>());
        Assert.Equal(checked(seed.HighWater + (uint)expected.NewUids.Length), after["HighWater"]!.GetValue<uint>());
        Assert.Equal(checked(before["Generation"]!.GetValue<ulong>() + 2), after["Generation"]!.GetValue<ulong>());
        Assert.True(JsonNode.DeepEquals(before["Operations"] ?? new JsonArray(), after["Operations"] ?? new JsonArray()));
        Assert.True(JsonNode.DeepEquals(before["ProducerIntents"] ?? new JsonArray(), after["ProducerIntents"] ?? new JsonArray()));
        foreach (var key in new[] { "Version", "Generation", "HighWater", "Operations", "ProducerIntents" }) { before.Remove(key); after.Remove(key); }
        Assert.True(JsonNode.DeepEquals(before, after));
        Assert.False(File.Exists(Path.Combine(f.Data.Registry, "pending.json"))); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }

    private static void AssertWholeFiles(DecomposeFixture f, DecomposeSeed seed, ExpectedDecompose expected)
    {
        expected.AssertImages(f); AssertLedger(f, seed, expected);
        var files = seed.Files.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        files[Path.GetRelativePath(f.Data.Root, f.CharacterPath)] = expected.Character;
        files[Path.GetRelativePath(f.Data.Root, f.ItemsPath)] = expected.Items;
        files[Path.GetRelativePath(f.Data.Root, f.SpellsPath)] = expected.Spells;
        files[Path.GetRelativePath(f.Data.Root, f.Data.State)] = File.ReadAllBytes(f.Data.State);
        f.Data.AssertFiles(files);
        Assert.Equal(LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(seed.Items).Where(uid => uid != DecomposeFixture.Target)
            .Concat(expected.NewUids).Order(), LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(expected.Items).Order());
    }

    private sealed partial class DecomposeFixture
    {
        internal void AssertConverged()
        {
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(Native.OrdinarySpellBinding);
            Assert.True(Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(Catalog, binding.ItemCatalog);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems), File.ReadAllBytes(ItemsPath));
            Assert.Equal(Current.PlayerSpells.Spells, PlayerSpellSnapshotLoader.Parse(File.ReadAllBytes(SpellsPath)).Spells);
            Assert.Equal(Current.PlayerSpells.Spells.Select(row => row.Id).Order(), Native.MySpells!.ClientSpells.Keys.Order());
            foreach (var row in Current.PlayerSpells.Spells)
            {
                var actual = Native.MySpells.ClientSpells[row.Id]; Assert.Equal(row.Level, actual.Level); Assert.Equal(row.Experience, actual.Experience);
                Assert.Equal(row.PreviousLevel, actual.PreviousLevel); Assert.Equal(row.SoulLevel, actual.SoulLevel); Assert.Equal(row.UseSpellSoul, actual.UseSpellSoul);
            }
            foreach (var row in Current.PlayerItems.RuneItems)
            {
                var actual = Assert.Single(binding.Items.Rune.Objects, item => item.Uid == row.Uid);
                Assert.Equal(row.ItemId, actual.ITEM_ID); Assert.Equal(row.StackSize, actual.StackSize); Assert.Equal(row.Position, actual.Position);
                Assert.Equal(row.RuneExperience, actual.RuneEXP); Assert.Equal(row.Color, (uint)actual.Color);
            }
            foreach (var row in Current.PlayerItems.InventoryItems)
            {
                var actual = Native.Inventory!.ClientItems[row.Uid]; Assert.Equal(row.ItemId, actual.ITEM_ID);
                Assert.Equal(row.StackSize, actual.StackSize); Assert.Equal(row.Bound, actual.Bound); Assert.Equal(row.Locked, actual.Locked);
            }
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out _));
            Assert.Equal(Current.Character.Money, saved.Money); Assert.Equal(Current.Character.ConquerPoints, saved.ConquerPoints);
            Assert.Equal(Current.Character.Money, Native.Player!.Money); Assert.Equal((uint)Current.Character.ConquerPoints, Native.Player.ConquerPoints);
            Assert.Equal(Current.Character.MinHitPoints, saved.MinHitPoints); Assert.Equal(Current.Character.MinHitPoints, (long)Native.Player.HitPoints);
            Assert.Equal(Origins, saved.Attributes.GetValueOrDefault(Marker)); Assert.False(LegacyTradeJournal.HasPending(Data.Users));
            Assert.Equal(OriginalSpells.AsSpan(4, 12).ToArray(), File.ReadAllBytes(SpellsPath).AsSpan(4, 12).ToArray());
        }
    }

    private static byte[] RemoveGolden(uint uid)
    {
        var bytes = new byte[91]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 91); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2137);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 3); return bytes;
    }
    private static byte[] RewardGolden(PlayerItemSnapshotItem row, bool update = false)
    {
        // Complete independent2116 layout; these explicit reward inputs have
        // zero runeSpell/effect/attributes/perfection and owner/signature tail.
        var bytes = new byte[206]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 206); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2116);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), row.Uid); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), row.ItemId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 1); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), update ? (ushort)3 : (ushort)1); bytes[18] = checked((byte)row.Position);
        bytes[32] = row.Bound; bytes[40] = row.Locked; BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), row.StackSize); return bytes;
    }
}
