using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task TwoRealSignedCreatorsAndANewbornBankCommitSurviveBagV6AndDecomposeRecoveryWithoutReissuingAnyRange() => Run(async f =>
    {
        var initialLedger = f.Ledger();
        Assert.Equal(new uint[] { CreationFixture.RuneUid }, BagAuthPhysicalUids(f));
        await CreateAndAssert(f); await DepositNewbornMoney(f);
        var first = BagAuthCompleted(f, CreationFixture.Account);
        Assert.Equal(4, first.Allocation!.Count); var firstHistory = HistoryFiles(f);
        var newborn = File.ReadAllBytes(f.CharacterPath);
        Assert.Equal(99_300, f.Current.Character.Money); Assert.Equal("700", f.Current.Character.Attributes["WHMoney"]);
        Assert.Equal((1L, 0L), (f.Current.Character.Level, f.Current.Character.Reborn));
        await f.ConnectRuneActorAsync();
        var actorConnection = f.RuneConnection!;
        var actor = f.CurrentFor(actorConnection); var native = actor.NativeGameClient!;
        var target = Assert.Single(actor.PlayerItems.RuneItems);
        Assert.Equal(CreationFixture.RuneUid, target.Uid); Assert.Equal((ushort)211, target.Position);
        Assert.Equal((140L, 2L), (actor.Character.Level, actor.Character.Reborn));
        var actorCharacter = File.ReadAllBytes(f.Data.UserPath);
        var actorSpells = File.ReadAllBytes(BagAuthActorPath(f, LegacyTradeJournalFileKind.Spells));
        var stateBeforeReturn = File.ReadAllBytes(f.Data.State);
        uint returnedUid = checked(f.Ledger().HighWater + 1);
        var returned = target.WithUid(returnedUid).WithPosition(0);
        var returnedItems = PlayerItemSnapshotLoader.ToLegacyBytes(actor.PlayerItems with { InventoryItems = [returned], RuneItems = [] });
        var expectedReturn = BagAuthExpectedFiles(f, returnedItems);
        int returnFirst = f.Frames(CreationFixture.RuneActor).Count;
        f.BeforeSend = (uid, _) =>
        {
            Assert.Equal(CreationFixture.RuneActor, uid);
            BagAuthAssertGameFiles(f, expectedReturn); BagAuthAssertActor(f, returnedItems, actorSpells);
            BagAuthAssertNamespaceStep(f, stateBeforeReturn, 1, 2, 6);
        };
        try { await f.SendOnAsync(actorConnection, BagAuthRequest(returned: true, target.Uid)); }
        finally { f.BeforeSend = null; }
        var actorFrames = f.Frames(CreationFixture.RuneActor);
        Assert.Equal(returnFirst + 3, actorFrames.Count);
        BagAuthAssertUsage(actorFrames[returnFirst], target.Uid, MsgItemUsageAction.RemoveInventory);
        BagAuthAssertItem(actorFrames[returnFirst + 1], returned);
        Assert.Equal(BagAuthRequest(true, target.Uid), actorFrames[returnFirst + 2]);
        BagAuthAssertGameFiles(f, expectedReturn); BagAuthAssertNamespaceStep(f, stateBeforeReturn, 1, 2, 6);
        Assert.Equal(newborn, File.ReadAllBytes(f.CharacterPath)); AssertHistoricalFiles(firstHistory, HistoryFiles(f));
        Assert.DoesNotContain(target.Uid, BagAuthPhysicalUids(f));

        // Full real Account Frame/password validator/signed file ticket and the
        // actual typed MissingSnapshot branch again, while this SAME Runtime's
        // UID state is6. No direct Issue or fabricated creation permit is used.
        const uint secondAccount = CreationFixture.Account + 1;
        const string secondName = "AfterBag";
        var runtime = f.Runtime; var stateBeforeSecond = File.ReadAllBytes(f.Data.State);
        foreach (var path in f.LifetimeParticipantPaths(secondAccount)) Assert.False(File.Exists(path));
        var entered = await f.EnterLifetimeAccountAsync(secondAccount, "later-creator");
        Assert.Same(runtime, f.Runtime); Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, entered.Connection));
        var secondBefore = f.Data.Files(); int secondObserved = 0, peerObserved = 0;
        f.BeforeSend = (uid, bytes) =>
        {
            Assert.Same(runtime, f.Runtime);
            AssertLifetimeCreated(f, entered.Connection, secondAccount, secondName, entered.Ticket);
            BagAuthAssertSecondCreator(f, secondBefore, secondAccount, stateBeforeSecond, firstHistory, newborn, actorCharacter, returnedItems, actorSpells);
            if (uid == secondAccount) { secondObserved++; return; }
            // The real startup also publishes the newborn spawn to existing
            // visible players. Those replies must observe the SAME committed
            // complete creation, not be mistaken for another account's grant.
            Assert.Contains(uid, new[] { CreationFixture.Account, CreationFixture.RuneActor });
            Assert.True(LegacyPlayerSpawnPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var spawn));
            Assert.Equal((ulong)secondAccount, spawn.GetNumericField(2));
            var viewer = f.CurrentFor(uid == CreationFixture.Account ? f.Connection : actorConnection);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(viewer.NativeGameClient!.OrdinarySpellBinding!));
            peerObserved++;
        };
        try { await f.CreateLifetimeAccountAsync(entered.Connection, secondName); }
        finally { f.BeforeSend = null; }
        Assert.True(secondObserved > 0); Assert.True(peerObserved > 0); Assert.Same(runtime, f.Runtime);
        BagAuthAssertSecondCreator(f, secondBefore, secondAccount, stateBeforeSecond, firstHistory, newborn, actorCharacter, returnedItems, actorSpells);
        var second = BagAuthCompleted(f, secondAccount);
        Assert.Equal(returnedUid + 1, second.Allocation!.First); Assert.Equal(4, second.Allocation.Count);
        BagAuthAssertCompletedUnchanged(f, first);
        var bothHistory = HistoryFiles(f); Assert.Equal(firstHistory.Count + 1, bothHistory.Count);
        var bothUids = first.Allocation.First + (uint)first.Allocation.Count;
        Assert.Equal(returnedUid, bothUids);
        var registered = BagAuthRange(first.Allocation).Concat(BagAuthRange(second.Allocation)).Append(returnedUid).Order().ToArray();
        Assert.Equal(registered, BagAuthPhysicalUids(f));

        var beforeAdd = File.ReadAllBytes(f.Data.State); var currentActor = f.CurrentFor(actorConnection);
        var inBag = returned.WithPosition(211);
        var bagItems = PlayerItemSnapshotLoader.ToLegacyBytes(currentActor.PlayerItems with { InventoryItems = [], RuneItems = [inBag] });
        var expectedAdd = BagAuthExpectedFiles(f, bagItems); int addFirst = actorFrames.Count;
        f.BeforeSend = (uid, _) =>
        {
            Assert.Equal(CreationFixture.RuneActor, uid); BagAuthAssertGameFiles(f, expectedAdd);
            BagAuthAssertActor(f, bagItems, actorSpells); Assert.Equal(beforeAdd, File.ReadAllBytes(f.Data.State));
        };
        try { await f.SendOnAsync(actorConnection, BagAuthRequest(false, returnedUid)); }
        finally { f.BeforeSend = null; }
        Assert.Equal(addFirst + 12, actorFrames.Count); BagAuthAssertItem(actorFrames[addFirst], inBag);
        Assert.Equal(BagAuthRequest(false, returnedUid), actorFrames[addFirst + 1]);
        LegacySelectedEquipmentQueryAssertions.NeutralQuery(actorFrames, addFirst + 2, CreationFixture.RuneActor);
        BagAuthAssertGameFiles(f, expectedAdd); Assert.Equal(beforeAdd, File.ReadAllBytes(f.Data.State));
        BagAuthAssertCompletedUnchanged(f, first); BagAuthAssertCompletedUnchanged(f, second);

        // Both now-admitted newborns still have exactly their original creation
        // entitlement. Repeating NewRole cannot consume another starter range.
        var beforeRetries = f.Data.Files(); int firstFrames = f.Sent.Count, secondFrames = f.Frames(secondAccount).Count;
        await f.CreateAsync("AgainFirst"); await f.CreateLifetimeAccountAsync(entered.Connection, "AgainSecond");
        Assert.Equal(firstFrames, f.Sent.Count); Assert.Equal(secondFrames, f.Frames(secondAccount).Count);
        f.Data.AssertFiles(beforeRetries); Assert.Equal(0, f.Runtime.RetainedCreationAccountCount);

        // Consume the genuinely returned/re-added UID through the real old18
        // Decompose producer. Capture its actual allocation/color, not a
        // manufactured receipt, namespace row or positive journal.
        var beforeDecompose = File.ReadAllBytes(f.Data.State); var decomposeFiles = f.Data.Files();
        var beforeActor = f.CurrentFor(actorConnection); var image = native.OrdinarySpellBinding!.Items.Image;
        var spells = native.MySpells!.ClientSpells; int actorCount = actorFrames.Count;
        using (var stop = LegacyJournalInterruption.StopAfterStageFlushed(3))
        {
            f.Handler.ItemSpellJournalInterruption = stop;
            try { await f.SendOnAsync(actorConnection, BagAuthDecomposeRequest(returnedUid)); }
            finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(stop.PendingObserved, f.Log.ToString());
        }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Equal(actorCount, actorFrames.Count);
        Assert.Same(beforeActor, f.CurrentFor(actorConnection)); Assert.Same(image, native.OrdinarySpellBinding.Items.Image);
        Assert.Same(spells, native.MySpells.ClientSpells); Assert.False(f.Store.IsBoundSpellOwnerCurrent(native.OrdinarySpellBinding));
        var pending = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!; Assert.Equal(18, pending["Version"]!.GetValue<int>());
        var operation = pending["RuneDecompose"]!; var allocation = operation["Allocation"]!;
        uint firstBirth = checked(JsonNode.Parse(beforeDecompose)!["HighWater"]!.GetValue<uint>() + 1);
        Assert.Equal(returnedUid, operation["SelectedUid"]!.GetValue<uint>());
        Assert.Equal(firstBirth, allocation["First"]!.GetValue<uint>()); Assert.Equal(2, allocation["Count"]!.GetValue<int>());
        BagAuthAssertAllocation(f, operation, beforeDecompose);
        byte color = operation["FragmentColor"]!.GetValue<byte>(); Assert.InRange(color, (byte)3, (byte)8);
        var essence = new PlayerItemSnapshotItem { Uid = firstBirth, ItemId = 4_040_001, Position = 211, StackSize = 8120, Durability = 1, MaximumDurability = 1 };
        var fragment = new PlayerItemSnapshotItem { Uid = firstBirth + 1, ItemId = 3_306_368, StackSize = 10, Bound = 1, Color = color, Durability = 1, MaximumDurability = 1 };
        var decomposed = PlayerItemSnapshotLoader.ToLegacyBytes(beforeActor.PlayerItems with { InventoryItems = [fragment], RuneItems = [essence] });
        BagAuthAssertCapturedDecompose(f, pending, actorCharacter, bagItems, actorSpells, decomposed);
        BagAuthAssertNamespaceStep(f, beforeDecompose, 2, 1, 6, pending: true);
        Assert.Equal(newborn, File.ReadAllBytes(f.CharacterPath)); AssertHistoricalFiles(bothHistory, HistoryFiles(f));
        await f.CloseAsync();
        var finalIds = registered.Where(uid => uid != returnedUid).Concat(new[] { firstBirth, firstBirth + 1 }).Order().ToArray();
        Dictionary<string, byte[]>? stable = null;
        for (int restart = 0; restart < 2; restart++)
        {
            using var ready = f.Open(); Assert.True(ready.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
            Assert.False(File.Exists(f.PendingPath)); BagAuthAssertNamespaceStep(f, beforeDecompose, 2, 2, 6);
            var expected = decomposeFiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            expected[Path.GetRelativePath(f.Data.Root, BagAuthActorPath(f, LegacyTradeJournalFileKind.Items))] = decomposed;
            expected[Path.GetRelativePath(f.Data.Root, f.Data.State)] = File.ReadAllBytes(f.Data.State);
            f.Data.AssertFiles(expected); Assert.Equal(finalIds, BagAuthPhysicalUids(f));
            BagAuthAssertCompletedUnchanged(f, first); BagAuthAssertCompletedUnchanged(f, second);
            AssertHistoricalFiles(bothHistory, HistoryFiles(f)); Assert.Equal(newborn, File.ReadAllBytes(f.CharacterPath));
            Assert.Equal(initialLedger.HighWater + 11, f.Ledger().HighWater); Assert.Equal(initialLedger.Generation + 10, f.Ledger().Generation);
            if (stable is null) stable = f.Data.Files(); else f.Data.AssertFiles(stable);
            Assert.Equal(actorCount, actorFrames.Count); Assert.Equal(0, ready.RetainedCreationAccountCount);
        }
    }, beforeBootstrap: BagAuthSeedActorInBag);
}
