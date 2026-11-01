using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private static Task OwnedLegacyInteractionJumpAsync(bool watcher, bool gate)
    {
        var catalog = CreateStaticVisibilityCatalog(items: new ItemType[] { },
            sobNpcs: gate ? new SobNpc[] { CreateSobNpc(25005, "Gate", 21, 1137, 1015, 840, 590) } : new SobNpc[] { });
        return WithOwnedLegacyInteractionsAsync(catalog, async run =>
        {
            var before = run.Owner.Data.Files(); await PairOwnedLegacyInteraction(run, before);
            var expected = OwnedLegacyInteractionMovedFiles(run, before, 840, 590);
            run.Observe = (_, _) => { run.Owner.Data.AssertFiles(expected); AssertOwnedLegacyInteractionState(run); };
            await run.Send(LegacyInteractionMover, new MsgActionPacket { ObjId = LegacyInteractionMover, Timestamp = 987,
                Type = MsgActionType.Jump, Facing = 6, TargetPositionX = 840, TargetPositionY = 590 }.ToFrameBytes());
            run.Observe = null; run.Owner.Data.AssertFiles(expected); AssertOwnedLegacyInteractionState(run);
            foreach (uint uid in run.Uids)
            {
                Assert.Equal(gate ? 2 : 1, run.Frames(uid).Count);
                Assert.Equal(OwnedLegacyInteractionWalkGolden(jump: true), run.Frames(uid)[0]);
                Assert.True(LegacyInterActionWalkPacket.TryParse(ReadFrame(run.Frames(uid)[0]), out var packet));
                Assert.Equal(LegacyInterActionWalkMode.Jump, packet.Mode); Assert.Equal((ushort)840, packet.X); Assert.Equal((ushort)590, packet.Y);
                Assert.Equal(2U, packet.Count); Assert.Equal(LegacyInteractionMover, packet.Uid); Assert.Equal(LegacyInteractionPeer, packet.OpponentUid);
                if (gate)
                {
                    Assert.Equal(OwnedLegacyInteractionGateGolden(jump: true), run.Frames(uid)[1]);
                    Assert.True(LegacySobNpcSpawnPacket.TryParse(ReadFrame(run.Frames(uid)[1]), out var spawn)); Assert.Equal(25005U, spawn.Uid);
                }
            }
            foreach (uint uid in new[] { LegacyInteractionMover, LegacyInteractionPeer })
            {
                var current = run.Current(uid); Assert.Equal((840L, 590L), (current.Character.X, current.Character.Y));
                Assert.Equal("6", current.Character.Attributes["Angle"]); Assert.Equal(100U, LegacyActionState.RequireCurrent(current.Character));
                Assert.Equal((ushort)830, current.NativeGameClient!.Player.Px); Assert.Equal((ushort)584, current.NativeGameClient.Player.Py);
            }
            if (watcher) Assert.Equal((835L, 584L), (run.Current(LegacyInteractionWatcher).Character.X, run.Current(LegacyInteractionWatcher).Character.Y));
            Assert.Contains("WorldServer sent MsgAction interaction jump", run.Owner.Log.ToString());
            if (watcher) Assert.Contains("playerCount=2", run.Owner.Log.ToString());
        }, watcher, namedPartner: true);
    }

    private static Task OwnedLegacyInteractionWalkAsync(bool statics)
    {
        var catalog = CreateStaticVisibilityCatalog(items: new ItemType[] { },
            npcs: statics ? new Npc[] { new(20499, 2, 47496, 1015, 831, 583, null) } : new Npc[] { },
            furniture: statics ? new Furniture[] { CreateFurniture(8924, 26, 837, 1015, 831, 583) } : new Furniture[] { },
            sobNpcs: statics ? new SobNpc[] { CreateSobNpc(25005, "Gate", 21, 1137, 1015, 831, 583) } : new SobNpc[] { });
        return WithOwnedLegacyInteractionsAsync(catalog, async run =>
        {
            var before = run.Owner.Data.Files(); await PairOwnedLegacyInteraction(run, before);
            var expected = OwnedLegacyInteractionMovedFiles(run, before, 831, 583);
            run.Observe = (_, _) => { run.Owner.Data.AssertFiles(expected); AssertOwnedLegacyInteractionState(run); };
            await run.Send(LegacyInteractionMover, new MsgMovementPacket { Direction = 5, Uid = 0, Running = MsgMovementMode.Walk, Timestamp = 456 }.ToFrameBytes());
            run.Observe = null; run.Owner.Data.AssertFiles(expected);
            foreach (uint uid in run.Uids)
            {
                Assert.Equal(statics ? 4 : 1, run.Frames(uid).Count);
                Assert.Equal(OwnedLegacyInteractionWalkGolden(jump: false), run.Frames(uid)[0]);
                Assert.True(LegacyInterActionWalkPacket.TryParse(ReadFrame(run.Frames(uid)[0]), out var walk));
                Assert.Equal(LegacyInterActionWalkMode.Walk, walk.Mode); Assert.Equal(5, walk.DirectionOne);
                Assert.Equal(LegacyInteractionMover, walk.Uid); Assert.Equal(LegacyInteractionPeer, walk.OpponentUid);
                Assert.Equal((831L, 583L), (run.Current(uid).Character.X, run.Current(uid).Character.Y));
                Assert.Equal("5", run.Current(uid).Character.Attributes["Angle"]);
                if (statics)
                {
                    Assert.Equal(OwnedLegacyInteractionNpcGolden(20499, 47496, 2), run.Frames(uid)[1]);
                    Assert.Equal(OwnedLegacyInteractionNpcGolden(8924, 837, 26), run.Frames(uid)[2]);
                    Assert.Equal(OwnedLegacyInteractionGateGolden(jump: false), run.Frames(uid)[3]);
                    Assert.True(LegacyNpcSpawnPacket.TryParse(ReadFrame(run.Frames(uid)[1]), out var npc)); Assert.Equal(20499U, npc.Uid);
                    Assert.True(LegacyNpcSpawnPacket.TryParse(ReadFrame(run.Frames(uid)[2]), out var furniture)); Assert.Equal(8924U, furniture.Uid);
                    Assert.True(LegacySobNpcSpawnPacket.TryParse(ReadFrame(run.Frames(uid)[3]), out var gate)); Assert.Equal(25005U, gate.Uid);
                }
            }
            Assert.Contains("WorldServer paired MsgAttack interaction", run.Owner.Log.ToString());
            Assert.Contains("WorldServer sent MsgMovement interaction walk", run.Owner.Log.ToString());
            if (!statics) await AssertOwnedLegacyInteractionLocation(run, expected, 831, 583, 5);
        });
    }
}
