using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData((ushort)10950, "-1")]
    [InlineData((ushort)10950, "not-a-number")]
    [InlineData((ushort)10950, "18446744073709551616")]
    [InlineData((ushort)10950, "")]
    [InlineData((ushort)20220, "-1")]
    [InlineData((ushort)20220, "not-a-number")]
    [InlineData((ushort)20220, "18446744073709551616")]
    [InlineData((ushort)20220, "")]
    public Task OwnedProcExplicitInvalidSavedStaminaCannotBecomeDefaultAdmissionResources(ushort proc, string rawStamina) =>
        RunOwnedProcAsync(proc, 100, async (f, world, clock) =>
        {
            var uid = proc == 10950 ? PlayerUid : TargetPlayerUid;
            var peerUid = proc == 10950 ? TargetPlayerUid : PlayerUid;
            var owner = f.Current(uid); var peer = f.Current(peerUid);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(owner.NativeGameClient!.OrdinarySpellBinding);
            var peerBinding = Assert.IsType<LegacyOrdinarySpellBinding>(peer.NativeGameClient!.OrdinarySpellBinding);
            // Observe the REAL admitted input, not an injected post-admission
            // status map or a synthetic owner which defaults before the action.
            foreach (var participant in new[] { owner, peer })
            {
                Assert.True(participant.Character.Attributes.ContainsKey("Stamina"));
                Assert.Equal(rawStamina, participant.Character.Attributes["Stamina"]);
                Assert.Equal(rawStamina, f.Data.Loader.LoadByAccountId(participant.AccountId)!.Attributes["Stamina"]);
                Assert.Null(participant.MagicCooldowns);
            }
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(peerBinding));
            Assert.False(f.Recovery.IsBlocked);
            AssertOwnedProcMana(f, uid, 100); AssertOwnedProcMana(f, peerUid, 100);
            var files = ProcFiles(f); var ownerFrames = f.Frames(uid).Count; var peerFrames = f.Frames(peerUid).Count;

            Assert.False(await InvokeOwnedProc(f, world, clock, proc));

            Assert.Same(owner, f.Current(uid)); Assert.Same(peer, f.Current(peerUid));
            Assert.Equal(rawStamina, f.Current(uid).Character.Attributes["Stamina"]);
            Assert.Equal(rawStamina, f.Current(peerUid).Character.Attributes["Stamina"]);
            AssertOwnedProcMana(f, uid, 100); AssertOwnedProcMana(f, peerUid, 100);
            Assert.Null(f.Current(uid).MagicCooldowns); Assert.Null(f.Current(peerUid).MagicCooldowns);
            Assert.Equal(ownerFrames, f.Frames(uid).Count); Assert.Equal(peerFrames, f.Frames(peerUid).Count);
            Assert.Same(binding, f.Current(uid).NativeGameClient!.OrdinarySpellBinding);
            Assert.Same(peerBinding, f.Current(peerUid).NativeGameClient!.OrdinarySpellBinding);
            Assert.False(binding.IsRetired); Assert.False(peerBinding.IsRetired);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(peerBinding));
            ProcFilesUnchanged(f, files);
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        }, rawStamina: rawStamina);
}
