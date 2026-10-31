using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneBagTests
{
    [Fact]
    public Task ActualFineRainQueryDenialFollowsCommittedBagEchoAndStillRefreshesTheStaleRank() => RunPhase(async f =>
    {
        var current = f.Current(BagFixture.Uid);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes, 212));
        // The actual Frame initial equipment query issued this admission. No
        // test-created stamp or accepting query callback is installed.
        Assert.Equal(10000, LegacyFineRainEquipmentAdmission.ForOwner(current).StampMilliseconds);
        Assert.Equal(10300U, Assert.Single(PhaseRanks(f).Snapshot()).Score);
        var moved = Assert.Single(current.PlayerItems.InventoryItems).WithPosition(211);
        var files = PhaseFiles(f, current.PlayerItems with { InventoryItems = [], RuneItems = [moved, current.PlayerItems.RuneItems[0]] });
        int first = f.ActorFrames.Count;
        f.BeforeSend = (_, _) => { AssertPhaseConverged(f, files); Assert.Equal(10300U, Assert.Single(PhaseRanks(f).Snapshot()).Score); };
        try { await f.SendAsync(BagFixture.Uid, BagFixture.Request(true)); } finally { f.BeforeSend = null; }
        AssertPhaseConverged(f, files); Assert.Equal(first + 3, f.ActorFrames.Count);
        Assert.Equal(ItemGolden(moved), f.ActorFrames[first]); Assert.Equal(BagFixture.Request(true), f.ActorFrames[first + 1]);
        Assert.True(LegacyMsgMessagePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(f.ActorFrames[first + 2]), out var wait));
        Assert.Contains("You Shoud wait 6 seconds.", wait.Content);
        Assert.Empty(PhaseRanks(f).Snapshot()); // The real5600 score evicts the stale10300 row after the wait.
        Assert.Equal(10000, LegacyFineRainEquipmentAdmission.ForOwner(f.Current(BagFixture.Uid)).StampMilliseconds);
        var after = f.Data.Files(); int count = f.ActorFrames.Count;
        await f.SendAsync(BagFixture.Uid, BagFixture.Request(true));
        Assert.Equal(count, f.ActorFrames.Count); f.Data.AssertFiles(after); Assert.Empty(PhaseRanks(f).Snapshot());
    }, fineRain: true);

    [Fact]
    public Task ActualTransportEofAfterEchoRetiresTheHolderBeforeQueryOrRankWithoutReplayingTheCommittedMove() => RunPhase(async f =>
    {
        var current = f.Current(BagFixture.Uid);
        var moved = Assert.Single(current.PlayerItems.InventoryItems).WithPosition(211);
        var files = PhaseFiles(f, current.PlayerItems with { InventoryItems = [], RuneItems = [moved, current.PlayerItems.RuneItems[0]] });
        int first = f.ActorFrames.Count; Task? close = null; LegacyOwnedNativeItemImage? committedImage = null;
        f.BeforeSend = (_, bytes) =>
        {
            AssertPhaseConverged(f, files);
            committedImage = f.Current(BagFixture.Uid).NativeGameClient!.OrdinarySpellBinding!.Items.Image;
            if (MsgItemUsagePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.Action == MsgItemUsageAction.Rune)
            {
                Assert.Null(close);
                // Actual EOF immediately marks Trades ended. Its asynchronous
                // holder removal waits out this active Frame; do not await in
                // the sender and deadlock the real world-operation barrier.
                close = f.Connection(BagFixture.Uid).CompleteTransportCloseAsync();
            }
        };
        try { await f.SendAsync(BagFixture.Uid, BagFixture.Request(true)); } finally { f.BeforeSend = null; }
        Assert.NotNull(close); await close!;
        Assert.Equal(first + 2, f.ActorFrames.Count); Assert.Equal(ItemGolden(moved), f.ActorFrames[first]);
        Assert.Equal(BagFixture.Request(true), f.ActorFrames[first + 1]);
        Assert.False(f.Store.TryGet(f.Connection(BagFixture.Uid), out _));
        Assert.Equal(10300U, Assert.Single(PhaseRanks(f).Snapshot()).Score);
        f.Data.AssertFiles(files); Assert.NotNull(committedImage);
        Assert.Equal((ushort)211, Assert.Single(committedImage!.FinalSnapshot.RuneItems, row => row.Uid == BagFixture.Target).Position);
        await f.Runtime.DisposeAsync();
        for (int restart = 0; restart < 2; restart++)
        {
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason), reason);
            await using var owner = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
            Assert.True(owner.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
            f.Data.AssertFiles(files); Assert.Equal(first + 2, f.ActorFrames.Count);
        }
    });

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public Task ActualNativeFinRaneChangesOnlyForCommittedBagActionsBeforeTheFirstReply(bool accepted) => RunPhase(async f =>
    {
        var current = f.Current(BagFixture.Uid); var player = current.NativeGameClient!.Player!;
        // Declared distant prior scalar, not a clock/provider/publication owner.
        // The actual native clock and real prepared publisher choose the result.
        var prior = LegacyNativeTime32.Now.AddDays(7); player.FinRane = prior;
        var before = f.Data.Files(); int first = f.ActorFrames.Count, observed = 0;
        var moved = Assert.Single(current.PlayerItems.InventoryItems).WithPosition(211);
        var files = accepted ? PhaseFiles(f, current.PlayerItems with { InventoryItems = [], RuneItems = [moved, current.PlayerItems.RuneItems[0]] }) : before;
        var earliest = LegacyNativeTime32.Now;
        f.BeforeSend = (_, _) =>
        {
            AssertPhaseConverged(f, files); var latest = LegacyNativeTime32.Now;
            Assert.NotEqual(prior, player.FinRane);
            Assert.InRange(unchecked((uint)(player.FinRane.Value - earliest.Value)), 0U, unchecked((uint)(latest.Value - earliest.Value)));
            observed++;
        };
        try { await f.SendAsync(BagFixture.Uid, BagFixture.Request(true, accepted ? BagFixture.Target : uint.MaxValue)); }
        finally { f.BeforeSend = null; }
        if (accepted) { Assert.Equal(12, observed); Assert.Equal(first + 12, f.ActorFrames.Count); Assert.NotEqual(prior, player.FinRane); }
        else { Assert.Equal(0, observed); Assert.Equal(first, f.ActorFrames.Count); Assert.Equal(prior, player.FinRane); Assert.Same(current, f.Current(BagFixture.Uid)); }
        AssertPhaseConverged(f, files);
    });

    private static Task RunPhase(Func<OwnedEquipmentBehaviorFixture, Task> body, bool fineRain = false)
    {
        // Declared saved FineRain state is not cast provenance. The real Frame
        // initial QueryEquipment is the sole issuer of the tested query stamp.
        var character = CharacterSnapshotParser.ParseText("[Character]\r\nUID=" + BagFixture.Uid.ToString(CultureInfo.InvariantCulture) + "\r\n" +
            "Name=OwnedBagPhase\r\nBody=1008\r\nFace=154\r\nHaire=0\r\nClass=7051\r\n" +
            "FirstClass=0\r\nSecoundeClass=0\r\nReborn=2\r\nLevel=140\r\nExperience=0\r\n" +
            "Map=1002\r\nX=430\r\nY=380\r\nPMap=1002\r\nPMapX=430\r\nPMapY=380\r\n" +
            "Strength=10\r\nAgility=10\r\nVitaliti=10\r\nSpirit=10\r\nAtributes=0\r\n" +
            "MinHitPoints=1\r\nMinMana=50\r\nMoney=777\r\nConquerPoints=222\r\n" +
            "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\n" +
            (fineRain ? "StatusFlags=0/0/0/0/0/0/1048576\r\n" : ""));
        var items = new PlayerItemSnapshot([BagFixture.Item(BagFixture.Target, BagFixture.RuneType, 0)], [], [], [],
            [BagFixture.Item(BagFixture.Kept, BagFixture.RuneType, 212)]);
        var row = OwnedEquipmentBehaviorFixture.Item(BagFixture.RuneType, "IronShield(+17)", new Dictionary<int, string>
        { [ItemType.RequiredClassFieldIndex] = "6000", [ItemType.RuneSpellIdFieldIndex] = "1416016", [ItemType.StackSizeFieldIndex] = "0" });
        var seedFields = new[] { "OwnedBagPhase", "10300", BagFixture.Uid.ToString(CultureInfo.InvariantCulture), "" };
        var seed = new RuneRankingRecord(seedFields[0], 10300, checked((int)BagFixture.Uid), "declared-phase-RuneRanking.txt", 2,
            string.Join('/', seedFields), seedFields);
        var catalog = NativeCallerWorldDataFixture.Create(new ItemType[] { row }, [], runeRankings: new RuneRankingRecord[] { seed });
        var clock = new PhaseClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10000));
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(new(character, items), catalog, options, body);
    }
    private sealed class PhaseClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    }
    private static LegacyRuneRankingState PhaseRanks(OwnedEquipmentBehaviorFixture f) =>
        Assert.IsType<LegacyRuneRankingState>(typeof(WorldServerFrameHandler)
            .GetField("_runeRankings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(f.Handler));
    private static Dictionary<string, byte[]> PhaseFiles(OwnedEquipmentBehaviorFixture f, PlayerItemSnapshot items)
    {
        var files = f.Data.Files(); files[Path.GetRelativePath(f.Data.Root, f.ItemPath(BagFixture.Uid))] = PlayerItemSnapshotLoader.ToLegacyBytes(items);
        return files;
    }
    private static void AssertPhaseConverged(OwnedEquipmentBehaviorFixture f, Dictionary<string, byte[]> files)
    {
        f.Data.AssertFiles(files); var current = f.Current(BagFixture.Uid); var binding = current.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Items.Image.IsCurrentPlain());
        Assert.Equal(File.ReadAllBytes(f.ItemPath(BagFixture.Uid)), PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
        Assert.Equal(File.ReadAllBytes(f.ItemPath(BagFixture.Uid)), PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }
}
