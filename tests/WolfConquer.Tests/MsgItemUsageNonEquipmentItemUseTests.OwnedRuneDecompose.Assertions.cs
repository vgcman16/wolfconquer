using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private sealed record OwnedLegacyDecomposeExpected(byte[] Items, PlayerItemSnapshotItem Essence, PlayerItemSnapshotItem Fragment);

    private static async Task<OwnedLegacyDecomposeExpected> CommitOwnedLegacyDecompose(OwnedEquipmentBehaviorFixture f,
        uint actor, uint selectedUid, ushort quantity, byte[] request)
    {
        AssertOwnedLegacyDecomposeRequestCodec(request, 9_999_999, 1, selectedUid);
        var before = f.Current(actor); var source = before.PlayerItems;
        var originalItems = PlayerItemSnapshotLoader.ToLegacyBytes(source);
        var character = File.ReadAllBytes(f.CharacterPath(actor)); string spellPath = Path.Combine(f.Data.Spells, actor + ".bin");
        var spells = File.ReadAllBytes(spellPath); var ledger = File.ReadAllBytes(f.Data.State); var files = f.Data.Files();
        uint highWater = JsonNode.Parse(ledger)!["HighWater"]!.GetValue<uint>();
        var essence = new PlayerItemSnapshotItem { Uid = checked(highWater + 1), ItemId = 4_040_001, Position = 211,
            StackSize = quantity, Durability = 1, MaximumDurability = 1 };
        OwnedLegacyDecomposeExpected? expected = null; int first = f.Frames(actor).Count;
        void Observe()
        {
            var actual = Assert.Single(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.ItemPath(actor))).InventoryItems,
                row => row.Uid == checked(highWater + 2));
            Assert.InRange(actual.Color, 3U, 8U);
            var fragment = new PlayerItemSnapshotItem { Uid = checked(highWater + 2), ItemId = 3_306_368, Position = 0,
                StackSize = 10, Bound = 1, Durability = 1, MaximumDurability = 1, Color = actual.Color };
            var after = source with { InventoryItems = source.InventoryItems.Append(fragment).ToArray(),
                RuneItems = source.RuneItems.Where(row => row.Uid != selectedUid).Append(essence)
                    .OrderBy(row => row.Position).ThenBy(row => row.Position == 211 ? 0U : row.Uid).ToArray() };
            var prepared = new OwnedLegacyDecomposeExpected(PlayerItemSnapshotLoader.ToLegacyBytes(after), essence, fragment);
            expected ??= prepared; Assert.Equal(expected.Items, prepared.Items);
            var wanted = files.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            wanted[Path.GetRelativePath(f.Data.Root, f.ItemPath(actor))] = expected.Items;
            wanted[Path.GetRelativePath(f.Data.Root, f.Data.State)] = File.ReadAllBytes(f.Data.State);
            f.Data.AssertFiles(wanted); Assert.Equal(character, File.ReadAllBytes(f.CharacterPath(actor))); Assert.Equal(spells, File.ReadAllBytes(spellPath));
            AssertOwnedLegacyDecomposeLedger(ledger, File.ReadAllBytes(f.Data.State), highWater + 2);
            AssertOwnedComposeConverged(f, actor, wanted);
        }
        f.BeforeSend = (uid, _) => { Assert.Equal(actor, uid); Observe(); };
        try { await f.SendAsync(actor, request); } finally { f.BeforeSend = null; }
        var outcome = f.Log.ToString().Split('\n').LastOrDefault(line => line.Contains("DecomposeRune", StringComparison.Ordinal))
            ?? "No DecomposeRune outcome was logged.";
        Assert.True(outcome.Contains("committed DecomposeRune", StringComparison.Ordinal), outcome);
        Observe(); var result = Assert.IsType<OwnedLegacyDecomposeExpected>(expected); var sent = f.Frames(actor);
        Assert.Equal(first + 3, sent.Count); Assert.Equal(OwnedComposeRemoveGolden(selectedUid), sent[first]);
        Assert.Equal(OwnedLegacyDecomposeRewardGolden(result.Essence), sent[first + 1]);
        Assert.Equal(OwnedLegacyDecomposeRewardGolden(result.Fragment), sent[first + 2]);
        Assert.Equal(originalItems, PlayerItemSnapshotLoader.ToLegacyBytes(source)); AssertRuneUntouchedSessionFields(before, f.Current(actor));
        Assert.Equal(result.Items, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(actor)));
        Assert.Equal(LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(originalItems).Where(uid => uid != selectedUid)
            .Concat(new[] { highWater + 1, highWater + 2 }).Order(), LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(result.Items).Order());
        Assert.Contains("committed DecomposeRune", f.Log.ToString(), StringComparison.Ordinal); return result;
    }

    private static async Task RefuseOwnedLegacyDecompose(OwnedEquipmentBehaviorFixture f, uint actor, byte[] request)
    {
        var current = f.Current(actor); var files = f.Data.Files(); int first = f.Frames(actor).Count;
        var native = current.NativeGameClient!; var binding = native.OrdinarySpellBinding!;
        var image = binding.Items.Image; var spellMap = native.MySpells!.ClientSpells;
        await f.SendAsync(actor, request);
        Assert.Equal(first, f.Frames(actor).Count); Assert.Same(current, f.Current(actor)); f.Data.AssertFiles(files);
        Assert.Same(image, binding.Items.Image); Assert.Same(spellMap, native.MySpells.ClientSpells); Assert.True(image.IsCurrentPlain());
        AssertOwnedComposeConverged(f, actor, files); Assert.False(f.Recovery.IsBlocked);
        Assert.DoesNotContain("committed DecomposeRune", f.Log.ToString(), StringComparison.Ordinal);
    }

    private static void AssertOwnedLegacyDecomposeLedger(byte[] oldBytes, byte[] newBytes, uint highWater)
    {
        var before = JsonNode.Parse(oldBytes)!.AsObject(); var after = JsonNode.Parse(newBytes)!.AsObject();
        Assert.Equal(5, after["Version"]!.GetValue<int>()); Assert.Equal(highWater, after["HighWater"]!.GetValue<uint>());
        Assert.Equal(before["Generation"]!.GetValue<ulong>() + 2, after["Generation"]!.GetValue<ulong>());
        Assert.True(JsonNode.DeepEquals(before["Operations"] ?? new JsonArray(), after["Operations"] ?? new JsonArray()));
        Assert.True(JsonNode.DeepEquals(before["ProducerIntents"] ?? new JsonArray(), after["ProducerIntents"] ?? new JsonArray()));
        foreach (var key in new[] { "Version", "Generation", "HighWater", "Operations", "ProducerIntents" }) { before.Remove(key); after.Remove(key); }
        Assert.True(JsonNode.DeepEquals(before, after));
    }

    private static void AssertOwnedLegacyDecomposeAfterQuery(OwnedEquipmentBehaviorFixture f, uint actor, byte[] items, byte[] spells, byte[] ledger)
    {
        // A genuine status query may canonically publish/clamp HP. Keep exact
        // item/spell/namespace bytes and current native ownership, not old holder identity.
        var current = f.Current(actor); Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
        Assert.Equal(items, File.ReadAllBytes(f.ItemPath(actor)));
        Assert.Equal(spells, File.ReadAllBytes(Path.Combine(f.Data.Spells, actor.ToString(CultureInfo.InvariantCulture) + ".bin")));
        Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
        var binding = current.NativeGameClient!.OrdinarySpellBinding!; Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal(current.Character.MinHitPoints, (long)current.NativeGameClient.Player!.HitPoints);
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath(actor)), out _));
        Assert.Equal(current.Character.MinHitPoints, saved.MinHitPoints); Assert.Equal(current.Character.Money, saved.Money);
        Assert.Equal(current.Character.ConquerPoints, saved.ConquerPoints);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }

    private static void AssertOwnedLegacyDecomposeRequestCodec(byte[] actual, uint packetId, uint count, params uint[] args)
    {
        // Retained static codec evidence only, not native D11 admission.
        var bytes = new byte[91 + args.Length * sizeof(uint)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length)); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2137);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), packetId); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 65);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(18), 511_043_495); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(22), count);
        for (int index = 0; index < args.Length; index++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(91 + index * sizeof(uint)), args[index]);
        Assert.Equal(bytes, actual);
    }

    private static byte[] OwnedLegacyDecomposeRewardGolden(PlayerItemSnapshotItem item)
    {
        var bytes = new byte[206]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 206); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2116);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), item.Uid); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), item.ItemId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 1); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 1); bytes[18] = checked((byte)item.Position); bytes[32] = item.Bound;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), item.StackSize); return bytes;
    }
}
