using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    private static void BagAuthSeedActorInBag(CreationFixture f)
    {
        // Declared saved input BEFORE bootstrap: same existing physical UID,
        // never a post-birth item injection or newborn eligibility change.
        var loader = new PlayerItemSnapshotLoader(f.Data.Items); var source = loader.LoadByCharacterUid(CreationFixture.RuneActor);
        Assert.Equal(CreationFixture.RuneUid, Assert.Single(source.RuneItems).Uid);
        loader.SaveByCharacterUid(CreationFixture.RuneActor, source with
        { RuneItems = source.RuneItems.Select(row => row.WithPosition(211)).ToArray() });
    }

    private static string BagAuthActorPath(CreationFixture f, LegacyTradeJournalFileKind kind) =>
        f.Data.Options().Roots.PathFor(kind, CreationFixture.RuneActor);

    private static LegacyItemUidProducerIntentSnapshot BagAuthCompleted(CreationFixture f, uint account)
    {
        var registry = f.Runtime.Ordinary.Registry;
        Assert.True(registry.TryReadProducerIntents(f.Data.ExpectedDomain, out var all, out var reason), reason);
        var completed = Assert.Single(all, row => row.Request.AccountId == account);
        Assert.True(registry.TryReadActiveProducerIntents(f.Data.ExpectedDomain, out var active, out reason), reason); Assert.Empty(active);
        Assert.Equal(f.Data.ExpectedDomain, completed.DomainId); Assert.Equal(account, completed.Request.CharacterUid);
        Assert.Equal(LegacyOrdinaryAdmissionUidOwner.Writer, completed.Request.Writer); Assert.NotNull(completed.Allocation);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(completed.CopyPayload())).ToLowerInvariant(), completed.Request.PayloadSha256);
        return completed;
    }

    private static void BagAuthAssertCompletedUnchanged(CreationFixture f, LegacyItemUidProducerIntentSnapshot expected)
    {
        var actual = BagAuthCompleted(f, expected.Request.AccountId);
        Assert.Equal(expected.Request, actual.Request); Assert.Equal(expected.PreparedGeneration, actual.PreparedGeneration);
        Assert.Equal(expected.Allocation, actual.Allocation); Assert.Equal(expected.CopyPayload(), actual.CopyPayload());
    }

    private static IEnumerable<uint> BagAuthRange(LegacyItemUidReservationReceipt receipt) =>
        Enumerable.Range(0, receipt.Count).Select(index => checked(receipt.First + (uint)index));

    private static uint[] BagAuthPhysicalUids(CreationFixture f)
    {
        var ids = Directory.EnumerateFiles(f.Data.Items, "*.bin").SelectMany(path =>
            LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(File.ReadAllBytes(path))).Order().ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count()); Assert.All(ids, uid => Assert.InRange(uid, 1U, f.Ledger().HighWater));
        return ids;
    }

    private static Dictionary<string, byte[]> BagAuthExpectedFiles(CreationFixture f, byte[] actorItems)
    {
        var files = f.Data.Files();
        files[Path.GetRelativePath(f.Data.Root, BagAuthActorPath(f, LegacyTradeJournalFileKind.Items))] = actorItems;
        return files;
    }

    private static void BagAuthAssertGameFiles(CreationFixture f, Dictionary<string, byte[]> files)
    {
        var expected = files.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        expected[Path.GetRelativePath(f.Data.Root, f.Data.State)] = File.ReadAllBytes(f.Data.State); f.Data.AssertFiles(expected);
    }

    private static void BagAuthAssertNamespaceStep(CreationFixture f, byte[] previous, uint births, ulong generations,
        int version, bool pending = false)
    {
        var before = JsonNode.Parse(previous)!.AsObject(); var after = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!.AsObject();
        Assert.Equal(version, after["Version"]!.GetValue<int>());
        Assert.Equal(before["HighWater"]!.GetValue<uint>() + births, after["HighWater"]!.GetValue<uint>());
        Assert.Equal(before["Generation"]!.GetValue<ulong>() + generations, after["Generation"]!.GetValue<ulong>());
        Assert.Empty(after["ProducerIntents"]!.AsArray());
        if (pending)
        {
            var row = Assert.Single(after["Operations"]!.AsArray())!;
            Assert.Equal("RuneDecompose", row["NamespaceReservation"]!["Kind"]!.GetValue<string>()); Assert.Equal(2, row["Count"]!.GetValue<int>());
        }
        else Assert.Empty(after["Operations"]!.AsArray());
        foreach (string key in new[] { "Version", "HighWater", "Generation", "Operations", "ProducerIntents" }) { before.Remove(key); after.Remove(key); }
        Assert.True(JsonNode.DeepEquals(before, after));
    }

    private static void BagAuthAssertActor(CreationFixture f, byte[] items, byte[] spells)
    {
        var current = f.CurrentFor(f.RuneConnection!); var native = current.NativeGameClient!; var binding = native.OrdinarySpellBinding!;
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Items.Image.IsCurrentPlain());
        Assert.Equal(items, File.ReadAllBytes(BagAuthActorPath(f, LegacyTradeJournalFileKind.Items)));
        Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
        Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
        Assert.Equal(current.PlayerItems.InventoryItems.Select(row => row.Uid).Order(), native.Inventory!.ClientItems.Keys.Order());
        Assert.Equal(current.PlayerItems.RuneItems.Select(row => row.Uid).Order(), native.Rune!.Objects.Select(row => row.Uid).Order());
        foreach (var row in current.PlayerItems.InventoryItems.Concat(current.PlayerItems.RuneItems))
        {
            var physical = row.Position == 0 ? native.Inventory.ClientItems[row.Uid] : Assert.Single(native.Rune.Objects, item => item.Uid == row.Uid);
            Assert.Equal(row.ItemId, physical.ITEM_ID); Assert.Equal(row.Position, physical.Position);
            Assert.Equal(row.StackSize, physical.StackSize); Assert.Equal(row.RuneExperience, physical.RuneEXP);
            Assert.Equal(row.Durability, physical.Durability); Assert.Equal(row.MaximumDurability, physical.MaximDurability);
            Assert.Equal(row.Bound, physical.Bound); Assert.Equal(row.Locked, physical.Locked);
        }
        Assert.Equal(spells, File.ReadAllBytes(BagAuthActorPath(f, LegacyTradeJournalFileKind.Spells)));
        Assert.Equal(new ushort[] { 6001 }, native.MySpells!.ClientSpells.Keys.Order());
        Assert.Equal(current.Character.Money, native.Player.Money); Assert.Equal(current.Character.MinHitPoints, (long)native.Player.HitPoints);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }

    private static void BagAuthAssertSecondCreator(CreationFixture f, Dictionary<string, byte[]> before, uint secondAccount,
        byte[] previousState, Dictionary<string, byte[]> firstHistory, byte[] newborn, byte[] actorCharacter, byte[] actorItems, byte[] actorSpells)
    {
        var completed = BagAuthCompleted(f, secondAccount); var recipe = Recipe(f, completed);
        var beforeState = JsonNode.Parse(previousState)!; var state = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
        Assert.Equal(6, state["Version"]!.GetValue<int>()); Assert.Equal(2UL, state["ArchiveCount"]!.GetValue<ulong>());
        Assert.Equal(beforeState["HighWater"]!.GetValue<uint>() + 1, completed.Allocation!.First);
        Assert.Equal(4, completed.Allocation.Count);
        Assert.Equal(beforeState["HighWater"]!.GetValue<uint>() + 4, state["HighWater"]!.GetValue<uint>());
        Assert.Equal(beforeState["Generation"]!.GetValue<ulong>() + 3, state["Generation"]!.GetValue<ulong>());
        Assert.Empty(state["ProducerIntents"]!.AsArray()); Assert.Empty(state["Operations"]!.AsArray());
        var expected = before.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var image in recipe.AfterImages(completed.Allocation))
        {
            if (image.Key == LegacyTradeJournalFileKind.Mailbox) { Assert.Null(image.Value); continue; }
            byte[] actualExpected = image.Key == LegacyTradeJournalFileKind.Items ? ExpectedStarterItems(recipe.Projection, completed.Allocation)
                : image.Key == LegacyTradeJournalFileKind.Character
                    ? LegacyOrdinaryAdmissionImages.ProjectCharacter(recipe.Projection.CopyCharacterImage(), recipe.Projection.PreparedAt) : image.Value!;
            expected[Path.GetRelativePath(f.Data.Root, f.Data.Options().Roots.PathFor(image.Key, secondAccount))] = actualExpected;
        }
        var history = HistoryFiles(f);
        foreach (var pair in firstHistory) Assert.Equal(pair.Value, history[pair.Key]);
        foreach (var pair in history) expected[pair.Key] = pair.Value;
        expected[Path.GetRelativePath(f.Data.Root, f.Data.State)] = File.ReadAllBytes(f.Data.State); f.Data.AssertFiles(expected);
        Assert.Equal(newborn, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(actorCharacter, File.ReadAllBytes(f.Data.UserPath));
        BagAuthAssertActor(f, actorItems, actorSpells); Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);
    }

    private static byte[] BagAuthRequest(bool returned, uint uid) => new MsgItemUsagePacket(uid, returned ? 1UL : 0UL,
        MsgItemUsageAction.Rune, 511_043_495, 112244, 55, 66).ToFrameBytes();

    private static void BagAuthAssertUsage(byte[] bytes, uint uid, MsgItemUsageAction action)
    {
        Assert.True(MsgItemUsagePacket.TryParse(CreationFixture.ReadFrame(bytes), out var packet));
        var expected = new byte[91]; BinaryPrimitives.WriteUInt16LittleEndian(expected, 91);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 2137);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4), uid);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(16), (ushort)action);
        Assert.Equal(expected, bytes);
        Assert.Equal(uid, packet.Id); Assert.Equal(action, packet.Action);
    }

    private static void BagAuthAssertItem(byte[] bytes, PlayerItemSnapshotItem expected)
    {
        Assert.True(LegacyGameItemPacket.TryParse(CreationFixture.ReadFrame(bytes), out var item));
        // Complete independent2116 image for this declared neutral root. The
        // saved row's complete payload is checked separately in all3 images.
        var golden = new byte[206]; BinaryPrimitives.WriteUInt16LittleEndian(golden, 206);
        BinaryPrimitives.WriteUInt16LittleEndian(golden.AsSpan(2), 2116);
        BinaryPrimitives.WriteUInt32LittleEndian(golden.AsSpan(4), expected.Uid);
        BinaryPrimitives.WriteUInt32LittleEndian(golden.AsSpan(8), expected.ItemId);
        BinaryPrimitives.WriteUInt16LittleEndian(golden.AsSpan(12), expected.Durability);
        BinaryPrimitives.WriteUInt16LittleEndian(golden.AsSpan(14), expected.MaximumDurability);
        BinaryPrimitives.WriteUInt16LittleEndian(golden.AsSpan(16), 1); golden[18] = checked((byte)expected.Position);
        BinaryPrimitives.WriteUInt32LittleEndian(golden.AsSpan(25), 1_416_016);
        BinaryPrimitives.WriteUInt16LittleEndian(golden.AsSpan(54), expected.StackSize);
        BinaryPrimitives.WriteUInt32LittleEndian(golden.AsSpan(166), expected.RuneExperience);
        Assert.Equal(golden, bytes);
        Assert.Equal(206, bytes.Length); Assert.Equal(expected.Uid, item.Uid); Assert.Equal(expected.ItemId, item.ItemId);
        Assert.Equal(checked((byte)expected.Position), item.Position); Assert.Equal(LegacyGameItemPacket.AddItemMode, item.Mode);
        Assert.Equal(expected.StackSize, item.StackSize); Assert.Equal(expected.RuneExperience, item.RuneExperience);
        Assert.Equal(expected.Locked, item.Locked); Assert.Equal(expected.Bound, item.Bound);
        Assert.Equal(expected.Durability, item.Durability); Assert.Equal(expected.MaximumDurability, item.MaximumDurability);
        Assert.Equal(1_416_016U, item.SpellId);
    }

    private static byte[] BagAuthDecomposeRequest(uint uid)
    {
        var writer = new WolfConquer.Core.Packets.PacketWriter(WorldPacketOpcodes.MsgItemUsage, MsgItemUsagePacket.PayloadLength + sizeof(uint));
        writer.WriteUInt32(0, uint.MaxValue); writer.WriteUInt16(12, (ushort)MsgItemUsageAction.DecomposeRune);
        writer.WriteUInt32(14, 511_043_495); writer.WriteUInt32(18, 1); writer.WriteUInt32(MsgItemUsagePacket.PayloadLength, uid);
        return writer.ToArray();
    }

    private static void BagAuthAssertAllocation(CreationFixture f, JsonNode operation, byte[] previous)
    {
        var allocation = operation["Allocation"]!; var request = operation["Request"]!;
        var state = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
        var reserved = Assert.Single(state["Operations"]!.AsArray())!;
        Assert.Equal(f.Data.ExpectedDomain, Guid.Parse(allocation["DomainId"]!.GetValue<string>()));
        Assert.Equal("rune-decompose-v1", request["Writer"]!.GetValue<string>());
        Assert.Equal(CreationFixture.RuneActor, request["AccountId"]!.GetValue<uint>());
        Assert.Equal(CreationFixture.RuneActor, request["CharacterUid"]!.GetValue<uint>());
        Assert.Equal(2, request["Count"]!.GetValue<int>());
        foreach (string key in new[] { "OperationId", "Writer", "AccountId", "CharacterUid", "PayloadSha256", "Count" })
            Assert.True(JsonNode.DeepEquals(request[key], allocation[key]));
        Assert.Equal(Guid.Parse(request["OperationId"]!.GetValue<string>()).ToString("N"), reserved["OperationId"]!.GetValue<string>());
        foreach (string key in new[] { "Writer", "AccountId", "CharacterUid", "PayloadSha256", "Count", "First", "Generation" })
            Assert.True(JsonNode.DeepEquals(allocation[key], reserved[key]));
        Assert.Equal(JsonNode.Parse(previous)!["Generation"]!.GetValue<ulong>() + 1, allocation["Generation"]!.GetValue<ulong>());
    }

    private static void BagAuthAssertCapturedDecompose(CreationFixture f, JsonNode record,
        byte[] character, byte[] items, byte[] spells, byte[] afterItems)
    {
        var expectedBefore = new[] { character, items, spells }; var expectedAfter = new[] { character, afterItems, spells };
        var kinds = new[] { LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items, LegacyTradeJournalFileKind.Spells };
        var changes = record["Changes"]!.AsArray(); Assert.Equal(3, changes.Count);
        for (int index = 0; index < 3; index++)
        {
            var row = changes[index]!; Assert.Equal((int)kinds[index], row["Kind"]!.GetValue<int>());
            Assert.Equal((long)CreationFixture.RuneActor, row["FileId"]!.GetValue<long>());
            Assert.Equal(expectedBefore[index], Convert.FromBase64String(row["Before"]!.GetValue<string>()));
            Assert.Equal(expectedAfter[index], Convert.FromBase64String(row["After"]!.GetValue<string>()));
            string path = BagAuthActorPath(f, kinds[index]);
            Assert.Equal(expectedBefore[index], File.ReadAllBytes(path));
            Assert.Equal(expectedAfter[index], File.ReadAllBytes(path + ".managed-trade-" + record["TransactionId"]!.GetValue<string>() + ".tmp"));
        }
    }
}
