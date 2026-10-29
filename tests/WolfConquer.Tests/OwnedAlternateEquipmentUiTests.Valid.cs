using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedAlternateEquipmentUiTests
{
    private const uint PlayerUid = LegacyOwnedAdmissionRuntimeFixture.Uid;
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectEquipmentAlternateUiPublishesSource5Then44OrSelectedQuery(bool active)
    {
        var fixture = await SourceAlternateUiFixture.CreateAsync(active);
        await WithFixtureAsync(fixture, async () =>
        {
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
        });
    }

}
