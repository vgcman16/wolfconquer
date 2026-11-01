using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public Task EarnedPendingExperienceSerializesRealPhysicalThenActualCompletionPermitsHit(bool pendingActor) =>
        RunEarnedPhysicalSerialization(async (f, monster, clock) =>
        {
            var reached = new TaskCompletionSource<LegacyExperienceContinuation>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellation = new CancellationTokenSource();
            Task? producer = null, queued = null;
            Exception? primary = null;
            int pauses = 0;
            try
            {
                f.BeforeSendAsync = async (uid, bytes) =>
                {
                    if (uid != EarnedActor || !bytes.AsSpan().SequenceEqual(Update(EarnedActor, LegacyMsgUpdateDataType.Level, 138)) ||
                        Interlocked.CompareExchange(ref pauses, 1, 0) != 0) return;
                    var current = f.Current(EarnedActor);
                    var pending = LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath(EarnedActor)),
                        current.AccountId, EarnedActor);
                    Assert.NotNull(pending);
                    Assert.Equal((ushort)138, pending.CurrentLevel); Assert.Equal((ushort)1, pending.StepIndex);
                    Assert.Equal(LegacyExperienceContinuationPhase.QueryPending, pending.Phase);
                    Assert.Equal(1UL, pending.ConsumedExperience); Assert.True(pending.CreditedExperience >= 2);
                    Assert.Equal(pending, LegacyExperienceContinuationCodec.ReadCharacter(current.Character, current.AccountId));
                    AssertEarnedFromHit(f.ActorFrames, monster, pending);
                    reached.SetResult(pending);
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                };

                producer = Hit(f, monster);
                var first = await Task.WhenAny(reached.Task, producer).WaitAsync(TimeSpan.FromSeconds(30));
                if (!ReferenceEquals(first, reached.Task))
                {
                    await producer;
                    throw new Xunit.Sdk.XunitException("The genuine monster hit never reached its earned level prefix.\n" + Diagnostic(f));
                }
                var pending = await reached.Task;
                Assert.Equal(1, pauses); Assert.False(producer.IsCompleted);
                AssertEarnedExclusiveQueue(f, 0);
                var before = CaptureEarnedPending(f);
                clock.Advance(); // Actual Frame clock, beyond the ordinary700ms cadence.
                uint requester = pendingActor ? EarnedActor : EarnedPeer;
                queued = f.Handler.HandleFrameAsync(f.Connection(requester),
                    OwnedEquipmentBehaviorFixture.ReadFrame(EarnedPhysicalRequest(pendingActor)), cancellation.Token).AsTask();

                Assert.False(queued.IsCompleted);
                AssertEarnedExclusiveQueue(f, 1);
                AssertEarnedPendingUnchanged(f, before);
                await Task.Yield();
                Assert.False(producer.IsCompleted); Assert.False(queued.IsCompleted);
                AssertEarnedExclusiveQueue(f, 1);
                AssertEarnedPendingUnchanged(f, before);

                release.SetResult();
                await producer.WaitAsync(TimeSpan.FromSeconds(30));
                await queued.WaitAsync(TimeSpan.FromSeconds(30));
                f.BeforeSendAsync = null;
                AssertEarnedCompletedPhysical(f, pendingActor, pending);
            }
            catch (Exception error) { primary = error; }

            // Always release and observe the real in-flight handlers before
            // fixture disposal. Cleanup cannot replace the original assertion.
            release.TrySetResult();
            if (primary is not null) cancellation.Cancel();
            var cleanup = new List<Exception>();
            foreach (var task in new[] { producer, queued })
                if (task is not null)
                    try { await task.WaitAsync(TimeSpan.FromSeconds(30)); }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                    catch (Exception error) { if (!ReferenceEquals(error, primary)) cleanup.Add(error); }
            f.BeforeSendAsync = null;
            if (primary is not null && cleanup.Count != 0) throw new AggregateException(new[] { primary }.Concat(cleanup));
            if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
            if (cleanup.Count == 1) ExceptionDispatchInfo.Capture(cleanup[0]).Throw();
            if (cleanup.Count > 1) throw new AggregateException(cleanup);
        });

    private static void AssertEarnedCompletedPhysical(OwnedEquipmentBehaviorFixture f, bool pendingActor,
        LegacyExperienceContinuation pending)
    {
        Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        var actor = f.Current(EarnedActor); var peer = f.Current(EarnedPeer);
        var actorBinding = Assert.IsType<LegacyOrdinarySpellBinding>(actor.NativeGameClient!.OrdinarySpellBinding);
        var peerBinding = Assert.IsType<LegacyOrdinarySpellBinding>(peer.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(actorBinding)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(peerBinding));
        Assert.Equal(139L, actor.Character.Level); Assert.Equal((ushort)139, actorBinding.Player.Level);
        Assert.Equal(18L, actor.Character.AttributePoints); Assert.Equal((ushort)18, actorBinding.Player.Atributes);
        ulong finalExperience = pending.InitialExperience + pending.CreditedExperience - 2;
        Assert.Equal(checked((long)finalExperience), actor.Character.Experience);
        Assert.Equal(finalExperience, actorBinding.Player.Experience);
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacter(actor.Character, actor.AccountId));
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath(EarnedActor)),
            actor.AccountId, EarnedActor));

        // Real100-strength, zero-stat sword, no gems/defense, same final level
        // and class:100 HP damage. Interaction scales only display floor(100*22/27)=81.
        int actorHp = pendingActor ? 11_028 : 10_928;
        int peerHp = pendingActor ? 1_900 : 2_000;
        Assert.Equal(actorHp, actorBinding.Player.HitPoints); Assert.Equal((long)actorHp, actor.Character.MinHitPoints);
        Assert.Equal(peerHp, peerBinding.Player.HitPoints); Assert.Equal((long)peerHp, peer.Character.MinHitPoints);
        uint requester = pendingActor ? EarnedActor : EarnedPeer;
        uint victim = pendingActor ? EarnedPeer : EarnedActor;
        foreach (uint uid in new[] { EarnedActor, EarnedPeer })
        {
            var replies = f.Frames(uid).Select(OwnedEquipmentBehaviorFixture.ReadFrame)
                .Select(frame => MsgAttackPacket.TryParse(frame, out var parsed) ? parsed : (MsgAttackPacket?)null)
                .Where(packet => packet is { } value && value.Query.Uid == requester && value.Query.OpponentUid == victim &&
                    value.Query.AttackType == MsgAttackType.Physical).Select(packet => packet!.Value).ToArray();
            var reply = Assert.Single(replies);
            Assert.Equal(81, reply.Query.Damage); Assert.Equal(MsgAttackEffect.None, reply.Query.Effect);
            var session = f.Current(uid); var binding = Assert.IsType<LegacyOrdinarySpellBinding>(session.NativeGameClient!.OrdinarySpellBinding);
            Assert.Equal(0U, binding.Player.Mana); Assert.Equal((ushort)100, binding.Player.Stamina);
            var savedBook = new PlayerProficiencySnapshotLoader(f.Data.Proficiencies).LoadByCharacterUid(uid);
            Assert.Equal(PlayerProficiencySnapshotLoader.ToLegacyBytes(session.PlayerProficiencies),
                PlayerProficiencySnapshotLoader.ToLegacyBytes(savedBook));
            Assert.Equal(session.PlayerProficiencies.Proficiencies.Count, binding.Proficiencies.ClientProf.Count);
            foreach (var row in session.PlayerProficiencies.Proficiencies)
            {
                Assert.True(binding.Proficiencies.ClientProf.TryGetValue(row.Id, out var native));
                Assert.Equal(row.Level, native.Level); Assert.Equal(row.Experience, native.Experience);
                Assert.Equal(row.NeededExperience, native.NeededExperience); Assert.Equal(row.PreviousLevel, native.PreviouseLevel);
            }
        }
        var attackIndex = f.ActorFrames.FindIndex(bytes => MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var parsed) &&
            parsed.Query.Uid == requester && parsed.Query.OpponentUid == victim && parsed.Query.AttackType == MsgAttackType.Physical);
        int completionIndex = f.ActorFrames.FindIndex(bytes => bytes.AsSpan().SequenceEqual(
            Update(EarnedActor, LegacyMsgUpdateDataType.Experience, finalExperience)));
        Assert.True(completionIndex >= 0 && attackIndex > completionIndex, Diagnostic(f));
    }
}
