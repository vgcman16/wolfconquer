using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class ClassicTargetAdmissionOwnedTests
{
    [Theory]
    [InlineData((ushort)1075)] [InlineData((ushort)1085)] [InlineData((ushort)1090)] [InlineData((ushort)1095)]
    public Task UnknownTargetPreservesActualOwnedResourcesFilesAndOutputs(ushort id) => F.ClassicTargetRunAsync(id, async (f, monster) =>
    {
        var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files(); Owner(f);
        await Cast(f, id, 799999); Rejected(f, actor, peer, files);
        Assert.Contains("classicTargetUnavailable", f.Log.ToString(), StringComparison.Ordinal);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task HiddenPeerOrActuallyDeadMonsterCannotSpendOrPublish(bool deadMonster) => F.ClassicTargetRunAsync(1075, async (f, monster) =>
    {
        if (deadMonster) Assert.True(States(f).ApplyDamage(monster, monster.MaxLife, DateTimeOffset.UtcNow).Killed);
        var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files();
        await Cast(f, 1075, deadMonster ? monster.Uid : Peer); Rejected(f, actor, peer, files);
        Assert.Contains("classicTargetUnavailable", f.Log.ToString(), StringComparison.Ordinal);
    }, hiddenPeer: !deadMonster);

    [Fact]
    public Task ARealCatalogTargetWithoutItsRequiredMonsterOwnerDoesNotSpend() => F.ClassicTargetRunAsync(1075, async (f, monster) =>
    {
        var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files();
        await F.ClassicTargetWithoutMonsterOwnerAsync(f, 1075, monster.Uid); Rejected(f, actor, peer, files);
        Assert.Contains("classicMonsterOwnerUnavailable", f.Log.ToString(), StringComparison.Ordinal);
    });

    [Fact]
    public Task ComponentOriginalPeerRecordCannotAuthorizeCostAfterALegitimateReplacement() => F.ClassicTargetRunAsync(1075, (f, monster) =>
    {
        var actor = f.Current(); var original = f.Current(Peer);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(f.Store, f.Connection(Actor), Actor, out var actorIdentity));
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(f.Store, f.Connection(Peer), Peer, out var peerIdentity));
        f.Store.Set(f.Connection(Peer), original with { Character = original.Character with
        { Attributes = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Stamina"] = "70" } } });
        var newer = f.Current(Peer); var files = f.Data.Files();
        var candidate = actor with { Character = actor.Character with { MinMana = 493,
            Attributes = new Dictionary<string, string>(actor.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Stamina"] = "96" } } };
        Assert.False(f.Store.TryCommitClassicBuffCosts(f.Connection(Actor), actor, actorIdentity, candidate,
            new WorldSessionStore.ProcParticipant(f.Connection(Peer), original, peerIdentity), null, out var receipt, out var reason));
        Assert.Null(receipt); Assert.Equal("classicTargetOwnerChanged", reason); Rejected(f, actor, newer, files);
        return Task.CompletedTask;
    });

    [WindowsTradeJournalFailureFact]
    public Task GenuineCharacterWriterRefusalKeepsBothOwnersAndThenRetrySucceeds() => F.ClassicTargetRunAsync(1075, async (f, monster) =>
    {
        var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files();
        using (var held = new FileStream(f.CharacterPath(), FileMode.Open, FileAccess.Read, FileShare.Read))
        { await Cast(f, 1075, Peer); Rejected(f, actor, peer, files); }
        await Cast(f, 1075, Peer); Owner(f);
        Assert.Equal(493L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
        Assert.True(Flag(f.Current(Peer).Character, 0x16)); Assert.Single(Animations(f));
    });
}
