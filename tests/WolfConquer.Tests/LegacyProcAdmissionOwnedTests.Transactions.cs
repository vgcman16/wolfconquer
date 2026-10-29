using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static async Task<bool> InvokeOwnedProc(OwnedEquipmentBehaviorFixture f, WorldDataCatalog world,
        ManualRuntimeTimeProvider clock, ushort proc)
    {
        var uid = proc == 10950 ? PlayerUid : TargetPlayerUid; var owner = f.Current(uid);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(owner.PlayerSpells, world, proc, out _, out var spell));
        return proc == 10950
            ? await InvokeProcAdmission("TryAdmitLegacyStoneCrackerStarFlowSpellAsync",
                [ProcContext(f, world, clock, uid), owner, spell, CancellationToken.None], 2)
            : await InvokeProcAdmission("TryAdmitLegacyCombatGearNestedPlayerDamageProcAsync",
                [ProcContext(f, world, clock, uid), f.Connection(PlayerUid), f.Current(PlayerUid), owner, spell, CancellationToken.None], 3);
    }
    private static void AssertOwnedProcMana(OwnedEquipmentBehaviorFixture f, uint uid, uint mana)
    {
        Assert.Equal((long)mana, f.Current(uid).Character.MinMana);
        Assert.Equal(mana, f.Current(uid).NativePlayer!.Mana);
        Assert.Equal((long)mana, f.Data.Loader.LoadByAccountId(uid)!.MinMana);
    }

    [Theory]
    [InlineData((ushort)10950)] [InlineData((ushort)20220)]
    public Task OwnedProcCommitsActualSavedStoreAndNativeManaBeforeItsFirstSuccessfulCostOutput(ushort proc) =>
        RunOwnedProcAsync(proc, 100, async (f, world, clock) =>
        {
            var uid = proc == 10950 ? PlayerUid : TargetPlayerUid; var before = f.Current(uid);
            var filesBefore = ProcFiles(f);
            var expectedImage = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(File.ReadAllBytes(f.CharacterPath(uid)),
                before.Character with { MinMana = 50 });
            var binding = before.NativeGameClient!.OrdinarySpellBinding; int start = f.Frames(uid).Count; bool observed = false;
            f.BeforeSend = (recipient, bytes) =>
            {
                if (recipient != uid || !LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) ||
                    !update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina)) return;
                observed = true; AssertOwnedProcMana(f, uid, 50);
                Assert.Equal(expectedImage, File.ReadAllBytes(f.CharacterPath(uid)));
                Assert.Equal("90", f.Current(uid).Character.Attributes["Stamina"]);
                Assert.Equal(clock.GetUtcNow().AddSeconds(1), f.Current(uid).MagicCooldowns![proc]);
                Assert.Same(binding, f.Current(uid).NativeGameClient!.OrdinarySpellBinding); Assert.False(binding!.IsRetired);
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            };
            Assert.True(await InvokeOwnedProc(f, world, clock, proc)); f.BeforeSend = null; Assert.True(observed);
            var updates = f.Frames(uid).Skip(start).Select(OwnedEquipmentBehaviorFixture.ReadFrame)
                .Where(frame => LegacyMsgUpdatePacket.TryParse(frame, out _))
                .Select(frame => { Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var parsed)); return Assert.Single(parsed.Updates); }).ToArray();
            Assert.Equal(proc == 10950 ? new[] { LegacyMsgUpdateDataType.Stamina, LegacyMsgUpdateDataType.Mana } :
                new[] { LegacyMsgUpdateDataType.Stamina }, updates.Select(entry => entry.Type));
            Assert.Equal(90UL, updates[0].Value); if (proc == 10950) Assert.Equal(50UL, updates[1].Value);
            AssertOwnedProcMana(f, uid, 50); Assert.False(f.Recovery.IsBlocked);
            Assert.Equal(expectedImage, File.ReadAllBytes(f.CharacterPath(uid)));
            foreach (var file in filesBefore.Where(file => file.Key != f.CharacterPath(uid)))
                Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            Assert.Equal(before.PlayerItems, f.Current(uid).PlayerItems);
            Assert.Equal(before.Character.Money, f.Current(uid).Character.Money);
            Assert.Equal(before.Character.ConquerPoints, f.Current(uid).Character.ConquerPoints);
            var files = ProcFiles(f); var committed = f.Current(uid); int sent = f.Frames(uid).Count;
            Assert.False(await InvokeOwnedProc(f, world, clock, proc)); // Real natural cooldown, no extra debit/output.
            Assert.Same(committed, f.Current(uid)); Assert.Equal(sent, f.Frames(uid).Count); ProcFilesUnchanged(f, files);
        });

    [WindowsTradeJournalFailureFact]
    public async Task OwnedProcActualCharacterReplacementRefusalPreservesEveryPreparedResourceAndOutput()
    {
        foreach (var proc in new ushort[] { 10950, 20220 })
            await RunOwnedProcAsync(proc, 100, async (f, world, clock) =>
            {
                var uid = proc == 10950 ? PlayerUid : TargetPlayerUid; var before = f.Current(uid);
                var files = ProcFiles(f); int sent = f.Frames(uid).Count;
                using (var held = new FileStream(f.CharacterPath(uid), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Assert.False(await InvokeOwnedProc(f, world, clock, proc));
                    Assert.Same(before, f.Current(uid)); AssertOwnedProcMana(f, uid, 100);
                    Assert.Equal("100", f.Current(uid).Character.Attributes["Stamina"]); Assert.Null(f.Current(uid).MagicCooldowns);
                    Assert.Equal(sent, f.Frames(uid).Count); ProcFilesUnchanged(f, files); Assert.False(f.Recovery.IsBlocked);
                }
                Assert.True(await InvokeOwnedProc(f, world, clock, proc)); AssertOwnedProcMana(f, uid, 50);
            });
    }

    [Fact]
    public Task OwnedProcObservedPeerRaceRefusesBeforeTheRealWriterOrAnyOwnerPublication() =>
        RunOwnedProcAsync(20220, 100, (f, _, _) =>
        {
            var owner = f.Current(TargetPlayerUid); var peer = f.Current(PlayerUid);
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(f.Store, f.Connection(TargetPlayerUid), TargetPlayerUid, out var ownerIdentity));
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(f.Store, f.Connection(PlayerUid), PlayerUid, out var peerIdentity));
            f.Store.Set(f.Connection(PlayerUid), peer with { ActiveNpcId = 17 }); // Genuine same-generation competing record.
            var currentPeer = f.Current(PlayerUid); var files = ProcFiles(f); int sent = f.Frames(TargetPlayerUid).Count;
            Assert.False(f.Store.TryCommitProc(new(f.Connection(TargetPlayerUid), owner, ownerIdentity),
                owner with { Character = owner.Character with { MinMana = 50 } },
                new WorldSessionStore.ProcParticipant(f.Connection(PlayerUid), peer, peerIdentity), out var receipt));
            Assert.Null(receipt); Assert.Same(owner, f.Current(TargetPlayerUid)); Assert.Same(currentPeer, f.Current(PlayerUid));
            AssertOwnedProcMana(f, TargetPlayerUid, 100); ProcFilesUnchanged(f, files);
            Assert.Equal(sent, f.Frames(TargetPlayerUid).Count); return Task.CompletedTask;
        });

    [Fact]
    public Task OwnedProcSameIdentityStoreMutationDuringStaminaStopsStaleManaAndFencesCommittedIssuance() =>
        RunOwnedProcAsync(10950, 100, async (f, world, clock) =>
        {
            var before = f.Current(PlayerUid); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            int start = f.Frames(PlayerUid).Count; bool changed = false;
            f.BeforeSend = (uid, bytes) =>
            {
                if (changed || uid != PlayerUid || !LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) ||
                    !update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina)) return;
                changed = true; AssertOwnedProcMana(f, PlayerUid, 50);
                var committed = f.Current(PlayerUid);
                f.Store.Set(f.Connection(PlayerUid), committed with { Character = committed.Character with { MinMana = 88 } });
                AssertOwnedProcMana(f, PlayerUid, 88); Assert.Same(binding, f.Current(PlayerUid).NativeGameClient!.OrdinarySpellBinding);
            };
            Assert.True(await InvokeOwnedProc(f, world, clock, 10950)); f.BeforeSend = null;
            Assert.True(changed); Assert.True(binding.IsRetired); Assert.True(f.Recovery.IsBlocked);
            Assert.False(f.Store.TryGet(f.Connection(PlayerUid), out _));
            Assert.Equal(88L, f.Data.Loader.LoadByAccountId(PlayerUid)!.MinMana); Assert.Equal(88U, before.NativePlayer!.Mana);
            var frames = f.Frames(PlayerUid).Skip(start).ToArray(); Assert.Single(frames);
            Assert.True(LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(frames[0]), out var only));
            Assert.Equal(LegacyMsgUpdateDataType.Stamina, Assert.Single(only.Updates).Type);
            Assert.ThrowsAny<InvalidOperationException>(() => f.Store.Set(f.Connection(PlayerUid), before));
        });

    [Fact]
    public Task OwnedProcRealTransportFailureRetainsCommittedFileAndFencesWithoutRefundOrRetry() =>
        RunOwnedProcAsync(10950, 100, async (f, world, clock) =>
        {
            var before = f.Current(PlayerUid); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            var fault = new IOException("owned-proc-real-send-route-failure"); int calls = 0;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != PlayerUid || !LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) ||
                    !update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina)) return;
                calls++; AssertOwnedProcMana(f, PlayerUid, 50); throw fault;
            };
            var error = await Assert.ThrowsAsync<WorldSessionPublicationException>(() => InvokeOwnedProc(f, world, clock, 10950));
            f.BeforeSend = null;
            Assert.Same(fault, error.InnerException); Assert.Equal(WorldSessionPublicationStage.ProcContinuation, error.Stage);
            Assert.Equal(1, calls); Assert.Equal(50L, error.PublishedSession.Character.MinMana);
            Assert.Equal("90", error.PublishedSession.Character.Attributes["Stamina"]);
            Assert.Equal(clock.GetUtcNow().AddSeconds(1), error.PublishedSession.MagicCooldowns![10950]);
            Assert.Equal(50L, f.Data.Loader.LoadByAccountId(PlayerUid)!.MinMana); Assert.Equal(50U, before.NativePlayer!.Mana);
            Assert.True(binding.IsRetired); Assert.True(f.Recovery.IsBlocked);
            Assert.False(f.Store.TryGet(f.Connection(PlayerUid), out _));
            Assert.ThrowsAny<InvalidOperationException>(() => f.Store.Set(f.Connection(PlayerUid), before));
        });

    [Theory]
    [InlineData("dead-owner")] [InlineData("dead-target")] [InlineData("out-of-view")] [InlineData("capture-denial")]
    public Task OwnedNestedRequiredTargetAndAliveGatesRefuseBeforeAnyCostOrCooldown(string gate) =>
        RunOwnedProcAsync(20220, 100, async (f, world, clock) =>
        {
            if (gate == "dead-owner")
            {
                var owner = f.Current(TargetPlayerUid);
                f.Store.Set(f.Connection(TargetPlayerUid), owner with { Character = owner.Character with { MinHitPoints = 0 } });
            }
            else if (gate == "capture-denial")
            {
                var owner = f.Current(TargetPlayerUid);
                f.Store.Set(f.Connection(TargetPlayerUid), owner with { Character = owner.Character with
                { Attributes = new Dictionary<string, string>(owner.Character.Attributes) { ["PkMode"] = "3" } } });
            }
            else
            {
                var target = f.Current(PlayerUid);
                f.Store.Set(f.Connection(PlayerUid), target with { Character = gate == "dead-target"
                    ? target.Character with { MinHitPoints = 0 } : target.Character with { X = 1000 } });
            }
            var before = f.Current(TargetPlayerUid); var peer = f.Current(PlayerUid); var files = ProcFiles(f);
            int count = f.Frames(TargetPlayerUid).Count;
            Assert.False(await InvokeOwnedProc(f, world, clock, 20220));
            Assert.Same(before, f.Current(TargetPlayerUid)); Assert.Same(peer, f.Current(PlayerUid));
            AssertOwnedProcMana(f, TargetPlayerUid, 100); Assert.Equal("100", f.Current(TargetPlayerUid).Character.Attributes["Stamina"]);
            Assert.Null(f.Current(TargetPlayerUid).MagicCooldowns); Assert.Equal(count, f.Frames(TargetPlayerUid).Count);
            ProcFilesUnchanged(f, files); Assert.False(f.Recovery.IsBlocked);
        });
}
