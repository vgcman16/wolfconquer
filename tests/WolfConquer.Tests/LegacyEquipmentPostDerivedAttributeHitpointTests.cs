using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerEquipmentQueryAddsCurrentAttributeHitpointsAfterVenomPublicationBeforeMana()
    {
        var entered = 0;
        using var fixture = new NativeEquipmentCacheFixture(venom: true, onSend: (current, bytes) =>
        {
            var frame = ReadFrame(bytes);
            if (LegacySpellPacket.TryParse(frame, out var spell) && spell.Id == 20_000)
            {
                Assert.Equal(0, entered++);
                Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(current.Owner, out var maximum));
                Assert.Equal(5_000U, maximum); // Item HP only: native CalculateHitPoint1554 has not entered.
                Assert.Equal(150U, LegacyPhysicalStatusOverlay.ForOwner(current.Owner).MaxMana);
                Assert.Equal(5_000L, current.Owner.Character.MinHitPoints);
                current.ChangeSpiritDuringPublication(20); // Declared same-UID callback changes only raw Spirit.
            }
            return ValueTask.CompletedTask;
        });
        await fixture.QueryAsync();
        Assert.Equal(1, entered);
        Assert.Equal(20L, fixture.Owner.Character.Spirit);
        Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(fixture.Owner, out var finalMaximum));
        Assert.Equal(5_060U, finalMaximum); // 5000 item + current Spirit20 * 3; NOT old Spirit10 * 3.
        Assert.Equal(5_000L, fixture.Owner.Character.MinHitPoints);
        var published = Assert.Single(fixture.Frames.Select(ReadFrame)
            .Where(frame => frame.Header.Opcode == LegacyPlayerStatusPacket.Opcode)
            .Select(frame =>
            {
                Assert.True(LegacyPlayerStatusPacket.TryParse(frame, out var packet));
                return packet;
            }));
        Assert.Equal(5_060U, published.MaxHP);
        Assert.Equal(750U, published.MaxMana); // 150 item/purification + current Spirit20 * 30.
        var queried = await fixture.ReadStatusAsync();
        Assert.Equal(5_060U, queried.MaxHP);
        Assert.Equal(750U, queried.MaxMana);
    }
}
