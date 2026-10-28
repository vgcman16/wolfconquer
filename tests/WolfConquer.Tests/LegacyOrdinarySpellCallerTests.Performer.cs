using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinarySpellCallerTests
{
    [Fact]
    public async Task GenuineAdmittedPerformerLearnsFromCanonicalRowsAndKeepsOpaqueBytes()
    {
        await Run(async f =>
        {
            await f.LoginAsync(); PreparePerformer(f); var before = f.Current;
            Assert.IsType<LegacyNativeSpellStorage.CanonicalSessionSpellRows>(before.PlayerSpells.Spells);
            byte[] prior = File.ReadAllBytes(f.SpellPath); Assert.Equal(0xA5, prior[15]); int first = f.Sent.Count;
            Assert.Equal(new ushort[] { 6001 }, before.PlayerSpells.Spells.Select(row => row.Id));
            Assert.True(await Performer(f, before)); var current = f.Current;
            Assert.NotSame(before, current); Assert.Same(before.Character, current.Character);
            Assert.Same(before.PlayerItems, current.PlayerItems); Assert.Same(before.PlayerProficiencies, current.PlayerProficiencies);
            Assert.Equal(before.Character.Money, current.Character.Money); Assert.Equal(before.Character.ConquerPoints, current.Character.ConquerPoints);
            Assert.Equal(0, Assert.Single(current.PlayerSpells.Spells, s => s.Id == 1416).Experience); AssertNative(f);
            var expected = Convert.FromHexString("02000000").Concat(prior.Skip(4))
                .Concat(Convert.FromHexString("880500000000000000000000")).ToArray();
            Assert.Equal(expected, File.ReadAllBytes(f.SpellPath)); Assert.Equal(0xA5, expected[15]);
            var sent = f.Sent.Skip(first).ToArray(); Assert.Equal(7, sent.Length);
            Assert.Equal(new ushort[] { LegacySpellPacket.Opcode, LegacyStringPacket.Opcode, LegacyMsgMessagePacket.Opcode,
                LegacyNpcReplyPacket.Opcode, LegacyNpcReplyPacket.Opcode, LegacyNpcReplyPacket.Opcode, LegacyNpcReplyPacket.Opcode },
                sent.Select(x => Frame(x).Header.Opcode));
            Assert.Equal(Convert.FromHexString("19003508000000008805000000000000000000000000000000"), sent[0]);
            Assert.True(LegacyMsgMessagePacket.TryParse(Frame(sent[2]), out var message));
            Assert.Equal("You`ve learned the Triumph.", message.Content[3]);
        }, lineage: false);
    }

    [Theory]
    [InlineData("saved-progress")]
    [InlineData("native-divergence")]
    public async Task GenuineAdmittedPerformerRefusesChangedSourceWithoutDebitOrSuccess(string fault)
    {
        await Run(async f =>
        {
            await f.LoginAsync(); PreparePerformer(f); var before = f.Current;
            if (fault == "saved-progress")
            {
                byte[] image = File.ReadAllBytes(f.SpellPath); image[8] ^= 1; File.WriteAllBytes(f.SpellPath, image);
            }
            else Assert.IsType<LegacyNativeSpellStorage>(f.Native.MySpells).ClientSpells[6001].Experience++;
            var files = f.Files(); int first = f.Sent.Count;
            Assert.True(await Performer(f, before));
            Assert.Same(before, f.Current); Assert.Equal(first, f.Sent.Count); f.RequireFiles(files);
            Assert.DoesNotContain(f.Current.PlayerSpells.Spells, s => s.Id == 1416);
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        }, lineage: false);
    }
}
