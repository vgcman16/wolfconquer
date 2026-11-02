using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicDecomposeTests
{
    [Fact]
    public Task NativePreallocationPreviewRetainsFinalWitnessesForTheActualPostwriterBoundary() => Run(async f =>
    {
        var before = f.Current; var binding = f.Native.OrdinarySpellBinding!;
        var savedBefore = binding.SavedSpells; var spellMap = f.Native.MySpells!.ClientSpells;
        var proficiencyMap = f.Native.MyProfs!.ClientProf;
        var character = File.ReadAllBytes(f.Data.UserPath);
        var spellPath = Path.Combine(f.Data.Spells, DecomposeFixture.Uid + ".bin");
        var proficiencyPath = Path.Combine(f.Data.Proficiencies, DecomposeFixture.Uid + ".bin");
        var spellBytes = File.ReadAllBytes(spellPath); var proficiencyBytes = File.ReadAllBytes(proficiencyPath);
        uint high = f.HighWater; int observed = 0;
        f.BeforeSend = _ =>
        {
            Assert.NotSame(before, f.Current); Assert.Same(binding, f.Native.OrdinarySpellBinding);
            Assert.NotSame(savedBefore, binding.SavedSpells);
            Assert.True(binding.SavedSpells.Matches(File.ReadAllBytes(spellPath)));
            Assert.Same(spellMap, f.Native.MySpells.ClientSpells); Assert.Same(proficiencyMap, f.Native.MyProfs.ClientProf);
            Assert.Equal(character, File.ReadAllBytes(f.Data.UserPath)); Assert.Equal(spellBytes, File.ReadAllBytes(spellPath));
            Assert.Equal(proficiencyBytes, File.ReadAllBytes(proficiencyPath));
            Assert.Equal(high + 9, f.HighWater); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            f.AssertConverged(); observed++;
        };
        try { await f.Decompose(); } finally { f.BeforeSend = null; }
        Assert.True(observed == 11, f.ReadDiagnostics()); Assert.Equal(11, f.Sent.Count);
        Assert.Equal(RemoveGolden(DecomposeFixture.First), f.Sent[0]);
        var rewards = f.Current.PlayerItems.InventoryItems.Where(row => row.ItemId == DecomposeFixture.Crystal).ToArray();
        Assert.Equal(9, rewards.Length);
        for (int index = 0; index < 9; index++)
        {
            Assert.Equal(high + checked((uint)index + 1), rewards[index].Uid);
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(f.Sent[index + 1]), out var added));
            Assert.Equal(rewards[index].Uid, added.Uid); Assert.Equal(DecomposeFixture.Crystal, added.ItemId);
            Assert.Equal((ushort)1, rewards[index].StackSize);
        }
        Assert.Equal(DecomposeFixture.Request(DecomposeFixture.First), f.Sent[^1]);
        Assert.False(f.Runtime.Recovery.IsBlocked);
        var committed = f.Data.Files(); var current = f.Current;
        f.Sent.Clear(); await f.Decompose(); Assert.Empty(f.Sent); Assert.Same(current, f.Current);
        f.Data.AssertFiles(committed); Assert.Equal(high + 9, f.HighWater); f.AssertConverged();
    });
}
