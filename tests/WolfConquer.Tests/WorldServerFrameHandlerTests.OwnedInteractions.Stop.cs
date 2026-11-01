using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private static Task OwnedLegacyInteractionStopAsync() => WithOwnedLegacyInteractionsAsync(
        CreateStaticVisibilityCatalog(items: new ItemType[] { }), async run =>
        {
            var before = run.Owner.Data.Files(); await PairOwnedLegacyInteraction(run, before);
            run.Observe = (_, _) =>
            {
                run.Owner.Data.AssertFiles(before); AssertOwnedLegacyInteractionState(run);
                Assert.Null(run.Current().InteractionPartnerUid); Assert.Null(run.Current(LegacyInteractionPeer).InteractionPartnerUid);
                Assert.Equal(0U, run.Current().ActiveInteractionDance); Assert.Equal(0U, run.Current(LegacyInteractionPeer).ActiveInteractionDance);
            };
            await run.Send(LegacyInteractionMover, LegacyInteractionPacket(MsgAttackType.InteractionStopEffect, LegacyInteractionMover, LegacyInteractionPeer, 0, 0));
            run.Observe = null;
            Assert.Equal(new[] { LegacyInteractionMover, LegacyInteractionPeer, LegacyInteractionMover, LegacyInteractionPeer }, run.Order.Select(row => row.Recipient));
            foreach (uint uid in run.Uids)
            {
                Assert.Equal(2, run.Frames(uid).Count);
                Assert.Equal(LegacyInteractionAttackGolden(50, LegacyInteractionMover, LegacyInteractionPeer, 0, 7), run.Frames(uid)[0]);
                Assert.Equal(LegacyInteractionAttackGolden(50, LegacyInteractionPeer, LegacyInteractionMover, 0, 7), run.Frames(uid)[1]);
                Assert.True(MsgAttackPacket.TryParse(ReadFrame(run.Frames(uid)[0]), out var stop));
                Assert.Equal(MsgAttackType.InteractionStopEffect, stop.Query.AttackType); Assert.Equal(7U, stop.Query.ResponseDamage);
                Assert.True(MsgAttackPacket.TryParse(ReadFrame(run.Frames(uid)[1]), out var swapped));
                Assert.Equal(LegacyInteractionMover, stop.Query.Uid); Assert.Equal(LegacyInteractionPeer, stop.Query.OpponentUid);
                Assert.Equal(LegacyInteractionPeer, swapped.Query.Uid); Assert.Equal(LegacyInteractionMover, swapped.Query.OpponentUid);
            }
            run.Clear(); var expected = OwnedLegacyInteractionMovedFiles(run, before, 831, 583, paired: false);
            run.Observe = (_, _) => { run.Owner.Data.AssertFiles(expected); AssertOwnedLegacyInteractionState(run); };
            await run.Send(LegacyInteractionMover, new MsgMovementPacket { Direction = 5, Uid = 0, Running = MsgMovementMode.Walk, Timestamp = 456 }.ToFrameBytes());
            run.Observe = null; run.Owner.Data.AssertFiles(expected);
            var movement = OwnedLegacyInteractionProto(2205, [(1, 5), (2, LegacyInteractionMover), (3, 0), (4, 456), (5, 1015)]);
            foreach (uint uid in run.Uids)
            {
                Assert.Equal(movement, Assert.Single(run.Frames(uid)));
                Assert.True(MsgMovementPacket.TryParse(ReadFrame(run.Frames(uid)[0]), out var parsed)); Assert.Equal(LegacyInteractionMover, parsed.Uid);
                Assert.False(LegacyInterActionWalkPacket.TryParse(ReadFrame(run.Frames(uid)[0]), out _));
            }
            Assert.Equal((831L, 583L), (run.Current().Character.X, run.Current().Character.Y));
            Assert.Equal((830L, 584L), (run.Current(LegacyInteractionPeer).Character.X, run.Current(LegacyInteractionPeer).Character.Y));
            Assert.Contains("WorldServer stopped MsgAttack interaction", run.Owner.Log.ToString());
            await AssertOwnedLegacyInteractionLocation(run, expected, 830, 584, 0);
        });

    private static async Task AssertOwnedLegacyInteractionLocation(OwnedLegacyInteractionContext run,
        Dictionary<string, byte[]> files, ushort x, ushort y, ushort facing)
    {
        run.Clear(); run.Observe = (_, _) => { run.Owner.Data.AssertFiles(files); AssertOwnedLegacyInteractionState(run); };
        await run.Send(LegacyInteractionPeer, new MsgActionPacket { ObjId = LegacyInteractionPeer, Timestamp = 789, Type = MsgActionType.SetLocation }.ToFrameBytes());
        run.Observe = null;
        Assert.True(MsgActionPacket.TryParse(ReadFrame(run.Frames(LegacyInteractionPeer)[0]), out var location));
        Assert.Equal((uint)x, location.PositionX); Assert.Equal((uint)y, location.PositionY); Assert.Equal(facing, location.Facing);
        var fields = new List<(int Field, ulong Value)> { (1, LegacyInteractionPeer), (3, 1015), (9, 789), (12, 74), (13, facing), (14, x), (15, y) };
        Assert.Equal(OwnedLegacyInteractionProto(2046, fields.ToArray()), run.Frames(LegacyInteractionPeer)[0]);
        run.Owner.Data.AssertFiles(files);
    }
}
