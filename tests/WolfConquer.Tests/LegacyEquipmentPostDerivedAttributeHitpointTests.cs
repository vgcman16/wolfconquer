using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task DetachedEquipmentQueryDoesNotGrantVenomOrChangeSpiritWithoutAnAdmittedOwner()
    {
        using var fixture = new NativeEquipmentCacheFixture(venom: true);
        var before = fixture.Owner.Character;
        await fixture.QueryAsync();
        Assert.Equal(before.Spirit, fixture.Owner.Character.Spirit);
        Assert.False(fixture.Owner.PlayerSpells.ContainsSpell(20000));
        Assert.DoesNotContain(fixture.Frames, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var row) && row.Id == 20000);
    }

    [Fact]
    public Task OwnedAttributeAllocationThenEquipmentQueryUsesCurrentSpiritForHitpointsAndMana() =>
        OwnedSequentialAttributeEquipmentAsync();
}
