using System.Globalization;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedRootMagicEquipmentTests
{
    private const uint Actor = 1_293_600, Peer = 1_293_601;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RootMagicUsesEarnedRidingCropCacheAcrossAdmittedOrRejectedUnequip(bool rejectQuery) =>
        F.RunOwnedRootEquipmentAsync("crop", async fixture =>
        {
            await fixture.Query();
            await fixture.Cast(7001);
            Assert.True(fixture.HasFlag(0x32));
            fixture.Advance(TimeSpan.FromMilliseconds(500));
            Assert.Equal(203_003U, fixture.Owner.PhysicalStatusOverlay!.RidingCropItemId);
            if (rejectQuery) await fixture.Cast(14500, Actor, 830, 584);
            fixture.Advance(TimeSpan.FromMilliseconds(500));
            await fixture.Unequip(18);
            Assert.DoesNotContain(fixture.Owner.PlayerItems.EquipmentItems, item => item.Position == 18);
            Assert.Contains(fixture.Owner.PlayerItems.InventoryItems, item => item.Uid == 99_180 && item.Position == 0);
            Assert.Equal(rejectQuery ? 203_003U : 0U, fixture.Owner.PhysicalStatusOverlay!.RidingCropItemId);
            var hadStamp = fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var beforeStamp);
            var start = fixture.Order.Count;
            await fixture.Cast(1000);
            Assert.Equal(rejectQuery, fixture.HasFlag(0x32));
            if (rejectQuery)
            {
                Assert.Equal(fixture.Now.ToString("O", CultureInfo.InvariantCulture), fixture.Owner.Character.Attributes["LegacyAttackStampUtc"]);
                Assert.Equal("90", fixture.Owner.Character.Attributes["Stamina"]);
                Assert.Equal(960L, fixture.Owner.Character.MinMana);
                Assert.Equal(new[] { (LegacyMsgUpdateDataType.Stamina, 90UL), (LegacyMsgUpdateDataType.Mana, 960UL) }, fixture.Costs(start));
                Assert.Single(fixture.Animations(Actor, start), packet => packet.SpellId == 1000);
            }
            else
            {
                Assert.Equal(hadStamp, fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var afterStamp));
                Assert.Equal(beforeStamp, afterStamp);
                Assert.Empty(fixture.Costs(start));
                Assert.DoesNotContain(fixture.Animations(Actor, start), packet => packet.SpellId == 1000);
                Assert.Equal(100_000L, fixture.Peer.Character.MinHitPoints);
            }
        });

    [Fact]
    public Task RootManualMpQueuedUnequipsKeepOuterEffectsThenFreshRingAndRetainedNeckControlTheLaterCast() =>
        F.RunOwnedRootEquipmentAsync("manual", async fixture =>
        {
            await fixture.Query();
            var queued = false;
            fixture.OnSend = (current, uid, bytes) =>
            {
                if (queued || uid != Actor || !Scalar(bytes, LegacyMsgUpdateDataType.Mana, 400)) return;
                queued = true;
                // Intent only. Dispatch/await is after the outer shared Frame.
                current.QueueUnequip(6); current.QueueUnequip(2);
                Assert.Equal(1U, LegacyPhysicalStatusOverlay.ForOwner(current.Owner).RingItemEffect);
                Assert.Equal(200U, LegacyPhysicalStatusOverlay.ForOwner(current.Owner).NecklaceItemEffect);
            };
            var first = fixture.Order.Count;
            await fixture.Cast(1000);
            fixture.OnSend = null;
            Assert.True(queued, fixture.Output);
            Assert.True(fixture.HasFlag(9)); Assert.True(fixture.HasFlag(8));
            Assert.Equal(360L, fixture.Owner.Character.MinMana);
            Assert.Equal(new[] { (LegacyMsgUpdateDataType.Mana, 400UL), (LegacyMsgUpdateDataType.Stamina, 90UL),
                (LegacyMsgUpdateDataType.Mana, 360UL) }, fixture.Costs(first));
            foreach (var uid in new[] { Actor, Peer })
            {
                Assert.Single(fixture.Animations(uid, first), packet => packet.SpellId == 1175);
                Assert.Single(fixture.Animations(uid, first), packet => packet.SpellId == 1095);
                Assert.Single(fixture.Animations(uid, first), packet => packet.SpellId == 1020);
            }
            await fixture.DrainQueuedGear();
            Assert.DoesNotContain(fixture.Owner.PlayerItems.EquipmentItems, row => row.Position is 6 or 2);
            Assert.Equal(0U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).RingItemEffect);
            Assert.Equal(200U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).NecklaceItemEffect);
            await fixture.AdvanceRuntime(TimeSpan.FromSeconds(20));
            Assert.False(fixture.HasFlag(9)); Assert.False(fixture.HasFlag(8));
            Assert.Equal(360L, fixture.Owner.Character.MinMana); // Player ticker has no automatic MP regeneration.
            var stamina = ulong.Parse(fixture.Owner.Character.Attributes["Stamina"], CultureInfo.InvariantCulture);
            var second = fixture.Order.Count;
            await fixture.Cast(1000);
            Assert.False(fixture.HasFlag(9)); Assert.True(fixture.HasFlag(8));
            Assert.Equal(620L, fixture.Owner.Character.MinMana); //360 + source min(300,1000-360) -40.
            Assert.Equal(new[] { (LegacyMsgUpdateDataType.Mana, 660UL), (LegacyMsgUpdateDataType.Stamina, stamina - 10),
                (LegacyMsgUpdateDataType.Mana, 620UL) }, fixture.Costs(second));
            foreach (var uid in new[] { Actor, Peer })
            {
                Assert.Single(fixture.Animations(uid, second), packet => packet.SpellId == 1175);
                Assert.DoesNotContain(fixture.Animations(uid, second), packet => packet.SpellId == 1095);
                Assert.Single(fixture.Animations(uid, second), packet => packet.SpellId == 1020);
            }
        });

    [Fact]
    public Task RootXpShieldQueuedGearPreservesOuterThirtyThenLaterEarnedFifteenSecondClock() =>
        F.RunOwnedRootEquipmentAsync("xp", async fixture =>
        {
            await fixture.Query();
            Assert.Equal(0U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).NecklaceItemEffect);
            Assert.Equal(200U, Assert.Single(fixture.Owner.PlayerItems.InventoryItems, row => row.Uid == 99_183).Effect);
            await fixture.EarnXp(); //100 actual player/global due ticks, never a seeded gauge/flag.
            Assert.True(fixture.HasFlag(4)); Assert.False(fixture.HasFlag(8));
            var queued = false;
            fixture.OnSend = (current, uid, bytes) =>
            {
                if (queued || uid != Actor || !Scalar(bytes, LegacyMsgUpdateDataType.XPList, 0)) return;
                queued = true;
                Assert.False(current.HasFlag(4)); Assert.False(current.HasFlag(8));
                current.QueueEquip(99_183, 2); // Typed real request; no dispatch inside this await.
            };
            var outer = fixture.Order.Count;
            await fixture.Cast(1020, Actor, 829, 583);
            fixture.OnSend = null;
            Assert.True(queued, fixture.Output);
            Assert.False(fixture.HasFlag(4)); Assert.True(fixture.HasFlag(8));
            Assert.Empty(fixture.Costs(outer));
            Assert.Equal(fixture.Now.AddSeconds(30), fixture.Deadline(8));
            foreach (var uid in new[] { Actor, Peer })
            {
                var animation = Assert.Single(fixture.Animations(uid, outer), packet => packet.SpellId == 1020 && packet.SpellLevel == 0);
                Assert.Equal((ushort)829, animation.X); Assert.Equal((ushort)583, animation.Y);
                Assert.Equal(30U, Assert.Single(animation.Targets).Damage);
            }
            var originalDeadline = fixture.Deadline(8);
            await fixture.DrainQueuedGear();
            Assert.Equal(99_183U, Assert.Single(fixture.Owner.PlayerItems.EquipmentItems, row => row.Position == 2).Uid);
            Assert.Equal(200U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).NecklaceItemEffect);
            Assert.Equal(originalDeadline, fixture.Deadline(8));
            await fixture.AdvanceRuntime(TimeSpan.FromSeconds(30));
            Assert.False(fixture.HasFlag(8));
            await fixture.MoveDiagonal();
            Assert.Equal((831L, 585L), (fixture.Owner.Character.X, fixture.Owner.Character.Y));
            Assert.False(fixture.HasFlag(0)); // This later1000 is the first hostile player cast.
            var later = fixture.Order.Count;
            await fixture.Cast(1000);
            Assert.True(fixture.HasFlag(8));
            Assert.Equal("90", fixture.Owner.Character.Attributes["Stamina"]);
            Assert.Equal(960L, fixture.Owner.Character.MinMana);
            Assert.Equal(new[] { (LegacyMsgUpdateDataType.Stamina, 90UL), (LegacyMsgUpdateDataType.Mana, 960UL) }, fixture.Costs(later));
            Assert.Equal(fixture.Now.AddSeconds(15), fixture.Deadline(8));
            Assert.Equal(fixture.Now.ToString("O", CultureInfo.InvariantCulture), fixture.Owner.Character.Attributes["LegacyAttackStampUtc"]);
            Assert.True(fixture.HasFlag(0));
            Assert.Contains("applied legacy player-target magic flashing-name", fixture.Output, StringComparison.Ordinal);
            foreach (var uid in new[] { Actor, Peer })
            {
                var frames = fixture.FramesSince(uid, later);
                var vectors = frames.Select((bytes, index) => (Frame: OwnedEquipmentBehaviorFixture.ReadFrame(bytes), Index: index))
                    .Where(row => LegacyMsgUpdatePacket.TryParse(row.Frame, out var packet) && packet.Uid == Actor &&
                        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag))
                    .Select(row =>
                    {
                        Assert.True(LegacyMsgUpdatePacket.TryParse(row.Frame, out var packet));
                        var values = Assert.Single(packet.Updates, entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag).Values;
                        Assert.NotNull(values); Assert.Equal(18, values.Count);
                        Assert.All(values.Skip(1), value => Assert.Equal(0UL, value));
                        return (Value: values[0], row.Index);
                    }).ToArray();
                // Source order: root necklace inserts shield8, then player-target
                // admission adds FlashingName0. The old &768 helper hid bit0,
                // making genuine full vectors256 and257 look like duplicates.
                Assert.Equal(new ulong[] { 256, 257 }, vectors.Select(row => row.Value));
                var shieldIndex = Array.FindIndex(frames, bytes =>
                    LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation) &&
                    animation.SpellId == 1020 && animation.SpellLevel == 4);
                Assert.True(vectors[0].Index < shieldIndex && shieldIndex < vectors[1].Index);
                Assert.Single(fixture.Animations(uid, later), packet => packet.SpellId == 1020 && packet.SpellLevel == 4);
            }
            await fixture.AdvanceRuntime(TimeSpan.FromMilliseconds(14_980));
            Assert.True(fixture.HasFlag(8));
            await fixture.AdvanceRuntime(TimeSpan.FromMilliseconds(20));
            Assert.False(fixture.HasFlag(8));
        });

    private static bool Scalar(byte[] bytes, LegacyMsgUpdateDataType type, ulong value) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.Uid == Actor &&
        packet.Updates.Any(row => row.Type == type && row.Value == value);
}
