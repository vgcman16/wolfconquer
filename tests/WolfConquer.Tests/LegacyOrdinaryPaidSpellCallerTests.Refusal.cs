using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryPaidSpellCallerTests
{
    [Theory]
    [InlineData(NativePaidSpellCase.Mount)]
    [InlineData(NativePaidSpellCase.Taoist)]
    [InlineData(NativePaidSpellCase.Trojan)]
    public async Task GenuineAdmittedPaidCallerRefusesSavedImageDriftWithoutDebitOrSuccess(NativePaidSpellCase kind)
    {
        await Run(kind, funded: true, async f =>
        {
            await f.LoginAsync(); var before = f.Current; NativeMatches(f);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding); var anchor = binding.SavedSpells;
            byte[] changed = File.ReadAllBytes(f.SpellPath); changed[8] ^= 1; File.WriteAllBytes(f.SpellPath, changed);
            var files = f.Files(); int first = f.Sent.Count;
            Assert.True(await Invoke(f, kind, before));
            Assert.Same(before, f.Current); Assert.Equal(before.Character.Money, f.Current.Character.Money);
            Assert.Equal(before.Character.ConquerPoints, f.Current.Character.ConquerPoints);
            Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == Learned(kind));
            Assert.Same(anchor, binding.SavedSpells); Assert.False(anchor.Matches(changed));
            Assert.Equal(first, f.Sent.Count); f.RequireFiles(files); NativeMatches(f);
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
            Assert.Contains("refused paid spell learning before commitment", f.Log.ToString(), StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData(NativePaidSpellCase.Mount)]
    [InlineData(NativePaidSpellCase.Taoist)]
    [InlineData(NativePaidSpellCase.Trojan)]
    public async Task GenuineAdmittedPaidCallerInsufficientFundsOnlyPublishesItsFailureDialog(NativePaidSpellCase kind)
    {
        await Run(kind, funded: false, async f =>
        {
            await f.LoginAsync(); var before = f.Current; NativeMatches(f);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding); var anchor = binding.SavedSpells;
            var files = f.Files(); int first = f.Sent.Count;
            Assert.True(await Invoke(f, kind, before));
            Assert.Same(before, f.Current); Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == Learned(kind));
            Assert.Same(anchor, binding.SavedSpells); f.RequireFiles(files); NativeMatches(f);
            var sent = f.Sent.Skip(first).ToArray(); Assert.NotEmpty(sent);
            Assert.All(sent, bytes => Assert.Equal(LegacyNpcReplyPacket.Opcode, Frame(bytes).Header.Opcode));
            var texts = sent.Select(bytes => { Assert.True(LegacyNpcReplyPacket.TryParse(Frame(bytes), out var reply)); return reply.Text; }).ToArray();
            Assert.Contains(texts, text => text.Contains(kind == NativePaidSpellCase.Mount ? "100,000" : "2,150", StringComparison.Ordinal));
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });
    }
}
