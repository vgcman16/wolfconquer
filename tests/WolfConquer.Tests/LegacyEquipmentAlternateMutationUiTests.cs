using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectEquipmentAlternateUiPublishesSource5Then44OrSelectedQuery(bool active)
    {
        using var fixture = new SourceAlternateUiFixture(active);
        await fixture.EquipAsync();
        var owner = fixture.OwnerFrames.ToArray();
        Assert.Equal(active ? 15 : 6, owner.Length);
        SourceAlternateUiAssertItemPrefix(owner);
        LegacySelectedEquipmentQueryAssertions.AlternateNotification(owner[4], 5, 92_602, 51);
        if (active)
        {
            var query = LegacySelectedEquipmentQueryAssertions.NeutralQuery(owner, 5, PlayerUid, alternate: true);
            Assert.Equal(92_602U, query.Equipment.EquipmentSlots.Head);
            Assert.Equal(700U, query.Status.MaxHP);
            var hp = ParseMsgUpdates(owner).Where(packet => packet.Uid == PlayerUid &&
                packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints)).ToArray();
            Assert.Equal(3, hp.Length);
            Assert.All(hp, packet => Assert.Collection(packet.Updates,
                max => Assert.Equal(700UL, max.Value),
                current => Assert.Equal(300UL, current.Value)));
            var peer = Assert.Single(fixture.PeerFrames);
            Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(peer), out var spawn));
            Assert.Equal((ulong)PlayerUid, spawn.GetNumericField(2));
            Assert.Equal(111_097UL, spawn.GetNumericField(8));
        }
        else
        {
            LegacySelectedEquipmentQueryAssertions.InactiveAlternate(owner, 4, 92_602, 51);
            Assert.Empty(fixture.PeerFrames);
            Assert.Empty(ParseMsgUpdates(owner));
        }

        Assert.Equal(300, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(active ? 700U : 500U, fixture.OwnedMaximum);
        Assert.Equal(0, fixture.Clock.Reads);
        Assert.Equal(0, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Equal(92_601U, Assert.Single(fixture.Owner.PlayerItems.EquipmentItems,
            item => item.Position == 1).Uid);
        Assert.Equal(92_602U, Assert.Single(fixture.Owner.PlayerItems.EquipmentItems,
            item => item.Position == 51).Uid);
        Assert.Empty(fixture.Owner.PlayerItems.InventoryItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectEquipmentAlternateUiFineRainInactiveSkipsAdmissionButSelectedRejectsAfter5(bool active)
    {
        using var fixture = new SourceAlternateUiFixture(active, guarded: true);
        await fixture.EquipAsync();
        var frames = fixture.OwnerFrames.ToArray();
        Assert.Equal(6, frames.Length);
        SourceAlternateUiAssertItemPrefix(frames);
        LegacySelectedEquipmentQueryAssertions.AlternateNotification(frames[4], 5, 92_602, 51);
        if (active)
        {
            Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(frames[5]), out var wait));
            Assert.Contains(wait.Content, text => text.Contains("wait 6 seconds", StringComparison.Ordinal));
        }
        else
        {
            LegacySelectedEquipmentQueryAssertions.AlternateNotification(frames[5],
                LegacyShowEquipmentPacket.AllowAlternanteAction, 0, 0);
        }

        Assert.Empty(ParseMsgUpdates(frames));
        Assert.Empty(fixture.PeerFrames);
        Assert.Equal(active ? 1 : 0, fixture.Clock.Reads);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Equal(600U, fixture.OwnedMaximum);
        Assert.Equal(300, fixture.Owner.Character.MinHitPoints);
        Assert.Equal((uint?)60, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).Breakthrough);
        Assert.Equal((uint?)75, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).Counteraction);
        Assert.Equal(92_602U, Assert.Single(fixture.Owner.PlayerItems.EquipmentItems,
            item => item.Position == 51).Uid);
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("rebind")]
    [InlineData("items")]
    public async Task DirectEquipmentAlternateUiStopsAfter5OnOriginalOwnerOrAcceptedItemsLoss(string loss)
    {
        WorldSession? replacement = null;
        var callbacks = 0;
        using var fixture = new SourceAlternateUiFixture(active: false, onOwnerSend: (current, bytes) =>
        {
            if (!SourceAlternateUiIs5(bytes)) return ValueTask.CompletedTask;
            callbacks++;
            if (loss == "remove")
            {
                Assert.True(current.Sessions.TryRemove(current.Connection, out _));
            }
            else if (loss == "rebind")
            {
                replacement = CreateSession(uid: PlayerUid + 77, name: "AlternateReplacement",
                    spirit: 0, minHitPoints: 111, attributes:
                    new Dictionary<string, string> { ["ReplacementMarker"] = "preserve" });
                current.Sessions.Set(current.Connection, replacement);
            }
            else
            {
                replacement = current.Owner with { PlayerItems = current.Owner.PlayerItems with { } };
                current.Sessions.Set(current.Connection, replacement);
            }

            return ValueTask.CompletedTask;
        });
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
        else
            Assert.Same(replacement, fixture.Owner);
    }

    [Fact]
    public async Task DirectEquipmentAlternateUiKeepsFreshSameUidHpAndFieldsAcross5And44()
    {
        var seen = new ConcurrentQueue<ushort>();
        using var fixture = new SourceAlternateUiFixture(active: false, onOwnerSend: (current, bytes) =>
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
    }

    private static void SourceAlternateUiAssertItemPrefix(IReadOnlyList<byte[]> frames)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(frames[0]), out var remove));
        Assert.Equal(92_602U, remove.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, remove.Action);
        Assert.Equal(0UL, remove.DwParam);
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(frames[1]), out var slot));
        Assert.Equal(92_602U, slot.Id);
        Assert.Equal(MsgItemUsageAction.Unequip, slot.Action);
        Assert.Equal(51UL, slot.DwParam);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frames[2]), out var added));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frames[3]), out var updated));
        Assert.Equal(92_602U, added.Uid);
        Assert.Equal(92_602U, updated.Uid);
        Assert.Equal(111_097U, added.ItemId);
        Assert.Equal(111_097U, updated.ItemId);
        Assert.Equal((byte)51, added.Position);
        Assert.Equal((byte)51, updated.Position);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, added.Mode);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updated.Mode);
    }

    private static bool SourceAlternateUiIs5(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        return frame.Header.Opcode == WorldPacketOpcodes.MsgItemUsage &&
            frame.Payload.Length == LegacyShowEquipmentPacket.PayloadLength &&
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.ToArray().AsSpan(12)) == 5;
    }

    private sealed class SourceAlternateUiClock : ILegacySignedMillisecondClock
    {
        public int Reads { get; private set; }
        public int GetMilliseconds() { Reads++; return 10_001; }
    }

    private sealed class SourceAlternateUiFixture : IDisposable
    {
        private readonly StringWriter _log = new();
        private readonly WorldPacketHandlerContext _context;
        public SourceAlternateUiFixture(bool active, bool guarded = false,
            Func<SourceAlternateUiFixture, byte[], ValueTask>? onOwnerSend = null)
        {
            ItemType Helmet(uint id, uint maximum)
            {
                var item = DirectOverlayItem(id, "SourceAlternateUiHelmet" + maximum);
                var fields = item.Fields.ToArray();
                fields[18] = maximum.ToString(CultureInfo.InvariantCulture);
                return item with { Fields = fields };
            }
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], [],
                items: [Helmet(111_096, 500), Helmet(111_097, 700)]);
            PlayerItemSnapshotItem Item(uint uid, uint id, ushort position) => new()
            {
                Uid = uid, ItemId = id, Position = position, Durability = 100, MaximumDurability = 100,
            };
            var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2,
                strength: 0, spirit: 0, minHitPoints: 300, spellIds: [],
                playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems = [Item(92_601, 111_096, 1)],
                    InventoryItems = [Item(92_602, 111_097, 0)],
                }) with { AlternateEquipmentActive = active };
            Assert.Equal("500", world.ItemsById[111_096][0].Fields[18]);
            Assert.Equal("700", world.ItemsById[111_097][0].Fields[18]);
            Assert.False(owner.Character.Attributes.ContainsKey("MaxHitpoints"));
            if (guarded)
            {
                owner = owner with
                {
                    Character = owner.Character with
                    {
                        Attributes = new Dictionary<string, string>
                        {
                            ["StatusFlags"] = BuildStatusFlags(FineRain1StatusFlag),
                            ["FineRainPower"] = "100",
                        },
                    },
                    PhysicalStatusOverlay = new LegacyPhysicalStatusOverlay(PlayerUid, 60, 75),
                    EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(PlayerUid, 600),
                    FineRainEquipmentAdmission = new LegacyFineRainEquipmentAdmission(PlayerUid, 10_000),
                };
            }
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: async (memory, _) =>
                {
                    var bytes = memory.ToArray();
                    OwnerFrames.Enqueue(bytes);
                    if (onOwnerSend is not null) await onOwnerSend(this, bytes);
                });
            var peerConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9959),
                sendAsync: (memory, _) =>
                {
                    PeerFrames.Enqueue(memory.ToArray());
                    return ValueTask.CompletedTask;
                });
            Sessions.Set(Connection, owner);
            Sessions.Set(peerConnection, CreateSession(uid: TargetPlayerUid, name: "AlternateUiPeer",
                mapId: 1_000, x: 832, spirit: 0, minHitPoints: 500, spellIds: []));
            _context = new WorldPacketHandlerContext(Connection, TextWriter.Synchronized(_log), owner,
                WorldData: world, Sessions: Sessions, LegacyClock: Clock,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public WorldSessionStore Sessions { get; } = new();
        public ConquerTcpConnection Connection { get; }
        public ConcurrentQueue<byte[]> OwnerFrames { get; } = new();
        public ConcurrentQueue<byte[]> PeerFrames { get; } = new();
        public SourceAlternateUiClock Clock { get; } = new();
        public WorldSession Owner
        {
            get
            {
                Assert.True(Sessions.TryGet(Connection, out var session));
                return Assert.IsType<WorldSession>(session);
            }
        }
        public uint OwnedMaximum
        {
            get
            {
                Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(Owner, out var maximum));
                return maximum;
            }
        }
        public async Task EquipAsync() =>
            await PacketHandlers.HandleAsync(_context with { Session = Owner },
                ReadFrame(new MsgItemUsagePacket(92_602, 51, MsgItemUsageAction.Equip,
                    Timestamp: 0, DwParam2: 0, DwParam3: 0, DwParam4: 0).ToFrameBytes()));
        public async Task QueryStatusAsync() =>
            await PacketHandlers.HandleAsync(_context with { Session = Owner },
                ReadFrame(new LegacyPlayerStatusQueryPacket(PlayerUid).ToFrameBytes()));
        public void Dispose() => _log.Dispose();
    }
}
