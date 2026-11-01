using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task ActualExistingLoginSeparatesAccountCharacterAndPlayerFileIdentities() => RunPhysicalDeathAsync(f =>
    {
        var session = f.TargetSession;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(session.NativeGameClient!.OrdinarySpellBinding);
        Assert.NotEqual(session.AccountId, session.Character.Uid);
        Assert.Equal((long)DeathTargetAccount, session.AccountId); Assert.Equal((long)DeathTargetUid, session.Character.Uid);
        Assert.Equal(DeathTargetUid, binding.Player.UID); Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(binding));
        var item = Assert.Single(session.PlayerItems.InventoryItems);
        Assert.Equal(DeathArmorUid, item.Uid); Assert.Equal(15U, item.MythSoul);
        Assert.Equal((byte)0xC7, File.ReadAllBytes(f.SpellsPath)[15]);
        Assert.False(File.Exists(Path.Combine(f.Owner.Data.Items, DeathTargetAccount + ".bin")));
        Assert.False(File.Exists(Path.Combine(f.Owner.Data.Spells, DeathTargetAccount + ".bin")));
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
        return Task.CompletedTask;
    });

    [Fact]
    public Task DuplicateSavedPlayerUidCannotBeAdoptedByAFreshExistingLogin() => RunPhysicalDeathAsync(async f =>
    {
        await f.Target.CompleteTransportCloseAsync();
        Assert.False(f.Owner.Store.TryGet(f.Target, out _));
        // Declared negative escaped disk edit only: no grant, fake live owner,
        // positive pre-created WAL or accepting callback is introduced.
        string duplicate = Path.Combine(f.Owner.Data.Users, (DeathTargetAccount + 1) + ".ini");
        File.Copy(f.CharacterPath, duplicate);
        var before = f.Owner.Data.Files(); var ledger = File.ReadAllBytes(f.Owner.Data.State);
        var next = f.Owner.NewConnection(DeathTargetAccount);
        f.Owner.Frames(DeathTargetAccount).Clear();

        await f.Owner.SendOnAsync(next, new LegacyMsgLoginGamePacket(DeathTargetAccount).ToFrameBytes());

        Assert.False(f.Owner.Store.TryGet(next, out _)); Assert.Empty(f.Owner.Frames(DeathTargetAccount));
        Assert.Contains("ordinaryAdmissionPreparation:InvalidDataException", f.Owner.Log.ToString(), StringComparison.Ordinal);
        f.Owner.Data.AssertFiles(before); Assert.Equal(ledger, File.ReadAllBytes(f.Owner.Data.State));
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
    });

    [Fact]
    public Task RenamingAnEscapedAccountFileCannotDuplicateAnAlreadyLivePlayer() => RunPhysicalDeathAsync(async f =>
    {
        var target = f.TargetSession; var binding = target.NativeGameClient!.OrdinarySpellBinding!;
        var original = File.ReadAllBytes(f.CharacterPath);
        uint otherAccount = DeathTargetAccount + 1;
        string foreign = Path.Combine(f.Owner.Data.Users, otherAccount + ".ini");
        // A negative disk escape leaves a unique on-disk mapping but must not
        // transfer the already live native player to a second account/session.
        File.WriteAllBytes(foreign, original); File.Delete(f.CharacterPath);
        try
        {
            var before = f.Owner.Data.Files(); var ledger = File.ReadAllBytes(f.Owner.Data.State);
            var next = f.Owner.NewConnection(otherAccount);
            await f.Owner.SendOnAsync(next, new LegacyMsgLoginGamePacket(otherAccount).ToFrameBytes());
            Assert.False(f.Owner.Store.TryGet(next, out _)); Assert.Empty(f.Owner.Frames(otherAccount));
            Assert.Same(target, f.TargetSession); Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Contains("key=" + otherAccount + " reason=ordinaryAdmissionStoreUnavailable", f.Owner.Log.ToString(), StringComparison.Ordinal);
            f.Owner.Data.AssertFiles(before); Assert.Equal(ledger, File.ReadAllBytes(f.Owner.Data.State));
            Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
        }
        finally
        {
            File.WriteAllBytes(f.CharacterPath, original); File.Delete(foreign);
        }
    });
}
