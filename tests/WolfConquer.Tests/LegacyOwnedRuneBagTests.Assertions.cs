using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneBagTests
{
    private sealed record BagSeed(WorldSession Session, byte[] Character, byte[] Items, byte[]? Spells,
        byte[] Ledger, Dictionary<string, byte[]> Files, uint HighWater);
    private sealed record BagExpected(byte[] Character, byte[] Items, byte[] Spells,
        PlayerItemSnapshotItem Result, uint? NewUid, uint? MergeUid)
    {
        internal void AssertImages(BagFixture f)
        {
            Assert.Equal(Character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(Items, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(Spells, File.ReadAllBytes(f.SpellsPath));
        }
    }
    private static BagSeed Seed(BagFixture f) => new(f.Current, File.ReadAllBytes(f.CharacterPath), File.ReadAllBytes(f.ItemsPath),
        File.ReadAllBytes(f.SpellsPath), File.ReadAllBytes(f.Data.State), f.Data.Files(), f.HighWater);

    private static BagExpected Expect(BagFixture f, BagSeed seed, uint? mergeUid = null)
    {
        var before = seed.Session.PlayerItems;
        var target = Assert.Single(f.IntoBag ? before.InventoryItems : before.RuneItems, row => row.Uid == BagFixture.Target);
        uint? fresh = !f.IntoBag && mergeUid is null ? checked(seed.HighWater + 1) : null;
        PlayerItemSnapshotItem result;
        PlayerItemSnapshot after;
        if (f.IntoBag)
        {
            result = target.WithPosition(211);
            after = before with
            {
                InventoryItems = before.InventoryItems.Where(row => row.Uid != target.Uid).ToArray(),
                RuneItems = before.RuneItems.Append(result).OrderBy(row => row.Position)
                    .ThenBy(row => row.Position is 211 or 212 ? 0U : row.Uid).ToArray(),
            };
        }
        else
        {
            result = mergeUid is { } receiver ? Assert.Single(before.InventoryItems, row => row.Uid == receiver)
                .WithStackSize(checked((ushort)(Math.Max((uint)target.StackSize, 1U) +
                    Math.Max((uint)Assert.Single(before.InventoryItems, row => row.Uid == receiver).StackSize, 1U))))
                : target.WithUid(fresh!.Value).WithPosition(0);
            after = before with
            {
                InventoryItems = mergeUid is not null ? before.InventoryItems.Select(row => row.Uid == mergeUid ? result : row).ToArray()
                    : before.InventoryItems.Append(result).ToArray(),
                RuneItems = before.RuneItems.Where(row => row.Uid != target.Uid).ToArray(),
            };
        }
        return new(seed.Character, PlayerItemSnapshotLoader.ToLegacyBytes(after),
            seed.Spells ?? throw new InvalidDataException("Present-image expectation requires its actual nonnull Spells baseline."), result, fresh, mergeUid);
    }

    private static void AssertLedger(BagFixture f, BagSeed seed, BagExpected expected)
    {
        var bytes = File.ReadAllBytes(f.Data.State);
        if (expected.NewUid is null) { Assert.Equal(seed.Ledger, bytes); return; }
        var before = JsonNode.Parse(seed.Ledger)!.AsObject(); var after = JsonNode.Parse(bytes)!.AsObject();
        Assert.Equal(expected.NewUid.Value, after["HighWater"]!.GetValue<uint>());
        Assert.Equal(checked(before["Generation"]!.GetValue<ulong>() + 2), after["Generation"]!.GetValue<ulong>());
        Assert.True(JsonNode.DeepEquals(before["Operations"] ?? new JsonArray(), after["Operations"] ?? new JsonArray()));
        Assert.True(JsonNode.DeepEquals(before["ProducerIntents"] ?? new JsonArray(), after["ProducerIntents"] ?? new JsonArray()));
        foreach (var key in new[] { "Version", "Generation", "HighWater", "Operations", "ProducerIntents" }) { before.Remove(key); after.Remove(key); }
        Assert.True(JsonNode.DeepEquals(before, after));
        Assert.False(File.Exists(Path.Combine(f.Data.Registry, "pending.json")));
    }

    private static void AssertWholeFiles(BagFixture f, BagSeed seed, BagExpected expected)
    {
        expected.AssertImages(f); AssertLedger(f, seed, expected);
        var files = seed.Files.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        files[Path.GetRelativePath(f.Data.Root, f.ItemsPath)] = expected.Items;
        files[Path.GetRelativePath(f.Data.Root, f.Data.State)] = File.ReadAllBytes(f.Data.State);
        f.Data.AssertFiles(files);
        var ids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(seed.Items);
        var expectedIds = f.IntoBag ? ids : ids.Where(uid => uid != BagFixture.Target)
            .Concat(expected.NewUid is { } fresh ? new[] { fresh } : []);
        Assert.Equal(expectedIds.Order(), LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(expected.Items).Order());
    }

    private static async Task<BagExpected> MoveAndAssert(BagFixture f, uint? mergeUid = null, ulong returnParameter = 1)
    {
        var seed = Seed(f); var expected = Expect(f, seed, mergeUid); int first = f.Sent.Count;
        var map = f.Native.MySpells!.ClientSpells; var oldSpell = map[6001];
        f.BeforeSend = _ => { AssertWholeFiles(f, seed, expected); f.AssertConverged(); };
        try { await f.SendAsync(BagFixture.Request(f.IntoBag, returnParameter: returnParameter)); } finally { f.BeforeSend = null; }
        AssertWholeFiles(f, seed, expected); f.AssertConverged();
        bool remove = !f.IntoBag || expected.Result.ItemId / 10_000 is < 401 or > 403;
        int prefix = remove ? 3 : 2;
        Assert.Equal(first + prefix + (f.IntoBag ? 10 : 0), f.Sent.Count);
        int index = first;
        if (remove) Assert.Equal(RemoveGolden(BagFixture.Target), f.Sent[index++]);
        Assert.Equal(ItemGolden(expected.Result, mergeUid is not null), f.Sent[index++]);
        Assert.Equal(BagFixture.Request(f.IntoBag, returnParameter: returnParameter), f.Sent[index++]);
        if (f.IntoBag) LegacySelectedEquipmentQueryAssertions.NeutralQuery(f.Sent, index, BagFixture.Uid);
        Assert.Same(map, f.Native.MySpells.ClientSpells); Assert.Same(oldSpell, map[6001]);
        Assert.Equal(seed.Session.Character.Money, f.Current.Character.Money); Assert.Equal(seed.Session.Character.ConquerPoints, f.Current.Character.ConquerPoints);
        Assert.Equal(seed.Session.Character.MinHitPoints, f.Current.Character.MinHitPoints);
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == 14160);
        Assert.Equal(seed.Session.Character.Attributes.GetValueOrDefault("ManagedRuneSpellOrigins"),
            f.Current.Character.Attributes.GetValueOrDefault("ManagedRuneSpellOrigins"));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); return expected;
    }

    private sealed partial class BagFixture
    {
        internal void AssertConverged()
        {
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(Native.OrdinarySpellBinding);
            Assert.True(Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(Catalog, binding.ItemCatalog);
            Assert.Equal(ConquerProtocolProfile.VisibleDx8D13, binding.ItemProfile); Assert.True(binding.Items.Image.IsCurrentPlain());
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems), File.ReadAllBytes(ItemsPath));
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems), PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
            Assert.Equal(Current.PlayerItems.InventoryItems.Select(row => row.Uid).Order(), Native.Inventory!.ClientItems.Keys.Order());
            Assert.Equal(Current.PlayerItems.RuneItems.Select(row => row.Uid).Order(), binding.Items.Rune.Objects.Select(row => row.Uid).Order());
            foreach (var row in Current.PlayerItems.InventoryItems.Concat(Current.PlayerItems.RuneItems))
            {
                var native = row.Position == 0 ? Native.Inventory.ClientItems[row.Uid] : Assert.Single(binding.Items.Rune.Objects, item => item.Uid == row.Uid);
                Assert.Equal(row.ItemId, native.ITEM_ID); Assert.Equal(row.Position, native.Position); Assert.Equal(row.StackSize, native.StackSize);
                Assert.Equal(row.Locked, native.Locked); Assert.Equal(row.Bound, native.Bound); Assert.Equal(row.RuneExperience, native.RuneEXP);
                Assert.Equal(row.PerfectionLevel, native.PerfectionLevel); Assert.Equal(row.PerfectionProgress, native.PerfectionProgress);
                Assert.Equal(row.OwnerName, native.OwnerName); Assert.Equal(row.Signature, native.Signature);
                Assert.Equal(row.DepositedItems.Select(child => child.Uid).Order(), native.Deposite.Keys.Order());
                foreach (var child in row.DepositedItems)
                { Assert.Equal(child.ItemId, native.Deposite[child.Uid].ITEM_ID); Assert.Equal(child.PerfectionLevel, native.Deposite[child.Uid].PerfectionLevel); }
            }
            if (StartsWithoutSpells)
            {
                Assert.Empty(Current.PlayerSpells.Spells); Assert.Empty(Native.MySpells!.ClientSpells);
                if (File.Exists(SpellsPath)) Assert.Equal(new byte[4], File.ReadAllBytes(SpellsPath));
            }
            else
            {
                Assert.Equal(OriginalSpells, File.ReadAllBytes(SpellsPath));
                Assert.Equal(Current.PlayerSpells.Spells, PlayerSpellSnapshotLoader.Parse(OriginalSpells).Spells);
                Assert.Equal(new ushort[] { 6001 }, Native.MySpells!.ClientSpells.Keys.Order());
            }
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out _));
            Assert.Equal(Current.Character.MinHitPoints, saved.MinHitPoints); Assert.Equal(Current.Character.MinHitPoints, (long)Native.Player!.HitPoints);
            Assert.Equal(Current.Character.Money, saved.Money); Assert.Equal(Current.Character.Money, Native.Player.Money);
            Assert.False(LegacyTradeJournal.HasPending(Data.Users));
        }
    }

    private static byte[] RemoveGolden(uint uid)
    {
        var bytes = new byte[91]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 91); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2137);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 3); return bytes;
    }

    // Independent complete2116 layout, including the rich row's full visible
    // payload. Reserved bytes remain zero; deposited records live in Items.
    private static byte[] ItemGolden(PlayerItemSnapshotItem row, bool update = false)
    {
        var bytes = new byte[206];
        void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        U16(0, 206); U16(2, 2116); U32(4, row.Uid); U32(8, row.ItemId); U16(12, row.Durability); U16(14, row.MaximumDurability);
        U16(16, update ? (ushort)3 : (ushort)1); bytes[18] = checked((byte)row.Position); U32(19, row.SocketProgress);
        bytes[23] = row.SocketOne; bytes[24] = row.SocketTwo; U32(25, row.ItemId == BagFixture.RuneType ? 1416016U : 0);
        bytes[29] = checked((byte)row.Effect); bytes[30] = row.Plus; bytes[31] = row.Bless; bytes[32] = row.Bound; bytes[33] = row.Enchant;
        bytes[38] = row.Suspicious; bytes[40] = row.Locked; U32(42, row.PlusProgress); U32(50, row.TimeLeftInMinutes);
        U16(54, row.StackSize); U32(56, row.PerfectionLevel); U32(60, row.PerfectionProgress); U32(64, row.OwnerUid);
        Encoding.UTF8.GetBytes(row.OwnerName).CopyTo(bytes, 68); Encoding.UTF8.GetBytes(row.Signature).CopyTo(bytes, 100);
        U32(166, row.RuneExperience); U32(170, row.Attribute1); U32(174, row.Attribute2); U32(178, row.Attribute3);
        U32(182, row.Attribute4); U32(186, row.Attribute5); U32(190, row.AnimaItemId); U32(198, row.MythSoul); U32(202, row.MythSoulPoints);
        return bytes;
    }
}
