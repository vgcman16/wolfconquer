using System.Buffers.Binary;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDragonSkinTransactionTests
{
    private sealed record ModelPreimage(WorldSession Session, byte[] ItemsJson,
        KeyValuePair<string, string>[] Fields, IReadOnlyList<PlayerItemSnapshotItem>[] Stores,
        PlayerItemSnapshotItem[][] Rows);

    private static IReadOnlyList<PlayerItemSnapshotItem>[] Stores(PlayerItemSnapshot items) =>
    [items.InventoryItems, items.RelicFuseItems, items.MythsoulBagItems, items.EquipmentItems,
        items.RuneItems, items.WardrobeItems, items.WarehouseItems];

    private static ModelPreimage Capture(WorldSession session)
    {
        var stores = Stores(session.PlayerItems);
        return new(session, JsonSerializer.SerializeToUtf8Bytes(session.PlayerItems), session.Character.Attributes.ToArray(),
            stores, stores.Select(rows => rows.ToArray()).ToArray());
    }

    private static void AssertModelUnchanged(ModelPreimage before)
    {
        Assert.Equal(before.ItemsJson, JsonSerializer.SerializeToUtf8Bytes(before.Session.PlayerItems));
        Assert.Equal(before.Fields, before.Session.Character.Attributes.ToArray());
        var stores = Stores(before.Session.PlayerItems);
        for (var group = 0; group < stores.Length; group++)
        {
            Assert.Same(before.Stores[group], stores[group]);
            Assert.Equal(before.Rows[group].Length, stores[group].Count);
            for (var index = 0; index < stores[group].Count; index++) Assert.Same(before.Rows[group][index], stores[group][index]);
        }
    }

    private static void AssertFilesUnchanged(Fixture fixture)
    {
        Assert.Equal(fixture.CharacterBefore, File.ReadAllBytes(fixture.CharacterPath));
        Assert.Equal(fixture.ItemsBefore, File.ReadAllBytes(fixture.ItemPath));
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static void AssertRejected(Fixture fixture, ModelPreimage before, string reason)
    {
        Assert.Same(before.Session, fixture.Current);
        AssertModelUnchanged(before);
        AssertFilesUnchanged(fixture);
        Assert.Empty(fixture.Sent);
        Assert.Contains(reason, fixture.Log.ToString());
        Assert.DoesNotContain("WorldServer persisted LegacyDragonSkin", fixture.Log.ToString());
        Assert.DoesNotContain("WorldServer unlocked LegacyDragonSkin", fixture.Log.ToString());
        Assert.DoesNotContain("WorldServer retrieved LegacyDragonSkin", fixture.Log.ToString());
    }

    private static void AssertCommitted(Fixture fixture, ModelPreimage before, string expectedState)
    {
        Assert.NotSame(before.Session, fixture.Current);
        Assert.Equal(expectedState, fixture.Current.Character.Attributes["AnimaSkin"]);
        Assert.Equal(expectedState, fixture.ReloadCharacter().Attributes["AnimaSkin"]);
        var expectedText = fixture.CharacterTextBefore.Replace("AnimaSkin=" + before.Session.Character.Attributes["AnimaSkin"],
            "AnimaSkin=" + expectedState, StringComparison.Ordinal);
        Assert.Equal(fixture.CharacterEncoding.GetPreamble().Concat(fixture.CharacterEncoding.GetBytes(expectedText)).ToArray(),
            File.ReadAllBytes(fixture.CharacterPath));
        var physical = PlayerItemSnapshotLoader.ToLegacyBytes(fixture.Current.PlayerItems);
        Assert.Equal(physical, File.ReadAllBytes(fixture.ItemPath));
        Assert.Equal(physical, PlayerItemSnapshotLoader.ToLegacyBytes(fixture.ReloadItems()));
        Assert.Equal(before.Session.AccountId, fixture.Current.AccountId);
        Assert.Equal(before.Session.Character.Uid, fixture.Current.Character.Uid);
        Assert.Equal(before.Session.Character.Money, fixture.Current.Character.Money);
        Assert.Equal(before.Session.Character.ConquerPoints, fixture.Current.Character.ConquerPoints);
        Assert.Equal(before.Session.Character with { Attributes = fixture.Current.Character.Attributes }, fixture.Current.Character);
        AssertModelUnchanged(before);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        Assert.DoesNotContain("outcome=rejected", fixture.Log.ToString());
    }

    private static byte[] SkinGolden(LegacyDragonSkinType type, uint skin, bool ok)
    {
        Assert.InRange(skin, 0U, 127U);
        var payload = new List<byte>();
        if (type != LegacyDragonSkinType.MyShow) payload.AddRange([8, (byte)type]);
        payload.AddRange([16, 0xa0, 0xfa, 0x4e]); // Actual preserved UID1293600 varint.
        if (skin != 0) payload.AddRange([24, (byte)skin]);
        if (ok) payload.AddRange([32, 1]);
        var result = new byte[4 + payload.Count];
        BinaryPrimitives.WriteUInt16LittleEndian(result, checked((ushort)result.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), 2415);
        payload.CopyTo(result, 4);
        return result;
    }

    private static byte[] ItemGolden(uint uid, uint itemId, ushort mode, ushort stack, ushort durability = 0, byte bound = 0)
    {
        // Independent full2116 frame offsets, not the production projection.
        var bytes = new byte[206];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 206);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2116);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), itemId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), durability);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), durability);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), mode);
        bytes[32] = bound;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), stack);
        return bytes;
    }

    private static void AssertWireAndLogOrder(Fixture fixture, LegacyDragonSkinType type, uint skin, byte[] inventory)
    {
        Assert.Equal(2, fixture.Sent.Count);
        Assert.Equal(inventory, fixture.Sent[0]);
        Assert.Equal(SkinGolden(type, skin, true), fixture.Sent[1]);
        var first = fixture.Trace.FindIndex(value => value.StartsWith("frame:", StringComparison.Ordinal));
        var persisted = fixture.Trace.FindIndex(value => value.Contains("WorldServer persisted LegacyDragonSkin", StringComparison.Ordinal));
        var action = fixture.Trace.FindIndex(value => value.Contains(type == LegacyDragonSkinType.Unlock
            ? "WorldServer unlocked LegacyDragonSkin" : "WorldServer retrieved LegacyDragonSkin", StringComparison.Ordinal));
        var acknowledgement = fixture.Trace.FindLastIndex(value => value == "frame:2415");
        Assert.True(first >= 0 && first < persisted && persisted < action && action < acknowledgement,
            string.Join(Environment.NewLine, fixture.Trace));
    }
}
