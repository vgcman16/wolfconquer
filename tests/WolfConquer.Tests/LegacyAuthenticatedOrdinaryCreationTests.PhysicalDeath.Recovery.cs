using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(2, true)]
    public Task GenuineChangedSpellPhysicalDeathPrefixesRecoverOnceWithoutStarterOrUidReplay(int boundary, bool flushed) =>
        RunPhysicalDeathAsync(async f =>
        {
            var captured = await CapturePhysicalDeathAsync(f, boundary, flushed);
            // The only output outside the failed physical producer is the
            // already committed Equip's in-flight first notification. No
            // physical/death/derived query output is authorized by pending22.
            Assert.Empty(f.Owner.Frames(CreationFixture.RuneActor));
            Assert.Single(f.Owner.Frames(DeathTargetAccount)); Assert.Empty(f.Owner.Sent);
            await RestartPhysicalDeathAsync(f, captured.Before, captured.TransactionId);
        });

    internal static async Task RestartPhysicalDeathAsync(PhysicalDeathFixture f, PhysicalDeathBefore before, string? transaction = null)
    {
        int outputs = f.Owner.Sent.Count + f.Owner.Frames(CreationFixture.RuneActor).Count + f.Owner.Frames(DeathTargetAccount).Count;
        await f.Owner.CloseAsync();
        for (int restart = 0; restart < 2; restart++)
        {
            using var opened = f.Owner.Open();
            Assert.True(opened.Recovery.IsReadyForCharacterItemsSpells(f.Owner.Data.Users, f.Owner.Data.Items, f.Owner.Data.Spells));
            // BOTH raw configured starts are checked BEFORE any fresh login;
            // login's own query cannot manufacture the expected20000 row.
            f.Owner.Data.AssertFiles(before.After); AssertDeathHistoryUnchanged(f, before);
            Assert.Equal(before.Payload, CompletedIntent(f.Owner).CopyPayload());
            Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
            Assert.True(LegacyItemUidRawCatalog.TryScan(f.Owner.Data.Items, f.Owner.Data.Mailbox, f.Owner.Data.Redeem, f.Owner.Data.Claim,
                out var observation, out var reason), reason);
            var state = JsonNode.Parse(before.Ledger)!;
            Assert.True(observation.Maximum <= state["HighWater"]!.GetValue<uint>());
            Assert.Equal(1UL, state["ArchiveCount"]!.GetValue<ulong>());
            var target = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
            Assert.Equal((long)DeathTargetUid, target.Uid); Assert.Equal(0L, target.MinHitPoints);
            if (f.Changed) Assert.Equal("20000:5", target.Attributes["ManagedDerivedSpellOrigins"]);
            else Assert.False(target.Attributes.ContainsKey("ManagedDerivedSpellOrigins"));
            var spells = PlayerSpellSnapshotLoader.Parse(File.ReadAllBytes(f.SpellsPath));
            Assert.Equal(f.Changed, spells.ContainsSpell(20000));
            Assert.Equal((byte)0xC7, File.ReadAllBytes(f.SpellsPath)[15]);
            if (transaction is not null)
                foreach (var path in new[] { f.CharacterPath, f.SpellsPath })
                    Assert.False(File.Exists(path + ".managed-trade-" + transaction + ".tmp"));
            Assert.Equal(outputs, f.Owner.Sent.Count + f.Owner.Frames(CreationFixture.RuneActor).Count + f.Owner.Frames(DeathTargetAccount).Count);
        }
    }
}
