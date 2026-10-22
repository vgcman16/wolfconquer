using System.Net;
using System.Reflection;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task HandlerRuneRankRebindsSameUidAfterTargetSend()
    {
        await AssertRuneRankingPostSendOwnerAsync(RuneRankPostSendChange.SameUid);
    }

    [Fact]
    public async Task HandlerRuneRankSkipsRemovedSessionAfterTargetSend()
    {
        await AssertRuneRankingPostSendOwnerAsync(RuneRankPostSendChange.Removed);
    }

    [Fact]
    public async Task HandlerRuneRankSkipsDifferentUidAfterTargetSend()
    {
        await AssertRuneRankingPostSendOwnerAsync(RuneRankPostSendChange.DifferentUid);
    }

    // Managed UID-only publication controls, not native callback/ABA/concurrency equivalence.
    private async Task AssertRuneRankingPostSendOwnerAsync(RuneRankPostSendChange change)
    {
        WriteRuneRankingPlayer(RuneRankPlayerA, targetLevel: 14, material: true);
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff,
            CreateRuneRankingCatalog([RuneRankSeed(RuneRankPlayerB, 10_000)]), log);
        var sessions = GetRuneRankingSessions(handler);
        ConquerTcpConnection? connectionA = null;
        var sentA = new List<byte[]>();
        var armed = false;
        var fired = false;
        connectionA = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            var bytes = frame.ToArray();
            sentA.Add(bytes);
            if (armed && !fired &&
                LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item) &&
                item.Uid == RuneRankTargetUid && item.Mode == LegacyGameItemPacket.UpdateItemMode)
            {
                fired = true;
                var acceptedConnection = connectionA ?? throw new InvalidOperationException();
                var current = GetSession(handler, acceptedConnection);
                if (change == RuneRankPostSendChange.Removed)
                {
                    Assert.True(sessions.TryRemove(acceptedConnection, out var removed));
                    Assert.Same(current, removed);
                }
                else if (change == RuneRankPostSendChange.DifferentUid)
                {
                    sessions.Set(acceptedConnection, current with
                    {
                        Character = current.Character with { Uid = RuneRankPlayerA + 99 },
                    });
                }
                else
                {
                    sessions.Set(acceptedConnection, current with
                    {
                        Character = current.Character with { Name = "RefreshedRankOwner" },
                        PlayerItems = RuneRankItems(17),
                    });
                }
            }

            return ValueTask.CompletedTask;
        });
        var admittedA = connectionA ?? throw new InvalidOperationException();
        var ticket = handoff.Issue(RuneRankPlayerA, "rank-publication-a", ConquerRuntimeDefaults.WorldServer);
        await handler.HandleFrameAsync(
            admittedA, ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
        var (connectionB, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);
        var originalA = GetSession(handler, admittedA).PlayerItems;
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        Assert.Equal(originalBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        var beforeA = sentA.Count;
        var beforeB = sentB.Count;
        armed = true;

        await handler.HandleFrameAsync(admittedA, ReadFrame(CreateRuneRankingComposeUse()));

        Assert.True(fired);
        Assert.Equal(beforeA + 2, sentA.Count);
        Assert.Equal(beforeB, sentB.Count);
        AssertItemUsage(sentA[beforeA], new MsgItemUsagePacket(
            RuneRankMaterialUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sentA[beforeA + 1]), out var sentTarget));
        // The old target16 was already sent; the hook must use the CURRENT holder, not that snapshot.
        Assert.Equal(RuneRankTargetId + 2, sentTarget.ItemId);
        var owner = GetRuneRankingOwner(handler);
        if (change == RuneRankPostSendChange.SameUid)
        {
            var entry = Assert.Single(owner.Snapshot(), candidate => candidate.CharacterUid == RuneRankPlayerA);
            Assert.Equal(new LegacyRuneRankingEntry(RuneRankPlayerA, "RefreshedRankOwner", 10_600), entry);
            Assert.Equal(10_600U, LegacyRuneStoragePacket.ComputeStorageScore(GetSession(handler, admittedA).PlayerItems));
            var reloaded = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA);
            Assert.Equal(10_600U, LegacyRuneStoragePacket.ComputeStorageScore(reloaded));
        }
        else
        {
            Assert.Null(owner.FindRank(RuneRankPlayerA));
            Assert.Null(owner.FindRank(RuneRankPlayerA + 99));
            Assert.Equal(RuneRankPlayerB, Assert.Single(owner.Snapshot()).CharacterUid);
            if (change == RuneRankPostSendChange.Removed)
                Assert.False(sessions.TryGet(admittedA, out _));
            else
                Assert.Equal(RuneRankPlayerA + 99, GetSession(handler, admittedA).Character.Uid);
        }

        await AssertRuneRankingQueryAsync(
            handler, connectionB, sentB, RuneRankPlayerB,
            rank: change == RuneRankPostSendChange.SameUid ? 2 : 1, score: 10_000);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
    }

    private static WorldSessionStore GetRuneRankingSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static LegacyRuneRankingState GetRuneRankingOwner(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_runeRankings", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<LegacyRuneRankingState>(field.GetValue(handler));
    }

    private enum RuneRankPostSendChange
    {
        SameUid,
        Removed,
        DifferentUid,
    }
}
