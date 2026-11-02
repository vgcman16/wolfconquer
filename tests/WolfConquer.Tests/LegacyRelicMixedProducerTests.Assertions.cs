using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicMixedProducerTests
{
    private sealed record Seed(WorldSession Session, Dictionary<string, byte[]> Files,
        byte[] Items, byte[] Registry, uint HighWater, ulong Generation, int Version, LegacyOwnedNativeItemImage NativeImage);

    private static Seed Capture(MixedFixture f) => new(f.Current, f.Data.Files(), File.ReadAllBytes(f.ItemsPath),
        File.ReadAllBytes(f.Data.State), f.HighWater, f.State()["Generation"]!.GetValue<ulong>(), f.State()["Version"]!.GetValue<int>(), f.Native.OrdinarySpellBinding!.Items.Image);

    private static void AssertSavedMutation(MixedFixture f, Seed before, PlayerItemSnapshot expected, bool issued, int version)
    {
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
        Assert.Equal(bytes, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems));
        Assert.NotSame(before.Session, f.Current);
        Assert.NotSame(before.NativeImage, f.Native.OrdinarySpellBinding!.Items.Image);
        f.AssertSettled(issued ? checked(before.HighWater + 1) : before.HighWater, version);
        if (!issued) Assert.Equal(before.Registry, File.ReadAllBytes(f.Data.State));
        else
        {
            var old = JsonNode.Parse(before.Registry)!.AsObject(); var current = f.State();
            Assert.True(current["Generation"]!.GetValue<ulong>() > before.Generation, "A real namespace issuance must publish a successor registry generation.");
            Assert.Equal(before.Version, old["Version"]!.GetValue<int>());
            Assert.Equal(version, current["Version"]!.GetValue<int>());
            foreach (var key in new[] { "Operations", "ProducerIntents" })
            {
                Assert.True(JsonNode.DeepEquals(old[key] ?? new JsonArray(), current[key] ?? new JsonArray()));
                old.Remove(key); current.Remove(key);
            }
            foreach (var key in new[] { "Generation", "HighWater", "Version" }) { old.Remove(key); current.Remove(key); }
            Assert.True(JsonNode.DeepEquals(old, current));
        }
        var files = before.Files.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        files[Path.GetRelativePath(f.Data.Root, f.ItemsPath)] = bytes;
        files[Path.GetRelativePath(f.Data.Root, f.Data.State)] = File.ReadAllBytes(f.Data.State);
        f.Data.AssertFiles(files);
        Assert.Equal(before.Session.Character.Money, f.Current.Character.Money);
        Assert.Equal(before.Session.Character.ConquerPoints, f.Current.Character.ConquerPoints);
        Assert.Equal(before.Session.Character.BoundConquerPoints, f.Current.Character.BoundConquerPoints);
        f.AssertConverged();
    }

    private sealed partial class MixedFixture
    {
        internal void AssertSettled(uint highwater, int version = 7)
        {
            var state = State(); Assert.Equal(version, state["Version"]!.GetValue<int>());
            Assert.Equal(Data.ExpectedDomain, Guid.Parse(state["Domain"]!.GetValue<string>()));
            Assert.Equal(highwater, state["HighWater"]!.GetValue<uint>());
            Assert.Empty(state["Operations"]?.AsArray() ?? new JsonArray());
            Assert.Empty(state["ProducerIntents"]?.AsArray() ?? new JsonArray());
            Assert.False(File.Exists(Path.Combine(Data.Registry, "pending.json")));
            Assert.False(LegacyTradeJournal.HasPending(Data.Users));
        }

        internal void AssertConverged()
        {
            var session = Current;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(Native.OrdinarySpellBinding);
            Assert.True(Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(Catalog, binding.ItemCatalog);
            Assert.Equal(ConquerProtocolProfile.VisibleDx8D13, binding.ItemProfile);
            Assert.True(binding.Items.Image.IsCurrentPlain());
            var image = PlayerItemSnapshotLoader.ToLegacyBytes(session.PlayerItems);
            Assert.Equal(image, File.ReadAllBytes(ItemsPath));
            Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
            var roots = new[] { session.PlayerItems.InventoryItems, session.PlayerItems.RelicFuseItems,
                session.PlayerItems.MythsoulBagItems, session.PlayerItems.EquipmentItems, session.PlayerItems.RuneItems,
                session.PlayerItems.WardrobeItems, session.PlayerItems.WarehouseItems }.SelectMany(rows => rows).ToArray();
            var ids = roots.SelectMany(row => new[] { row.Uid }.Concat(row.DepositedItems.Select(child => child.Uid))).ToArray();
            Assert.Equal(ids.Length, ids.Distinct().Count());
            Assert.All(ids, uid => Assert.InRange(uid, 1U, HighWater));
            Assert.Equal(ids.Order(), LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(image).Order());
            Assert.Equal(session.PlayerItems.InventoryItems.Select(row => row.Uid).Order(), Native.Inventory!.ClientItems.Keys.Order());
            Assert.Equal(session.PlayerItems.RuneItems.Select(row => row.Uid).Order(), binding.Items.Rune.Objects.Select(row => row.Uid).Order());
            Assert.Equal(session.PlayerItems.RelicFuseItems.Select(row => row.Uid).Order(), Native.RelicFuse!.Values.Select(row => row.Uid).Order());
            foreach (var row in session.PlayerItems.InventoryItems.Concat(session.PlayerItems.RuneItems).Concat(session.PlayerItems.RelicFuseItems))
            {
                var native = row.Position == 0 ? Native.Inventory.ClientItems[row.Uid] : row.Position == 211
                    ? Assert.Single(binding.Items.Rune.Objects, item => item.Uid == row.Uid) : Native.RelicFuse.Values.Single(item => item.Uid == row.Uid);
                Assert.Equal(row.ItemId, native.ITEM_ID); Assert.Equal(row.Position, native.Position);
                Assert.Equal(row.StackSize, native.StackSize); Assert.Equal(row.Durability, native.Durability);
                Assert.Equal(row.MaximumDurability, native.MaximDurability); Assert.Equal(row.Bound, native.Bound); Assert.Equal(row.Locked, native.Locked);
                Assert.Equal(row.OwnerName, native.OwnerName); Assert.Equal(row.Signature, native.Signature);
                Assert.Equal(new[] { row.Attribute1, row.Attribute2, row.Attribute3, row.Attribute4, row.Attribute5 },
                    native.RelicAttributes.Select(attribute => (uint)attribute));
            }
            Assert.Equal(File.ReadAllBytes(SpellsPath), PlayerSpellSnapshotLoader.ToLegacyBytes(session.PlayerSpells));
            Assert.Equal(session.PlayerSpells.Spells.Select(row => row.Id).Order(), Native.MySpells!.ClientSpells.Keys.Order());
            var character = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out _));
            Assert.Equal(session.Character.Money, character.Money); Assert.Equal(session.Character.Money, Native.Player!.Money);
            Assert.Equal(session.Character.MinHitPoints, character.MinHitPoints);
            Assert.Equal(session.Character.MinHitPoints, (long)Native.Player.HitPoints);
            Assert.False(LegacyTradeJournal.HasPending(Data.Users));
        }
    }
}
