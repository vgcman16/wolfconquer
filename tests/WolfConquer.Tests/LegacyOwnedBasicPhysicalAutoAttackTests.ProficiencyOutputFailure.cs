using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ProficiencyOutputFailureKeepsOneRealJointCommitAcrossRestart(bool existing) =>
        RunProficiencyAsync(existing, async f =>
        {
            var before = f.Files(); var after = HpImage(f, before, 2000, 1700);
            string profKey = Path.GetRelativePath(f.Owner.Data.Root, ProficiencyPath(f));
            byte[] profAfter = ProficiencyAfterImage(File.ReadAllBytes(ProficiencyPath(f)), existing);
            after[profKey] = profAfter;
            var bookAfter = PlayerProficiencySnapshotLoader.Parse(profAfter);
            var actor = f.Binding(Actor); var target = f.Binding(Target);
            byte[] ledger = File.ReadAllBytes(f.Owner.Data.State);
            int seen = 0;
            Exception? observationFailure = null;
            f.BeforeSend = (recipient, bytes) =>
            {
                try
                {
                    // The entire committed pair and canonical native/holder state
                    // must already exist before the very first interaction frame.
                    f.AssertFiles(after); f.AssertCoherent(1700); AssertProficiencyMap(f, bookAfter);
                    Assert.False(actor.Client.OnAutoAttack);
                    if (!LegacyProficiencyPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
                    Assert.Equal(Actor, recipient); Assert.Equal(ProficiencyGolden(existing), bytes);
                    Assert.Equal(4, f.Order.Count); seen++;
                }
                catch (Exception error) { observationFailure ??= error; throw; }
                // Keep expected transport loss separate from observation errors.
                throw new SocketException((int)SocketError.ConnectionReset);
            };
            try
            {
                await f.Owner.Handler.HandleFrameAsync(f.Owner.Connection(Actor),
                    OwnedEquipmentBehaviorFixture.ReadFrame(Request().ToFrameBytes()));
            }
            finally
            {
                f.BeforeSend = null;
                if (observationFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
            }
            Assert.Equal(1, seen); f.AssertFiles(after);
            Assert.Equal(4, f.Order.Count);
            Assert.Equal(new[] { Actor, Target, Target, Target }, f.Order.Select(row => row.Recipient));
            Assert.Equal(InteractionGolden(), f.Order[0].Bytes); Assert.Equal(InteractionGolden(), f.Order[1].Bytes);
            Assert.Equal(MaximumGolden(Target), f.Order[2].Bytes); Assert.Equal(HpGolden(Target, 1700), f.Order[3].Bytes);
            Assert.True(f.Owner.Recovery.IsBlocked);
            Assert.False(f.Owner.Store.TryGet(f.Owner.Connection(Actor), out _));
            Assert.False(f.Owner.Store.TryGet(f.Owner.Connection(Target), out _));
            Assert.True(actor.IsRetired); Assert.True(target.IsRetired); Assert.False(actor.Client.OnAutoAttack);
            Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
            Assert.Equal(ledger, File.ReadAllBytes(f.Owner.Data.State));
            int outputs = f.Order.Count; f.Clock.Advance(OrdinaryDelay * 5); await f.TickAsync();
            f.AssertFiles(after); Assert.Equal(outputs, f.Order.Count);
            await f.Owner.Runtime.DisposeAsync();
            for (int restart = 0; restart < 2; restart++)
            {
                Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(f.Owner.Data.Loader, f.Owner.Data.Options(),
                    out var candidate, out var reason), reason);
                await using var recovered = Assert.IsType<LegacyOwnedAdmissionRuntime>(candidate);
                f.AssertFiles(after); Assert.Equal(ledger, File.ReadAllBytes(f.Owner.Data.State));
                Assert.Equal(outputs, f.Order.Count); Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
            }
        });
}
