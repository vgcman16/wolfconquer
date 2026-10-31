using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task CompletedCreationHistoryAndANewbornBusinessChangeSurviveRealPeerRuneRecoveryAndSecondRestart(bool decompose) => Run(async f =>
    {
        await CreateAndAssert(f); await DepositNewbornMoney(f); await f.ConnectRuneActorAsync();
        var newborn = File.ReadAllBytes(f.CharacterPath); var history = HistoryFiles(f);
        var stateBefore = File.ReadAllBytes(f.Data.State); var actor = f.CurrentFor(f.RuneConnection!);
        string actorCharacter = f.Data.UserPath, actorItems = Path.Combine(f.Data.Items, CreationFixture.RuneActor + ".bin"),
            actorSpells = Path.Combine(f.Data.Spells, CreationFixture.RuneActor + ".bin");
        var charBefore = File.ReadAllBytes(actorCharacter); var spellsBefore = File.ReadAllBytes(actorSpells);
        uint highWater = JsonNode.Parse(stateBefore)!["HighWater"]!.GetValue<uint>();
        var native = actor.NativeGameClient!; var image = native.OrdinarySpellBinding!.Items.Image; var map = native.MySpells!.ClientSpells;
        using (var fault = decompose ? LegacyJournalInterruption.StopAfterStageFlushed(3) : LegacyJournalInterruption.StopAfter(3))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.SendOnAsync(f.RuneConnection!, decompose ? DecomposeRequest() : ChanceRequest()); }
            finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString());
        }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Frames(CreationFixture.RuneActor));
        Assert.Same(actor, f.CurrentFor(f.RuneConnection!)); Assert.Same(image, native.OrdinarySpellBinding.Items.Image);
        Assert.Same(map, native.MySpells.ClientSpells); Assert.False(f.Store.IsBoundSpellOwnerCurrent(native.OrdinarySpellBinding));
        var pending = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!; Assert.Equal(decompose ? 18 : 17, pending["Version"]!.GetValue<int>());
        var expected = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase) { [actorCharacter] = charBefore, [actorSpells] = spellsBefore };
        if (!decompose)
        {
            byte roll = pending["RuneChance"]!["Roll"]!.GetValue<byte>(); Assert.InRange(roll, (byte)0, (byte)99);
            var source = Assert.Single(actor.PlayerItems.RuneItems); var after = source.WithRuneExperience(0);
            if (roll < 50) after = after.WithItemId(source.ItemId + 1);
            expected[actorItems] = PlayerItemSnapshotLoader.ToLegacyBytes(actor.PlayerItems with { RuneItems = [after] });
        }
        else
        {
            var operation = pending["RuneDecompose"]!; byte color = operation["FragmentColor"]!.GetValue<byte>(); Assert.InRange(color, (byte)3, (byte)8);
            Assert.Equal(highWater + 1, operation["Allocation"]!["First"]!.GetValue<uint>()); Assert.Equal(2, operation["Allocation"]!["Count"]!.GetValue<int>());
            var essence = new PlayerItemSnapshotItem { Uid = highWater + 1, ItemId = 4_040_001, Position = 211,
                StackSize = 8120, Durability = 1, MaximumDurability = 1 };
            var fragment = new PlayerItemSnapshotItem { Uid = highWater + 2, ItemId = 3_306_368, StackSize = 10,
                Durability = 1, MaximumDurability = 1, Bound = 1, Color = color };
            expected[actorItems] = PlayerItemSnapshotLoader.ToLegacyBytes(actor.PlayerItems with { RuneItems = [essence], InventoryItems = [fragment] });
        }
        foreach (var row in pending["Changes"]!.AsArray())
        {
            var kind = (LegacyTradeJournalFileKind)row!["Kind"]!.GetValue<int>();
            var path = f.Data.Options().Roots.PathFor(kind, CreationFixture.RuneActor);
            Assert.Equal(expected[path], Convert.FromBase64String(row["After"]!.GetValue<string>()));
        }
        Assert.Equal(newborn, File.ReadAllBytes(f.CharacterPath)); AssertHistoricalFiles(history, HistoryFiles(f));
        await f.CloseAsync();
        using (f.Open())
        {
            foreach (var target in expected) Assert.Equal(target.Value, File.ReadAllBytes(target.Key));
            Assert.Equal(newborn, File.ReadAllBytes(f.CharacterPath)); AssertHistoricalFiles(history, HistoryFiles(f));
            _ = CompletedIntent(f); Assert.False(File.Exists(f.PendingPath));
            var settledState = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
            Assert.Equal(highWater + (decompose ? 2U : 0U), settledState["HighWater"]!.GetValue<uint>());
            Assert.Equal(JsonNode.Parse(stateBefore)!["Generation"]!.GetValue<ulong>() + (decompose ? 2UL : 0UL), settledState["Generation"]!.GetValue<ulong>());
        }
        var settled = f.Data.Files(); using (f.Open()) f.Data.AssertFiles(settled);
        await f.ReopenFrameAsync(() => f.Data.AssertFiles(settled));
        Assert.Equal(newborn, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(99_300, f.Current.Character.Money);
        await f.ConnectRuneActorAsync();
        Assert.Equal(expected[actorItems], PlayerItemSnapshotLoader.ToLegacyBytes(f.CurrentFor(f.RuneConnection!).PlayerItems));
        AssertHistoricalFiles(history, HistoryFiles(f));
    });

    [Fact]
    public Task ActualPeerNamespaceOrphanCleanupPreservesCompletedOrdinaryHistoryAndTheNewbornBankCommit() => Run(async f =>
    {
        await CreateAndAssert(f); await DepositNewbornMoney(f); await f.ConnectRuneActorAsync();
        var files = f.Data.Files(); var history = HistoryFiles(f); var state = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
        uint highWater = state["HighWater"]!.GetValue<uint>(); ulong generation = state["Generation"]!.GetValue<ulong>();
        f.Runtime.RuneDecomposeNamespaceInterruption = LegacyRuneDecomposeNamespaceInterruption.AfterReservation;
        await f.SendOnAsync(f.RuneConnection!, DecomposeRequest());
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Frames(CreationFixture.RuneActor)); Assert.False(File.Exists(f.PendingPath));
        foreach (var pair in files) if (pair.Key != Path.GetRelativePath(f.Data.Root, f.Data.State)) Assert.Equal(pair.Value, f.Data.Files()[pair.Key]);
        var actual = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!; Assert.Equal(highWater + 2, actual["HighWater"]!.GetValue<uint>());
        Assert.Equal("RuneDecompose", Assert.Single(actual["Operations"]!.AsArray())!["NamespaceReservation"]!["Kind"]!.GetValue<string>());
        AssertHistoricalFiles(history, HistoryFiles(f)); await f.CloseAsync();
        using (f.Open())
        {
            var settled = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!; Assert.Empty(settled["Operations"]!.AsArray());
            Assert.Equal(highWater + 2, settled["HighWater"]!.GetValue<uint>()); Assert.Equal(generation + 2, settled["Generation"]!.GetValue<ulong>());
            AssertHistoricalFiles(history, HistoryFiles(f)); _ = CompletedIntent(f);
            foreach (var pair in files) if (pair.Key != Path.GetRelativePath(f.Data.Root, f.Data.State)) Assert.Equal(pair.Value, f.Data.Files()[pair.Key]);
        }
        var after = f.Data.Files(); using (f.Open()) f.Data.AssertFiles(after);
    });

    private static byte[] ChanceRequest() => new MsgItemUsagePacket(CreationFixture.RuneUid, 0,
        MsgItemUsageAction.ChanceRune, 511_043_495, 1, 0, 0).ToFrameBytes();
    private static Dictionary<string, byte[]> HistoryFiles(CreationFixture f)
    {
        string prefix = Path.GetRelativePath(f.Data.Root, f.Data.Registry) + Path.DirectorySeparatorChar;
        string state = Path.GetRelativePath(f.Data.Root, f.Data.State);
        return f.Data.Files().Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && pair.Key != state)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }
    private static void AssertHistoricalFiles(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
}
