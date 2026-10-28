using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyFullAccountClassMutationTests
{
    [Fact]
    public async Task FullyQualifiedLiteralRootsWithTrailingSeparatorsUseTheSameConfiguredRealFiles()
    {
        await WithDurableClassAsync(async fixture =>
        {
            var users = fixture.Users + Path.DirectorySeparatorChar;
            var spells = fixture.SpellsRoot + Path.DirectorySeparatorChar;
            Assert.True(Path.IsPathFullyQualified(users)); Assert.True(Path.IsPathFullyQualified(spells));
            fixture.UseLiteralUsersRoot(users);
            var before = new DurableClassImage(fixture.Original);
            var characterBefore = fixture.CharacterBytes; var spellsBefore = fixture.SpellBytes;
            Assert.NotEqual(fixture.Original.AccountId, fixture.Original.Character.Uid);

            Assert.True(await fixture.InvokeAsync(fixture.Original, literalSpellsRoot: spells));

            Assert.True(fixture.Store.TryGet(fixture.Connection, out var current));
            Assert.NotNull(current); Assert.NotSame(fixture.Original, current);
            Assert.Equal(users, current.UsersRoot);
            Assert.Equal(1005, current.Character.Class); Assert.Equal(910, current.Character.AttributePoints);
            Assert.Equal(2, current.Character.Reborn);
            before.RequireUnchanged(); before.RequireUnrelatedResources(current);
            AssertCharacterCoreEqual(current.Character, fixture.ReadCharacter());
            var expectedSpells = Convert.FromHexString("05000000" +
                "470400000000000000000000" + "FC0300000000000000000000" +
                "420400000000000000000000" + "160400000000000000000000" + "150400000000000000000000");
            Assert.Equal(expectedSpells, PlayerSpellSnapshotLoader.ToLegacyBytes(current.PlayerSpells));
            Assert.NotEqual(characterBefore, fixture.CharacterBytes); Assert.NotEqual(spellsBefore, fixture.SpellBytes);
            fixture.ExpectImage(fixture.CharacterPath, fixture.CharacterBytes);
            fixture.ExpectImage(fixture.SpellPath, expectedSpells);
            fixture.ExpectNoPending(); fixture.RequireFilesUnchanged();
            Assert.Equal(13, fixture.Sent.Count);
            Assert.True(fixture.Disconnected);
        });
    }
}
