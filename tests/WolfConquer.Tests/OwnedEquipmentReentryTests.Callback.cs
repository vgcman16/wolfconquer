using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentReentryTests
{
    [Theory]
    [InlineData((ushort)17_490, 225U)]
    [InlineData((ushort)17_650, 168U)]
    public Task QueuedEquipKeepsOriginalRefineryBranchUntilAttackCompletes(ushort spellId, uint expectedDamage)
    {
        var inputs = LegacyMagicAttackRuntimeTests.OwnedOverlayReentryInputs(impregnation: false, spellId, callback: true);
        return OwnedEquipmentBehaviorFixture.RunAsync(inputs.Actor, inputs.World, async f =>
        {
            var observer = Actor + 50;
            var recipients = new List<uint>();
            using var producerCancellation = new CancellationTokenSource();
            var enqueue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var issued = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
            // This packet producer starts outside the active Frame execution
            // context. Its real dispatch waits behind the current Shared lease.
            var producer = Task.Run(async () =>
            {
                if (!await enqueue.Task) return;
                var actual = f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(
                    new MsgItemUsagePacket(90_002, 1, MsgItemUsageAction.Equip, 0, 0, 0, 0).ToFrameBytes()), producerCancellation.Token).AsTask();
                issued.TrySetResult(actual);
                await actual;
            });
            var scheduled = false;
            var entryCritical = 0u; var earnedBreakthrough = 0u; long targetHp = 0;
            var start = f.ActorFrames.Count;
            f.BeforeSend = (recipient, bytes) =>
            {
                if (!LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) || update.Uid != Actor ||
                    !update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.StatusFlag && row.Values is { } flags &&
                        flags.Count > 313 / 32 && (flags[313 / 32] & (1UL << (313 % 32))) != 0)) return;
                recipients.Add(recipient);
                if (recipient != Actor || scheduled) return;
                var current = f.Current();
                entryCritical = LegacyPlayerStatusCalculator.CalculateFields(current.Character, current.PlayerItems, inputs.World, 100_000, 0)
                    [LegacyPlayerStatusPacket.CriticalStrikeIndex];
                // The queued equipment change cannot alter this entered attack's
                // refinery decision: 10% critical versus zero immunity, roll 0,
                // and equal battle power enter the refinery branch. Rolls 99
                // fail both effects, so that branch returns before Dash.
                Assert.Equal(90_001u, Assert.Single(current.PlayerItems.EquipmentItems).Uid);
                Assert.Contains(current.PlayerItems.InventoryItems, row => row.Uid == 90_002u);
                var target = f.Current(Target);
                var targetFields = LegacyPlayerStatusCalculator.CalculateFields(target.Character, target.PlayerItems, inputs.World, 100_000, 0);
                Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.ImmunityIndex]);
                Assert.Equal("120", current.Character.Attributes["BattlePower"]);
                Assert.Equal("120", target.Character.Attributes["BattlePower"]);
                Assert.Equal("0", current.Character.Attributes["LegacyPhysicalRefineryRoll"]);
                Assert.Equal("99", current.Character.Attributes["LegacyPhysicalPlayerLuckyBranchRoll"]);
                Assert.Equal("99", current.Character.Attributes["LegacyPhysicalPlayerCriticalRoll"]);
                Assert.Equal("10000", current.Character.Attributes["DashRate"]);
                Assert.Equal("0", current.Character.Attributes["LegacyPhysicalDashRoll"]);
                earnedBreakthrough = LegacyPhysicalStatusOverlay.ForOwner(current).Breakthrough ?? 0;
                targetHp = f.Current(Target).Character.MinHitPoints;
                scheduled = true;
                Assert.True(enqueue.TrySetResult(true));
                Assert.True(issued.Task.Wait(TimeSpan.FromSeconds(5)), "Independent packet producer did not enter dispatch.");
                Assert.False(issued.Task.Result.IsCompleted, "The exclusive equip must wait for the entered Shared attack.");
            };
            Exception? outerFailure = null;
            try { await f.SendAsync(Actor, Attack(Actor, Target)); Assert.True(scheduled); }
            catch (Exception error) { outerFailure = error; }
            f.BeforeSend = null;
            enqueue.TrySetResult(false);
            Exception? producerFailure = null;
            if (outerFailure is not null) producerCancellation.Cancel();
            try { await producer.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException timeout)
            {
                // The real queued lease receives cancellation; do not dispose
                // the owner while that packet producer remains unjoined.
                producerCancellation.Cancel();
                try { await producer; }
                catch (OperationCanceledException) when (producerCancellation.IsCancellationRequested) { }
                catch (Exception cleanup) { producerFailure = new AggregateException(timeout, cleanup); }
                producerFailure ??= timeout;
            }
            catch (OperationCanceledException) when (outerFailure is not null && producerCancellation.IsCancellationRequested) { }
            catch (Exception error) { producerFailure = error; }
            if (outerFailure is not null && producerFailure is not null) throw new AggregateException(outerFailure, producerFailure);
            if (outerFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(outerFailure).Throw();
            if (producerFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(producerFailure).Throw();
            Assert.Equal(1_000U, entryCritical);
            Assert.Equal(66U, earnedBreakthrough);
            Assert.Equal(100_000, targetHp);
            Assert.Equal(3, recipients.Count);
            Assert.Equal(Actor, recipients[0]);
            Assert.Equal(new[] { Target, observer }.Order(), recipients.Skip(1).Order());
            var hit = Assert.Single(Animation(f.ActorFrames.Skip(start), spellId).Targets);
            Assert.Equal(Target, hit.Uid); Assert.Equal(expectedDamage, hit.Damage); Assert.Equal(MsgAttackEffect.None, hit.Effect);
            Assert.Equal(100_000 - (long)expectedDamage, f.Current(Target).Character.MinHitPoints);
            Assert.Equal(90_002u, Assert.Single(f.Current().PlayerItems.EquipmentItems).Uid);
            Assert.True(Flag(f.Current(), 313));
            var raw = LegacyPlayerStatusCalculator.CalculateFields(f.Current().Character, f.Current().PlayerItems, inputs.World, 100_000, 0);
            Assert.Equal(0U, raw[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
            Assert.Equal(60U, raw[LegacyPlayerStatusPacket.BreakthroughIndex]);
            var actorStatus = await Status(f, Actor);
            Assert.Equal(60U, actorStatus.Breakthrough); Assert.Equal(0U, actorStatus.CriticalStrike);
            Assert.Equal(75U, (await Status(f, Target)).Counteraction);
            Assert.DoesNotContain(f.ActorFrames.Skip(start), bytes => LegacyMsgRefineEffectPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _));
            Assert.DoesNotContain(f.ActorFrames.Skip(start), bytes => MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var ordinary) && ordinary.Query.AttackType == MsgAttackType.Physical);
        }, inputs.Target, inputs.Observer);
    }
}
