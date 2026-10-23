using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Theory]
    [MemberData(nameof(NatureForceInvalidMetadataCases))]
    public async Task NatureForcePreparationRejectsInvalidRequiredMetadataAndUidWithoutPrefix(string kind, string fault)
    {
        var scene = await RunNatureForceRewardAsync(kind, fault: fault);
        AssertNatureForceUnchanged(scene);
        Assert.Contains(fault == "missing" ? "itemtype.txt" :
            fault == "uid" ? "nature-force-preparation-overflow" : "nature-force-metadata-invalid", scene.Log);
        Assert.Contains(scene.Sent, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Death);
    }

    [Theory]
    [InlineData("rune")]
    [InlineData("star")]
    [InlineData("stone")]
    public async Task NatureForceLoadedStackFrameOverflowCannotCommitAnUnpublishableGrant(string kind)
    {
        var scene = await RunNatureForceRewardAsync(kind, existing: 1, fault: "frame");
        AssertNatureForceUnchanged(scene);
        Assert.Equal(256U, scene.Original.PlayerItems.InventoryItems[1].Effect);
        Assert.Equal((ushort)1, scene.Current.PlayerItems.InventoryItems[1].StackSize);
        Assert.Contains("nature-force-frame-invalid", scene.Log);
    }

    [Theory]
    [MemberData(nameof(NatureForceRewardKinds))]
    public async Task NatureForceStaleSameUidRewardOwnerCannotOverwriteTheCurrentSession(string kind)
    {
        var scene = await RunNatureForceRewardAsync(kind, ownership: "stale");
        Assert.Null(scene.Failure);
        Assert.Null(scene.Committed);
        Assert.Same(scene.Replacement, scene.Current);
        Assert.Equal(77, scene.Current.Character.Money);
        AssertNatureForceOriginalAndFiles(scene);
        Assert.DoesNotContain(scene.Sent, bytes => IsNatureForceItemFrame(bytes, scene.Spec.Item));
        Assert.Contains("nature-force-session-registration-missing-or-stale", scene.Log);
    }

    [Theory]
    [MemberData(nameof(NatureForceRewardKinds))]
    public async Task NatureForceMetadataRetirementBeforeCasRefusesWithoutRewardOutput(string kind)
    {
        var scene = await RunNatureForceRewardAsync(kind, fault: "retire");
        Assert.Null(scene.Failure);
        Assert.True(scene.Retired);
        Assert.Null(scene.Committed);
        Assert.Same(scene.Replacement, scene.Current);
        Assert.Equal(77, scene.Current.Character.Money);
        Assert.Equal(scene.PhysicalBefore, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Current.PlayerItems));
        AssertNatureForceOriginalAndFiles(scene);
        Assert.Empty(scene.PublicationOwners);
        Assert.DoesNotContain(scene.Sent, bytes => IsNatureForceItemFrame(bytes, scene.Spec.Item));
        Assert.Contains("nature-force-session-changed-before-commit", scene.Log);
    }

    private static void AssertNatureForceUnchanged(NatureForceScene scene)
    {
        Assert.Null(scene.Failure);
        Assert.Null(scene.Committed);
        Assert.Same(scene.Original, scene.Current);
        AssertNatureForceOriginalAndFiles(scene);
        Assert.Empty(scene.PublicationOwners);
        Assert.DoesNotContain(scene.Sent, bytes => IsNatureForceItemFrame(bytes, scene.Spec.Item));
        Assert.DoesNotContain(ParseNpcReplies(scene.Sent), reply =>
            reply.Text.Contains("You received", StringComparison.Ordinal));
    }

    private static void AssertNatureForceOriginalAndFiles(NatureForceScene scene)
    {
        Assert.Equal(scene.PhysicalBefore, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Original.PlayerItems));
        Assert.Equal(scene.UserBefore, scene.UserAfter);
        Assert.Equal(scene.ItemsBefore, scene.ItemsAfter);
        Assert.Equal(scene.AttributesBefore, scene.Original.Character.Attributes.ToArray());
        Assert.Equal(scene.RowsBefore.Length, scene.Original.PlayerItems.InventoryItems.Count);
        for (var index = 0; index < scene.RowsBefore.Length; index++)
            Assert.Same(scene.RowsBefore[index], scene.Original.PlayerItems.InventoryItems[index]);
    }
}
