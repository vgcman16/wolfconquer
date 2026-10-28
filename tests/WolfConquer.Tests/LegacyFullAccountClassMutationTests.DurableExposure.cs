using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyFullAccountClassMutationTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task MissingActualSendOrDisconnectRouteRefusesBeforeEitherSavedFileOrSessionChanges(bool withSend, bool withDisconnect)
    {
        await WithDurableClassAsync(async fixture =>
        {
            fixture.ReplaceConnection(withSend, withDisconnect);
            var before = new DurableClassImage(fixture.Original);
            var handled = false;
            var error = await Record.ExceptionAsync(async () =>
            { handled = await fixture.InvokeAsync(fixture.Original); });

            fixture.RequireFilesUnchanged();
            before.RequireUnchanged();
            Assert.True(fixture.Store.TryGet(fixture.Connection, out var current));
            Assert.Same(fixture.Original, current);
            Assert.Empty(fixture.Sent);
            Assert.False(fixture.Disconnected);
            Assert.True(handled);
            Assert.Null(error);
        });
    }

    [Fact]
    public async Task RealSenderFailureAfterCommitKeepsBothDurableImagesAndCurrentSessionCommitted()
    {
        await WithDurableClassAsync(async fixture =>
        {
            fixture.ReplaceConnection(throwOnSend: true);
            var before = new DurableClassImage(fixture.Original);
            var error = await Record.ExceptionAsync(async () =>
            { await fixture.InvokeAsync(fixture.Original); });

            Assert.True(fixture.Store.TryGet(fixture.Connection, out var current));
            Assert.NotNull(current);
            Assert.NotSame(fixture.Original, current);
            Assert.Equal(1005, current.Character.Class);
            Assert.Equal(2, current.Character.Reborn);
            Assert.Equal(140, current.Character.Level);
            Assert.Equal(910, current.Character.AttributePoints);
            before.RequireUnchanged();
            before.RequireUnrelatedResources(current);
            AssertCharacterCoreEqual(current.Character, fixture.ReadCharacter());
            Assert.Contains("[Other]", Encoding.Latin1.GetString(fixture.CharacterBytes));
            var expectedSpells = Convert.FromHexString("05000000" +
                "470400000000000000000000" + "FC0300000000000000000000" +
                "420400000000000000000000" + "160400000000000000000000" + "150400000000000000000000");
            Assert.Equal(new ushort[] { 1095, 1020, 1090, 1046, 1045 }, current.PlayerSpells.Spells.Select(spell => spell.Id));
            Assert.Equal(expectedSpells, PlayerSpellSnapshotLoader.ToLegacyBytes(current.PlayerSpells));
            fixture.ExpectImage(fixture.CharacterPath, fixture.CharacterBytes);
            fixture.ExpectImage(fixture.SpellPath, expectedSpells);
            fixture.ExpectNoPending();
            fixture.RequireFilesUnchanged();
            Assert.Empty(fixture.Sent);
            Assert.False(fixture.Disconnected);
            Assert.Equal("owned-class-send-failure", Assert.IsType<IOException>(error).Message);
            Assert.DoesNotContain("no character, spell, session, packet, or disconnect mutation was committed", fixture.Log.ToString());
        });
    }
}
