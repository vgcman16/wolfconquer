using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task GenuineEquipmentQueryCommitsOriginAndSpellBeforeItsGrantNotification() => RunPhysicalDeathAsync(async f =>
    {
        var before = await EquipForDerivedQueryAsync(f);

        f.Owner.Data.AssertFiles(before.After); AssertDerivedQueryHistory(f, before);
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(before.Binding));
        Assert.Equal(200L, f.TargetSession.Character.MinHitPoints); Assert.Equal(200, before.Binding.Player.HitPoints);
        Assert.Equal("20000:5", f.TargetSession.Character.Attributes["ManagedDerivedSpellOrigins"]);
        var row = Assert.Single(f.TargetSession.PlayerSpells.Spells, row => row.Id == 20000);
        Assert.Equal(new PlayerSpellSnapshotEntry(20000, 5, 0, 0, 0, 0), row);
        Assert.Equal((ushort)5, before.Binding.Storage.ClientSpells[20000].Level);
        Assert.Equal(DeathGrantedGolden(), Assert.Single(f.Owner.Frames(DeathTargetAccount),
            bytes => bytes.AsSpan().SequenceEqual(DeathGrantedGolden())));
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
    });

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(2, true)]
    public Task GenuineDerivedQueryPrefixesRecoverWithoutRegrantOrUidAllocation(int boundary, bool flushed) =>
        RunPhysicalDeathAsync(async f =>
        {
            DerivedQueryBefore before;
            using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
            {
                before = await EquipForDerivedQueryAsync(f, fault);
                Assert.True(fault.PendingObserved, f.Owner.Log.ToString());
                Assert.Equal(flushed ? new[] { 0, boundary } : Enumerable.Range(0, boundary + 1), fault.Visited);
            }
            Assert.True(f.Owner.Runtime.Recovery.IsBlocked);
            Assert.False(f.Owner.Store.TryGet(f.Target, out _)); Assert.True(before.Binding.IsRetired);
            Assert.False(before.Binding.Storage.ClientSpells.ContainsKey(20000));
            Assert.Equal(200, before.Binding.Player.HitPoints);
            Assert.DoesNotContain(f.Owner.Frames(DeathTargetAccount), bytes => bytes.AsSpan().SequenceEqual(DeathGrantedGolden()));
            var pending = JsonNode.Parse(File.ReadAllBytes(f.Owner.PendingPath))!;
            Assert.Equal(23, pending["Version"]!.GetValue<int>());
            var operation = pending["DerivedSpellQuery"]!;
            Assert.Equal(DeathTargetUid, operation["TargetUid"]!.GetValue<uint>());
            Assert.Equal(2, operation["Source"]!.GetValue<int>()); Assert.Empty(operation["RankingRows"]!.AsArray());
            Assert.Equal(20006, operation["GrantedRow"]!["Id"]!.GetValue<int>());
            var paths = new[] { f.CharacterPath, f.SpellsPath };
            var changes = pending["Changes"]!.AsArray(); Assert.Equal(2, changes.Count);
            int completed = flushed ? 0 : boundary;
            for (int index = 0; index < paths.Length; index++)
            {
                string key = Path.GetRelativePath(f.Owner.Data.Root, paths[index]);
                Assert.Equal(before.Files[key], Convert.FromBase64String(changes[index]!["Before"]!.GetValue<string>()));
                Assert.Equal(before.After[key], Convert.FromBase64String(changes[index]!["After"]!.GetValue<string>()));
                Assert.Equal(index < completed ? before.After[key] : before.Files[key], File.ReadAllBytes(paths[index]));
            }
            AssertDerivedQueryHistory(f, before);
            int outputCount = f.Owner.Frames(DeathTargetAccount).Count;
            await f.Owner.CloseAsync();
            for (int restart = 0; restart < 2; restart++)
            {
                using var opened = f.Owner.Open();
                Assert.True(opened.Recovery.IsReadyForCharacterItemsSpells(f.Owner.Data.Users, f.Owner.Data.Items, f.Owner.Data.Spells));
                f.Owner.Data.AssertFiles(before.After); AssertDerivedQueryHistory(f, before);
                Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
                Assert.Equal(outputCount, f.Owner.Frames(DeathTargetAccount).Count);
                var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
                Assert.Equal(200L, saved.MinHitPoints); Assert.Equal("20000:5", saved.Attributes["ManagedDerivedSpellOrigins"]);
            }
        });
}
