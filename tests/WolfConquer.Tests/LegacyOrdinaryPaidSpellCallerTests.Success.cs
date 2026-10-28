using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryPaidSpellCallerTests
{
    [Theory]
    [InlineData(NativePaidSpellCase.Mount)]
    [InlineData(NativePaidSpellCase.Taoist)]
    [InlineData(NativePaidSpellCase.Trojan)]
    public async Task GenuineAdmittedPaidCallerCommitsDebitSpellAndSavedEvidenceBeforeFirstOutput(NativePaidSpellCase kind)
    {
        await Run(kind, funded: true, async f =>
        {
            await f.LoginAsync(); f.RequireCommitObserved(); var before = f.Current; NativeMatches(f);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var anchor = binding.SavedSpells; var files = f.Files(); int first = f.Sent.Count, observed = 0;
            long money = before.Character.Money - (kind == NativePaidSpellCase.Mount ? Cost(kind) : 0);
            long cps = before.Character.ConquerPoints - (kind == NativePaidSpellCase.Mount ? 0 : Cost(kind));
            if (kind == NativePaidSpellCase.Trojan)
                Assert.Contains(before.PlayerItems.EquipmentItems, item => item.Position == 4 && item.ItemId == 614000);
            f.ObserveSend = (bytes, _) =>
            {
                if (observed++ == 0)
                {
                    // Observe only: actual sender has not accepted any scalar,
                    // spell or dialog output when these state proofs are read.
                    var current = f.Current; var saved = SavedCharacter(f);
                    Assert.Equal(money, current.Character.Money); Assert.Equal(cps, current.Character.ConquerPoints);
                    Assert.Equal(money, saved.Money); Assert.Equal(cps, saved.ConquerPoints);
                    Assert.Contains(current.PlayerSpells.Spells, row => row.Id == Learned(kind));
                    var image = File.ReadAllBytes(f.SpellPath);
                    Assert.Contains(PlayerSpellSnapshotLoader.Parse(image).Spells, row => row.Id == Learned(kind));
                    Assert.NotSame(anchor, binding.SavedSpells); Assert.True(binding.SavedSpells.Matches(image));
                    Assert.False(LegacyTradeJournal.HasPending(f.Users)); Assert.False(f.Recovery.IsBlocked); NativeMatches(f);
                }
                f.Sent.Add(bytes); return ValueTask.CompletedTask;
            };
            try { Assert.True(await Invoke(f, kind, before)); }
            finally { f.ObserveSend = null; }
            var after = f.Current;
            Assert.True(observed >= 3); Assert.NotSame(before, after);
            Assert.Equal(money, after.Character.Money); Assert.Equal(cps, after.Character.ConquerPoints);
            Assert.Equal(before.Character.BoundConquerPoints, after.Character.BoundConquerPoints);
            Assert.Same(before.PlayerItems, after.PlayerItems); Assert.Same(before.PlayerProficiencies, after.PlayerProficiencies);
            var added = Assert.Single(after.PlayerSpells.Spells, row => row.Id == Learned(kind));
            Assert.Equal(new PlayerSpellSnapshotEntry(Learned(kind), 0, 0, 0, 0, 0), added);
            var sent = f.Sent.Skip(first).ToArray();
            Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(sent[0]), out var scalar));
            Assert.Equal((uint)after.Character.Uid, scalar!.Uid);
            var entry = Assert.Single(scalar.Updates);
            Assert.Equal(kind == NativePaidSpellCase.Mount ? LegacyMsgUpdateDataType.Money : LegacyMsgUpdateDataType.ConquerPoints, entry.Type);
            Assert.Equal(checked((ulong)(kind == NativePaidSpellCase.Mount ? money : cps)), entry.Value);
            Assert.True(LegacySpellPacket.TryParse(Frame(sent[1]), out var spell)); Assert.Equal(Learned(kind), spell.Id);
            Assert.All(sent.Skip(2), bytes => Assert.Equal(LegacyNpcReplyPacket.Opcode, Frame(bytes).Header.Opcode));
            RequireOpaque(f); NativeMatches(f);
            var afterFiles = f.Files(); Assert.Equal(files.Keys.Order(), afterFiles.Keys.Order());
            foreach (var file in files.Where(file => file.Key != f.CharacterPath && file.Key != f.SpellPath))
                Assert.Equal(file.Value, afterFiles[file.Key]);
        });
    }
}
