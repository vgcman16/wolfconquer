using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;
using Item = WolfConquer.Core.Data.PlayerItemSnapshotItem;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedItemRepairTests
{
    private const uint TargetId = 410009, MeteorId = 1088001, TearId = 1088002;
    private static Item Target(bool meteor, bool equipped = false) => new()
    {
        Uid = Fixture.ItemUid, ItemId = TargetId, Durability = meteor ? (ushort)0 : (ushort)5,
        MaximumDurability = 10, Position = equipped ? (ushort)4 : (ushort)0, StackSize = 1,
    };
    private static Item Material(int index, ushort count = 10, Item[]? children = null) => new()
    {
        Uid = checked(Fixture.ItemUid + (uint)index + 1), ItemId = index % 2 == 0 ? MeteorId : TearId,
        Durability = 1, MaximumDurability = 1, StackSize = count,
        DepositedItems = children ?? Array.Empty<Item>(),
    };
    private static PlayerItemSnapshot Saved(bool meteor, bool equipped = false, int materials = 5)
    {
        var target = Target(meteor, equipped);
        var cost = meteor ? Enumerable.Range(0, materials).Select(index => Material(index)).ToArray() : Array.Empty<Item>();
        return PlayerItemSnapshot.Empty with
        {
            InventoryItems = equipped ? cost : new[] { target }.Concat(cost).ToArray(),
            EquipmentItems = equipped ? new[] { target } : Array.Empty<Item>(),
        };
    }
    private static ItemType[] Catalog(uint goldWorth = 100) => new[]
    {
        Metadata(TargetId, goldWorth), Metadata(MeteorId, 0), Metadata(TearId, 0),
    };
    private static ItemType Metadata(uint id, uint goldWorth)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "OwnedRepair" + id;
        fields[ItemType.GoldWorthFieldIndex] = goldWorth.ToString(CultureInfo.InvariantCulture);
        fields[ItemType.DurabilityFieldIndex] = "10"; fields[ItemType.StackSizeFieldIndex] = "99";
        return new(checked((int)id), fields[1], fields);
    }
    private static Task WithRepair(bool meteor, bool equipped, Func<Fixture, Task> body,
        PlayerItemSnapshot? saved = null, uint goldWorth = 100) =>
        WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, body,
            playerItems: saved ?? Saved(meteor, equipped), additionalItems: Catalog(goldWorth));
    private static byte[] Request() => new MsgItemUsagePacket(Fixture.ItemUid, 0,
        MsgItemUsageAction.RepairItem, 1234, 0, 0, 0).ToFrameBytes();
    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
    private static Item RepairedTarget(WorldSession session) => Assert.Single(
        session.PlayerItems.InventoryItems.Concat(session.PlayerItems.EquipmentItems), row => row.Uid == Fixture.ItemUid);
    private static PlayerItemSnapshot ExpectedItems(WorldSession before, bool meteor)
    {
        Item Convert(Item row) => row.Uid == Fixture.ItemUid ? row.WithDurability(10) :
            meteor && (row.ItemId is MeteorId or TearId) ? row.WithStackSize(checked((ushort)(row.StackSize - 1))) : row;
        return before.PlayerItems with
        {
            InventoryItems = before.PlayerItems.InventoryItems.Select(Convert).ToArray(),
            EquipmentItems = before.PlayerItems.EquipmentItems.Select(Convert).ToArray(),
        };
    }
    private static WorldSession PaidProposal(WorldSession before)
    {
        var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["Money"] = "943" };
        return before with { Character = before.Character with { Money = 943, Attributes = attributes },
            PlayerItems = ExpectedItems(before, meteor: false) };
    }
    private static byte[] ExpectedCharacter(byte[] before, bool meteor)
    {
        if (meteor) return before;
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        Assert.Contains("Money=1000", text, StringComparison.Ordinal);
        return encoding.GetPreamble().Concat(encoding.GetBytes(
            text.Replace("Money=1000", "Money=943", StringComparison.Ordinal))).ToArray();
    }
    private static LegacyOrdinarySpellBinding AssertGraph(Fixture f)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        Assert.Same(f.Current.PlayerItems, binding.ItemSnapshot);
        Assert.Same(f.Current.PlayerItems, binding.Items.Image.FinalSnapshot);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.True(binding.Items.PhysicalOwnerIsCurrentPlain());
        Assert.Equal(f.Current.PlayerItems.InventoryItems.Select(row => row.Uid).Order(), f.Native.Inventory!.ClientItems.Keys.Order());
        Assert.Equal(f.Current.PlayerItems.EquipmentItems.Select(row => row.Uid).Order(), f.Native.Equipment!.ClientItems.Keys.Order());
        return binding;
    }
    private static void AssertPaidFrames(IReadOnlyList<byte[]> frames, uint uid = Fixture.ItemUid)
    {
        Assert.Equal(3, frames.Count);
        foreach (var bytes in frames.Take(2))
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(bytes), out var packet));
            Assert.Equal(Fixture.Uid, packet.Uid); var update = Assert.Single(packet.Updates);
            Assert.Equal(LegacyMsgUpdateDataType.Money, update.Type); Assert.Equal(943UL, update.Value);
        }
        Assert.True(LegacyGameItemPacket.TryParse(Frame(frames[2]), out var repaired));
        Assert.Equal(uid, repaired.Uid); Assert.Equal(TargetId, repaired.ItemId);
        Assert.Equal((ushort)10, repaired.Durability); Assert.Equal(LegacyGameItemPacket.UpdateItemMode, repaired.Mode);
    }
}
