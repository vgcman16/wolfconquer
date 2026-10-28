using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyFullAccountClassMutationTests
{
    [Fact]
    public async Task RetainedUnselectedSpellKeepsItsRawOpaqueByteAcrossARealJointClassReset()
    {
        await WithDurableClassAsync(async fixture =>
        {
            var image = fixture.SpellBytes;
            image[4 + 12 + 11] = 0xA5; // Existing9999 row is retained by the Warrior grant.
            using (var file = new FileStream(fixture.SpellPath, FileMode.Open, FileAccess.Write, FileShare.None))
            { file.Write(image); file.Flush(true); }
            fixture.ExpectImage(fixture.SpellPath, image);
            var before = new DurableClassImage(fixture.Original);

            Assert.True(await fixture.InvokeAsync(fixture.Original, optionId: 1));

            Assert.True(fixture.Store.TryGet(fixture.Connection, out var current));
            Assert.NotNull(current);
            Assert.NotSame(fixture.Original, current);
            Assert.Equal(2005, current.Character.Class);
            Assert.Equal(910, current.Character.AttributePoints);
            before.RequireUnchanged();
            before.RequireUnrelatedResources(current);
            var retained = Assert.Single(current.PlayerSpells.Spells, row => row.Id == 9999);
            Assert.Equal(Spell(9999, 5, 444, 2, 1, 1), retained);
            AssertCharacterCoreEqual(current.Character, fixture.ReadCharacter());
            var expected = Convert.FromHexString("09000000" +
                "FC0300000000000000000000" + "0F270500BC010000020101A5" +
                "470400000000000000000000" + "420400000000000000000000" +
                "010400000000000000000000" + "150400000000000000000000" +
                "160400000000000000000000" + "F70300000000000000000000" + "100400000000000000000000");
            Assert.Equal(image.AsSpan(16, 12).ToArray(), expected.AsSpan(16, 12).ToArray());
            fixture.ExpectImage(fixture.CharacterPath, fixture.CharacterBytes);
            fixture.ExpectImage(fixture.SpellPath, expected);
            fixture.ExpectNoPending();
            fixture.RequireFilesUnchanged();
            AssertScalarUpdate(fixture.Sent[5], LegacyMsgUpdateDataType.Class, 2005);
            Assert.Equal(15, fixture.Sent.Count); // Five initial stats, Class, nine source Add calls (including duplicate1020).
            Assert.True(fixture.Disconnected);
        });
    }
}
