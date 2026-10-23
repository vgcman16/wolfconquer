using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Theory]
    [InlineData(4, "missing-earth")]
    [InlineData(5, "missing-earth")]
    [InlineData(4, "invalid-earth")]
    [InlineData(5, "invalid-earth")]
    [InlineData(4, "malformed-earth")]
    [InlineData(5, "malformed-earth")]
    [InlineData(4, "uid-exhausted")]
    [InlineData(5, "uid-exhausted")]
    [InlineData(0, "missing-fragment")]
    [InlineData(4, "missing-fragment")]
    [InlineData(0, "invalid-fragment")]
    [InlineData(4, "invalid-fragment")]
    public async Task EarthForceTransactionPreparationFailurePreservesFullStateFilesAndRewardOutput(
        ushort fragments, string fault)
    {
        var scene = await RunEarthForceTransactionAsync(fragments, fault);
        AssertEarthForceTransactionUnchanged(scene);
        Assert.Contains(fault == "uid-exhausted" ? "reward-preparation-overflow" : "reward-metadata-invalid-or-missing", scene.Log);
        // The real final-hit route was reached; missing authority is not a
        // substitute for exercising the requested item preparation failure.
        Assert.Contains(scene.Sent, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Death);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("unregistered")]
    [InlineData("missing-store")]
    public async Task EarthForceTransactionRequiresTheExactRegisteredRewardOwner(string ownership)
    {
        var scene = await RunEarthForceTransactionAsync(4, ownership: ownership);
        Assert.Null(scene.Failure);
        Assert.Null(scene.Committed);
        if (ownership == "stale") Assert.Same(scene.Replacement, scene.Current);
        else if (ownership == "unregistered") Assert.Null(scene.Current);
        else Assert.Same(scene.Original, scene.Current);
        AssertEarthForceTransactionOriginalAndFiles(scene);
        Assert.DoesNotContain(scene.Sent, IsEarthForceTransactionRewardFrame);
        if (ownership == "unregistered")
        {
            // The real ArrowBlades ingress refuses the absent connection before
            // death/reward dispatch. This is not Earth preflight coverage.
            Assert.Contains("WorldServer handled MsgAttack", scene.Log);
            Assert.DoesNotContain("map 3998 Earth Force reward", scene.Log);
            Assert.Empty(scene.Sent);
        }
        else Assert.Contains("session-registration-missing-or-stale", scene.Log);
    }

    [Fact]
    public async Task EarthForceTransactionLateMetadataRetirementRefusesWithoutDebitOrOutput()
    {
        var scene = await RunEarthForceTransactionAsync(4, "metadata-retire");
        Assert.Null(scene.Failure);
        Assert.True(scene.MetadataRetired);
        Assert.Null(scene.Committed);
        Assert.Same(scene.Replacement, scene.Current);
        Assert.Equal(scene.ReplacementBefore, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Current!.PlayerItems));
        Assert.Equal(77, scene.Current.Character.Money);
        AssertEarthForceTransactionOriginalAndFiles(scene);
        Assert.DoesNotContain(scene.Sent, IsEarthForceTransactionRewardFrame);
        Assert.Contains("session-changed-before-commit", scene.Log);
    }

    private static void AssertEarthForceTransactionUnchanged(EarthForceTransactionScene scene)
    {
        Assert.Null(scene.Failure);
        Assert.Same(scene.Original, scene.Current);
        Assert.Null(scene.Committed);
        AssertEarthForceTransactionOriginalAndFiles(scene);
        Assert.Empty(scene.PublicationOwners);
        Assert.DoesNotContain(scene.Sent, IsEarthForceTransactionRewardFrame);
        Assert.DoesNotContain(ParseNpcReplies(scene.Sent), reply =>
            reply.Text.Contains("received the Earth Force", StringComparison.Ordinal));
        Assert.DoesNotContain("converted=True", scene.Log);
    }

    private static void AssertEarthForceTransactionOriginalAndFiles(EarthForceTransactionScene scene)
    {
        Assert.Equal(scene.PhysicalBefore, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Original.PlayerItems));
        Assert.Equal(scene.ItemsBefore, scene.ItemsAfter);
        Assert.Equal(scene.UserBefore, scene.UserAfter);
        Assert.Equal(scene.AttributesBefore, scene.Original.Character.Attributes.ToArray());
        Assert.Equal(scene.RowReferences.Length, scene.Original.PlayerItems.InventoryItems.Count);
        for (var index = 0; index < scene.RowReferences.Length; index++)
            Assert.Same(scene.RowReferences[index], scene.Original.PlayerItems.InventoryItems[index]);
    }
}
