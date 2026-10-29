using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
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
                replacement = current.Owner with { PlayerItems = current.Owner.PlayerItems with { } };
                current.Sessions.Set(current.Connection, replacement);
                replacement = current.Owner;
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
            else Assert.Same(replacement, fixture.Owner);
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
