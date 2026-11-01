using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeOwnedHitpointsTests
{
    [Fact]
    public Task GenuineOwnedHpWritePreservesCaseDistinctOpaqueRuntimeFields()
        => Run(async f =>
        {
            await f.LoginAsync(); f.Sent.Clear();
            var initial = f.Current;
            var attrs = new Dictionary<string, string>(initial.Character.Attributes, StringComparer.Ordinal)
                { ["Other"] = "one", ["other"] = "two" };
            f.Store.Set(f.Connection, initial with { Character = initial.Character with { Attributes = attrs } });
            var before = f.Current; var binding = Binding(f);
            var beforeImage = File.ReadAllBytes(f.CharacterPath);
            var afterImage = HpImage(f, beforeImage, 555);
            var changes = HpChanges(f, beforeImage, afterImage);
            int writes = 0;
            Assert.True(f.Store.TryCommitSession(f.Connection, before,
                before with { Character = before.Character with { MinHitPoints = 555 } }, () =>
                {
                    writes++;
                    Assert.True(LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, changes, out var reason), reason);
                    return true;
                }, out var final));
            Assert.Equal(1, writes); Assert.Same(f.Current, final); Assert.Same(binding, Binding(f));
            Assert.Equal("one", final!.Character.Attributes["Other"]);
            Assert.Equal("two", final.Character.Attributes["other"]);
            Assert.Equal(afterImage, File.ReadAllBytes(f.CharacterPath));
            AssertHp(f, 555); Assert.Empty(f.Sent);
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });
}
