using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Pure decoder/projector contract checks, never evidence of an issued WAL or a
// synthetic permission owner. Actual producers are exercised by the Frame tests.
public sealed class LegacyRuneMappingSemanticsTests
{
    [Theory]
    [InlineData(4_011_501U, 9000U, true)]
    [InlineData(4_020_201U, 2000U, true)]
    [InlineData(4_020_227U, 2000U, false)]
    [InlineData(4_030_102U, 0U, false)]
    [InlineData(4_036_409U, 0U, false)]
    public void RawSelfIsNoGrantWhileHistoricalDisplaySemanticsRemainExplicit(uint itemId, uint requiredClass, bool oldNoGrant)
    {
        var current = new LegacyRuneMetadata(itemId, itemId, requiredClass, 0, null);
        LegacyRuneOperation.ValidateMetadata([current]);
        Assert.Null(WorldPacketHandlers.ResolveRecordedRuneSpell(current));
        var display = Assert.IsType<LegacyGameItemPacket.RuneSpellOverride>(LegacyGameItemPacket.CreateRuneSpellOverride(itemId, itemId));
        Assert.Equal((byte)1, display.Effect);
        Assert.Equal(itemId - itemId % 100 + 1, display.SpellId);
        if (oldNoGrant)
        {
            LegacyRuneOperation.ValidateMetadata([current], LegacyRuneMappingSemantics.LegacyDisplay);
            Assert.Null(WorldPacketHandlers.ResolveRecordedRuneSpell(current, LegacyRuneMappingSemantics.LegacyDisplay));
        }
        else
        {
            var historical = current with { IsRuneSkill = true };
            LegacyRuneOperation.ValidateMetadata([historical], LegacyRuneMappingSemantics.LegacyDisplay);
            var row = Assert.IsType<WolfConquer.Core.Data.PlayerSpellSnapshotEntry>(
                WorldPacketHandlers.ResolveRecordedRuneSpell(historical, LegacyRuneMappingSemantics.LegacyDisplay));
            Assert.Equal(itemId / 100, (uint)row.Id); Assert.Equal(itemId % 100, (uint)row.Level);
            Assert.Throws<InvalidDataException>(() => LegacyRuneOperation.ValidateMetadata([historical]));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CurrentIntentRequiresExplicitTransformPresenceAndLegacyIntentForbidsIt(bool container)
    {
        LegacyRuneMetadata[] rows = [new(4_020_517, 1_416_016, 6000, 0, true)];
        var captured = new LegacyRuneTransformDependency([]);
        if (container)
        {
            var old = new LegacyRuneContainerOperation(LegacyRuneContainerKind.Drop, 123, rows, false);
            old.Validate(LegacyRuneMappingSemantics.LegacyDisplay);
            Assert.Throws<InvalidDataException>(() => old.Validate());
            var current = old with { Transform = captured }; current.Validate();
            Assert.Throws<InvalidDataException>(() => current.Validate(LegacyRuneMappingSemantics.LegacyDisplay));
        }
        else
        {
            var old = new LegacyRuneOperation(false, 123, 212, rows, false, 0, 0, null);
            old.Validate(LegacyRuneMappingSemantics.LegacyDisplay);
            Assert.Throws<InvalidDataException>(() => old.Validate());
            var current = old with { Transform = captured }; current.Validate();
            Assert.Throws<InvalidDataException>(() => current.Validate(LegacyRuneMappingSemantics.LegacyDisplay));
        }
    }

    [Fact]
    public void NonselfAliasIsNotGeneralizedIntoTheProvedPassiveBranch()
    {
        const uint outlier = 4_035_009, encoded = 4_011_301;
        Assert.False(LegacyRuneSpellIdentity.IsRawSelf(outlier, encoded));
        Assert.False(LegacyRuneSpellIdentity.IsNoSkill(outlier, encoded, LegacyRuneMappingSemantics.RawSelf));
        Assert.Throws<InvalidDataException>(() => LegacyRuneOperation.ValidateMetadata([new(outlier, encoded, 0, 0, null)]));
    }
}
