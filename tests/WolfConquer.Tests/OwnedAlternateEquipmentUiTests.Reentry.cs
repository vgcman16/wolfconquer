using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedAlternateEquipmentUiTests
{
    [Theory]
    [InlineData("remove")]
    [InlineData("rebind")]
    [InlineData("items")]
    public async Task DirectEquipmentAlternateUiStopsAfter5OnOriginalOwnerOrAcceptedItemsLoss(string loss)
    {
        WorldSession? replacement = null;
        LegacyOrdinarySpellBinding? itemBinding = null;
        LegacyOwnedNativeItemImage? priorImage = null;
        LegacyNativeGameItemRuntime? priorNativeItem = null;
        PlayerItemSnapshot? priorItems = null;
        var callbacks = 0;
        var fixture = await SourceAlternateUiFixture.CreateAsync(active: false, onOwnerSend: (current, bytes) =>
        {
            if (!SourceAlternateUiIs5(bytes)) return ValueTask.CompletedTask;
            callbacks++;
            if (loss == "remove")
            {
                Assert.True(current.Sessions.TryRemove(current.Connection, out _));
            }
            else if (loss == "rebind")
            {
                Assert.True(current.Sessions.TryRemove(current.Connection, out _));
            }
            else
            {
                itemBinding = Assert.IsType<LegacyOrdinarySpellBinding>(
                    current.Connection.NativeGameClient!.OrdinarySpellBinding);
                priorImage = itemBinding.Items.Image;
                priorItems = current.Owner.PlayerItems;
                priorNativeItem = itemBinding.Client.Equipment!.ClientItems[92_602];
                Assert.Equal((ushort)100, priorNativeItem.Durability);
                replacement = current.SetAcceptedEquipmentDurability(99);
                Assert.NotSame(priorItems, replacement.PlayerItems);
                Assert.NotSame(priorImage, itemBinding.Items.Image);
            }

            return ValueTask.CompletedTask;
        });
        await WithFixtureAsync(fixture, async () =>
        {
            await fixture.EquipAsync();
            var frames = fixture.OwnerFrames.ToArray();
            Assert.Equal(1, callbacks);
            Assert.Equal(5, frames.Length);
            SourceAlternateUiAssertItemPrefix(frames);
            LegacySelectedEquipmentQueryAssertions.AlternateNotification(frames[4], 5, 92_602, 51);
            Assert.Empty(ParseMsgUpdates(frames));
            Assert.Empty(fixture.PeerFrames);
            Assert.Equal(0, fixture.Clock.Reads);
            if (loss == "remove")
                Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
            else if (loss == "rebind")
            {
                replacement = await fixture.ReissueAsync();
                Assert.Same(replacement, fixture.Owner);
            }
            else
            {
                Assert.Same(replacement, fixture.Owner);
                Assert.NotNull(itemBinding);
                var native = fixture.Connection.NativeGameClient!;
                Assert.Same(itemBinding, native.OrdinarySpellBinding);
                Assert.Same(itemBinding.Client, native);
                Assert.Same(itemBinding.Player, native.Player);
                Assert.Same(itemBinding.Items.Inventory, native.Inventory);
                Assert.Same(itemBinding.Items.Equipment, native.Equipment);
                Assert.Same(itemBinding.Items.Warehouse, native.Warehouse);
                Assert.Same(itemBinding.Items.Wardrobe, native.MyWardrobe);
                Assert.Same(itemBinding.Items.Rune, native.Rune);
                Assert.Same(itemBinding.Items.Relic, native.RelicFuse);
                Assert.Same(itemBinding.Items.Gear, native.GearMythsoulBag);
                Assert.Same(fixture.Owner.PlayerItems, itemBinding.ItemSnapshot);
                Assert.Same(fixture.Owner.PlayerItems, itemBinding.Items.Image.FinalSnapshot);
                Assert.True(itemBinding.Items.PhysicalOwnerIsCurrentPlain());
                var latest = native.Equipment!.ClientItems[92_602];
                Assert.NotSame(priorNativeItem, latest);
                Assert.Equal((ushort)99, latest.Durability);
                Assert.Equal((ushort)99, Assert.Single(fixture.Owner.PlayerItems.EquipmentItems,
                    item => item.Uid == 92_602).Durability);
                Assert.Equal((ushort)100, priorNativeItem!.Durability);
                Assert.Equal((ushort)100, Assert.Single(priorItems!.EquipmentItems,
                    item => item.Uid == 92_602).Durability);
                Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(fixture.Connection, fixture.Owner));
            }
        });
    }

    [Fact]
    public async Task DirectEquipmentAlternateUiKeeps5Then44ForCanonicalNoOpItemClone()
    {
        WorldSession? canonical = null;
        LegacyOrdinarySpellBinding? binding = null;
        LegacyOwnedNativeItemImage? image = null;
        LegacyNativeEquipItemStorage? equipment = null;
        LegacyNativeGameItemRuntime? mainItem = null;
        LegacyNativeGameItemRuntime? alternateItem = null;
        var callbacks = 0;
        var fixture = await SourceAlternateUiFixture.CreateAsync(active: false, onOwnerSend: (current, bytes) =>
        {
            if (!SourceAlternateUiIs5(bytes)) return ValueTask.CompletedTask;
            callbacks++;
            var before = current.Owner;
            binding = Assert.IsType<LegacyOrdinarySpellBinding>(
                current.Connection.NativeGameClient!.OrdinarySpellBinding);
            image = binding.Items.Image;
            equipment = binding.Client.Equipment!;
            mainItem = equipment.ClientItems[92_601];
            alternateItem = equipment.ClientItems[92_602];
            var proposal = before with { PlayerItems = before.PlayerItems with { } };
            Assert.NotSame(before.PlayerItems, proposal.PlayerItems);
            current.Sessions.Set(current.Connection, proposal, out var final);
            canonical = final;
            Assert.Same(final, current.Owner);
            Assert.Same(before.PlayerItems, final.PlayerItems);
            Assert.Same(image, binding.Items.Image);
            Assert.Same(equipment, binding.Client.Equipment);
            Assert.Same(mainItem, equipment.ClientItems[92_601]);
            Assert.Same(alternateItem, equipment.ClientItems[92_602]);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems),
                PlayerItemSnapshotLoader.ToLegacyBytes(final.PlayerItems));
            return ValueTask.CompletedTask;
        });
        await WithFixtureAsync(fixture, async () =>
        {
            await fixture.EquipAsync();
            var frames = fixture.OwnerFrames.ToArray();
            Assert.Equal(1, callbacks);
            Assert.Equal(6, frames.Length);
            SourceAlternateUiAssertItemPrefix(frames);
            LegacySelectedEquipmentQueryAssertions.InactiveAlternate(frames, 4, 92_602, 51);
            Assert.Empty(ParseMsgUpdates(frames));
            Assert.Empty(fixture.PeerFrames);
            Assert.Equal(0, fixture.Clock.Reads);
            Assert.Same(canonical, fixture.Owner);
            Assert.NotNull(binding);
            Assert.Same(binding, fixture.Connection.NativeGameClient!.OrdinarySpellBinding);
            Assert.Same(image, binding.Items.Image);
            Assert.Same(fixture.Owner.PlayerItems, binding.ItemSnapshot);
            Assert.Same(fixture.Owner.PlayerItems, image!.FinalSnapshot);
            Assert.Same(equipment, binding.Client.Equipment);
            Assert.Same(mainItem, equipment!.ClientItems[92_601]);
            Assert.Same(alternateItem, equipment.ClientItems[92_602]);
            Assert.True(binding.Items.PhysicalOwnerIsCurrentPlain());
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(fixture.Connection, fixture.Owner));
        });
    }

    [Fact]
    public async Task DirectEquipmentAlternateUiKeepsFreshSameUidHpAndFieldsAcross5And44()
    {
        var seen = new ConcurrentQueue<ushort>();
        var fixture = await SourceAlternateUiFixture.CreateAsync(active: false, onOwnerSend: (current, bytes) =>
        {
            var frame = ReadFrame(bytes);
            if (frame.Header.Opcode != WorldPacketOpcodes.MsgItemUsage ||
                frame.Payload.Length != LegacyShowEquipmentPacket.PayloadLength)
                return ValueTask.CompletedTask;
            var action = BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.ToArray().AsSpan(12));
            if (action is not (5 or LegacyShowEquipmentPacket.AllowAlternanteAction))
                return ValueTask.CompletedTask;
            seen.Enqueue(action);
            var before = current.Owner;
            current.Sessions.Set(current.Connection, before with
            {
                Character = before.Character with
                {
                    MinHitPoints = action == 5 ? 177 : 188,
                    Attributes = new Dictionary<string, string>(before.Character.Attributes)
                    {
                        ["AlternateUiCallback"] = action.ToString(CultureInfo.InvariantCulture),
                    },
                },
                PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(before) with
                {
                    Breakthrough = 99, Counteraction = 88,
                },
            });
            return ValueTask.CompletedTask;
        });
        await WithFixtureAsync(fixture, async () =>
        {
            await fixture.EquipAsync();
            var frames = fixture.OwnerFrames.ToArray();
            Assert.Equal(6, frames.Length);
            SourceAlternateUiAssertItemPrefix(frames);
            LegacySelectedEquipmentQueryAssertions.InactiveAlternate(frames, 4, 92_602, 51);
            Assert.Equal(new ushort[] { 5, 44 }, seen.ToArray());
            Assert.Equal(188, fixture.Owner.Character.MinHitPoints);
            Assert.Equal("44", fixture.Owner.Character.Attributes["AlternateUiCallback"]);
            Assert.Equal(500U, fixture.OwnedMaximum);
            Assert.Equal(0, fixture.Clock.Reads);
            Assert.Empty(fixture.PeerFrames);
            await fixture.QueryStatusAsync();
            var status = Assert.Single(fixture.OwnerFrames.ToArray().Skip(frames.Length));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(status), out var parsed));
            Assert.Equal(99U, parsed.Breakthrough);
            Assert.Equal(88U, parsed.Counteraction);
        });
    }

}
