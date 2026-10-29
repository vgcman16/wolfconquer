using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task InterruptedSingleOwnerJournalReplaysBeforeFreshLoginAndCannotPayAgain(int replacements)
    {
        const uint uid = 1_299_061;
        Seed(uid);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var original = Current(owner, player);
        var expected = SaleResult(original, 1_300, 50);

        // Deliberate managed interruption boundary, not a killed process or
        // power-loss test. The old in-memory owner is abandoned without reuse.
        Assert.False(LegacyNpcSalePersistence.TryPersist(original, expected, ItemsRoot,
            LegacyJournalInterruption.StopAfter(replacements), out var reason));
        Assert.Contains("tradeJournalRecoveryRequired", reason);
        Assert.True(LegacyTradeJournal.HasPending(UsersRoot));

        var restarted = NewOwner();
        var recovered = await Login(restarted, uid);

        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        AssertSaved(uid, expected);
        Assert.Equal(1_300, Current(restarted, recovered).Character.Money);
        Assert.Equal(KeptUid, Assert.Single(Current(restarted, recovered).PlayerItems.InventoryItems).Uid);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected.PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(Current(restarted, recovered).PlayerItems));
        await AssertReplayCannotPay(restarted, recovered);

        var again = NewOwner();
        var reloaded = await Login(again, uid);
        AssertSaved(uid, expected);
        Assert.Equal(1_300, Current(again, reloaded).Character.Money);
        await AssertReplayCannotPay(again, reloaded);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("kind")]
    [InlineData("path")]
    public async Task MalformedSingleOwnerJournalFencesAdmissionWithoutTouchingEitherAssetFile(string mutation)
    {
        const uint uid = 1_299_071;
        Seed(uid);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var original = Current(owner, player);
        Assert.False(LegacyNpcSalePersistence.TryPersist(original, SaleResult(original, 1_300, 50), ItemsRoot,
            LegacyJournalInterruption.StopAfter(0), out _));
        var record = JsonNode.Parse(File.ReadAllText(PendingPath))!.AsObject();
        Assert.Equal(2, record["Version"]!.GetValue<int>());
        if (mutation == "count") record["Changes"]!.AsArray().RemoveAt(1);
        else if (mutation == "kind") record["Changes"]![1]!["Kind"] = 0;
        else record["Changes"]![0]!["Path"] = "../unrelated.ini";
        File.WriteAllText(PendingPath, record.ToJsonString());
        var journalBytes = File.ReadAllBytes(PendingPath);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var fresh = NewOwner();
        var attempted = CreateConnection(uid);

        await SendLogin(fresh, attempted);

        Assert.False(fresh.Sessions.TryGet(attempted.Connection, out _));
        Assert.DoesNotContain(attempted.Sent, IsAnswerOk);
        Assert.DoesNotContain(attempted.Sent, IsSaleSuccessFrame);
        Assert.Contains("trade recovery", fresh.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(journalBytes, File.ReadAllBytes(PendingPath));
    }
}
