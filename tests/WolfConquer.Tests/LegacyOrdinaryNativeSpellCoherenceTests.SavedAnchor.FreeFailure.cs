using WolfConquer.Core.World;
using Xunit.Sdk;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task WindowsActualManagedSoulAtomicReplaceDenialLeavesMemoryFileAndSavedAnchorUnchanged()
    {
        if (!OperatingSystem.IsWindows()) throw SkipException.ForSkip("Actual Windows deny-delete sharing is required for deterministic atomic-write refusal.");
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; var storage = RequireCoherent(f);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var anchor = binding.SavedSpells; byte[] saved = File.ReadAllBytes(f.SpellPath);
            var learned = Assert.Single(before.PlayerSpells.Spells, row => row.Id == 6001);
            var request = LegacySpellPacket.FromSnapshot(learned) with { UseSoul = LegacySpellPacket.MapUseSoul(3) };
            int sends = f.Sent.Count;
            using (var denyReplace = new FileStream(f.SpellPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                await f.FrameAsync(request.ToFrameBytes());
            Assert.Same(before, f.Current); Assert.Same(storage, f.Native.MySpells); RequireCoherent(f);
            Assert.Same(anchor, binding.SavedSpells); Assert.Equal(saved, File.ReadAllBytes(f.SpellPath));
            Assert.Equal(sends, f.Sent.Count); Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
            Assert.Contains("refused soul selection before commitment", f.Log.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("handled LegacySpell soul selection", f.Log.ToString(), StringComparison.Ordinal);
            await f.FrameAsync(request.ToFrameBytes());
            Assert.Equal(3, Assert.Single(f.Current.PlayerSpells.Spells, row => row.Id == 6001).UseSpellSoul);
            Assert.NotSame(anchor, binding.SavedSpells); Assert.True(binding.SavedSpells.Matches(File.ReadAllBytes(f.SpellPath)));
            RequireSavedOpaqueByte(File.ReadAllBytes(f.SpellPath), 6001, 0xA5); RequireCoherent(f);
        });
    }
}
