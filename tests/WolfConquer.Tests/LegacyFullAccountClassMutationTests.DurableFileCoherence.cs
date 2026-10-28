using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyFullAccountClassMutationTests
{
    [Theory]
    [InlineData("character")]
    [InlineData("spells")]
    public async Task NewerModeledParticipantOnDiskIsPreservedWithoutReplacingTheCapturedSession(string participant)
    {
        await WithDurableClassAsync(async fixture =>
        {
            var before = new DurableClassImage(fixture.Original);
            var path = participant == "character" ? fixture.CharacterPath : fixture.SpellPath;
            var originalBytes = File.ReadAllBytes(path);
            byte[] winner;
            if (participant == "character")
            {
                winner = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(fixture.CharacterBytes)
                    .Replace("Money=100", "Money=101", StringComparison.Ordinal));
            }
            else
            {
                winner = fixture.SpellBytes;
                Assert.Equal(0x78, winner[8]); // First existing row's Experience888, little endian.
                winner[8] = 0x79; // Same real ID/level, newer Experience889.
            }
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            { file.Write(winner); file.SetLength(winner.Length); file.Flush(true); }
            Assert.NotEqual(originalBytes, winner);
            fixture.ExpectImage(path, winner);
            if (participant == "character")
            {
                var parsed = fixture.ReadCharacter();
                Assert.Equal(101, parsed.Money);
                Assert.NotEqual(fixture.Original.Character.Money, parsed.Money);
            }
            else
            {
                var parsed = new PlayerSpellSnapshotLoader(fixture.SpellsRoot).LoadByCharacterUid(fixture.Original.Character.Uid);
                Assert.Equal(1020, parsed.Spells[0].Id);
                Assert.Equal(7, parsed.Spells[0].Level);
                Assert.Equal(889, parsed.Spells[0].Experience);
                Assert.NotEqual(fixture.Original.PlayerSpells.Spells[0].Experience, parsed.Spells[0].Experience);
            }
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
            Assert.DoesNotContain("WorldServer applied LegacyNpc AccountSecurity FullAccount class reset", fixture.Log.ToString());
        });
    }
}
