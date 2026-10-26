using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    private void MarkEndedTradeEndpoint(Player player)
    {
        lock (trades.SyncRoot) trades.MarkConnectionEnded(player.Connection);
        Assert.True(trades.HasConnectionEnded(player.Connection));
    }

    private async Task QueueTradeWithEndedEndpointAsync(Player sender, LegacyTradePacket packet, Player? ended)
    {
        var held = await closureOperations.EnterExclusiveAsync();
        Task? actual = null;
        var failures = new List<Exception>();
        try
        {
            actual = SendItemClosureAsync(sender, packet);
            Assert.False(actual.IsCompleted); // The actual registered handler is queued on its real operation owner.
            if (ended is not null) MarkEndedTradeEndpoint(ended);
        }
        catch (Exception error) { failures.Add(error); }
        finally { held.Dispose(); }
        if (actual is not null)
            try { await actual; } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Queued trade action and terminal observation failed.", failures);
    }

    private void AssertEndedTradeActionUnchanged(Player left, Player right, ItemClosurePreimage beforeLeft,
        ItemClosurePreimage beforeRight)
    {
        AssertItemClosureUnchanged(left, beforeLeft);
        AssertItemClosureUnchanged(right, beforeRight);
        Assert.Equal(beforeLeft.SentCount, left.Sent.Count);
        Assert.Equal(beforeRight.SentCount, right.Sent.Count);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private void AssertTradeClaimsConsumed(Player left, Player right)
    {
        Assert.True(LegacyTradeEscrowClaims.TryRead(Current(left).Character.Attributes, out var a, out var reason), reason);
        Assert.True(LegacyTradeEscrowClaims.TryRead(Current(right).Character.Attributes, out var b, out reason), reason);
        Assert.Null(a);
        Assert.Null(b);
    }
}
